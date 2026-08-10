import asyncio
import enum
import json
import logging
import os
import random
import re
import socket
import struct
import time
import uuid
from collections.abc import Callable
from dataclasses import dataclass
from pathlib import Path

from .constants import DEFAULT_PORT, SESSION_TIMEOUT

logger = logging.getLogger(__name__)

import contextlib

from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S, HeartbeatMixin
from unity_mcp.bridge_reload_state import DOMAIN_RELOAD_EXPIRY_S, DomainReloadTracker
from unity_mcp.bridge_retry import RetryPolicy
from unity_mcp.bridge_socket import (
    _TCP_KEEPALIVE_DARWIN,
    _TCP_KEEPINTVL_DARWIN,
    DomainReloadError,
    _apply_socket_options,
    frame_read_with_timeout,
    frame_write,
)
from unity_mcp.compile_state import CompileStateProbe
from unity_mcp.crash_log import CrashLogger
from unity_mcp.lockfile import is_pid_alive
from unity_mcp.metrics import METRICS

# Re-export so existing `from .bridge import DomainReloadError` keeps working
__all__ = [
    "UnityBridge", "DomainReloadError", "BridgeState", "DeliveryState",
    "MIN_RECONNECT_INTERVAL", "DOMAIN_RELOAD_EXPIRY_S",
    "_apply_socket_options",
    "_TCP_KEEPALIVE_DARWIN", "_TCP_KEEPINTVL_DARWIN",
    "PROTOCOL_VERSION", "VersionInfo", "parse_version_string", "check_protocol_version",
]

PROTOCOL_VERSION = 3

_NEW_FMT = re.compile(r"proto:(\d+)(?:\|plugin:([^|]*))?(?:\|stamp:(.*))?")
_OLD_FMT = re.compile(r"[\d.]+(?:\|stamp:(.*))?")


@dataclass
class VersionInfo:
    proto: int = 0
    plugin: str = ""
    stamp: str = ""


def parse_version_string(s: str) -> VersionInfo:
    """Parse both `proto:3|plugin:X|stamp:Y` (new) and `1.0|stamp:Y` (old)."""
    m = _NEW_FMT.fullmatch(s)
    if m:
        return VersionInfo(
            proto=int(m.group(1)),
            plugin=m.group(2) or "",
            stamp=m.group(3) or "",
        )
    m = _OLD_FMT.fullmatch(s)
    if m:
        return VersionInfo(proto=1, stamp=m.group(1) or "")
    return VersionInfo()


def check_protocol_version(python_proto: int, unity_proto: int) -> None:
    """Warn or raise on proto mismatch."""
    if python_proto > unity_proto:
        logger.warning(
            "Unity plugin is outdated (proto %d < %d). "
            "Upgrade the Unity Biome MCP plugin package.", unity_proto, python_proto
        )
    elif python_proto < unity_proto:
        raise ConnectionError(
            f"Python MCP server must upgrade: Unity proto {unity_proto} > Python proto {python_proto}. "
            "Run: pip install --upgrade unity-biome-mcp"
        )

CONNECT_TIMEOUT = float(os.environ.get("UNITY_MCP_CONNECT_TIMEOUT", "5.0"))
MAX_RETRIES = int(os.environ.get("UNITY_MCP_MAX_RETRIES", "3"))
MIN_RECONNECT_INTERVAL = float(os.environ.get("UNITY_MCP_MIN_RECONNECT_INTERVAL", "5.0"))
STARTUP_GRACE_S = float(os.environ.get("UNITY_MCP_STARTUP_GRACE", "90.0"))


class BridgeState(enum.Enum):
    DISCONNECTED = "disconnected"
    CONNECTED = "connected"
    DOMAIN_RELOADING = "domain_reloading"
    FAILED = "failed"  # startup grace expired


class DeliveryState(enum.Enum):
    """Tracks per-attempt write/read lifecycle for idempotent retry (P-322)."""
    UNSENT = "unsent"       # not yet written to socket
    SENT = "sent"           # written + drained; response not yet received
    DELIVERED = "delivered" # response received
    FAILED = "failed"       # error before or after write


class _CandidateIdentityError(ConnectionError):
    """A TCP endpoint answered, but could not prove it is the intended Editor."""


class UnityBridge(HeartbeatMixin):
    """TCP client for Unity Editor communication."""

    def __init__(self, host: str = "127.0.0.1", port: int | None = None,
                 probe: CompileStateProbe | None = None,
                 port_discoverer: Callable[[], int] | None = None,
                 is_retry_safe: Callable[[str], bool] | None = None,
                 expected_project_path: str | os.PathLike[str] | None = None):
        self._host = host
        try:
            self._port = port or int(os.environ.get("UNITY_MCP_PORT", str(DEFAULT_PORT)))
        except ValueError:
            self._port = DEFAULT_PORT
        self._reader = None
        self._writer = None
        self._counter = 0
        self._lock = asyncio.Lock()
        detected_project = None
        if probe is None:
            detected_project = CompileStateProbe.autodetect_project_path(port=self._port)
            self._probe = CompileStateProbe(detected_project, port=self._port)
        else:
            self._probe = probe
        project_path = expected_project_path or detected_project
        self._expected_project_path: str | None = (
            self._canonical_project_path(project_path) if project_path else None
        )
        self._first_failure_ts: float | None = None
        self._reconnect_started_at: float | None = None
        self._hard_deadline_started_at: float | None = None
        self._state: BridgeState = BridgeState.DISCONNECTED
        self._on_reconnect_callbacks: list = []
        self._crash_log = CrashLogger()
        self._heartbeat_task: asyncio.Task | None = None
        self._heartbeat_interval: float = 15.0
        self._ping_failures: int = 0
        self._ping_stall_failures: int = 0
        self._last_reconnect_at: float = 0.0
        self._min_reconnect_interval: float = MIN_RECONNECT_INTERVAL  # kept for compat
        self._reconnect_backoff: float = BACKOFF_MIN_S
        self._port_discoverer: Callable[[], int] | None = port_discoverer
        self._reload: DomainReloadTracker = DomainReloadTracker()
        self._reload_gate: asyncio.Event = asyncio.Event()
        self._reload_gate.set()  # open by default; wait() returns immediately
        self._ppid_mismatch_count: int = 0
        self._pinned_port: int | None = None
        self._pinned_pid: int | None = None
        self._bridge_id: str = f"br-{os.getpid():x}-{id(self) & 0xFFFF:04x}"
        self._is_retry_safe: Callable[[str], bool] = is_retry_safe or (lambda cmd: False)
        self._retry_policy = RetryPolicy(
            probe=self._probe, reload=self._reload,
            is_retry_safe=self._is_retry_safe, max_retries=MAX_RETRIES,
        )
        # P-092: serialize concurrent requests through a single consumer task.
        self._send_queue: asyncio.Queue = asyncio.Queue()
        self._queue_consumer_task: asyncio.Task | None = None

    @property
    def _startup_grace_expired(self) -> bool:
        return self._state == BridgeState.FAILED

    @_startup_grace_expired.setter
    def _startup_grace_expired(self, value: bool) -> None:
        if value:
            self._state = BridgeState.FAILED

    def add_reconnect_callback(self, fn) -> None:
        self._on_reconnect_callbacks.append(fn)

    async def connect(self):
        reader, writer = await asyncio.wait_for(
            asyncio.open_connection(self._host, self._port),
            timeout=CONNECT_TIMEOUT,
        )
        _apply_socket_options(writer.get_extra_info("socket"))
        try:
            await self._verify_candidate_project(reader, writer, self._port)
        except BaseException:
            await self._close_candidate(writer)
            raise
        self._reader = reader
        self._writer = writer
        self._pin_candidate(self._port)

    def should_retry(self, error: Exception, attempt: int, session_deadline: float,
                      cmd: str = "") -> tuple[bool, float, str]:
        """Decide if send() should retry after error.

        Returns: (should_retry, delay_s, reason)

        C8: thin backward-compat wrapper — the actual decision now lives in
        RetryPolicy.decide() (bridge_retry.py). BridgeState is a connection
        state-machine concern, not a retry-policy concern (SRP), so it's set
        here rather than inside the policy object.
        """
        if isinstance(error, DomainReloadError):
            self._state = BridgeState.DOMAIN_RELOADING
        return self._retry_policy.decide(error, attempt, session_deadline, cmd=cmd)

    def _probe_busy(self) -> bool:
        """Compat shim: bridge_heartbeat.py (HeartbeatMixin) calls self._probe_busy()
        directly, outside the should_retry()/RetryPolicy decision path."""
        return self._retry_policy.probe_busy()

    async def send(self, cmd: str, args: dict, timeout: float = 30.0) -> dict:
        if self._reload.is_active():
            raise DomainReloadError("Domain reload in progress — retry after recompile")
        if self._state == BridgeState.FAILED:
            if not self._reconnect_cooldown_ok():
                raise ConnectionError("Reconnect cooldown active — retry in a moment")
            self._last_reconnect_at = time.monotonic()
            try:
                async with self._lock:
                    await self._reconnect(fire_callbacks=False)
            except Exception:
                raise ConnectionError(self._describe_failure(cmd, ConnectionRefusedError())) from None
        self._counter += 1
        msg_id = f"{self._counter:04x}"
        operation_id = str(uuid.uuid4())  # P-322: idempotency key for dedup
        payload = json.dumps(
            {"id": msg_id, "cmd": cmd, "args": args, "op_id": operation_id},
            ensure_ascii=False,
        ).encode("utf-8")
        if len(payload) > 10_000_000:
            raise ConnectionError(f"Outbound payload too large: {len(payload)} bytes (max 10MB)")
        session_deadline = time.monotonic() + SESSION_TIMEOUT
        future: asyncio.Future = asyncio.get_event_loop().create_future()
        if self._queue_consumer_task is None or self._queue_consumer_task.done():
            self._queue_consumer_task = asyncio.ensure_future(self._queue_consumer())
        await self._send_queue.put((cmd, payload, msg_id, timeout, session_deadline, operation_id, future))
        try:
            return await future
        except asyncio.CancelledError:
            await self.close()
            raise

    async def _queue_consumer(self) -> None:
        """Serial consumer: processes one send at a time so the circuit breaker
        and TCP socket always see exactly one in-flight request (P-092)."""
        while True:
            try:
                cmd, payload, msg_id, timeout, deadline, op_id, future = (
                    await self._send_queue.get()
                )
            except asyncio.CancelledError:
                break
            try:
                result = await self._send_with_retry(cmd, payload, msg_id, timeout, deadline, op_id)
                if not future.done():
                    future.set_result(result)
            except asyncio.CancelledError:
                if not future.done():
                    future.cancel()
                raise
            except Exception as exc:  # noqa: BLE001
                if not future.done():
                    future.set_exception(exc)
            finally:
                self._send_queue.task_done()

    async def _send_with_retry(self, cmd: str, payload: bytes,
                               msg_id: str, timeout: float, session_deadline: float,
                               operation_id: str = "") -> dict:
        """Send cmd with retry loop.

        operation_id (P-322): UUID included in payload as op_id so C# can dedup
        retries. On retry after a SENT state (write OK, read failed) the payload
        is rebuilt with 'retry_op_id' so Unity knows to check its dedup registry.
        """
        attempt = 0
        result = None
        delivery = DeliveryState.UNSENT  # P-322: delivery FSM
        # Join the connection lock before applying the first-attempt cooldown.
        # A concurrent sender may be performing the one shared reconnect; after
        # it completes, this sender can reuse the live socket without starting
        # another reconnect or being rejected as a storm.
        if not self.connected:
            async with self._lock:
                if not self.connected and not self._reconnect_cooldown_ok():
                    raise ConnectionError(
                        "Reconnect cooldown active — retry in a moment"
                    )
        while attempt <= MAX_RETRIES:
            if time.monotonic() > session_deadline:
                raise TimeoutError(f"Session deadline ({SESSION_TIMEOUT}s) exceeded")

            delivery = DeliveryState.UNSENT  # reset per attempt
            try:
                async with self._lock:
                    if not self.connected:
                        self._last_reconnect_at = time.monotonic()
                        await self._reconnect(fire_callbacks=False)
                        METRICS.inc("reconnect.send_path")
                    frame_write(self._writer, payload)
                    await self._writer.drain()
                    delivery = DeliveryState.SENT  # P-322: write committed
                    try:
                        result = await asyncio.wait_for(
                            self._read_response(), timeout=timeout)
                    except asyncio.CancelledError:
                        await self.close()
                        raise
            except (ConnectionRefusedError, asyncio.TimeoutError, ConnectionError,
                    asyncio.IncompleteReadError, OSError, json.JSONDecodeError,
                    RuntimeError) as e:
                if isinstance(e, DomainReloadError):
                    self._reload.mark()
                elif isinstance(e, asyncio.TimeoutError) and delivery == DeliveryState.SENT and self._reload.is_active():
                    # P-183: read timeout after payload delivered → Unity was processing
                    # the command (not reloading). Clear stale reload flag so next
                    # send() isn't immediately blocked by DomainReloadError.
                    self._reload.clear()
                    self._reload_gate.set()
                async with self._lock:
                    await self.close()
                if self._first_failure_ts is None:
                    self._first_failure_ts = time.monotonic()
                do_retry, delay, reason = self.should_retry(e, attempt, session_deadline, cmd=cmd)
                self._crash_log.log_disconnect(cmd=cmd, retry=attempt,
                                               error_type=type(e).__name__,
                                               unity_busy=reason in ("busy", "domain_reload"),
                                               port=self._port,
                                               bid=self._bridge_id,
                                               reason=reason,
                                               path="send")
                if do_retry:
                    attempt += 1
                    # P-322: if payload was already written (SENT), the retry must carry
                    # retry_op_id so C# DedupRegistry can detect and suppress re-execution.
                    if delivery == DeliveryState.SENT and operation_id:
                        raw = json.loads(payload.decode("utf-8"))
                        raw["retry_op_id"] = operation_id
                        payload = json.dumps(raw, ensure_ascii=False).encode("utf-8")
                    jitter = random.uniform(0, delay * 0.1)
                    if reason == "domain_reload":
                        self._reload_gate.clear()
                        with contextlib.suppress(asyncio.TimeoutError):
                            await asyncio.wait_for(
                                self._reload_gate.wait(), timeout=delay + jitter)
                    else:
                        await asyncio.sleep(delay + jitter)
                    continue
                raise ConnectionError(self._describe_failure(cmd, e)) from e

            if result.get("id") != msg_id:
                async with self._lock:
                    await self.close()
                raise ConnectionError(
                    f"Response ID mismatch: expected {msg_id}, got {result.get('id')}")

            # Unity retry hint (compilation busy)
            if not result.get("ok") and result.get("retry"):
                # G17: check for terminal reload failure before re-sending.
                try:
                    from unity_mcp.editor_log import detect_wedge
                    wedge = detect_wedge()
                    if wedge is not None:
                        return {"ok": False, "data": (
                            f"BUILD-FAILED-WEDGE: reload failed ({wedge.kind}) — "
                            "reimport the file: package (sync), do NOT restart"
                        )}
                except Exception:
                    pass
                # A1/C8: the busy-hint retry is the same risk as a TimeoutError
                # retry — the command may have already reached Unity's
                # dispatcher. Gate it through RetryPolicy's same is_retry_safe
                # fail-closed check (decide()'s TimeoutError branch and
                # allow_hint_retry() now share one gate — the whole point of
                # unifying the two retry surfaces).
                if not self._retry_policy.allow_hint_retry(cmd):
                    return result
                if attempt < MAX_RETRIES:
                    await asyncio.sleep(result["retry"] / 1000)
                    attempt += 1
                    continue
                return result

            delivery = DeliveryState.DELIVERED  # P-322: response received
            self._reload.clear()
            self._state = BridgeState.CONNECTED
            if self._first_failure_ts is not None:
                outage = time.monotonic() - self._first_failure_ts
                METRICS.observe("recompile.duration_ms", outage * 1000)
                self._crash_log.log_reconnect(outage_s=outage, retries=attempt,
                                              port=self._port, bid=self._bridge_id,
                                              path="send")
                self._first_failure_ts = None
            return result
        raise RuntimeError(f"_send_with_retry exhausted {MAX_RETRIES} retries without result for cmd={cmd!r}")

    def _describe_failure(self, cmd: str, exc: Exception) -> str:
        try:
            if self._probe.is_process_dead():
                return f"Unity crashed (process dead). Restart Unity. Port :{self._port}"
        except Exception:
            pass
        try:
            if self._probe.is_unity_busy():
                rem = self._probe.estimated_remaining_s()
                return (f"Unity busy: C# compilation/domain reload in progress "
                        f"(~{rem:.0f}s left). Retry in a moment.")
        except Exception:
            pass
        if isinstance(exc, ConnectionRefusedError):
            return (f"Unity TCP refused connection — likely mid domain-reload. "
                    f"Wait 5-10s and retry. Port :{self._port}")
        return f"Unity not responding (process dead? port wrong?). Check :{self._port}."

    async def _read_response(self) -> dict:
        header = await self._reader.readexactly(4)
        length = struct.unpack("!I", header)[0]
        if length == 0 or length > 10_000_000:
            raise ConnectionError(
                f"Protocol desync: length prefix {length} (0x{length:08X}) — reconnecting"
            )
        payload = await self._reader.readexactly(length)
        data = json.loads(payload.decode("utf-8"))
        if data.get("ev") == "going_away":
            raise DomainReloadError(f"Unity domain reload: {data.get('reason', 'unknown')}")
        return data

    @staticmethod
    def _canonical_project_path(path: str | os.PathLike[str]) -> str:
        """Canonicalize a Unity project path without requiring it to exist."""
        return os.path.normcase(os.path.realpath(os.path.abspath(os.fspath(path))))

    @staticmethod
    async def _close_candidate(writer) -> None:
        writer.close()
        with contextlib.suppress(Exception):
            await writer.wait_closed()

    async def _verify_candidate_project(self, reader, writer, port: int) -> None:
        """Reject an endpoint that cannot prove it owns the expected project."""
        if self._expected_project_path is None:
            return

        request_id = "project-identity"
        request = json.dumps(
            {
                "id": request_id,
                "cmd": "editor",
                "args": {"action": "project_path"},
            },
            ensure_ascii=False,
        ).encode("utf-8")
        frame_write(writer, request)
        await writer.drain()
        try:
            payload = await frame_read_with_timeout(reader, CONNECT_TIMEOUT)
            response = json.loads(payload.decode("utf-8"))
        except (asyncio.TimeoutError, OSError, EOFError, UnicodeDecodeError, json.JSONDecodeError) as exc:
            raise _CandidateIdentityError(
                f"Unity on port {port} did not return a valid project identity"
            ) from exc

        if not isinstance(response, dict) or response.get("ev") == "going_away":
            raise _CandidateIdentityError(
                f"Unity on port {port} became unavailable during project identity check"
            )
        if response.get("id") != request_id or not response.get("ok"):
            raise _CandidateIdentityError(
                f"Unity on port {port} did not confirm its project identity"
            )
        reported = response.get("data")
        if not isinstance(reported, str) or not reported.strip():
            raise _CandidateIdentityError(
                f"Unity on port {port} returned an empty project identity"
            )
        actual = self._canonical_project_path(reported.strip())
        if actual != self._expected_project_path:
            raise _CandidateIdentityError(
                f"Refusing Unity on port {port}: expected project "
                f"{self._expected_project_path!r}, received {actual!r}"
            )

    def _discover_port(self) -> int | None:
        if self._port_discoverer is None:
            return None
        try:
            import inspect
            parameters = inspect.signature(self._port_discoverer).parameters
            kwargs = {"skip_probe": True} if "skip_probe" in parameters else {}
            return self._port_discoverer(**kwargs)
        except Exception:
            return None

    async def _open_reconnect_candidate(self, port: int):
        reader, writer = await asyncio.wait_for(
            asyncio.open_connection(self._host, port),
            timeout=CONNECT_TIMEOUT,
        )
        _apply_socket_options(writer.get_extra_info("socket"))
        try:
            self._counter += 1
            ping_id = f"rc{self._counter:04x}"
            client = os.environ.get("UNITY_MCP_CLIENT", "")
            ping = json.dumps(
                {"id": ping_id, "cmd": "ping", "role": client or "mcp", "args": {}},
                ensure_ascii=False,
            ).encode("utf-8")
            frame_write(writer, ping)
            await writer.drain()
            pong_payload = await frame_read_with_timeout(reader, CONNECT_TIMEOUT)
            pong = json.loads(pong_payload.decode("utf-8"))
            if pong.get("ev") == "going_away":
                raise DomainReloadError("Unity going_away during reconnect")
            if not pong.get("ok"):
                raise ConnectionError("Unity ping failed after reconnect")

            # Verify ownership before interpreting any optional protocol data.
            # A reused port may answer ping/get_version successfully while
            # belonging to a completely different Unity project.
            await self._verify_candidate_project(reader, writer, port)

            # Protocol version check is non-fatal unless Unity requires a newer
            # Python protocol.
            try:
                version_message = json.dumps(
                    {"id": "ver", "cmd": "get_version", "args": {}},
                    ensure_ascii=False,
                ).encode("utf-8")
                frame_write(writer, version_message)
                await writer.drain()
                version_payload = await frame_read_with_timeout(reader, CONNECT_TIMEOUT)
                version_response = json.loads(version_payload.decode("utf-8"))
                if version_response.get("ev") == "going_away":
                    raise DomainReloadError("Unity going_away during version check")
                if version_response.get("ok") and version_response.get("data"):
                    info = parse_version_string(version_response["data"])
                    check_protocol_version(PROTOCOL_VERSION, info.proto)
                    if info.plugin:
                        from .server_updater import _updater
                        asyncio.create_task(_updater.maybe_update(info.plugin))
            except ConnectionError:
                raise
            except Exception as exc:
                logger.warning("Protocol version check failed (non-fatal): %s", exc)

            return reader, writer
        except BaseException:
            await self._close_candidate(writer)
            raise

    def _accept_candidate(self, port: int, reader, writer) -> None:
        if port != self._port:
            self._port = port
            project = (
                Path(self._expected_project_path)
                if self._expected_project_path is not None
                else CompileStateProbe.autodetect_project_path(port=port)
            )
            self._probe = CompileStateProbe(project, port=port)
            self._retry_policy.probe = self._probe

        # Atomic: assign both only after every candidate check has passed.
        self._reader = reader
        self._writer = writer
        self._first_failure_ts = None
        self._reconnect_started_at = None
        self._hard_deadline_started_at = None
        self._state = BridgeState.CONNECTED
        self._reload.clear()
        self._reload_gate.set()
        self._ping_stall_failures = 0
        self._last_reconnect_at = time.monotonic()
        self._pin_candidate(port)

    def _pin_candidate(self, port: int) -> None:
        self._pinned_port = port
        self._pinned_pid = self._candidate_pid(port)

    def _candidate_pid(self, port: int) -> int | None:
        try:
            from unity_mcp.lockfile import read_pid_from_port_file
            return read_pid_from_port_file(
                port, project_path=self._expected_project_path
            )
        except Exception:
            return None

    async def _reconnect(self, fire_callbacks: bool = True):
        await self.close()
        pinned_is_live = (
            self._pinned_port is not None
            and self._pinned_pid is not None
            and is_pid_alive(self._pinned_pid)
        )
        if pinned_is_live and self._expected_project_path is not None:
            discovered_pid = self._candidate_pid(self._pinned_port)
            if discovered_pid is not None and discovered_pid != self._pinned_pid:
                # A newer discovery record now owns this project/port tuple.
                # Treat the old live PID as stale and re-enter project-aware discovery.
                self._pinned_port = None
                self._pinned_pid = None
                pinned_is_live = False
        candidate_port = self._pinned_port if pinned_is_live else self._discover_port()
        if candidate_port is None:
            candidate_port = self._port

        try:
            reader, writer = await self._open_reconnect_candidate(candidate_port)
        except _CandidateIdentityError:
            # A live PID only proves that an old discovery record still has an
            # owner. The port itself may already have been reused by another
            # Editor. Reject it, then discover and verify a fresh candidate.
            if not pinned_is_live or self._port_discoverer is None:
                raise
            self._pinned_port = None
            self._pinned_pid = None
            discovered_port = self._discover_port()
            if discovered_port is None or discovered_port == candidate_port:
                raise
            reader, writer = await self._open_reconnect_candidate(discovered_port)
            candidate_port = discovered_port
        except ConnectionRefusedError:
            # Port was valid when pinned but Unity stopped listening (port drift).
            # Other errors (TimeoutError, OSError) keep the pin — they indicate
            # transient network issues, not a port change.
            self._pinned_port = None
            self._pinned_pid = None
            raise
        self._accept_candidate(candidate_port, reader, writer)
        self.start_heartbeat(self._heartbeat_interval)
        if fire_callbacks:
            for cb in self._on_reconnect_callbacks:
                with contextlib.suppress(Exception):
                    cb()

    @property
    def connected(self) -> bool:
        return self._writer is not None and not self._writer.is_closing()

    @property
    def status(self) -> str:
        """Semantic connection status for user-facing display."""
        if self._writer is not None and not self._writer.is_closing():
            return "connected"
        if self._state == BridgeState.FAILED:
            return "disconnected"
        if self._state == BridgeState.DOMAIN_RELOADING:
            return "domain-reloading"
        return "reconnecting"

    @property
    def transport_status(self) -> str:
        """TCP transport layer status — independent of stdio health."""
        if self._writer is not None and not self._writer.is_closing():
            return "tcp:connected"
        if self._state == BridgeState.FAILED:
            return "tcp:failed"
        return "tcp:reconnecting"

    async def close(self):
        if asyncio.current_task() is not self._heartbeat_task:
            self.stop_heartbeat()
        if asyncio.current_task() is not self._queue_consumer_task:
            task, self._queue_consumer_task = self._queue_consumer_task, None
            if task is not None and not task.done():
                task.cancel()
                with contextlib.suppress(asyncio.CancelledError):
                    await task
            while not self._send_queue.empty():
                try:
                    *_, future = self._send_queue.get_nowait()
                    if not future.done():
                        future.set_exception(ConnectionError("Bridge closed"))
                except asyncio.QueueEmpty:  # noqa: PERF203
                    break
        w = self._writer
        self._writer = None
        self._reader = None
        if w:
            sock = w.get_extra_info("socket")
            if sock is not None:
                with contextlib.suppress(OSError):
                    sock.shutdown(socket.SHUT_WR if os.name == "nt" else socket.SHUT_RDWR)
            w.close()
            with contextlib.suppress(Exception):
                await asyncio.wait_for(w.wait_closed(), timeout=2.0)
