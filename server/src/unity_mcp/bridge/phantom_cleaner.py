"""Phantom process cleanup — dead-PID lock/port file sweeping.

Extracted from lockfile.py: that module owns lock *acquisition* and
discovery *reads* (acquire_lock, read_pid_from_port_file, read_reload_port,
...); this module owns *cleanup* of stale discovery files left behind by
processes that exited without removing them.

Known limitation (pre-existing, not introduced here): is_pid_alive (and the
C# mirror, PortFileManager.IsProcessAlive) only check PID *existence*, not
identity/start-time. If the OS recycles a dead PID before a sweep runs,
cleanup_stale_port_files can mistake an unrelated live process holding that
recycled PID for the original one and leave a truly-stale file in place.
Not fixed here — documented so it isn't mistaken for new work.
"""
import socket
import time
from pathlib import Path

from unity_mcp.generated.timing_constants import TIMING_ROWS

from ..paths import ports_dir as _ports_dir
from ..paths import unity_mcp_dir
from .lockfile import _read_port_file_lines, is_pid_alive

_TIMING = TIMING_ROWS[0]


def cleanup_stale_locks(port: int, lock_dir: Path = None) -> int:
    """Delete lockfiles for dead PIDs. Returns count cleaned."""
    if lock_dir is None:
        lock_dir = unity_mcp_dir()
    lock_dir = Path(lock_dir)
    if not lock_dir.exists():
        return 0
    cleaned = 0
    for f in lock_dir.glob(f"server-{port}-*.lock"):
        try:
            pid = int(f.stem.rsplit("-", 1)[1])
        except (ValueError, IndexError):
            continue
        if not is_pid_alive(pid):
            try:
                f.unlink()
                cleaned += 1
            except OSError:
                pass
    return cleaned


# Cadence for the idle-heartbeat stale-port sweep (bridge_heartbeat throttles
# _maybe_sweep_stale_ports to at most once per this interval), and therefore
# the minimum real-world spacing between two tcp_probe=True calls from that
# caller. Mirrored independently in C# as MCPServer.PortSweepIntervalSeconds
# — not auto-synced across languages; update both definition sites together.
PORT_SWEEP_INTERVAL_S: float = 30.0

# C1 #11: a single failed tcp_probe must NOT delete a live-PID port file.
# Unity's own same-port bind-retry loop can leave the port briefly unbound
# while retrying after a bind conflict. Worst case (MCPServer.cs, Windows):
# 6 same-port attempts, linear backoff PortResolver.BackoffDelayMs(i) =
# 600ms * (i + 1) for i in 0..5:
#   sum_{i=0}^{5} 600*(i+1) = 600*(1+2+3+4+5+6) = 600*21 = 12600ms = 12.6s
# PROBE_GRACE_S must clear that window with margin. Sourced from the generated
# Timing row (Task 1.4) rather than a second hand literal; test_timing_constants.py
# pins PROBE_GRACE_S == PORT_SWEEP_INTERVAL_S so "failure persisted" still reads
# as "still failing on the NEXT sweep, ~30s later".
PROBE_GRACE_S: float = _TIMING.probeGraceS  # 30.0s >> 12.6s worst case

# port-file-path (str) -> monotonic time of its first consecutive probe
# failure. Module-level because cleanup_stale_port_files is invoked
# repeatedly (once per idle heartbeat tick) against the same files, so the
# grace window must survive across calls.
_tcp_probe_fail_since: dict[str, float] = {}


def _tcp_probe(port: int, timeout: float = 0.2) -> bool:
    """Return True if TCP port accepts connections."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=timeout):
            return True
    except OSError:
        return False


def cleanup_stale_port_files(tcp_probe: bool = False, now_fn=time.monotonic) -> int:
    """Delete port files for dead PIDs. Returns count cleaned.

    tcp_probe=True: also removes files where PID is alive but port is not
    listening (catches AssetImportWorker processes that hold a PID but lost
    the TCP server). A live-PID file is only deleted once its probe failure
    has PERSISTED for at least PROBE_GRACE_S: a single failed probe can be a
    sibling project's Unity mid-domain-reload or mid-bind-retry rather than a
    truly dead server (see PROBE_GRACE_S for the bind-retry-window
    derivation). A dead PID is still removed immediately, no grace applied.
    A single one-shot call (e.g. the startup sweep in server.py:~524) can
    therefore never delete a live PID's file on its own -- only a repeating
    caller, such as the idle heartbeat, persists across enough calls to
    outlast PROBE_GRACE_S and remove it.
    now_fn: injectable monotonic clock (default time.monotonic) so grace-period
    tests can advance time deterministically instead of monkeypatching the
    global time module.
    """
    ports_dir = _ports_dir()
    if not ports_dir.exists():
        return 0
    if tcp_probe:
        # Entries for files removed by another path (or a previous sweep)
        # must not accumulate forever.
        for key in [k for k in _tcp_probe_fail_since if not Path(k).exists()]:
            _tcp_probe_fail_since.pop(key, None)
    now = now_fn()
    cleaned = 0
    for pattern in ("*.port", "*.chat-port", "*.reload-port"):
        for f in ports_dir.glob(pattern):
            key = str(f)
            try:
                pid = int(f.stem)
                if not is_pid_alive(pid):
                    f.unlink()
                    cleaned += 1
                    _tcp_probe_fail_since.pop(key, None)
                    continue
                if tcp_probe:
                    lines = _read_port_file_lines(f, max_lines=1)
                    if not lines:
                        continue
                    port = int(lines[0])
                    if _tcp_probe(port):
                        _tcp_probe_fail_since.pop(key, None)
                        continue
                    first_fail = _tcp_probe_fail_since.setdefault(key, now)
                    if now - first_fail >= PROBE_GRACE_S:
                        f.unlink()
                        cleaned += 1
                        _tcp_probe_fail_since.pop(key, None)
            except (ValueError, OSError):
                pass
    return cleaned
