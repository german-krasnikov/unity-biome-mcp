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
from dataclasses import dataclass
from typing import Callable, Optional
from .constants import DEFAULT_PORT, SESSION_TIMEOUT

logger = logging.getLogger(__name__)

from unity_mcp.bridge_socket import (
    DomainReloadError,
    _apply_socket_options,
    _TCP_KEEPALIVE_DARWIN,
    _TCP_KEEPINTVL_DARWIN,
    frame_read,
    frame_write,
    frame_read_with_timeout,
)
from unity_mcp.bridge_heartbeat import HeartbeatMixin, BACKOFF_MIN_S
from unity_mcp.bridge_reload_state import DomainReloadTracker, DOMAIN_RELOAD_EXPIRY_S
from unity_mcp.bridge_retry import RetryPolicy
from unity_mcp.compile_state import CompileStateProbe
from unity_mcp.crash_log import CrashLogger
from unity_mcp.metrics import METRICS
from unity_mcp.lockfile import is_pid_alive

# Re-export so existing `from .bridge import DomainReloadError` keeps working
__all__ = [
    "UnityBridge", "DomainReloadError", "BridgeState",
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
            "Upgrade the Unity MCP plugin package.", unity_proto, python_proto
        )
    elif python_proto < unity_proto:
        raise ConnectionError(
            f"Python MCP server must upgrade: Unity proto {unity_proto} > Python proto {python_proto}. "
            "Run: pip install --upgrade unity-mcp"
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


class UnityBridge(HeartbeatMixin):
    """TCP client for Unity Editor communication."""

    def __init__(self, host: str = "127.0.0.1", port: Optional[int] = None,
                 probe: Optional[CompileStateProbe] = None,
                 port_discoverer: Optional[Callable[[], int]] = None,
                 is_retry_safe: Optional[Callable[[str], bool]] = None):
        self._host = host
        try:
            self._port = port or int(os.environ.get("UNITY_MCP_PORT", str(DEFAULT_PORT)))
        except ValueError:
            self._port = DEFAULT_PORT
        self._reader = None
        self._writer = None
        self._counter = 0
        self._lock = asyncio.Lock()
        self._probe: CompileStateProbe = probe if probe is not None else CompileStateProbe(
            CompileStateProbe.autodetect_project_path(port=self._port), port=self._port
        )
        self._first_failure_ts: Optional[float] = None
        self._reconnect_started_at: Optional[float] = None
        self._hard_deadline_started_at: Optional[float] = None
        self._state: BridgeState = BridgeState.DISCONNECTED
        self._on_reconnect_callbacks: list = []
        self._crash_log = CrashLogger()
        self._heartbeat_task: Optional[asyncio.Task] = None
        self._heartbeat_interval: float = 15.0
        self._ping_failures: int = 0
        self._ping_stall_failures: int = 0
        self._last_reconnect_at: float = 0.0
        self._min_reconnect_interval: float = MIN_RECONNECT_INTERVAL  # kept for compat
        self._reconnect_backoff: float = BACKOFF_MIN_S
        self._port_discoverer: Optional[Callable[[], int]] = port_discoverer
        self._reload: DomainReloadTracker = DomainReloadTracker()
        self._reload_gate: asyncio.Event = asyncio.Event()
        self._reload_gate.set()  # open by default; wait() returns immediately
        self._ppid_mismatch_count: int = 0
        self._pinned_port: Optional[int] = None
        self._pinned_pid: Optional[int] = None
        self._bridge_id: str = f"br-{os.getpid():x}-{id(self) & 0xFFFF:04x}"
        self._is_retry_safe: Callable[[str], bool] = is_retry_safe or (lambda cmd: False)
        self._retry_policy = RetryPolicy(
            probe=self._probe, reload=self._reload,
            is_retry_safe=self._is_retry_safe, max_retries=MAX_RETRIES,
        )

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
        self._reader, self._writer = await asyncio.wait_for(
            asyncio.open_connection(self._host, self._port),
            timeout=CONNECT_TIMEOUT,
        )
        _apply_socket_options(self._writer.get_extra_info("socket"))

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
        if self._state == BridgeState.FAILED:
            if not self._reconnect_cooldown_ok():
                raise ConnectionError("Reconnect cooldown active — retry in a moment")
            self._last_reconnect_at = time.monotonic()
            try:
                async with self._lock:
                    await self._reconnect(fire_callbacks=False)
            except Exception:
                raise ConnectionError(self._describe_failure(cmd, ConnectionRefusedError()))
        self._counter += 1
        msg_id = f"{self._counter:04x}"
        payload = json.dumps({"id": msg_id, "cmd": cmd, "args": args}, ensure_ascii=False).encode("utf-8")
        if len(payload) > 10_000_000:
            raise ConnectionError(f"Outbound payload too large: {len(payload)} bytes (max 10MB)")
        session_deadline = time.monotonic() + SESSION_TIMEOUT
        return await self._send_with_retry(cmd, payload, msg_id, timeout, session_deadline)

    async def _send_with_retry(self, cmd: str, payload: bytes,
                               msg_id: str, timeout: float, session_deadline: float) -> dict:
        attempt = 0
        result = None
        # Cooldown gate: fail-fast on FIRST attempt only — prevent burst reconnect storms.
        # Retries within this call are already gated by the sleep(delay) in the retry loop.
        if not self.connected and not self._reconnect_cooldown_ok():
            raise ConnectionError("Reconnect cooldown active — retry in a moment")
        while attempt <= MAX_RETRIES:
            if time.monotonic() > session_deadline:
                raise TimeoutError(f"Session deadline ({SESSION_TIMEOUT}s) exceeded")

            try:
                async with self._lock:
                    if not self.connected:
                        self._last_reconnect_at = time.monotonic()
                        await self._reconnect(fire_callbacks=False)
                        METRICS.inc("reconnect.send_path")
                    frame_write(self._writer, payload)
                    await self._writer.drain()
                    try:
                        result = await asyncio.wait_for(
                            self._read_response(), timeout=timeout)
                    except asyncio.CancelledError:
                        await self.close()
                        raise
            except (ConnectionRefusedError, asyncio.TimeoutError, ConnectionError,
                    asyncio.IncompleteReadError, OSError, json.JSONDecodeError,
                    RuntimeError) as e:
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
                    jitter = random.uniform(0, delay * 0.1)
                    if reason == "domain_reload":
                        self._reload_gate.clear()
                        try:
                            await asyncio.wait_for(
                                self._reload_gate.wait(), timeout=delay + jitter)
                        except asyncio.TimeoutError:
                            pass
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

    async def _reconnect(self, fire_callbacks: bool = True):
        await self.close()
        if self._port_discoverer is not None:
            try:
                # B2: explicit None guard — is_pid_alive(None) returns False (intentional
                # fallthrough), but we want deliberate bypass when pid is unknown.
                if (self._pinned_port is not None and self._pinned_pid is not None
                        and is_pid_alive(self._pinned_pid)):
                    new_port = self._pinned_port
                else:
                    import inspect
                    kw = {"skip_probe": True} if "skip_probe" in inspect.signature(self._port_discoverer).parameters else {}
                    new_port = self._port_discoverer(**kw)
                # B3: None means no live candidates — preserve current port.
                if new_port is not None and new_port != self._port:
                    self._port = new_port
                    self._probe = CompileStateProbe(
                        CompileStateProbe.autodetect_project_path(port=new_port), port=new_port)
                    # C8: RetryPolicy holds probe by reference — keep it in sync
                    # so a post-migration busy-check doesn't consult a stale probe
                    # still pointed at the old port.
                    self._retry_policy.probe = self._probe
            except Exception:
                pass
        reader, writer = await asyncio.wait_for(
            asyncio.open_connection(self._host, self._port),
            timeout=CONNECT_TIMEOUT,
        )
        _apply_socket_options(writer.get_extra_info("socket"))
        try:
            self._counter += 1
            ping_id = f"rc{self._counter:04x}"
            _client = os.environ.get("UNITY_MCP_CLIENT", "")
            _role = _client or "mcp"
            ping = json.dumps({"id": ping_id, "cmd": "ping", "role": _role, "args": {}}, ensure_ascii=False).encode("utf-8")
            frame_write(writer, ping)
            await writer.drain()
            # Read ping response directly from local reader (not self._reader)
            # to avoid _reader/_writer desync during the await window.
            pay_bytes = await frame_read_with_timeout(reader, CONNECT_TIMEOUT)
            pong = json.loads(pay_bytes.decode("utf-8"))
            if pong.get("ev") == "going_away":
                raise DomainReloadError("Unity going_away during reconnect")
            if not pong.get("ok"):
                raise ConnectionError("Unity ping failed after reconnect")
        except BaseException:
            writer.close()
            try:
                await writer.wait_closed()
            except Exception:
                pass
            raise
        # Protocol version check (non-fatal except Python < Unity proto)
        try:
            ver_msg = json.dumps({"id": "ver", "cmd": "get_version", "args": {}},
                                 ensure_ascii=False).encode("utf-8")
            frame_write(writer, ver_msg)
            await writer.drain()
            ver_pay = await frame_read_with_timeout(reader, CONNECT_TIMEOUT)
            ver_resp = json.loads(ver_pay.decode("utf-8"))
            if ver_resp.get("ok") and ver_resp.get("data"):
                info = parse_version_string(ver_resp["data"])
                check_protocol_version(PROTOCOL_VERSION, info.proto)
        except ConnectionError:
            raise  # Python < Unity proto — hard error per spec
        except Exception as e:
            logger.warning("Protocol version check failed (non-fatal): %s", e)

        # Atomic: assign both only after ping succeeds, no await between them.
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
        # Pin port+pid so future reconnects stay on same Unity instance while alive.
        self._pinned_port = self._port
        try:
            from unity_mcp.lockfile import read_pid_from_port_file
            self._pinned_pid = read_pid_from_port_file(self._port)
        except Exception:
            self._pinned_pid = None
        self.start_heartbeat(self._heartbeat_interval)
        if fire_callbacks:
            for cb in self._on_reconnect_callbacks:
                try:
                    cb()
                except Exception:
                    pass

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

    async def close(self):
        if asyncio.current_task() is not self._heartbeat_task:
            self.stop_heartbeat()
        w = self._writer
        self._writer = None
        self._reader = None
        if w:
            sock = w.get_extra_info("socket")
            if sock is not None:
                try:
                    sock.shutdown(socket.SHUT_RDWR)
                except OSError:
                    pass
            w.close()
            try:
                await asyncio.wait_for(w.wait_closed(), timeout=2.0)
            except Exception:
                pass
