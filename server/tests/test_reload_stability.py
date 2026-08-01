"""Stress tests — reload stability. Covers 13 fixes (CP-1 through SD-4, SH-8).
All tests run without Unity (marker: not live).
"""
import json
import socket as _socket
from pathlib import Path
from unittest.mock import patch, MagicMock

import pytest

from unity_mcp.lockfile import (
    cleanup_stale_port_files,
    read_pid_from_port_file,
    is_pid_alive,
)
from unity_mcp.compile_state import CompileStateProbe, _DISCONNECT_WINDOW_S
from unity_mcp.bridge_reload_state import DomainReloadTracker, DOMAIN_RELOAD_EXPIRY_S

# ---------------------------------------------------------------------------
# Paths to C# source files (Group C)
# ---------------------------------------------------------------------------
_PROJECT = Path(__file__).parents[2]
_PLUGIN = _PROJECT / "unity-plugin"
_RELOAD = _PROJECT / "unity-plugin-reload"


# ===========================================================================
# Group A: Compile Detection constants
# ===========================================================================

def test_domain_reload_expiry_is_90s():
    """DOMAIN_RELOAD_EXPIRY_S must be 90.0 — aligns with STARTUP_GRACE_S to prevent premature flood."""
    assert DOMAIN_RELOAD_EXPIRY_S == 90.0


def test_disconnect_window_is_120s():
    """_DISCONNECT_WINDOW_S must be 120.0 (SESSION_TIMEOUT, separate from DOMAIN_RELOAD_EXPIRY_S)."""
    assert _DISCONNECT_WINDOW_S == 120.0


def test_domain_reload_tracker_active_at_60s():
    """At 60s elapsed, tracker still active (< 90s expiry)."""
    tracker = DomainReloadTracker()
    with patch("unity_mcp.bridge_reload_state.time") as t:
        t.monotonic.side_effect = [0.0, 60.0]
        tracker.mark()
        assert tracker.is_active() is True


def test_domain_reload_tracker_expires_after_90s():
    """After 91s, tracker auto-clears and returns False."""
    tracker = DomainReloadTracker()
    with patch("unity_mcp.bridge_reload_state.time") as t:
        t.monotonic.side_effect = [0.0, 91.0]
        tracker.mark()
        assert tracker.is_active() is False
        assert tracker._active is False  # must auto-clear internal flag


def test_compile_error_state_has_strong_busy_signal():
    """state file: not stale, is_busy=True → has_strong_busy_signal() True."""
    state = MagicMock()
    state.is_stale = False
    state.is_busy = True
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is True


# ===========================================================================
# Group B: Bridge Resilience — cleanup & startup probe
# ===========================================================================

def test_cleanup_removes_10_dead_port_files(tmp_path):
    """10 dead-PID .port files all deleted; returns 10."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    for pid in range(99990, 100000):
        (ports_dir / f"{pid}.port").write_text("9500\n/some/path\n", encoding="utf-8")
    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        assert cleanup_stale_port_files() == 10
    assert list(ports_dir.iterdir()) == []


def test_cleanup_keeps_alive_removes_dead(tmp_path):
    """3 dead + 2 alive: 3 deleted, 2 kept."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    for pid in [111, 222, 333]:
        (ports_dir / f"{pid}.port").write_text("9500\n", encoding="utf-8")
    for pid in [444, 555]:
        (ports_dir / f"{pid}.port").write_text("9501\n", encoding="utf-8")

    def is_alive(pid):
        return pid in (444, 555)

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", side_effect=is_alive):
        assert cleanup_stale_port_files() == 3
    remaining = {f.name for f in ports_dir.iterdir()}
    assert remaining == {"444.port", "555.port"}


def test_cleanup_handles_all_port_patterns(tmp_path):
    """Cleans .port, .chat-port, .reload-port patterns in one call."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "11111.port").write_text("9500\n", encoding="utf-8")
    (ports_dir / "22222.chat-port").write_text("9510\n", encoding="utf-8")
    (ports_dir / "33333.reload-port").write_text("9600\n", encoding="utf-8")
    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        assert cleanup_stale_port_files() == 3
    assert list(ports_dir.iterdir()) == []


def test_read_pid_skips_dead_returns_alive(tmp_path):
    """2 dead + 1 alive port files for same port — returns alive PID only."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    for pid in [11111, 22222]:
        (ports_dir / f"{pid}.port").write_text("9500\n/proj\n", encoding="utf-8")
    (ports_dir / "33333.port").write_text("9500\n/proj\n", encoding="utf-8")

    def is_alive(pid):
        return pid == 33333

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", side_effect=is_alive):
        assert read_pid_from_port_file(9500) == 33333


def test_startup_not_in_progress_when_tcp_responds():
    """State absent + PID alive + TCP responds → False (Unity ready)."""
    mock_sock = MagicMock()
    mock_sock.connect.return_value = None
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", return_value=mock_sock):
        assert CompileStateProbe(port=9500).is_startup_in_progress() is False


def test_startup_in_progress_when_tcp_refuses():
    """State absent + PID alive + TCP refuses → True (genuinely starting)."""
    mock_sock = MagicMock()
    mock_sock.connect.side_effect = OSError("Connection refused")
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", return_value=mock_sock):
        assert CompileStateProbe(port=9500).is_startup_in_progress() is True


def test_startup_false_when_state_present():
    """State file exists → short-circuit False without TCP probe."""
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=MagicMock()), \
         patch("socket.socket") as mock_socket:
        assert CompileStateProbe(port=9500).is_startup_in_progress() is False
    mock_socket.assert_not_called()


def test_startup_false_when_no_port():
    """port=None → False always."""
    assert CompileStateProbe(port=None).is_startup_in_progress() is False


def test_autodetect_project_path_passes_port(monkeypatch):
    """autodetect_project_path(port=9500) must call read_project_path_from_port_file(9500)."""
    monkeypatch.delenv("UNITY_MCP_PROJECT_PATH", raising=False)
    with patch("unity_mcp.compile_state.read_project_path_from_port_file") as mock_r:
        mock_r.return_value = Path("/project")
        result = CompileStateProbe.autodetect_project_path(port=9500)
    mock_r.assert_called_once_with(9500)
    assert result == Path("/project")


# ===========================================================================
# Group C: Source Verification — structural invariants via text search
# ===========================================================================

def test_syncher_no_tundra_digestcache():
    """SyncHelper.cs must NOT delete tundra.digestcache (corrupts Bee artifact graph)."""
    src = (_PLUGIN / "Editor/SyncHelper.cs").read_text(encoding="utf-8")
    assert "tundra.digestcache" not in src


def test_reloadguard_forceunlock_calls_refresh():
    """ReloadGuard.ForceUnlock must call AssetDatabase.Refresh() to re-arm file watcher."""
    src = (_PLUGIN / "Editor/Chat/CLI/ReloadGuard.cs").read_text(encoding="utf-8")
    assert "AssetDatabase.Refresh()" in src


def test_compute_stamp_uses_get_assemblies():
    """ComputeStamp must use GetAssemblies() for multi-assembly MVID stamp."""
    src = (_PLUGIN / "Editor/SyncHelper.cs").read_text(encoding="utf-8")
    assert "GetAssemblies()" in src


def test_mcp_status_window_has_on_disable():
    """MCPStatusWindow must have OnDisable to pause schedulers before domain reload."""
    src = (_PLUGIN / "Editor/Wizard/MCPStatusWindow.cs").read_text(encoding="utf-8")
    assert "OnDisable" in src


def test_mcp_status_window_has_moved_from():
    """MCPStatusWindow must have [MovedFrom] for layout migration."""
    src = (_PLUGIN / "Editor/Wizard/MCPStatusWindow.cs").read_text(encoding="utf-8")
    assert "[MovedFrom" in src


def test_reload_mini_server_has_active_clients():
    """ReloadMiniServer must have _activeClients tracking field."""
    src = (_RELOAD / "Editor/ReloadMiniServer.cs").read_text(encoding="utf-8")
    assert "_activeClients" in src
    assert ".Close()" in src


def test_wizard_asmdef_auto_referenced_false():
    """Wizard asmdef must have autoReferenced=false to avoid circular references."""
    asmdef = (_PLUGIN / "Editor/Wizard/UnityMCP.Editor.Wizard.asmdef").read_text(encoding="utf-8")
    data = json.loads(asmdef)
    assert data.get("autoReferenced") is False


# ===========================================================================
# Group D: Edge Cases
# ===========================================================================

def test_cleanup_empty_ports_dir(tmp_path):
    """Empty ports dir → returns 0, no crash."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir):
        assert cleanup_stale_port_files() == 0


def test_cleanup_nonexistent_dir(tmp_path):
    """Missing ports dir → returns 0, no crash."""
    with patch("unity_mcp.lockfile._ports_dir", return_value=tmp_path / "missing"):
        assert cleanup_stale_port_files() == 0


def test_read_pid_no_matching_port(tmp_path):
    """Port file exists but for different port → returns None."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "12345.port").write_text("9501\n", encoding="utf-8")
    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=True):
        assert read_pid_from_port_file(9500) is None


def test_read_pid_zero_not_returned(tmp_path):
    """Port file with stem '0' → pid=0 not alive, returns None."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "0.port").write_text("9500\n", encoding="utf-8")
    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False):
        assert read_pid_from_port_file(9500) is None


def test_startup_port_zero():
    """port=0 → is_startup_in_progress() False (no meaningful state for port 0)."""
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=None):
        assert CompileStateProbe(port=0).is_startup_in_progress() is False


def test_tracker_elapsed_zero_when_not_started():
    """Fresh tracker with no mark → elapsed() == 0.0."""
    assert DomainReloadTracker().elapsed() == 0.0


def test_tracker_clear_resets_state():
    """mark() then clear() → is_active() returns False immediately."""
    tracker = DomainReloadTracker()
    with patch("unity_mcp.bridge_reload_state.time") as t:
        t.monotonic.return_value = 0.0
        tracker.mark()
    tracker.clear()
    assert tracker.is_active() is False


def test_cleanup_concurrent_unlink_no_crash(tmp_path):
    """File disappears between glob and unlink — must not raise, returns 0."""
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "99999.port").write_text("9500\n", encoding="utf-8")

    def flaky_unlink(self, *args, **kwargs):
        raise FileNotFoundError("already gone")

    with patch("unity_mcp.lockfile._ports_dir", return_value=ports_dir), \
         patch("unity_mcp.lockfile.is_pid_alive", return_value=False), \
         patch.object(Path, "unlink", flaky_unlink):
        cleaned = cleanup_stale_port_files()
    assert cleaned == 0  # unlink failed → not counted


# ===========================================================================
# Group E: Reload Ladder — Timing Constants (wedge fix)
# ===========================================================================

def test_t1_poll_constant():
    """_T1_POLL_S must be 40.0 — calibrated for 9-assembly cold build (30-45s)."""
    from unity_mcp.tools.reload_ladder import _T1_POLL_S
    assert _T1_POLL_S == 40.0


def test_t4_poll_constant():
    """_T4_POLL_S must be 45.0 — T4+T5 share this constant."""
    from unity_mcp.tools.reload_ladder import _T4_POLL_S
    assert _T4_POLL_S == 45.0


def test_t2_sleep_constant():
    """_T2_SLEEP_S must be 8.0 — compile-start latency for 9 assemblies."""
    from unity_mcp.tools.reload_ladder import _T2_SLEEP_S
    assert _T2_SLEEP_S == 8.0


def test_t1_max_polls_none():
    """_T1_MAX_POLLS must be None — deadline governs, not poll count."""
    from unity_mcp.tools.reload_ladder import _T1_MAX_POLLS
    assert _T1_MAX_POLLS is None


# ===========================================================================
# Group F: Guard Probe (_probe_guard_locked)
# ===========================================================================

import sys
import importlib.util


def _load_check_unity():
    """Load check_unity.py as module (it lives in scripts/, not in package)."""
    spec = importlib.util.spec_from_file_location(
        "check_unity",
        Path(__file__).parents[1] / "scripts/check_unity.py",
    )
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def test_guard_probe_true():
    """_probe_guard_locked returns True when execute_code returns 'True'."""
    mod = _load_check_unity()
    response_body = json.dumps({"data": "True"}).encode()
    import struct

    def fake_create_connection(addr, timeout):
        class FakeSock:
            def sendall(self, data): pass
            def recv(self, n):
                # Return 4-byte length then body
                combined = struct.pack(">I", len(response_body)) + response_body
                return combined[:n]
            def __enter__(self): return self
            def __exit__(self, *a): pass
        return FakeSock()

    with patch("socket.create_connection", side_effect=fake_create_connection):
        with patch.object(mod, "_recvexactly") as mock_recv:
            mock_recv.side_effect = [
                struct.pack(">I", len(response_body)),
                response_body,
            ]
            result = mod._probe_guard_locked(9500)
    assert result is True


def test_guard_probe_false():
    """_probe_guard_locked returns False when execute_code returns 'False'."""
    mod = _load_check_unity()
    response_body = json.dumps({"data": "False"}).encode()
    import struct

    with patch("socket.create_connection"):
        with patch.object(mod, "_recvexactly") as mock_recv:
            mock_recv.side_effect = [
                struct.pack(">I", len(response_body)),
                response_body,
            ]
            result = mod._probe_guard_locked(9500)
    assert result is False


def test_guard_probe_oserror():
    """_probe_guard_locked returns None on OSError (socket failure)."""
    mod = _load_check_unity()
    with patch("socket.create_connection", side_effect=OSError("refused")):
        result = mod._probe_guard_locked(9500)
    assert result is None


# ===========================================================================
# Group G: Source Verification — TCP server death fixes
# ===========================================================================

def test_domain_reload_expiry_aligns_with_startup_grace():
    """DOMAIN_RELOAD_EXPIRY_S must be >= STARTUP_GRACE_S (90s).
    If expiry < STARTUP_GRACE, Python stops waiting before Unity finishes
    compiling and floods the dead socket → bridge transitions to FAILED.
    """
    import unity_mcp.bridge as _bridge
    assert DOMAIN_RELOAD_EXPIRY_S >= _bridge.STARTUP_GRACE_S


def test_watchdog_tick_uses_is_really_compiling():
    """WatchdogTick must use IsReallyCompiling, NOT EditorApplication.isCompiling.
    EditorApplication.isCompiling can latch to True after domain reload on Windows,
    preventing the server from ever restarting.
    """
    src = (_PLUGIN / "Editor/MCPServer.cs").read_text(encoding="utf-8")
    # Extract WatchdogTick body — between method signature and the next private static method.
    start = src.find("private static void WatchdogTick()")
    end = src.find("\n        private static", start + 1)
    watchdog_body = src[start:end]
    assert "IsReallyCompiling" in watchdog_body
    assert "EditorApplication.isCompiling" not in watchdog_body


def test_watchdog_registered_before_port_file_manager():
    """WatchdogTick must be registered BEFORE PortFileManager calls in static ctor.
    If PortFileManager throws, WatchdogTick must still be wired up to restart the server.
    """
    src = (_PLUGIN / "Editor/MCPServer.cs").read_text(encoding="utf-8")
    # Scope search to static constructor body only (avoid the public SavePorts wrapper above it)
    ctor_start = src.find("static MCPServer()")
    ctor_end = src.find("\n        public static async void StartAsync()", ctor_start)
    ctor = src[ctor_start:ctor_end]
    idx_watchdog = ctor.find("EditorApplication.update += WatchdogTick")
    idx_clean = ctor.find("PortFileManager.CleanStalePeerPortFiles()")
    idx_save = ctor.find("PortFileManager.SavePorts(")
    assert idx_watchdog < idx_clean, "WatchdogTick must be registered before CleanStalePeerPortFiles()"
    assert idx_watchdog < idx_save, "WatchdogTick must be registered before SavePorts()"
