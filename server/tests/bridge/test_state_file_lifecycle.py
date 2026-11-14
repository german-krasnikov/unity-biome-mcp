"""State file lifecycle tests — covers gaps in test_unity_state.py and test_compile_state.py.

Tests: compile_failed is_busy, partial-write, OSError race, epoch field,
3-line backward compat, state transition sequence, compile_failed end-to-end probe.
No Unity required (marker: not live).
"""
import time
from pathlib import Path
from unittest.mock import patch

from unity_mcp.unity_state import UnityState, read_state_for_port
from unity_mcp.compile_state import CompileStateProbe


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _write_state_file(tmp_path: Path, port: int, content: str) -> Path:
    """Write content to the state file for the given port under tmp_path's home."""
    state_dir = tmp_path / ".unity-biome-mcp" / "state"
    state_dir.mkdir(parents=True, exist_ok=True)
    f = state_dir / f"port-{port}.state"
    f.write_text(content, encoding="utf-8")
    return f


# ===========================================================================
# T1: compile_failed is not a busy state
# ===========================================================================

def test_is_busy_false_for_compile_failed():
    """compile_failed → is_busy is False (build finished, failed, Unity not compiling)."""
    assert UnityState("compile_failed", time.time()).is_busy is False


# ===========================================================================
# T2: Partial write (1-line file) → None
# ===========================================================================

def test_read_state_returns_none_for_single_line(tmp_path):
    """File with state name but no timestamp → len(lines) < 2 → None."""
    _write_state_file(tmp_path, 9500, "compiling")  # no \n, no timestamp
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_state_for_port(9500) is None


# ===========================================================================
# T3: OSError during read → None
# ===========================================================================

def test_read_state_returns_none_on_oserror(tmp_path):
    """OSError mid-read (e.g. file vanished in delete→move window) → None."""
    state_dir = tmp_path / ".unity-biome-mcp" / "state"
    state_dir.mkdir(parents=True)
    with patch.object(Path, "home", return_value=tmp_path), \
         patch("pathlib.Path.read_text", side_effect=OSError("file gone")):
        assert read_state_for_port(9500) is None


# ===========================================================================
# T4: 4-line state file → epoch parsed correctly
# ===========================================================================

def test_read_state_parses_epoch_from_4_line_file(tmp_path):
    """4-line file (state/ts/pid/epoch) → epoch field populated."""
    _write_state_file(tmp_path, 9500, "ready\n1714700000.0\n12345\n42")
    with patch.object(Path, "home", return_value=tmp_path):
        s = read_state_for_port(9500)
    assert s is not None
    assert s.state == "ready"
    assert s.epoch == 42


# ===========================================================================
# T5: 3-line state file (no epoch) → epoch defaults to 0
# ===========================================================================

def test_read_state_epoch_defaults_to_0_for_3_line_file(tmp_path):
    """3-line file (no epoch, pre-v0.21 format) → epoch=0."""
    _write_state_file(tmp_path, 9500, "compiling\n1714700000.0\n99999")
    with patch.object(Path, "home", return_value=tmp_path):
        s = read_state_for_port(9500)
    assert s is not None
    assert s.state == "compiling"
    assert s.epoch == 0


# ===========================================================================
# T6: State transition sequence
# ===========================================================================

def test_state_transitions_across_lifecycle(tmp_path):
    """Each overwrite is immediately visible to read_state_for_port (no caching)."""
    port = 9500
    ts = str(time.time())
    state_dir = tmp_path / ".unity-biome-mcp" / "state"
    state_dir.mkdir(parents=True)
    path = state_dir / f"port-{port}.state"

    def write(state: str) -> None:
        path.write_text(f"{state}\n{ts}\n12345\n1", encoding="utf-8")

    with patch.object(Path, "home", return_value=tmp_path):
        write("ready")
        assert read_state_for_port(port).is_busy is False

        write("compiling")
        assert read_state_for_port(port).is_busy is True

        write("reloading")
        assert read_state_for_port(port).is_busy is True

        write("ready")
        assert read_state_for_port(port).is_busy is False


# ===========================================================================
# T7: compile_failed → has_strong_busy_signal False (end-to-end)
# ===========================================================================

def test_has_strong_busy_signal_false_for_compile_failed():
    """compile_failed in state file → probe says not busy (build finished, failed)."""
    state = UnityState("compile_failed", time.time())
    with patch("unity_mcp.compile_state.read_state_for_port", return_value=state):
        probe = CompileStateProbe(port=9500)
        assert probe.has_strong_busy_signal() is False
