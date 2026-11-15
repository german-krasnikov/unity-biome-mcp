"""Tests for install.py's _load_stop_server() — wires biome_mcp.server_control's
generic stop_server(port, lock_dir, ...) to Unity's own lockfile directory,
since lock_dir has no engine-specific default in the generic module."""
import importlib.util
from pathlib import Path
from unittest.mock import patch

# Load install.py directly (avoids conflict with install/ package)
REPO_ROOT = Path(__file__).parent.parent.parent
_spec = importlib.util.spec_from_file_location("install_script_stop", REPO_ROOT / "install.py")
inst = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(inst)


def test_load_stop_server_passes_unity_lock_dir():
    """The returned callable must default lock_dir to unity_mcp_dir()."""
    calls = []

    def fake_stop_server(port, **kwargs):
        calls.append((port, kwargs))
        return True

    with patch("biome_mcp.server_control.stop_server", fake_stop_server):
        stop = inst._load_stop_server()
        assert stop is not None
        result = stop(port=9500, timeout=5.0)

    assert result is True
    assert len(calls) == 1
    port, kwargs = calls[0]
    assert port == 9500
    assert kwargs["timeout"] == 5.0
    from unity_mcp.paths import unity_mcp_dir
    assert kwargs["lock_dir"] == unity_mcp_dir()


def test_load_stop_server_caller_can_override_lock_dir(tmp_path):
    """An explicit lock_dir kwarg must not be clobbered by the Unity default."""
    calls = []

    def fake_stop_server(port, **kwargs):
        calls.append(kwargs)
        return False

    with patch("biome_mcp.server_control.stop_server", fake_stop_server):
        stop = inst._load_stop_server()
        stop(port=9500, lock_dir=tmp_path)

    assert calls[0]["lock_dir"] == tmp_path


def test_load_stop_server_returns_none_on_import_error():
    with patch.object(inst, "_add_server_to_path", side_effect=ImportError("no venv yet")):
        assert inst._load_stop_server() is None
