import asyncio
import json
import logging
import os
import random
import threading
import time

from unity_mcp.bridge_socket import DomainReloadError, frame_write
from unity_mcp.lockfile import cleanup_stale_port_files

logger = logging.getLogger(__name__)

# Exponential backoff bounds for reconnect attempts.
BACKOFF_MIN_S: float = 5.0
BACKOFF_MAX_S: float = 60.0
# Fast reconnect backoff for expected domain reloads (PlayMode enter/exit).
# Keep separate from BACKOFF_MIN_S so unexpected disconnects still back off at 5s.
RELOAD_BACKOFF_S: float = 1.0
# ARC-7 T1: minimum interval between stale-port sweeps from the idle heartbeat
# branch. Mirrors the reconnect-callback debounce precedent (server.py ~591).
PORT_SWEEP_INTERVAL_S: float = 30.0

_hard_exit_scheduled: bool = False


def _schedule_hard_exit() -> None:
    """Schedule os._exit(0) after 2s delay — gives current heartbeat tick time to finish."""
    global _hard_exit_scheduled
    if _hard_exit_scheduled:
        return
    _hard_exit_scheduled = True
    import logging
    logging.getLogger("unity_mcp.bridge").warning("Parent died — scheduling hard exit in 2s")
    t = threading.Timer(2.0, os._exit, args=(0,))
    t.daemon = True
    t.start()

# Captured at import time — all bridges in this process share the same parent.
_ORIGINAL_PPID: int = os.getppid()


class ProtocolDesyncError(ConnectionError):
    """Raised when the heartbeat receives an ID-mismatched ping response.

    Unlike a dead-process detection, this indicates the TCP stream is desynchronised
    (in-flight responses crossed). The connection should be drained and re-established
    without voting the Unity process as dead — is_pid_alive must be consulted first.
    """


# P7: hard deadline = 5× STARTUP_GRACE_S. Latches even while busy so a truly
# stuck reconnect loop eventually gives up without waiting for PID death.
HARD_DEADLINE_S: float = 450.0


class HeartbeatMixin:
    """Heartbeat loop and reconnection scheduling. No __init__.

    Expected instance attributes (set by UnityBridge.__init__):
      _heartbeat_task, _heartbeat_interval, _ping_failures,
      _last_reconnect_at, _min_reconnect_interval,
      _lock, _probe, _writer, _counter, _reader,
      _ppid_mismatch_count, _reload (DomainReloadTracker), _state (BridgeState)
    """

    def start_heartbeat(self, interval: float = 15.0) -> None:
        if self._heartbeat_task is not None and not self._heartbeat_task.done():
            return
        self._heartbeat_interval = interval
        self._heartbeat_task = asyncio.get_running_loop().create_task(self._heartbeat_loop(interval))

    def stop_heartbeat(self) -> None:
        if self._heartbeat_task is not None:
            self._heartbeat_task.cancel()
            self._heartbeat_task = None

    async def _heartbeat_loop(self, interval: float) -> None:
        while True:
            try:
                await self._heartbeat_tick(interval)
            except asyncio.CancelledError:  # noqa: PERF203
                raise
            except Exception:
                # Safety net: never let heartbeat task die silently.
                await asyncio.sleep(5.0)

    async def _heartbeat_tick(self, interval: float) -> None:
        """Single heartbeat iteration. Separated for safety-net wrapping."""
        # Parent death: stop heartbeat after grace period expires.
        # Never raise SystemExit/BaseException from a background task — it kills
        # the anyio task group, closing stdio → -32000 for any in-flight MCP call.
        if self._check_orphan():
            return
        if not self.connected:
            await self._tick_disconnected()
            return
        await self._tick_connected(interval)

    def _check_orphan(self) -> bool:
        """Return True if parent died (tick should abort). Resets counters on live parent."""
        if os.getppid() == _ORIGINAL_PPID:
            self._ppid_mismatch_count = 0
            self._orphan_detected_at = None
            return False
        self._ppid_mismatch_count += 1  # kept for diagnostics
        if self._orphan_detected_at is None:
            self._orphan_detected_at = time.monotonic()
        from .global_config import GlobalConfig
        cfg = GlobalConfig.load()
        terminate_orphan, _ = cfg.effective_terminate_orphan()
        if not terminate_orphan:
            return True  # permanent bridge mode — never self-terminate
        grace_s, _ = cfg.effective_orphan_grace_s()
        if time.monotonic() - self._orphan_detected_at >= grace_s:
            _schedule_hard_exit()
            self.stop_heartbeat()
        return True

    def _init_reconnect_timers(self) -> None:
        """Set reconnect and hard-deadline clocks on first disconnected tick."""
        if self._reconnect_started_at is None:
            self._reconnect_started_at = time.monotonic()
        # Hard deadline uses a separate clock — set once, never reset while busy.
        if getattr(self, "_hard_deadline_started_at", None) is None:
            self._hard_deadline_started_at = time.monotonic()

    def _check_hard_deadline(self) -> bool:
        """Return True and mark grace expired if hard deadline elapsed."""
        hard_elapsed = time.monotonic() - self._hard_deadline_started_at
        if hard_elapsed <= HARD_DEADLINE_S:
            return False
        # P7: hard deadline — latches even while busy; prevents eternal reconnect loop.
        self._startup_grace_expired = True
        if hasattr(self, "_on_unavailable") and self._on_unavailable:
            self._on_unavailable()
        return True

    async def _try_reconnect(self) -> None:
        """Attempt reconnect if cooldown elapsed and not actively reloading."""
        if not self._reconnect_cooldown_ok():
            return
        if self._reload.is_active() and self._probe_busy():
            return
        # A2: arm cooldown BEFORE attempt — success and failure both count.
        self._last_reconnect_at = time.monotonic()
        async with self._lock:
            if self.connected:
                return
            try:
                from unity_mcp.metrics import METRICS
                await self._reconnect()
                METRICS.inc("reconnect.heartbeat")
                self._ping_failures = 0
                # B1: success — reset backoff for fast recovery after Unity returns.
                self._reconnect_backoff = BACKOFF_MIN_S
            except Exception:
                # B1: failure — double backoff (exponential dampening).
                # N3: cap AFTER jitter so result never exceeds BACKOFF_MAX_S.
                self._reconnect_backoff = min(
                    self._reconnect_backoff * 2 * (1.0 + random.uniform(-0.1, 0.1)),
                    BACKOFF_MAX_S,
                )

    async def _tick_disconnected(self) -> None:
        """Handle one heartbeat tick when not connected."""
        import unity_mcp.bridge as _bm  # lazy to avoid circular at module level
        # DORMANT: intentional TCP close — heartbeat must not attempt reconnect.
        if self._state == _bm.BridgeState.DORMANT:
            return
        self._init_reconnect_timers()
        busy = self._probe_busy()
        if busy:
            self._reconnect_started_at = time.monotonic()
        reload_active = self._reload.is_active()
        if reload_active and not busy:
            self._reconnect_backoff = min(self._reconnect_backoff, RELOAD_BACKOFF_S)
            wait = RELOAD_BACKOFF_S
        elif busy:
            wait = 5.0
        else:
            wait = 2.0
            self._maybe_sweep_stale_ports()
        await asyncio.sleep(wait)

        if self._check_hard_deadline():
            return
        elapsed = time.monotonic() - self._reconnect_started_at
        # Check grace deadline: if elapsed > STARTUP_GRACE_S and not busy,
        # stop silently looping — next send() will surface the STOP error.
        if elapsed > _bm.STARTUP_GRACE_S and not busy:
            self._startup_grace_expired = True
            return
        await self._try_reconnect()

    def _maybe_sweep_stale_ports(self) -> None:
        """Periodic ghost-port cleanup — only reached from the idle disconnected branch.

        Reuses cleanup_stale_port_files(tcp_probe=True) unchanged (PID-check first,
        cheap; TCP-probe second, only for PID-alive entries). Throttled by
        PORT_SWEEP_INTERVAL_S so every idle tick doesn't re-scan the ports dir.
        """
        now = time.monotonic()
        # getattr default: HeartbeatMixin is also exercised via bare test stubs
        # that don't carry every UnityBridge field (not in the documented
        # contract above) — mirrors _hard_deadline_started_at's own guard.
        if now - getattr(self, "_last_port_sweep_at", 0.0) < PORT_SWEEP_INTERVAL_S:
            return
        self._last_port_sweep_at = now
        cleanup_stale_port_files(tcp_probe=True)

    async def _handle_ping_timeout(self) -> None:
        """Handle TimeoutError from ping: apply stall counter, close only when dead or stalled."""
        # Timeout = Unity alive but unresponsive (App Nap / heavy compile).
        self._ping_failures += 1
        if self._ping_failures < 3:
            return
        if self._probe.is_process_dead():
            async with self._lock:
                await self.close()
            self._ping_failures = 0
            self._ping_stall_failures = 0
        else:
            self._ping_stall_failures += 1
            logger.warning("Unity ping stall #%d (process alive)", self._ping_stall_failures)
            self._ping_failures = 0
            if self._ping_stall_failures >= 6:
                logger.error("Unity unreachable for 6 stall windows (~6 min) — closing")
                async with self._lock:
                    await self.close()
                self._ping_stall_failures = 0

    async def _tick_connected(self, interval: float) -> None:
        """Handle one heartbeat tick when connected: sleep then probe with a ping."""
        await asyncio.sleep(interval)
        if self._lock.locked():
            return
        try:
            await self._raw_ping(timeout=5.0)
            self._ping_failures = 0
            self._ping_stall_failures = 0
        except DomainReloadError:
            self._probe.mark_recompile_issued()
            self._reload.mark()          # FIX: mark so send() gets extended retry window
            async with self._lock:
                await self.close()
            self._ping_failures = 0
            self._ping_stall_failures = 0
        except ProtocolDesyncError:
            # ID mismatch = stream desync, not necessarily process death.
            # Drain and reconnect without voting the process dead.
            async with self._lock:
                await self.close()
            self._ping_failures = 0
            self._ping_stall_failures = 0
        except TimeoutError:
            await self._handle_ping_timeout()
        except Exception:
            # Connection error (ConnectionReset, IncompleteRead, OSError, etc.)
            # = dead TCP, not App Nap. Close immediately and reconnect.
            async with self._lock:
                await self.close()
            self._ping_failures = 0
            self._ping_stall_failures = 0

    def _reconnect_cooldown_ok(self) -> bool:
        """True if enough time elapsed since last reconnect attempt (success or failure)."""
        return (time.monotonic() - self._last_reconnect_at) >= self._reconnect_backoff

    async def _raw_ping(self, timeout: float = 5.0) -> None:
        """Send ping directly on socket, bypassing send() retry machinery.

        P4 SAFETY: both _raw_ping and send() hold self._lock for their full
        write→read cycle. asyncio.Lock serialises them — heartbeat ping can
        never interleave with a tool-call response, so ID collision is impossible.
        """
        async with self._lock:
            if not self.connected:
                raise ConnectionError("Not connected")
            self._counter += 1
            ping_id = f"hb{self._counter:04x}"
            payload = json.dumps({"id": ping_id, "cmd": "ping", "args": {}}, ensure_ascii=False).encode("utf-8")
            frame_write(self._writer, payload)
            await self._writer.drain()
            resp = await asyncio.wait_for(self._read_response(), timeout=timeout)
            if resp.get("id") != ping_id:
                # P7: ID mismatch = TCP stream desync, not process death.
                raise ProtocolDesyncError(
                    f"Heartbeat ID mismatch: {resp.get('id')} != {ping_id}"
                )
            on_ta = getattr(self, "_on_transport_activity", None)
            if on_ta is not None:
                on_ta()
            self._last_contact_at = time.monotonic()  # ARC-7 T2: confirmed pong
