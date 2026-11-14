"""Back-compat shim pins for the bridge/ package extraction (Phase 4).

11 root modules became sys.modules-aliased shims pointing at their new
unity_mcp.bridge.* home (see Plans/task-p4-bridge-package.md). Each pin here
is two assertions:
  1. identity — old and new dotted names resolve to the SAME module object,
     not a copy (sys.modules aliasing, not the read-only __getattr__/PEP-562
     pattern used elsewhere in this codebase).
  2. patch-propagation — monkeypatching the OLD path must be observable
     through the NEW path. This is the false-green gap a `__getattr__` shim
     would reopen: ~100 existing tests patch these modules by old string
     path, and a distinct shim module object would silently stop affecting
     the real code those patches target.
"""
import unity_mcp.bridge.cassette
import unity_mcp.bridge.compile_state
import unity_mcp.bridge.connection_slot
import unity_mcp.bridge.crash_log
import unity_mcp.bridge.heartbeat
import unity_mcp.bridge.lockfile
import unity_mcp.bridge.phantom_cleaner
import unity_mcp.bridge.reload_state
import unity_mcp.bridge.retry
import unity_mcp.bridge.socket_io
import unity_mcp.bridge.unity_state
import unity_mcp.bridge_cassette
import unity_mcp.bridge_heartbeat
import unity_mcp.bridge_reload_state
import unity_mcp.bridge_retry
import unity_mcp.bridge_socket
import unity_mcp.compile_state
import unity_mcp.connection_slot
import unity_mcp.crash_log
import unity_mcp.lockfile
import unity_mcp.phantom_cleaner
import unity_mcp.unity_state


def test_socket_io_shim_is_same_module_object():
    assert unity_mcp.bridge_socket is unity_mcp.bridge.socket_io


def test_socket_io_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.bridge_socket.frame_write", "sentinel")
    assert unity_mcp.bridge.socket_io.frame_write == "sentinel"


def test_cassette_shim_is_same_module_object():
    assert unity_mcp.bridge_cassette is unity_mcp.bridge.cassette


def test_cassette_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.bridge_cassette.record", "sentinel")
    assert unity_mcp.bridge.cassette.record == "sentinel"


def test_crash_log_shim_is_same_module_object():
    assert unity_mcp.crash_log is unity_mcp.bridge.crash_log


def test_crash_log_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.crash_log.CrashLogger", "sentinel")
    assert unity_mcp.bridge.crash_log.CrashLogger == "sentinel"


def test_unity_state_shim_is_same_module_object():
    assert unity_mcp.unity_state is unity_mcp.bridge.unity_state


def test_unity_state_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.unity_state.read_state_for_port", "sentinel")
    assert unity_mcp.bridge.unity_state.read_state_for_port == "sentinel"


def test_reload_state_shim_is_same_module_object():
    assert unity_mcp.bridge_reload_state is unity_mcp.bridge.reload_state


def test_reload_state_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.bridge_reload_state.DomainReloadTracker", "sentinel")
    assert unity_mcp.bridge.reload_state.DomainReloadTracker == "sentinel"


def test_lockfile_shim_is_same_module_object():
    assert unity_mcp.lockfile is unity_mcp.bridge.lockfile


def test_lockfile_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.lockfile.is_pid_alive", lambda pid: "sentinel")
    assert unity_mcp.bridge.lockfile.is_pid_alive(1) == "sentinel"


def test_phantom_cleaner_shim_is_same_module_object():
    assert unity_mcp.phantom_cleaner is unity_mcp.bridge.phantom_cleaner


def test_phantom_cleaner_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.phantom_cleaner.cleanup_stale_port_files", "sentinel")
    assert unity_mcp.bridge.phantom_cleaner.cleanup_stale_port_files == "sentinel"


def test_compile_state_shim_is_same_module_object():
    assert unity_mcp.compile_state is unity_mcp.bridge.compile_state


def test_compile_state_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.compile_state.CompileStateProbe", "sentinel")
    assert unity_mcp.bridge.compile_state.CompileStateProbe == "sentinel"


def test_retry_shim_is_same_module_object():
    assert unity_mcp.bridge_retry is unity_mcp.bridge.retry


def test_retry_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.bridge_retry.RetryPolicy", "sentinel")
    assert unity_mcp.bridge.retry.RetryPolicy == "sentinel"


def test_heartbeat_shim_is_same_module_object():
    assert unity_mcp.bridge_heartbeat is unity_mcp.bridge.heartbeat


def test_heartbeat_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.bridge_heartbeat.HeartbeatMixin", "sentinel")
    assert unity_mcp.bridge.heartbeat.HeartbeatMixin == "sentinel"


def test_connection_slot_shim_is_same_module_object():
    assert unity_mcp.connection_slot is unity_mcp.bridge.connection_slot


def test_connection_slot_shim_patch_affects_real_module(monkeypatch):
    monkeypatch.setattr("unity_mcp.connection_slot.ConnectionSlot", "sentinel")
    assert unity_mcp.bridge.connection_slot.ConnectionSlot == "sentinel"
