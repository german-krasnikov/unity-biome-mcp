"""Tests for unity_state.py — UnityState reader (Tier 2)."""
import time
from pathlib import Path
from unittest.mock import patch

import pytest

from unity_mcp.unity_state import UnityState, read_state_for_port


# ---------------------------------------------------------------------------
# B1–B4: read_state_for_port
# ---------------------------------------------------------------------------

def _write_state_file(tmp_path: Path, port: int, content: str) -> Path:
    state_dir = tmp_path / ".unity-biome-mcp" / "state"
    state_dir.mkdir(parents=True)
    f = state_dir / f"port-{port}.state"
    f.write_text(content, encoding="utf-8")
    return f


def test_read_state_returns_state_from_valid_file(tmp_path):
    _write_state_file(tmp_path, 9500, "compiling\n1714700000.123")
    with patch.object(Path, "home", return_value=tmp_path):
        s = read_state_for_port(9500)
    assert s is not None
    assert s.state == "compiling"
    assert s.timestamp == pytest.approx(1714700000.123)


def test_read_state_returns_none_for_missing_file():
    assert read_state_for_port(9999) is None


def test_read_state_returns_none_for_corrupt_file(tmp_path):
    _write_state_file(tmp_path, 9500, "garbage")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_state_for_port(9500) is None


def test_read_state_returns_none_for_empty_file(tmp_path):
    _write_state_file(tmp_path, 9500, "")
    with patch.object(Path, "home", return_value=tmp_path):
        assert read_state_for_port(9500) is None


# ---------------------------------------------------------------------------
# B5–B7: is_busy property
# ---------------------------------------------------------------------------

def test_is_busy_true_for_compiling():
    assert UnityState("compiling", time.time()).is_busy is True


def test_is_busy_true_for_reloading():
    assert UnityState("reloading", time.time()).is_busy is True


def test_is_busy_true_for_restarting():
    assert UnityState("restarting", time.time()).is_busy is True


def test_is_busy_false_for_ready():
    assert UnityState("ready", time.time()).is_busy is False


# ---------------------------------------------------------------------------
# B8–B9: is_stale property
# ---------------------------------------------------------------------------

def test_is_stale_when_old():
    assert UnityState("compiling", time.time() - 200).is_stale is True


def test_is_stale_false_when_recent():
    assert UnityState("compiling", time.time()).is_stale is False


# ---------------------------------------------------------------------------
# B10: CompileStateProbe uses state file (authoritative)
# ---------------------------------------------------------------------------

def test_has_strong_busy_signal_uses_state_file(tmp_path):
    """State file compiling → has_strong_busy_signal True without checking BeeDriver."""
    from unity_mcp.compile_state import CompileStateProbe
    from unittest.mock import patch as _patch

    state = UnityState("compiling", time.time())
    with _patch("unity_mcp.compile_state.read_state_for_port", return_value=state) as mock_read:
        probe = CompileStateProbe(unity_project_path=None, port=9500)
        result = probe.has_strong_busy_signal()

    assert result is True
    mock_read.assert_called_once_with(9500)


# ---------------------------------------------------------------------------
# backward compat
# ---------------------------------------------------------------------------

def test_state_without_pid_backward_compat():
    """2-line state file (no PID) still works."""
    s = UnityState("ready", time.time())
    assert s.is_stale is False
