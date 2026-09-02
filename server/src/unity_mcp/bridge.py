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
from dataclasses import dataclass
from pathlib import Path

from .constants import DEFAULT_PORT, SESSION_TIMEOUT

logger = logging.getLogger(__name__)

import contextlib  # noqa: E402
from collections.abc import Callable  # noqa: TC003

from unity_mcp.bridge_heartbeat import BACKOFF_MIN_S, RELOAD_BACKOFF_S, HeartbeatMixin
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
    "CommandStatus", "CommandLedger", "EditorIdentity",
    "MIN_RECONNECT_INTERVAL", "DOMAIN_RELOAD_EXPIRY_S",
    "_apply_socket_options",
    "_TCP_KEEPALIVE_DARWIN", "_TCP_KEEPINTVL_DARWIN",
    "PROTOCOL_VERSION", "VersionInfo", "parse_version_string", "check_protocol_version",
]

PROTOCOL_VERSION = 3

_NEW_FMT = re.compile(r"proto:(\d+)(?:\|plugin:([^|]*))?(?:\|stamp:(.*))?")
_OLD_FMT = re.compile(r"[\d.]+(?:\|stamp:(.*))?")
_background_tasks: set = set()


@dataclass
class VersionInfo:
    proto: int = 0
    plugin: str = ""
    stamp: str = ""


@dataclass
class ClientHelloPayload:
    role: str
    session_id: str
    lock_token: str
    bridge_pid: int
    bridge_parent_pid: int
    agent_id: str
    display_name: str
    bridge_version: str
    cwd: str
    started_at_utc: str


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
    DORMANT = "dormant"   # intentional TCP close, heartbeat stopped, process alive
    WAKING  = "waking"    # reconnect triggered by incoming request from DORMANT


class DeliveryState(enum.Enum):
    """Tracks per-attempt write/read lifecycle for idempotent retry (P-322)."""
    UNSENT = "unsent"       # not yet written to socket
    SENT = "sent"           # accepted by writer; response not yet received
    DELIVERED = "delivered" # response received
    FAILED = "failed"       # error before or after write


class CommandStatus(enum.Enum):
    """Persistent lifecycle state for a single op_id in the CommandLedger."""
    NOT_FOUND = "not_found"    # op_id unknown (never sent or TTL expired)
    SENT = "sent"              # queued but not yet written to TCP
    ACCEPTED = "accepted"      # writer accepted frame; Unity may have received it
    COMPLETED = "completed"    # response received (success or Unity-level error)
    FAILED = "failed"          # terminal transport error; no response


@dataclass
class _LedgerEntry:
    status: CommandStatus
    result: dict | None = None
    ts: float | None = None

    def __post_init__(self):
        if self.ts is None:
            self.ts = time.monotonic()


_STATUS_ORDER = {
    CommandStatus.NOT_FOUND: 0,
    CommandStatus.SENT: 1,
    CommandStatus.ACCEPTED: 2,
    CommandStatus.COMPLETED: 3,
    CommandStatus.FAILED: 3,  # same level as COMPLETED (both terminal)
}


class CommandLedger:
    """Asyncio-safe (single event-loop thread) dict-backed ledger tracking op_id → (status, result).

    TTL-based cleanup prevents unbounded memory growth.
    Lives on the bridge instance; survives close/reconnect cycles.
    """
    TTL = 300.0  # seconds; matches DedupRegistry.TtlSeconds on C# side

    def __init__(self) -> None:
        self._store: dict[str, _LedgerEntry] = {}

    def record(self, op_id: str, status: CommandStatus,
               result: dict | None = None) -> None:
        """Upsert entry; never regresses status (forward-only)."""
        if not op_id:
            return
        existing = self._store.get(op_id)
        if existing and _STATUS_ORDER.get(status, 0) < _STATUS_ORDER.get(existing.status, 0):
            return  # never regress
        self._store[op_id] = _LedgerEntry(
            status=status,
            result=result if result is not None else (existing.result if existing else None),
            ts=existing.ts if existing else time.monotonic(),
        )
        self._evict()

    def get(self, op_id: str) -> tuple[CommandStatus, dict | None]:
        entry = self._store.get(op_id)
        if entry is None:
            return CommandStatus.NOT_FOUND, None
        if time.monotonic() - entry.ts > self.TTL:
            del self._store[op_id]
            return CommandStatus.NOT_FOUND, None
        return entry.status, entry.result

    def _evict(self) -> None:
        cutoff = time.monotonic() - self.TTL
        stale = [k for k, v in self._store.items() if v.ts < cutoff]
        for k in stale:
            del self._store[k]


def _with_retry_op_id(payload: bytes, op_id: str) -> bytes:
    """P-322: rebuild payload with 'retry_op_id' so C# DedupRegistry can detect
    and suppress re-execution of a command that was already dispatched. Shared
    by both retry paths (exception-after-SENT and in-band Unity 'retry' hint) --
    a frame reaching Unity via either path must be resend-tagged the same way."""
    raw = json.loads(payload.decode("utf-8"))
    raw["retry_op_id"] = op_id
    return json.dumps(raw, ensure_ascii=False).encode("utf-8")


class _CandidateIdentityError(ConnectionError):
    """A TCP endpoint answered, but could not prove it is the intended Editor."""


@dataclass(frozen=True)
class EditorIdentity:
    """Immutable editor identity captured on first successful handshake (MCP-SESS-024)."""
    project_id: str
    project_path: str


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
        # ARC-7 T1: throttle clock for the periodic stale-port sweep (bridge_heartbeat.py).
        self._last_port_sweep_at: float = 0.0
        # ARC-7 T2: monotonic timestamp of the last *successful* contact with Unity
        # (heartbeat pong or a completed send() round-trip). None until either occurs.
        self._last_contact_at: float | None = None
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
        self._orphan_detected_at: float | None = None
        self._pinned_port: int | None = None
        self._pinned_pid: int | None = None
        # ARC-9 T2: one-shot (old_port, new_port) set when a same-pid port
        # rebind is adopted; consumed via pop_port_drift_notice().
        self._port_drift: tuple[int, int] | None = None
        self._bridge_id: str = f"br-{os.getpid():x}-{id(self) & 0xFFFF:04x}"
        self._is_retry_safe: Callable[[str], bool] = is_retry_safe or (lambda cmd: False)
        self._retry_policy = RetryPolicy(
            probe=self._probe, reload=self._reload,
            is_retry_safe=self._is_retry_safe, max_retries=MAX_RETRIES,
        )
        # P-092: serialize concurrent requests through a single consumer task.
        self._send_queue: asyncio.Queue = asyncio.Queue()
        self._queue_consumer_task: asyncio.Task | None = None
        # T5: stable per-session identifiers for client_hello handshake.
        self._session_id: str = str(uuid.uuid4())
        self._lock_token: str = str(uuid.uuid4())
        self._started_at_utc: str = time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())
        # T19: stable project identity extracted from C# client_hello (cloudProjectId or sha256[:12])
        self._project_id: str | None = None
        # MCP-SESS-024: immutable editor identity; None until first handshake
        self._editor_identity: EditorIdentity | None = None
        # MCP-TRANS-008: persistent command ledger (survives reconnect)
        self._ledger: CommandLedger = CommandLedger()

    @property
    def _startup_grace_expired(self) -> bool:
        return self._state == BridgeState.FAILED

    @_startup_grace_expired.setter
    def _startup_grace_expired(self, value: bool) -> None:
        if value:
            self._state = BridgeState.FAILED

    @property
    def session_id(self) -> str:
        return self._session_id

    @property
    def lock_token(self) -> str:
        return self._lock_token

    @property
    def project_id(self) -> str | None:
        """Stable project identity from C# client_hello (cloudProjectId or sha256[:12]).
        None until first successful handshake with a C# plugin that sends projectId."""
        return self._project_id

    def get_command_status(self, op_id: str) -> tuple[CommandStatus, dict | None]:
        """Query the fate of a command by its op_id (MCP-TRANS-008).

        Returns (CommandStatus, cached_result). Callers use this after a transport
        disconnect to determine if the command was accepted by Unity before the drop.
        """
        return self._ledger.get(op_id)

    def _build_hello(self, msg_id: str) -> bytes:
        """Build a client_hello JSON frame for the combined handshake."""
        client = os.environ.get("UNITY_MCP_CLIENT", "")
        payload = ClientHelloPayload(
            role=client or "mcp",
            session_id=self._session_id,
            lock_token=self._lock_token,
            bridge_pid=os.getpid(),
            bridge_parent_pid=os.getppid() if hasattr(os, "getppid") else 0,
            agent_id=self._bridge_id,
            display_name=client or "mcp",
            bridge_version="",
            cwd=os.getcwd(),
            started_at_utc=self._started_at_utc,
        )
        data = {
            "id": msg_id,
            "cmd": "client_hello",
            "role": payload.role,
            "sessionId": payload.session_id,
            "lockToken": payload.lock_token,
            "bridgePid": payload.bridge_pid,
            "bridgeParentPid": payload.bridge_parent_pid,
            "agentId": payload.agent_id,
            "displayName": payload.display_name,
            "bridgeVersion": payload.bridge_version,
            "cwd": payload.cwd,
            "startedAtUtc": payload.started_at_utc,
            "chatMode": os.environ.get("UNITY_MCP_CHAT_MODE", ""),
        }
        return json.dumps(data, ensure_ascii=False).encode("utf-8")

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
            # DEV-60: negotiate protocol version on the very first connect too —
            # previously only _reconnect() did this, so a mixed-version pair
            # went undetected until the first reconnect (or never, for a
            # long-lived single connection). Gated the same as the project
            # check above: an unknown project means no real Unity endpoint is
            # pinned yet (e.g. ad hoc/test bridges), so skip the extra round trip.
            if self._expected_project_path is not None:
                await self._check_initial_protocol_version(reader, writer)
        except BaseException:
            await self._close_candidate(writer)
            raise
        self._reader = reader
        self._writer = writer
        self._pin_candidate(self._port)

    async def _check_initial_protocol_version(self, reader, writer) -> None:
        """DEV-60: client_hello + version check, mirroring _open_reconnect_candidate.
        Only the frame read/parse is tolerant of a pre-hello plugin (ARC-17
        wire-compat) — once a hello payload is parsed, going_away/capacity/
        version-mismatch signals are handled exactly as in
        _open_reconnect_candidate and propagate to connect()'s outer handler
        (DEV-60b: these must not be swallowed as a non-fatal handshake failure)."""
        self._counter += 1
        hello_id = f"c{self._counter:04x}"
        frame_write(writer, self._build_hello(hello_id))
        await writer.drain()
        try:
            hello_raw = await frame_read_with_timeout(reader, CONNECT_TIMEOUT)
            hello = json.loads(hello_raw.decode("utf-8"))
        except Exception as exc:
            logger.warning("client_hello handshake failed during connect (non-fatal): %s", exc)
            return

        if hello.get("ev") == "going_away":
            raise DomainReloadError("Unity going_away during initial connect")

        if hello.get("error") == "CLIENT_CAPACITY_BUSY":
            from .errors import CapacityBusyError
            raise CapacityBusyError(
                f"Unity at capacity: {hello.get('active', '?')}/{hello.get('capacity', '?')} clients",
                retry_after_seconds=float(hello.get("retry_after_seconds", 5.0)),
                capacity=int(hello.get("capacity", 0)),
                active=int(hello.get("active", 0)),
            )

        if hello.get("ok") and hello.get("helloVersion"):
            # T19: extract stable project identity (cloudProjectId or sha256[:12])
            project_id = hello.get("projectId", "")
            if isinstance(project_id, str) and project_id.strip():
                self._project_id = project_id.strip()
            # MCP-SESS-024: capture/enforce editor identity on every handshake
            _pid = project_id.strip() if isinstance(project_id, str) else ""
            _raw_path = hello.get("projectPath", "") or ""
            _canon = (
                self._canonical_project_path(_raw_path.strip())
                if _raw_path.strip() else ""
            )
            self._capture_or_check_identity(project_id=_pid, project_path=_canon)
            await self._check_version_from_hello(hello)
        else:
            await self._fetch_and_check_version(reader, writer)

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
            self._reconnect_backoff = RELOAD_BACKOFF_S  # fast reconnect for expected reloads
            self._reload.mark()
            self._probe.mark_recompile_issued()
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
        self._ledger.record(operation_id, CommandStatus.SENT)  # MCP-TRANS-008
        payload = json.dumps(
            {"id": msg_id, "cmd": cmd, "args": args, "op_id": operation_id},
            ensure_ascii=False,
        ).encode("utf-8")
        if len(payload) > 10_000_000:
            raise ConnectionError(f"Outbound payload too large: {len(payload)} bytes (max 10MB)")
        session_deadline = time.monotonic() + SESSION_TIMEOUT
        future: asyncio.Future = asyncio.get_running_loop().create_future()
        if self._queue_consumer_task is None or self._queue_consumer_task.done():
            self._queue_consumer_task = asyncio.create_task(self._queue_consumer())
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
                logger.debug("queue consumer cancelled while waiting for next item")
                raise
            try:
                result = await self._send_with_retry(cmd, payload, msg_id, timeout, deadline, op_id)
                self._ledger.record(op_id, CommandStatus.COMPLETED, result)  # MCP-TRANS-008
                self._last_contact_at = time.monotonic()  # ARC-7 T2
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
                               op_id: str = "") -> dict:
        """Send cmd with retry loop.

        op_id (P-322): UUID included in payload as op_id so C# can dedup
        retries. On retry after a SENT state (writer accepted the frame, response
        failed) the payload is rebuilt with 'retry_op_id' so Unity knows to check
        its dedup registry.
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
                        if self._state == BridgeState.DORMANT:
                            self._state = BridgeState.WAKING
                        self._last_reconnect_at = time.monotonic()
                        await self._reconnect(fire_callbacks=False)
                        METRICS.inc("reconnect.send_path")
                    frame_write(self._writer, payload)
                    # StreamWriter.write() may hand bytes to the transport before
                    # drain() reports failure.  From this point a mutating command
                    # is delivery-uncertain and must be treated as SENT.
                    delivery = DeliveryState.SENT  # P-322: frame may be delivered
                    self._ledger.record(op_id, CommandStatus.ACCEPTED)  # MCP-TRANS-008
                    await self._writer.drain()
                    try:
                        result = await asyncio.wait_for(
                            self._read_response(), timeout=timeout)
                    except asyncio.CancelledError:
                        await self.close()
                        raise
            except (
                OSError,
                asyncio.IncompleteReadError,
                json.JSONDecodeError,
                UnicodeDecodeError,
                RuntimeError,
            ) as e:
                if isinstance(e, TimeoutError) and delivery == DeliveryState.SENT and self._reload.is_active():
                    # P-183: read timeout after payload delivered → Unity was processing
                    # the command (not reloading). Clear stale reload flag so next
                    # send() isn't immediately blocked by DomainReloadError.
                    self._reload.clear()
                    self._reload_gate.set()
                async with self._lock:
                    await self.close()
                if self._first_failure_ts is None:
                    self._first_failure_ts = time.monotonic()
                # Preserve transport/reload observation side effects even when
                # resend is forbidden; DomainReloadError still transitions the
                # bridge and marks the compile probe.
                do_retry, delay, reason = self.should_retry(
                    e, attempt, session_deadline, cmd=cmd)
                observed_reason = reason
                # A written frame may already have executed in Unity. Only commands
                # proven read-only/idempotent by ToolAnnotations may be resent after
                # that boundary. Before SENT, reconnecting is transport recovery.
                if delivery == DeliveryState.SENT and not self._is_retry_safe(cmd):
                    do_retry, delay, reason = False, 0.0, "unsafe_sent"
                self._crash_log.log_disconnect(cmd=cmd, retry=attempt,
                                               error_type=type(e).__name__,
                                               unity_busy=observed_reason in ("busy", "domain_reload"),
                                               port=self._port,
                                               bid=self._bridge_id,
                                               reason=reason,
                                               path="send")
                if do_retry:
                    attempt += 1
                    # P-322: if payload was already written (SENT), the retry must carry
                    # retry_op_id so C# DedupRegistry can detect and suppress re-execution.
                    if delivery == DeliveryState.SENT and op_id:
                        payload = _with_retry_op_id(payload, op_id)
                    jitter = random.uniform(0, delay * 0.1)
                    if reason == "domain_reload":
                        self._reload_gate.clear()
                        with contextlib.suppress(TimeoutError):
                            await asyncio.wait_for(
                                self._reload_gate.wait(), timeout=delay + jitter)
                    else:
                        await asyncio.sleep(delay + jitter)
                    continue
                if reason == "unsafe_sent":
                    from .errors import UncertainDeliveryError
                    raise UncertainDeliveryError(
                        cmd=cmd,
                        op_id=op_id,
                        delivery=CommandStatus.ACCEPTED,
                    ) from e
                self._ledger.record(op_id, CommandStatus.FAILED)  # MCP-TRANS-008
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
                # A1/C8: Unity has already dispatched a command before emitting
                # this in-band hint, so use the same annotation-derived safety
                # predicate as the exception path's SENT gate.
                if not self._retry_policy.allow_hint_retry(cmd):
                    return result
                if attempt < MAX_RETRIES:
                    await asyncio.sleep(result["retry"] / 1000)
                    attempt += 1
                    # DEV-59/P-322: Unity already dispatched this command before
                    # replying with the in-band hint, so the resend must carry
                    # retry_op_id too -- same dedup contract as the SENT/exception
                    # retry path, otherwise C# DedupRegistry can't suppress a
                    # second execution of the mutation.
                    if op_id:
                        payload = _with_retry_op_id(payload, op_id)
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
        if not isinstance(data, dict):
            raise ConnectionError(
                f"Protocol desync: expected JSON object (dict), got non-dict {type(data).__name__} — reconnecting"
            )
        if data.get("ev") == "going_away":
            raise DomainReloadError(f"Unity domain reload: {data.get('reason', 'unknown')}")
        return data

    @staticmethod
    def _canonical_project_path(path: str | os.PathLike[str]) -> str:
        """Canonicalize a Unity project path without requiring it to exist."""
        return os.path.normcase(os.path.realpath(os.path.abspath(os.fspath(path))))

    def _capture_or_check_identity(self, project_id: str, project_path: str) -> None:
        """Capture editor identity on first handshake; raise on mismatch (MCP-SESS-024)."""
        from .errors import SessionIdentityMismatch
        new = EditorIdentity(project_id=project_id, project_path=project_path)
        if self._editor_identity is None:
            self._editor_identity = new
            return
        stored = self._editor_identity
        # Primary: compare project_id when both sides have it
        if stored.project_id and new.project_id:
            if stored.project_id != new.project_id:
                raise SessionIdentityMismatch(
                    f"Reconnect rejected: project changed from "
                    f"{stored.project_id!r} to {new.project_id!r}"
                )
            return
        # Fallback: path comparison when project_id is absent
        if stored.project_path and new.project_path and stored.project_path != new.project_path:
            raise SessionIdentityMismatch(
                f"Reconnect rejected: project path changed from "
                f"{stored.project_path!r} to {new.project_path!r}"
            )

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
        except (OSError, EOFError, UnicodeDecodeError, json.JSONDecodeError) as exc:
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
            hello_id = f"rc{self._counter:04x}"
            frame_write(writer, self._build_hello(hello_id))
            await writer.drain()
            hello_raw = await frame_read_with_timeout(reader, CONNECT_TIMEOUT)
            hello = json.loads(hello_raw.decode("utf-8"))

            if hello.get("ev") == "going_away":
                raise DomainReloadError("Unity going_away during reconnect")

            if hello.get("error") == "CLIENT_CAPACITY_BUSY":
                from .errors import CapacityBusyError
                raise CapacityBusyError(
                    f"Unity at capacity: {hello.get('active', '?')}/{hello.get('capacity', '?')} clients",
                    retry_after_seconds=float(hello.get("retry_after_seconds", 5.0)),
                    capacity=int(hello.get("capacity", 0)),
                    active=int(hello.get("active", 0)),
                )

            if hello.get("ok") and hello.get("helloVersion"):
                # New C#: combined response contains version + projectPath — 1 roundtrip.
                if self._expected_project_path is not None:
                    reported = hello.get("projectPath", "")
                    if not isinstance(reported, str) or not reported.strip():
                        raise _CandidateIdentityError(
                            f"Unity on port {port} returned empty projectPath in client_hello"
                        )
                    actual = self._canonical_project_path(reported.strip())
                    if actual != self._expected_project_path:
                        raise _CandidateIdentityError(
                            f"Refusing Unity on port {port}: expected project "
                            f"{self._expected_project_path!r}, received {actual!r}"
                        )
                # T19: extract stable project identity (cloudProjectId or sha256[:12])
                project_id = hello.get("projectId", "")
                if isinstance(project_id, str) and project_id.strip():
                    self._project_id = project_id.strip()
                # MCP-SESS-024: capture/enforce editor identity on every handshake
                _pid = project_id.strip() if isinstance(project_id, str) else ""
                _raw_path = hello.get("projectPath", "") or ""
                _canon = (
                    self._canonical_project_path(_raw_path.strip())
                    if _raw_path.strip() else ""
                )
                self._capture_or_check_identity(project_id=_pid, project_path=_canon)
                await self._check_version_from_hello(hello)
            else:
                # Old C#: hello returned ok:false — fallback to project check + get_version.
                # Connection is already proven live (we got a response), skip extra ping.
                await self._verify_candidate_project(reader, writer, port)
                await self._fetch_and_check_version(reader, writer)

            return reader, writer
        except BaseException:
            await self._close_candidate(writer)
            raise

    async def _check_version_from_hello(self, hello: dict) -> None:
        """Parse version from a client_hello response; non-fatal unless proto mismatch."""
        version_str = hello.get("version", "")
        if not version_str:
            return
        try:
            info = parse_version_string(version_str)
            check_protocol_version(PROTOCOL_VERSION, info.proto)
            if info.plugin:
                from .server_updater import _updater
                task = asyncio.create_task(_updater.maybe_update(info.plugin))
                _background_tasks.add(task)
                task.add_done_callback(_background_tasks.discard)
        except ConnectionError:
            raise
        except Exception as exc:
            logger.warning("Protocol version check failed (non-fatal): %s", exc)

    async def _fetch_and_check_version(self, reader, writer) -> None:
        """Send get_version and check protocol; non-fatal unless proto mismatch."""
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
                    task = asyncio.create_task(_updater.maybe_update(info.plugin))
                    _background_tasks.add(task)
                    task.add_done_callback(_background_tasks.discard)
        except ConnectionError:
            raise
        except Exception as exc:
            logger.warning("Protocol version check failed (non-fatal): %s", exc)

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

    def _current_port_for_pid(self, pid: int) -> int | None:
        try:
            from unity_mcp.lockfile import read_port_for_pid
            return read_port_for_pid(pid, project_path=self._expected_project_path)
        except Exception:
            return None

    def peek_port_drift_notice(self) -> str | None:
        """Non-destructive twin of pop_port_drift_notice(). Lets a caller
        check whether a notice is pending before deciding it's safe to
        consume (e.g. skipping a protocol-record or JSON response)."""
        drift = self._port_drift
        if drift is None:
            return None
        old_port, new_port = drift
        return f"port changed {old_port}->{new_port}"

    def pop_port_drift_notice(self) -> str | None:
        """Consume-once notice for a same-pid port rebind adopted by the
        fast path in _reconnect(). Returns None once already consumed."""
        notice = self.peek_port_drift_notice()
        self._port_drift = None
        return notice

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
        if pinned_is_live:
            # ARC-9 T2: my own pid may have rebound to a new port (bind
            # conflict fallback) without dying. Reading its port file
            # directly beats waiting for a doomed connect + backoff cycle.
            current_port = self._current_port_for_pid(self._pinned_pid)
            if current_port is not None and current_port != self._pinned_port:
                self._port_drift = (self._pinned_port, current_port)
                self._pinned_port = current_port
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
        if self._state == BridgeState.DORMANT:
            return "dormant"
        if self._state == BridgeState.WAKING:
            return "waking"
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
        if self._state == BridgeState.DORMANT:
            return "tcp:dormant"
        if self._state == BridgeState.WAKING:
            return "tcp:waking"
        if self._writer is not None and not self._writer.is_closing():
            return "tcp:connected"
        if self._state == BridgeState.FAILED:
            return "tcp:failed"
        return "tcp:reconnecting"

    @property
    def last_contact_age_s(self) -> float | None:
        """Seconds since the last successful contact with Unity (heartbeat pong
        or a completed send() round-trip). None until either has ever occurred
        — ARC-7 T2, diagnostic input for mcp_status (T3)."""
        if self._last_contact_at is None:
            return None
        return time.monotonic() - self._last_contact_at

    @property
    def pending_queue_depth(self) -> int:
        """Number of commands queued for the serial send consumer, not yet
        dispatched to Unity — a plain getter over the existing queue (ARC-7 T2)."""
        return self._send_queue.qsize()

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
                    cmd, payload, msg_id, timeout, deadline, op_id, future = self._send_queue.get_nowait()
                    self._ledger.record(op_id, CommandStatus.FAILED)
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

    async def suspend(self) -> bool:
        """CONNECTED → DORMANT. Returns True if suspended, False if aborted.

        Aborts when: state not CONNECTED, or queue non-empty under lock.
        """
        async with self._lock:
            if self._state != BridgeState.CONNECTED:
                return False
            if not self._send_queue.empty():
                return False
            self._state = BridgeState.DORMANT

        self.stop_heartbeat()
        w, self._writer = self._writer, None
        self._reader = None
        if w:
            sock = w.get_extra_info("socket")
            if sock is not None:
                with contextlib.suppress(OSError):
                    sock.shutdown(socket.SHUT_WR if os.name == "nt" else socket.SHUT_RDWR)
            w.close()
            with contextlib.suppress(Exception):
                await asyncio.wait_for(w.wait_closed(), timeout=2.0)
        self._last_reconnect_at = 0.0
        self._reconnect_backoff = BACKOFF_MIN_S
        # If a concurrent send() reconnected during wait_closed(), state is no longer DORMANT.
        # Return False so the caller knows suspension did not hold.
        return self._state == BridgeState.DORMANT
