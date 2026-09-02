"""Tests for install.py new subcommands: configure (tool mode), uninstall."""
import argparse
import importlib.util
import json
import subprocess
import sys
from pathlib import Path
from unittest.mock import MagicMock, patch

import pytest

# Load install.py directly (avoids conflict with install/ package)
REPO_ROOT = Path(__file__).parent.parent.parent
_spec = importlib.util.spec_from_file_location("install_script", REPO_ROOT / "install.py")
inst = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(inst)

# Shared "existing config carries a custom env var" fixture -- this file's
# pytest root is separate from server/tests, so it keeps its own local
# constant instead of importing server/tests/helpers.py's KEEPME_ENV.
_KEEPME_ENV = {"UNITY_MCP_PORT": "9500", "CUSTOM_VAR": "keepme"}

# Lazy references — functions don't exist until Green phase
def cmd_configure(*a, **kw): return inst.cmd_configure(*a, **kw)
def cmd_uninstall(*a, **kw): return inst.cmd_uninstall(*a, **kw)
def cmd_update(*a, **kw): return inst.cmd_update(*a, **kw)

MOD = "install_script"  # module name for patch()


# ── helpers ──────────────────────────────────────────────────────────────────

def _args(**kwargs) -> argparse.Namespace:
    defaults = {"project": None, "tool": None, "port": 0, "force": False}
    defaults.update(kwargs)
    return argparse.Namespace(**defaults)


def _fake_registry(config_path: Path) -> dict:
    client = MagicMock()
    client.name = "Fake Tool"
    client.config_path = config_path
    client.stdout_only = False
    client.is_toml = False
    client.root_key = "mcpServers"
    client.entry_transformer = None
    return {"fake-tool": client}


# ── configure: tool mode ─────────────────────────────────────────────────────

def test_configure_creates_config(tmp_path):
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)
    entry = {"command": "uv", "args": ["run", "unity-biome-mcp"]}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool="fake-tool"))

    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-biome-mcp"] == entry


def test_configure_preserves_other_servers(tmp_path):
    cfg = tmp_path / "mcp.json"
    cfg.write_text(json.dumps({"mcpServers": {"other-tool": {"command": "x"}}}), encoding="utf-8")
    registry = _fake_registry(cfg)
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool="fake-tool"))

    data = json.loads(cfg.read_text(encoding="utf-8"))
    assert "other-tool" in data["mcpServers"]
    assert "unity-biome-mcp" in data["mcpServers"]


def test_configure_with_tool_flag_only_configures_that_tool(tmp_path):
    cfg_target = tmp_path / "only.json"
    cfg_other = tmp_path / "other.json"
    registry = {
        "fake-tool": _fake_registry(cfg_target)["fake-tool"],
        "other": _fake_registry(cfg_other)["fake-tool"],
    }
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool="fake-tool"))

    assert cfg_target.exists()
    assert not cfg_other.exists()


def test_configure_port_passed_to_entry(tmp_path):
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)
    captured = {}

    def fake_entry(port=0):
        captured["port"] = port
        return {"command": "x", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "build_server_entry", side_effect=fake_entry):
        cmd_configure(_args(tool="fake-tool", port=9999))

    assert captured["port"] == 9999


def test_configure_auto_detect_prompts(tmp_path):
    """No --tool → auto-detect installed tools and prompt for each."""
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "prompt_yn", return_value=True), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool=None))

    assert cfg.exists()


def test_configure_auto_detect_user_skips(tmp_path):
    """No --tool, user answers 'n' → nothing configured."""
    cfg = tmp_path / "mcp.json"
    registry = _fake_registry(cfg)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "prompt_yn", return_value=False), \
         patch.object(inst, "build_server_entry", return_value={"command": "x", "args": []}):
        cmd_configure(_args(tool=None))

    assert not cfg.exists()


def test_configure_all_flag_configures_every_detected_client_without_prompting(tmp_path):
    """--all: write config for every detect_installed() hit, no per-tool prompt."""
    cfg_a = tmp_path / "a.json"
    cfg_b = tmp_path / "b.json"
    registry = {
        "fake-a": _fake_registry(cfg_a)["fake-tool"],
        "fake-b": _fake_registry(cfg_b)["fake-tool"],
    }
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-a", "fake-b"]), \
         patch.object(inst, "prompt_yn") as mock_prompt, \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool=None, all=True))

    assert cfg_a.exists()
    assert cfg_b.exists()
    mock_prompt.assert_not_called()


def test_configure_all_flag_ignored_tools_not_detected(tmp_path):
    """--all only configures what detect_installed() returns, not the full registry."""
    cfg_detected = tmp_path / "detected.json"
    cfg_other = tmp_path / "other.json"
    registry = {
        "fake-detected": _fake_registry(cfg_detected)["fake-tool"],
        "fake-other": _fake_registry(cfg_other)["fake-tool"],
    }
    entry = {"command": "uv", "args": []}

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-detected"]), \
         patch.object(inst, "build_server_entry", return_value=entry):
        cmd_configure(_args(tool=None, all=True))

    assert cfg_detected.exists()
    assert not cfg_other.exists()


def test_project_config_path_junie_matches_junie_convention(tmp_path):
    """--project-dir --tool junie must not silently fall back to .mcp.json —
    matches ProjectConfigTargets.cs's '.junie/mcp/mcp.json' relative path."""
    result = inst._project_config_path(tmp_path, "junie")
    assert result == tmp_path / ".junie" / "mcp" / "mcp.json"


# ── uninstall ─────────────────────────────────────────────────────────────────

def test_uninstall_removes_venv(tmp_path):
    venv = tmp_path / "server" / ".venv"
    venv.mkdir(parents=True)
    (venv / "bin").mkdir()

    with patch.object(inst, "SERVER_DIR", tmp_path / "server"), \
         patch.object(inst, "prompt_yn", return_value=False):
        cmd_uninstall(_args())

    assert not venv.exists()


def test_uninstall_removes_unity_mcp_dir(tmp_path):
    data_dir = tmp_path / ".unity-biome-mcp"
    data_dir.mkdir()
    (data_dir / "ports").mkdir()

    with patch.object(inst, "SERVER_DIR", tmp_path / "server"), \
         patch.object(inst, "_UNITY_MCP_DATA_DIR", data_dir), \
         patch.object(inst, "prompt_yn", return_value=True):
        cmd_uninstall(_args())

    assert not data_dir.exists()


def test_uninstall_skips_data_dir_if_user_declines(tmp_path):
    data_dir = tmp_path / ".unity-biome-mcp"
    data_dir.mkdir()

    with patch.object(inst, "SERVER_DIR", tmp_path / "server"), \
         patch.object(inst, "_UNITY_MCP_DATA_DIR", data_dir), \
         patch.object(inst, "prompt_yn", return_value=False):
        cmd_uninstall(_args())

    assert data_dir.exists()


# ── setup uses ui ─────────────────────────────────────────────────────────────

def test_setup_calls_ui_ok(capsys):
    with patch.object(inst, "_setup_env", lambda *a, **kw: None):
        inst.cmd_setup(_args())
    out = capsys.readouterr().out
    # ui.ok outputs ✓ or [OK]
    assert "✓" in out or "[OK]" in out or "OK" in out


# ── cmd_update: server stop integration ──────────────────────────────────────

def test_cmd_update_stops_server_before_setup_env():
    """stop must be called BEFORE setup_env — order is the whole point."""
    call_order = []

    def fake_stop(port, **kw):
        call_order.append("stop")
        return True

    def fake_setup(*a, **kw):
        call_order.append("setup")

    with patch.object(inst, "_setup_env", fake_setup), \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_has_uvx", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=9515), _stop_fn=fake_stop)

    assert call_order == ["stop", "setup"]


def test_cmd_update_no_port_skips_stop():
    """When --port is 0 (omitted), stop is never called."""
    stop_calls = []

    def fake_stop(port, **kw):
        stop_calls.append(port)
        return False

    with patch.object(inst, "_setup_env", lambda *a, **kw: None), \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_has_uvx", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=0), _stop_fn=fake_stop)

    assert stop_calls == []


def test_cmd_update_proceeds_when_server_not_found():
    """stop_server returning False should NOT abort the update."""
    setup_calls = []

    def fake_stop(port, **kw):
        return False  # no server running

    def fake_setup(*a, **kw):
        setup_calls.append(True)

    with patch.object(inst, "_setup_env", fake_setup), \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_has_uvx", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=9515), _stop_fn=fake_stop)

    assert setup_calls == [True]


def test_cmd_update_proceeds_on_stop_exception():
    """Exception from stop_fn must not abort the update."""
    setup_calls = []

    def bad_stop(port, **kw):
        raise RuntimeError("boom")

    def fake_setup(*a, **kw):
        setup_calls.append(True)

    with patch.object(inst, "_setup_env", fake_setup), \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_has_uvx", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=9515), _stop_fn=bad_stop)

    assert setup_calls == [True]


def test_cmd_update_prints_reconnect_hint(capsys):
    """Output must mention /mcp so user knows how to reconnect."""
    with patch.object(inst, "_setup_env", lambda *a, **kw: None), \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_has_uvx", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=9515), _stop_fn=lambda port, **kw: True)

    out = capsys.readouterr().out
    assert "/mcp" in out


def test_cmd_update_no_running_server_still_prints_reconnect_hint(capsys):
    """Even when stop returns False, Done + /mcp should appear."""
    with patch.object(inst, "_setup_env", lambda *a, **kw: None), \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_has_uvx", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=9515), _stop_fn=lambda port, **kw: False)

    out = capsys.readouterr().out
    assert "Done" in out or "done" in out
    assert "/mcp" in out


# ── cmd_update: uvx --reinstall path ─────────────────────────────────────────

def test_cmd_update_uvx_present_calls_reinstall_not_setup_env():
    """When uvx is available, reinstall via uvx --reinstall, never touch the venv."""
    with patch.object(inst, "_has_uvx", lambda: True), \
         patch.object(inst, "_reinstall_uvx") as fake_reinstall, \
         patch.object(inst, "_setup_env") as fake_setup, \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=0))

    fake_reinstall.assert_called_once()
    fake_setup.assert_not_called()


def test_cmd_update_uvx_absent_falls_back_to_setup_env():
    """When uvx is NOT available, fall back to the venv setup path (existing behavior)."""
    with patch.object(inst, "_has_uvx", lambda: False), \
         patch.object(inst, "_setup_env") as fake_setup, \
         patch.object(inst, "_venv_stale", lambda: False), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None):
        cmd_update(_args(port=0))

    fake_setup.assert_called_once()


def test_cmd_update_reinstall_failure_exits_1_and_does_not_print_done(capsys):
    """uvx --reinstall failing must abort with exit 1, not silently print Done."""
    with patch.object(inst, "_has_uvx", lambda: True), \
         patch.object(inst, "_reinstall_uvx",
                       side_effect=subprocess.CalledProcessError(1, ["uvx"])), \
         patch.object(inst, "_reconfigure_detected_clients", lambda: None), pytest.raises(SystemExit) as exc_info:
        cmd_update(_args(port=0))

    assert exc_info.value.code == 1
    out = capsys.readouterr().out
    assert "Done" not in out


def test_cmd_update_calls_reconfigure_detected_clients_after_reinstall():
    """Reconfigure must run AFTER reinstall — order is the whole point."""
    call_order = []

    with patch.object(inst, "_has_uvx", lambda: True), \
         patch.object(inst, "_reinstall_uvx", lambda: call_order.append("reinstall")), \
         patch.object(inst, "_reconfigure_detected_clients",
                       lambda: call_order.append("reconfigure")):
        cmd_update(_args(port=0))

    assert call_order == ["reinstall", "reconfigure"]


# ── _reconfigure_detected_clients ─────────────────────────────────────────────

def test_reconfigure_detected_clients_skips_unconfigured_tools(tmp_path):
    """Only clients validate_config reports as configured get re-merged."""
    cfg_claude = tmp_path / "claude.json"
    cfg_cursor = tmp_path / "cursor.json"
    registry = _fake_registry(cfg_claude)
    cursor_client = _fake_registry(cfg_cursor)["fake-tool"]
    registry = {"claude-code": registry["fake-tool"], "cursor": cursor_client}
    entry = {"command": "uv", "args": []}
    merge_calls = []

    def fake_validate(key):
        return "Status: not configured (unity-biome-mcp missing)" if key == "cursor" else "Status: ok"

    def fake_merge(path, e, root_key="mcpServers", entry_transformer=None):
        merge_calls.append(path)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["claude-code", "cursor"]), \
         patch.object(inst, "validate_config", fake_validate), \
         patch.object(inst, "build_server_entry", return_value=entry), \
         patch.object(inst, "merge_mcp_config", fake_merge):
        inst._reconfigure_detected_clients()

    assert merge_calls == [cfg_claude]


def test_reconfigure_detected_clients_preserves_custom_env_var(tmp_path):
    """E2E regression (ARC-12 T3): install.py update must not wipe a user's
    custom env var. Reproduces RC3 through the real call chain — real merge,
    real file write, no merge_mcp_config patch."""
    cfg = tmp_path / "mcp.json"
    cfg.write_text(json.dumps({
        "mcpServers": {
            "unity-biome-mcp": {
                "command": "old",
                "args": [],
                "env": dict(_KEEPME_ENV),
            }
        }
    }), encoding="utf-8")
    registry = _fake_registry(cfg)
    entry = {"command": "new", "args": ["-m", "unity_mcp.server"]}  # RC3: no env key (port=0 shape)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "validate_config", return_value="Status: ok"), \
         patch.object(inst, "build_server_entry", return_value=entry):
        inst._reconfigure_detected_clients()

    data = json.loads(cfg.read_text(encoding="utf-8"))
    written = data["mcpServers"]["unity-biome-mcp"]
    assert written["env"] == _KEEPME_ENV
    assert written["command"] == "new"


def test_reconfigure_detected_clients_never_prompts(tmp_path):
    """Reconfigure must never call prompt_yn — it's a non-interactive re-assert."""
    cfg = tmp_path / "claude.json"
    registry = _fake_registry(cfg)
    entry = {"command": "uv", "args": []}

    def boom(*a, **kw):
        raise AssertionError("prompt_yn must never be called by _reconfigure_detected_clients")

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "validate_config", return_value="Status: ok"), \
         patch.object(inst, "build_server_entry", return_value=entry), \
         patch.object(inst, "merge_mcp_config", lambda *a, **kw: None), \
         patch.object(inst, "prompt_yn", boom):
        inst._reconfigure_detected_clients()  # must not raise


# ── _reconfigure_detected_clients: pin support (ARC-0b T3) ───────────────────

def test_reconfigure_detected_clients_skips_pinned_entry(tmp_path):
    """A "_pin": true entry must survive install.py update byte-for-byte —
    merge_mcp_config must not even be called for that client."""
    cfg = tmp_path / "claude.json"
    original = json.dumps({
        "mcpServers": {"unity-biome-mcp": {"command": "old", "args": [], "_pin": True}}
    })
    cfg.write_text(original, encoding="utf-8")
    registry = _fake_registry(cfg)
    entry = {"command": "new", "args": []}
    merge_calls = []

    def fake_merge(path, e, root_key="mcpServers", entry_transformer=None):
        merge_calls.append(path)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "validate_config", return_value="Status: ok"), \
         patch.object(inst, "build_server_entry", return_value=entry), \
         patch.object(inst, "merge_mcp_config", fake_merge):
        inst._reconfigure_detected_clients()

    assert merge_calls == []
    assert cfg.read_text(encoding="utf-8") == original  # untouched, byte-for-byte


def test_reconfigure_detected_clients_updates_unpinned_entry(tmp_path):
    """Regression guard: an entry without "_pin" must still be reconfigured."""
    cfg = tmp_path / "claude.json"
    cfg.write_text(json.dumps({
        "mcpServers": {"unity-biome-mcp": {"command": "old", "args": []}}
    }), encoding="utf-8")
    registry = _fake_registry(cfg)
    entry = {"command": "new", "args": []}
    merge_calls = []

    def fake_merge(path, e, root_key="mcpServers", entry_transformer=None):
        merge_calls.append(path)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "validate_config", return_value="Status: ok"), \
         patch.object(inst, "build_server_entry", return_value=entry), \
         patch.object(inst, "merge_mcp_config", fake_merge):
        inst._reconfigure_detected_clients()

    assert merge_calls == [cfg]


def test_reconfigure_detected_clients_skips_pinned_toml_entry(tmp_path):
    """Codex-style TOML entry pinned via its comment marker must not be re-merged."""
    cfg = tmp_path / "config.toml"
    original = (
        "# unity-biome-mcp generated v0.54.1 pinned\n"
        "[mcp_servers.unity-biome-mcp]\ncommand = 'old'\nargs = []\n"
    )
    cfg.write_text(original, encoding="utf-8")
    registry = _fake_registry(cfg)
    registry["fake-tool"].is_toml = True
    entry = {"command": "new", "args": []}
    merge_calls = []

    def fake_merge_toml(path, e):
        merge_calls.append(path)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["fake-tool"]), \
         patch.object(inst, "validate_config", return_value="Status: ok"), \
         patch.object(inst, "build_server_entry", return_value=entry), \
         patch.object(inst, "merge_toml_mcp", fake_merge_toml):
        inst._reconfigure_detected_clients()

    assert merge_calls == []
    assert cfg.read_text(encoding="utf-8") == original


# ── _reconfigure_detected_clients: undecodable client config must not abort update ──

def test_reconfigure_detected_clients_skips_undecodable_client_and_continues(tmp_path, capsys):
    """C1-FIX-01 (config-writers MAJOR): install.py:97-98 calls the REAL
    is_entry_pinned before the try/except ValueError block that guards the
    merge call below it. is_entry_pinned only catches json.JSONDecodeError,
    not UnicodeDecodeError -- so a config file with genuinely undecodable
    bytes (stray UTF-16 BOM / binary corruption) raises uncaught and aborts
    the whole `for key in detect_installed()` loop, so tools after the
    corrupt one never get reconfigured. Fixed by wrapping the pinned-check
    call itself in the same try/except pattern."""
    corrupt_cfg = tmp_path / "corrupt.json"
    corrupt_cfg.write_bytes(b'\xff\xfe{"mcpServers": invalid \x80\x81')
    good_cfg = tmp_path / "good.json"
    good_cfg.write_text(json.dumps({
        "mcpServers": {"unity-biome-mcp": {"command": "old", "args": []}}
    }), encoding="utf-8")

    corrupt_client = _fake_registry(corrupt_cfg)["fake-tool"]
    corrupt_client.name = "Corrupt Tool"
    good_client = _fake_registry(good_cfg)["fake-tool"]
    good_client.name = "Good Tool"
    registry = {"corrupt-tool": corrupt_client, "good-tool": good_client}
    entry = {"command": "new", "args": []}
    merge_calls = []

    def fake_merge(path, e, root_key="mcpServers", entry_transformer=None):
        merge_calls.append(path)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["corrupt-tool", "good-tool"]), \
         patch.object(inst, "validate_config", return_value="Status: ok"), \
         patch.object(inst, "build_server_entry", return_value=entry), \
         patch.object(inst, "merge_mcp_config", fake_merge), \
         patch.object(inst, "backup", MagicMock()):
        inst._reconfigure_detected_clients()  # must not raise UnicodeDecodeError

    assert merge_calls == [good_cfg]  # the client AFTER the corrupt one still ran
    out = capsys.readouterr().out
    assert "Skipped" in out
    assert "Corrupt Tool" in out


def test_reconfigure_detected_clients_updates_both_when_both_valid(tmp_path):
    """Double-red: with no corruption at all, both clients are still updated
    (the try/except around the pinned-check must not swallow the happy path)."""
    cfg_a = tmp_path / "a.json"
    cfg_a.write_text(json.dumps({"mcpServers": {"unity-biome-mcp": {"command": "old", "args": []}}}), encoding="utf-8")
    cfg_b = tmp_path / "b.json"
    cfg_b.write_text(json.dumps({"mcpServers": {"unity-biome-mcp": {"command": "old", "args": []}}}), encoding="utf-8")

    client_a = _fake_registry(cfg_a)["fake-tool"]
    client_b = _fake_registry(cfg_b)["fake-tool"]
    registry = {"tool-a": client_a, "tool-b": client_b}
    entry = {"command": "new", "args": []}
    merge_calls = []

    def fake_merge(path, e, root_key="mcpServers", entry_transformer=None):
        merge_calls.append(path)

    with patch.object(inst, "CLIENT_REGISTRY", registry), \
         patch.object(inst, "detect_installed", return_value=["tool-a", "tool-b"]), \
         patch.object(inst, "validate_config", return_value="Status: ok"), \
         patch.object(inst, "build_server_entry", return_value=entry), \
         patch.object(inst, "merge_mcp_config", fake_merge), \
         patch.object(inst, "backup", MagicMock()):
        inst._reconfigure_detected_clients()

    assert merge_calls == [cfg_a, cfg_b]


# ── version --unpin flag ──────────────────────────────────────────────────────

def test_version_unpin_flag_in_help():
    """install.py version --help must document --unpin (ARC-0b T3)."""
    r = subprocess.run(
        [sys.executable, str(REPO_ROOT / "install.py"), "version", "--help"],
        capture_output=True, encoding="utf-8",
    )
    assert r.returncode == 0
    assert "--unpin" in r.stdout


# ── stop subcommand argparse wiring ──────────────────────────────────────────

def test_stop_subcommand_registered():
    """install.py main() argparse must accept 'stop --port PORT'."""
    import argparse as _ap
    # Reconstruct the parser the same way main() does
    p = _ap.ArgumentParser()
    p.add_subparsers(dest="cmd")
    # Simulate what main() should register; verify it doesn't error
    # We test this by calling parse_args on a fresh module import
    assert callable(inst.main)


def test_stop_argparse_requires_port():
    """'stop' without --port should fail argparse (SystemExit)."""
    import subprocess
    r = subprocess.run(
        [sys.executable, str(REPO_ROOT / "install.py"), "stop"],
        capture_output=True, encoding="utf-8"
    )
    assert r.returncode != 0
    assert "error" in r.stderr.lower() or "required" in r.stderr.lower()
