"""Tests for CompileStateProbe — Phase 1 (14 tests)."""
import os
import time
from pathlib import Path
from unittest.mock import patch, MagicMock

import pytest

from unity_mcp.compile_state import CompileStateProbe


# ---------------------------------------------------------------------------
# 1. Degrade-open: no project path
# ---------------------------------------------------------------------------

def test_probe_no_project_path_degrades_open():
    probe = CompileStateProbe(None)
    assert probe.is_unity_busy() is False  # no crash, returns False


# ---------------------------------------------------------------------------
# 2-4. FS-based heuristics
# ---------------------------------------------------------------------------

def test_probe_detects_recent_dll(tmp_path):
    # DLL mtime no longer used for is_unity_busy (removed in Cycle 15).
    # Probe relies on lock file and disconnect window only.
    (tmp_path / "Library" / "ScriptAssemblies").mkdir(parents=True)
    dll = tmp_path / "Library" / "ScriptAssemblies" / "Foo.dll"
    dll.touch()
    probe = CompileStateProbe(tmp_path)
    # No lock file, no recent disconnect → not busy
    assert probe.is_unity_busy() is False


def test_probe_idle_old_dll(tmp_path):
    asm_dir = tmp_path / "Library" / "ScriptAssemblies"
    asm_dir.mkdir(parents=True)
    dll = asm_dir / "Foo.dll"
    dll.touch()
    # backdate mtime by 30s
    old_ts = time.time() - 30
    os.utime(dll, (old_ts, old_ts))

    probe = CompileStateProbe(tmp_path)
    assert probe.is_unity_busy() is False


def test_probe_lock_file_present(tmp_path):
    (tmp_path / "Library" / "BeeDriver").mkdir(parents=True)
    (tmp_path / "Library" / "BeeDriver" / "Lock").touch()
    probe = CompileStateProbe(tmp_path)
    assert probe.is_unity_busy() is True


# ---------------------------------------------------------------------------
# 5-6. Disconnect-window heuristic
# ---------------------------------------------------------------------------

def test_probe_recent_disconnect_marks_busy(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.mark_recompile_issued()
    assert probe.is_unity_busy() is True


def test_probe_disconnect_window_expires(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    base_ts = 1000.0
    with patch("unity_mcp.compile_state.time") as mock_time:
        mock_time.monotonic.return_value = base_ts
        probe.mark_recompile_issued()
        # Advance 121 seconds past mark (window is now 120s)
        mock_time.monotonic.return_value = base_ts + 121
        assert probe.is_unity_busy() is False


# ---------------------------------------------------------------------------
# 7-9. estimated_remaining_s
# ---------------------------------------------------------------------------

def test_probe_estimated_remaining_default(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    assert probe.estimated_remaining_s() == 5.0


def test_probe_estimated_remaining_capped_at_60(tmp_path):
    # estimated_remaining_s caps at 60 when _last_known_duration is very large
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe._last_known_duration = 200.0
    probe._compile_elapsed = 0.0
    result = probe.estimated_remaining_s()
    assert result == 60.0


def test_probe_estimated_shorter_for_older_dll(tmp_path):
    # estimated_remaining_s returns less than default when elapsed is known
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe._last_known_duration = 10.0
    probe._compile_elapsed = 8.0  # 8s elapsed of a 10s compile → 2s left
    assert probe.estimated_remaining_s() < 5.0


# ---------------------------------------------------------------------------
# 10-11. Error resilience
# ---------------------------------------------------------------------------

def test_probe_handles_missing_library_dir(tmp_path):
    # path exists but no Library/ subdir
    probe = CompileStateProbe(tmp_path)
    assert probe.is_unity_busy() is False  # no crash


def test_probe_handles_permission_error(tmp_path):
    (tmp_path / "Library" / "ScriptAssemblies").mkdir(parents=True)

    def boom(*args, **kwargs):
        raise PermissionError("no access")

    probe = CompileStateProbe(tmp_path)
    with patch("pathlib.Path.iterdir", side_effect=boom):
        assert probe.is_unity_busy() is False


# ---------------------------------------------------------------------------
# 12-13. autodetect_project_path
# ---------------------------------------------------------------------------

def test_autodetect_reads_env(tmp_path, monkeypatch):
    (tmp_path / "Library").mkdir()
    monkeypatch.setenv("UNITY_MCP_PROJECT_PATH", str(tmp_path))
    result = CompileStateProbe.autodetect_project_path()
    assert result == tmp_path


def test_autodetect_returns_none_when_invalid(monkeypatch):
    # No env var → returns None (subprocess detection removed in Cycle 15)
    monkeypatch.delenv("UNITY_MCP_PROJECT_PATH", raising=False)
    assert CompileStateProbe.autodetect_project_path() is None


def test_has_project_property(tmp_path):
    assert CompileStateProbe(tmp_path).has_project is True
    assert CompileStateProbe(None).has_project is False


# ---------------------------------------------------------------------------
# 14. Injected path doesn't use Path.home
# ---------------------------------------------------------------------------

def test_probe_no_real_home_access(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)

    with patch.object(Path, "home", side_effect=RuntimeError("home accessed!")):
        # Should never call Path.home — injected path used directly
        assert probe.is_unity_busy() is False  # no crash, no RuntimeError


# ---------------------------------------------------------------------------
# 15-19. update_compile_info + estimated_remaining_s with last_known_duration
# ---------------------------------------------------------------------------

def test_update_compile_info_compiling(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("compiling|12.3")
    assert probe._compile_elapsed == pytest.approx(12.3)


def test_update_compile_info_idle(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("idle|8.2")
    assert probe._last_known_duration == pytest.approx(8.2)
    assert probe._compile_elapsed is None


def test_estimated_remaining_uses_last_duration(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("idle|45.0")   # last compile took 45s
    probe.update_compile_info("compiling|10.0")  # 10s elapsed
    result = probe.estimated_remaining_s()
    assert result == pytest.approx(35.0, abs=1.0)


def test_update_compile_info_invalid(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    probe.update_compile_info("garbage")
    probe.update_compile_info("")
    probe.update_compile_info("bad|notanumber")
    # No crash, state unchanged
    assert probe._compile_elapsed is None
    assert probe._last_known_duration is None


def test_max_remaining_raised_to_60(tmp_path):
    from unity_mcp.compile_state import _MAX_REMAINING_S
    assert _MAX_REMAINING_S == 60


# ---------------------------------------------------------------------------
# 20-22. has_strong_busy_signal
# ---------------------------------------------------------------------------

def test_has_strong_busy_signal_lock_file(tmp_path):
    (tmp_path / "Library" / "BeeDriver").mkdir(parents=True)
    (tmp_path / "Library" / "BeeDriver" / "Lock").touch()
    probe = CompileStateProbe(tmp_path)
    assert probe.has_strong_busy_signal() is True


def test_has_strong_busy_signal_idle(tmp_path):
    (tmp_path / "Library").mkdir()
    probe = CompileStateProbe(tmp_path)
    assert probe.has_strong_busy_signal() is False


# ---------------------------------------------------------------------------
# is_process_dead (used by heartbeat loop + _describe_failure)
# ---------------------------------------------------------------------------

def test_is_process_dead_returns_false_when_no_port():
    probe = CompileStateProbe(port=None)
    assert probe.is_process_dead() is False


def test_is_process_dead_returns_true_when_pid_dead():
    with patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=999999), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=False):
        probe = CompileStateProbe(port=9500)
        assert probe.is_process_dead() is True


# ---------------------------------------------------------------------------
# has_strong_busy_signal — state-file branches (P1-14)
# ---------------------------------------------------------------------------

def _make_state(is_stale: bool, is_busy: bool):
    from unittest.mock import MagicMock
    from unity_mcp.unity_state import UnityState
    m = MagicMock(spec=UnityState)
    m.is_stale = is_stale
    m.is_busy = is_busy
    return m


def test_has_strong_busy_signal_state_not_stale_is_busy():
    """state file: not stale, is_busy=True → True"""
    state = _make_state(is_stale=False, is_busy=True)
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is True


def test_has_strong_busy_signal_state_not_stale_not_busy():
    """state file: not stale, is_busy=False → False (falls through to lock file which is absent)"""
    state = _make_state(is_stale=False, is_busy=False)
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is False


def test_has_strong_busy_signal_state_none_process_dead():
    """state=None (stale/missing) + process dead → False (not lock file check)"""
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=999999), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=False):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is False


def test_has_strong_busy_signal_state_none_process_alive_falls_to_lock(tmp_path):
    """state=None + process alive → fallthrough to lock file"""
    (tmp_path / "Library" / "BeeDriver").mkdir(parents=True)
    (tmp_path / "Library" / "BeeDriver" / "Lock").touch()
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True):
        probe = CompileStateProbe(unity_project_path=tmp_path, port=9500)
        assert probe.has_strong_busy_signal() is True


# ---------------------------------------------------------------------------
# RC-3: autodetect_project_path with port parameter
# ---------------------------------------------------------------------------

def test_autodetect_project_path_from_port_file(tmp_path, monkeypatch):
    """env absent, port file present → path from port file."""
    monkeypatch.delenv("UNITY_MCP_PROJECT_PATH", raising=False)
    project_dir = tmp_path / "MyProject"
    project_dir.mkdir()
    with patch("unity_mcp.compile_state.read_project_path_from_port_file", return_value=project_dir):
        result = CompileStateProbe.autodetect_project_path(port=9500)
    assert result == project_dir


def test_autodetect_project_path_env_override(tmp_path, monkeypatch):
    """env present → env wins over port file."""
    project_dir = tmp_path / "EnvProject"
    project_dir.mkdir()
    (project_dir / "Library").mkdir()
    monkeypatch.setenv("UNITY_MCP_PROJECT_PATH", str(project_dir))
    # Even with port file returning different path, env wins
    other = tmp_path / "OtherProject"
    other.mkdir()
    with patch("unity_mcp.compile_state.read_project_path_from_port_file", return_value=other):
        result = CompileStateProbe.autodetect_project_path(port=9500)
    assert result == project_dir


# ---------------------------------------------------------------------------
# FIX-4: state=None + pid-alive + no lock → is_startup_in_progress AND
#         has_strong_busy_signal both True (purely from startup detection)
# ---------------------------------------------------------------------------

def test_startup_in_progress_no_state_pid_alive_no_lock(tmp_path):
    """state-file None + PID alive + TCP refuses → is_startup_in_progress True."""
    (tmp_path / "Library").mkdir()  # no BeeDriver/Lock subfolder
    mock_sock = MagicMock()
    mock_sock.connect.side_effect = OSError("Connection refused")
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=99999), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", return_value=mock_sock):
        probe = CompileStateProbe(unity_project_path=tmp_path, port=9500)
        assert probe.is_startup_in_progress() is True
        assert probe.has_strong_busy_signal() is True


# ---------------------------------------------------------------------------
# P6: stale+busy state must consult PID (was falling through to lock-file only)
# ---------------------------------------------------------------------------

def test_stale_busy_state_pid_alive_returns_true():
    """P6: state.is_stale=True AND is_busy=True AND PID alive → True (still compiling)."""
    state = _make_state(is_stale=True, is_busy=True)
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is True


def test_stale_busy_state_pid_dead_returns_false():
    """P6: state.is_stale=True AND is_busy=True AND PID dead → False (Unity gone)."""
    state = _make_state(is_stale=True, is_busy=True)
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=99999), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=False):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is False


def test_stale_not_busy_state_returns_false():
    """P6: state.is_stale=True AND is_busy=False → False (stale state not compiling)."""
    state = _make_state(is_stale=True, is_busy=False)
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is False


# ---------------------------------------------------------------------------
# G15: stale+busy+PID-alive + detect_wedge returns failure → not busy (terminal)
# ---------------------------------------------------------------------------

def test_stale_busy_live_pid_with_log_error_is_failed_not_busy():
    """G15: stale+busy state + PID alive + detect_wedge returns a wedge → False (terminal).

    Red-precondition: compile_state.py:78-79 returned not self.is_process_dead() = True
    for ANY stale+busy+alive combination. G15 adds detect_wedge() check: a terminal
    reload failure (build-failed-wedge) means the domain CANNOT self-heal via retry —
    has_strong_busy_signal must return False so the bridge stops treating it as busy.

    A8: feeds REAL WedgeReport into the real has_strong_busy_signal; inject only
    detect_wedge return value, not the SUT method itself.
    """
    from unity_mcp.editor_log import WedgeReport
    state = _make_state(is_stale=True, is_busy=True)
    wedge = WedgeReport(kind="build-failed-wedge", cs_errors=["CS0535: foo"])

    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("unity_mcp.editor_log.detect_wedge", return_value=wedge):
        probe = CompileStateProbe(port=9500)
        result = probe.has_strong_busy_signal()

    assert result is False, (
        "stale+busy+PID-alive + terminal wedge → NOT busy (domain cannot self-heal)"
    )


def test_stale_busy_live_pid_no_wedge_is_still_busy():
    """G15 companion: stale+busy+PID-alive + NO wedge → True (still legitimately compiling).

    Proves the wedge check is additive: without a wedge report, P6 behavior is preserved.
    """
    state = _make_state(is_stale=True, is_busy=True)

    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("unity_mcp.editor_log.detect_wedge", return_value=None):
        probe = CompileStateProbe(port=9500)
        result = probe.has_strong_busy_signal()

    assert result is True, "stale+busy+PID-alive + no wedge → still busy (P6 preserved)"




# ---------------------------------------------------------------------------
# SD-4: TCP probe in is_startup_in_progress
# ---------------------------------------------------------------------------

def test_is_startup_tcp_responds_returns_false():
    """State absent + PID alive + TCP responds → False (Unity ready, state file missing)."""
    mock_sock = MagicMock()
    mock_sock.connect.return_value = None  # success
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", return_value=mock_sock):
        probe = CompileStateProbe(port=9500)
        assert probe.is_startup_in_progress() is False


def test_is_startup_tcp_not_responding_returns_true():
    """State absent + PID alive + TCP refuses → True (genuinely starting)."""
    mock_sock = MagicMock()
    mock_sock.connect.side_effect = OSError("Connection refused")
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", return_value=mock_sock):
        probe = CompileStateProbe(port=9500)
        assert probe.is_startup_in_progress() is True


def test_is_startup_tcp_probe_not_called_when_state_file_present():
    """If state file exists → returns False before TCP probe."""
    state = MagicMock()
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state), \
         patch("socket.socket") as mock_socket:
        probe = CompileStateProbe(port=9500)
        result = probe.is_startup_in_progress()
    assert result is False
    mock_socket.assert_not_called()  # TCP probe never reached


def test_is_startup_tcp_probe_setup_failure_returns_false():
    """If socket creation/probe fails, classify as not-startup instead of raising."""
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=12345), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=True), \
         patch("socket.socket", side_effect=OSError("cannot open socket")):
        probe = CompileStateProbe(port=9500)
        assert probe.is_startup_in_progress() is False


def test_is_startup_no_port_returns_false():
    """port=None → False always (no TCP probe)."""
    probe = CompileStateProbe(port=None)
    assert probe.is_startup_in_progress() is False


def test_is_startup_dead_pid_returns_false():
    """State absent + PID dead → False (no TCP probe needed)."""
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=None), \
         patch("unity_mcp.compile_state.read_pid_from_port_file", return_value=99999), \
         patch("unity_mcp.compile_state.is_pid_alive", return_value=False), \
         patch("socket.socket") as mock_socket:
        probe = CompileStateProbe(port=9500)
        assert probe.is_startup_in_progress() is False
        mock_socket.assert_not_called()
