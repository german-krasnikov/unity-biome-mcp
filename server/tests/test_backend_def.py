"""Tests for backend_def.py — all 5 CLI backends + resolve_binary. No real processes."""
import asyncio
import json
import os
import sys
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import pytest

_TEST_PORT = 19500

from unity_mcp.backend_def import (
    BACKENDS,
    AgyDef,
    ClaudeDef,
    CodexDef,
    KimiDef,
    OpenCodeDef,
    _which_via_login_shell,
    _which_windows_registry,
    sanitize_extra_args,
    MCP_BLANKET,
    MCP_PERMISSION_TOOL,
)


# ─── resolve_binary (3 tests) ───────────────────────────────────────────────

async def test_resolve_binary_shutil_found(monkeypatch):
    monkeypatch.setattr("unity_mcp.backend_def.shutil.which", lambda _: "/usr/bin/claude")
    assert await ClaudeDef().resolve_binary() == "/usr/bin/claude"


async def test_resolve_binary_shutil_miss_zsh_ok(monkeypatch):
    from unittest.mock import AsyncMock
    monkeypatch.setattr("unity_mcp.backend_def.shutil.which", lambda _: None)
    monkeypatch.setattr(
        "unity_mcp.backend_def._which_via_login_shell",
        AsyncMock(return_value="/opt/homebrew/bin/claude"),
    )
    assert await ClaudeDef().resolve_binary() == "/opt/homebrew/bin/claude"


async def test_resolve_binary_both_miss(monkeypatch):
    from unittest.mock import AsyncMock
    monkeypatch.setattr("unity_mcp.backend_def.shutil.which", lambda _: None)
    monkeypatch.setattr("unity_mcp.backend_def._which_via_login_shell", AsyncMock(return_value=None))
    assert await ClaudeDef().resolve_binary() is None


# ─── Windows registry PATH resolution (4 tests) ─────────────────────────────

def _fake_winreg(path_value: str):
    """Build a fake winreg module returning path_value for both HKCU and HKLM keys."""
    fake = MagicMock()
    fake.HKEY_CURRENT_USER = "HKCU"
    fake.HKEY_LOCAL_MACHINE = "HKLM"

    class _Key:
        def __enter__(self):
            return self

        def __exit__(self, *a):
            return False

    def _open_key(root, subkey):
        return _Key()

    fake.OpenKey.side_effect = _open_key
    fake.QueryValueEx.return_value = (path_value, 1)
    return fake


def test_which_windows_registry_finds_binary_in_path_dir(tmp_path, monkeypatch):
    (tmp_path / "codex.exe").write_text("stub", encoding="utf-8")
    monkeypatch.setattr("unity_mcp.backend_def.winreg", _fake_winreg(str(tmp_path)))
    found = _which_windows_registry("codex")
    assert found == str(tmp_path / "codex.exe")


def test_which_windows_registry_finds_cmd_shim(tmp_path, monkeypatch):
    (tmp_path / "opencode.cmd").write_text("stub", encoding="utf-8")
    monkeypatch.setattr("unity_mcp.backend_def.winreg", _fake_winreg(str(tmp_path)))
    found = _which_windows_registry("opencode")
    assert found == str(tmp_path / "opencode.cmd")


def test_which_windows_registry_no_match_returns_none(tmp_path, monkeypatch):
    monkeypatch.setattr("unity_mcp.backend_def.winreg", _fake_winreg(str(tmp_path)))
    assert _which_windows_registry("nonexistent-binary") is None


def test_which_windows_registry_checks_fallback_dirs(tmp_path, monkeypatch):
    npm_dir = tmp_path / "npm"
    npm_dir.mkdir()
    (npm_dir / "claude.cmd").write_text("stub", encoding="utf-8")
    monkeypatch.setenv("APPDATA", str(tmp_path))
    # registry PATH is empty/unrelated — only the %APPDATA%\npm fallback has it
    monkeypatch.setattr("unity_mcp.backend_def.winreg", _fake_winreg(""))
    found = _which_windows_registry("claude")
    assert found == str(npm_dir / "claude.cmd")


async def test_which_via_login_shell_dispatches_to_registry_on_windows(monkeypatch):
    monkeypatch.setattr("unity_mcp.backend_def.sys.platform", "win32")
    monkeypatch.setattr("unity_mcp.backend_def._which_windows_registry", lambda binary: f"C:\\{binary}.exe")
    assert await _which_via_login_shell("codex") == "C:\\codex.exe"


# ─── Claude (7 tests) ───────────────────────────────────────────────────────

def test_claude_build_args_ask_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert "--permission-mode" in argv
    assert argv[argv.index("--permission-mode") + 1] == "plan"


def test_claude_build_args_agent_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert argv[argv.index("--permission-mode") + 1] == "acceptEdits"


def test_claude_resume_includes_session_id(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        session_id="abc123",
                                        config_dir=str(tmp_path))
    assert "--resume" in argv
    assert argv[argv.index("--resume") + 1] == "abc123"


def test_claude_env_strip_keys(tmp_path):
    _, _, strip = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                         config_dir=str(tmp_path))
    assert "CLAUDECODE"        in strip
    assert "UNITY_MCP_PORT"   in strip


def test_claude_agent_name_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        agent_name="my-agent",
                                        config_dir=str(tmp_path))
    assert "--agent" in argv
    assert argv[argv.index("--agent") + 1] == "my-agent"


def test_claude_model_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model="claude-opus-4",
                                        mcp_port=_TEST_PORT, config_dir=str(tmp_path))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "claude-opus-4"


def test_claude_no_resume_when_none(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        session_id=None, config_dir=str(tmp_path))
    assert "--resume" not in argv


# ─── Codex (5 tests + 3 mode-approval tests) ────────────────────────────────

def test_codex_ask_mode_uses_ask_approval():
    """T9: ask mode must pass -s ask to Codex (not danger-full-access)."""
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT, prompt="x")
    assert "-s" in argv
    assert argv[argv.index("-s") + 1] == "ask"


def test_codex_agent_mode_uses_danger_full_access_approval():
    """T9: agent mode must pass -s danger-full-access."""
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT, prompt="x")
    assert argv[argv.index("-s") + 1] == "danger-full-access"


def test_codex_unknown_mode_defaults_to_danger_full_access():
    """T9: unknown mode falls back to danger-full-access (safe default)."""
    argv, _, _ = CodexDef().build_args(mode="yolo", model=None, mcp_port=_TEST_PORT, prompt="x")
    assert argv[argv.index("-s") + 1] == "danger-full-access"


def test_codex_first_turn_argv_structure():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                       prompt="hello")
    assert argv[0] == "exec"
    assert "--json" in argv
    assert "-s" in argv
    assert argv[argv.index("-s") + 1] == "danger-full-access"


def test_codex_resume_argv_structure():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                       prompt="hello", session_id="sess-42")
    assert argv[:2] == ["exec", "resume"]
    assert argv[2] == "sess-42"
    assert "--dangerously-bypass-approvals-and-sandbox" in argv


def test_codex_prompt_is_last_arg():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                       prompt="do the thing")
    assert argv[-1] == "do the thing"


def test_codex_toml_mcp_flags_present():
    argv, _, _ = CodexDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                       prompt="x")
    # Collect all -c values
    c_values = [argv[i + 1] for i, v in enumerate(argv) if v == "-c"]
    assert any("mcp_servers.unity-biome-mcp.command=" in v for v in c_values)
    assert any("mcp_servers.unity-biome-mcp.args=" in v for v in c_values)
    assert any("mcp_servers.unity-biome-mcp.startup_timeout_sec=30" in v for v in c_values)


def test_codex_env_set_unity_mcp_port():
    _, env_set, strip = CodexDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                              prompt="x")
    assert env_set.get("UNITY_MCP_PORT") == str(_TEST_PORT)
    assert "UNITY_MCP_PORT" not in strip


def test_codex_argv_env_port_flag_matches_env_set():
    """T3: port in -c TOML flag must match env_set port (no drift between the two)."""
    import re
    argv, env_set, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=9601, prompt="hi")
    c_values = [argv[i + 1] for i, a in enumerate(argv) if a == "-c"]
    port_flag = next((v for v in c_values if "UNITY_MCP_PORT" in v), None)
    assert port_flag is not None, f"-c UNITY_MCP_PORT flag not found in argv: {argv}"
    m = re.search(r'UNITY_MCP_PORT="(\d+)"', port_flag)
    assert m is not None, f"port not extractable from: {port_flag}"
    assert m.group(1) == env_set["UNITY_MCP_PORT"] == "9601"


# ─── Kimi (5 tests) ─────────────────────────────────────────────────────────

def test_kimi_argv_structure(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                      prompt="hi", config_dir=str(tmp_path))
    assert argv[0] == "-p"
    assert "--input-format" not in argv  # kimi v0.20.1+ does not support --input-format
    assert "--output-format" in argv
    assert argv[argv.index("--output-format") + 1] == "stream-json"


def test_kimi_model_flag(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model="kimi-k2", mcp_port=_TEST_PORT,
                                      prompt="x", config_dir=str(tmp_path))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "kimi-k2"


def test_kimi_env_set_unity_mcp_port(tmp_path):
    _, env_set, strip = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                              prompt="x", config_dir=str(tmp_path))
    assert env_set.get("UNITY_MCP_PORT") == str(_TEST_PORT)
    assert "UNITY_MCP_PORT" not in strip


def test_kimi_writes_mcp_config(tmp_path):
    KimiDef().build_args(mode="ask", model=None, mcp_port=9601,
                         prompt="x", config_dir=str(tmp_path))
    mcp_path = tmp_path / "mcp.json"
    assert mcp_path.exists()
    data = json.loads(mcp_path.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-biome-mcp"]["env"]["UNITY_MCP_PORT"] == "9601"


def test_kimi_no_resume():
    assert KimiDef().has_resume is False


# ─── Agy (4 tests) ──────────────────────────────────────────────────────────

def test_agy_ask_no_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                     prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" not in argv


def test_agy_agent_adds_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                     prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" in argv


def test_agy_env_set_unity_mcp_port(tmp_path):
    _, env_set, strip = AgyDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                             prompt="x", config_dir=str(tmp_path))
    assert env_set.get("UNITY_MCP_PORT") == str(_TEST_PORT)
    assert "UNITY_MCP_PORT" not in strip


def test_agy_writes_settings_json(tmp_path):
    AgyDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                        prompt="x", config_dir=str(tmp_path))
    settings_path = tmp_path / "settings.json"
    assert settings_path.exists()
    data = json.loads(settings_path.read_text(encoding="utf-8"))
    assert data["mcpServers"]["unity-biome-mcp"]["env"]["UNITY_MCP_PORT"] == str(_TEST_PORT)


# ─── OpenCode (4 tests) ─────────────────────────────────────────────────────

def test_opencode_argv_structure(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                          prompt="x", config_dir=str(tmp_path))
    assert argv[0] == "run"
    assert "--format" in argv
    assert argv[argv.index("--format") + 1] == "json"
    assert "--dangerously-skip-permissions" in argv


def test_opencode_resume_flag(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                          prompt="x", session_id="oc-99",
                                          config_dir=str(tmp_path))
    assert "-s" in argv
    assert argv[argv.index("-s") + 1] == "oc-99"


def test_opencode_env_set_opencode_config(tmp_path):
    _, env_set, _ = OpenCodeDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                              prompt="x", config_dir=str(tmp_path))
    assert "OPENCODE_CONFIG" in env_set
    assert f"opencode-unity-biome-mcp-{_TEST_PORT}.json" in env_set["OPENCODE_CONFIG"]


def test_opencode_writes_config_file(tmp_path):
    OpenCodeDef().build_args(mode="agent", model=None, mcp_port=9603,
                             prompt="x", config_dir=str(tmp_path))
    config_path = tmp_path / "opencode-unity-biome-mcp-9603.json"
    assert config_path.exists()
    data = json.loads(config_path.read_text(encoding="utf-8"))
    assert data["mcp"]["unity-biome-mcp"]["environment"]["UNITY_MCP_PORT"] == "9603"


# ─── M3: sanitize_extra_args (7 tests) ─────────────────────────────────────

def test_sanitize_passes_safe_flag():
    assert sanitize_extra_args("--verbose") == ["--verbose"]


def test_sanitize_blocks_permission_mode_and_value():
    result = sanitize_extra_args("--permission-mode acceptEdits --verbose")
    assert "--permission-mode" not in result
    assert "acceptEdits" not in result
    assert "--verbose" in result


def test_sanitize_blocks_output_format_and_value():
    result = sanitize_extra_args("--output-format text")
    assert result == []


def test_sanitize_blocks_format_flag():
    assert sanitize_extra_args("--format json -p hello") == ["-p", "hello"]


def test_sanitize_blocks_mcp_config_and_value():
    result = sanitize_extra_args("--mcp-config /evil/path --model sonnet")
    assert "--mcp-config" not in result
    assert "/evil/path" not in result
    assert "--model" in result
    assert "sonnet" in result


def test_sanitize_empty_string_returns_empty():
    assert sanitize_extra_args("") == []
    assert sanitize_extra_args(None) == []


def test_claude_extra_args_cannot_inject_permission_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=_TEST_PORT,
        config_dir=str(tmp_path),
        extra_args="--permission-mode acceptEdits",
    )
    idx = argv.index("--permission-mode")
    assert argv[idx + 1] == "plan"
    assert argv.count("--permission-mode") == 1


def test_claude_extra_args_cannot_inject_output_format(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=_TEST_PORT,
        config_dir=str(tmp_path),
        extra_args="--output-format text",
    )
    idx = argv.index("--output-format")
    assert argv[idx + 1] == "stream-json"
    assert argv.count("--output-format") == 1


# ─── reads_stdin flag ────────────────────────────────────────────────────────

def test_claude_reads_stdin():
    assert ClaudeDef().reads_stdin is True

def test_kimi_reads_stdin():
    assert KimiDef().reads_stdin is False

def test_agy_does_not_read_stdin():
    assert AgyDef().reads_stdin is False

def test_codex_does_not_read_stdin():
    assert CodexDef().reads_stdin is False

def test_kimi_argv_has_no_input_format(tmp_path):
    """kimi v0.20.1+ does not support --input-format; prompt is sent via stdin."""
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                       config_dir=str(tmp_path))
    assert "--input-format" not in argv
    assert "--output-format" in argv
    assert argv[argv.index("--output-format") + 1] == "stream-json"

def test_kimi_argv_prompt_value(tmp_path):
    """After -p must come the prompt string (Kimi v0.20.1+ -p <prompt> syntax)."""
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                       prompt="hello", config_dir=str(tmp_path))
    p_idx = argv.index("-p")
    assert argv[p_idx + 1] == "hello"


# ─── login_shell_path (3 tests) ─────────────────────────────────────────────
# Manual cache reset per test (no conftest fixture dependency — hermetic on its own).

import subprocess as _subprocess  # noqa: E402  (for TimeoutExpired only)


async def test_login_shell_path_success_caches_and_skips_second_spawn(monkeypatch):
    from unity_mcp import backend_def
    backend_def._LOGIN_PATH_CACHE = None
    monkeypatch.setattr(backend_def.sys, "platform", "darwin")
    run_mock = MagicMock(return_value=MagicMock(stdout="/usr/bin:/bin\n"))
    monkeypatch.setattr(backend_def.subprocess, "run", run_mock)

    first = await backend_def.login_shell_path()
    second = await backend_def.login_shell_path()

    assert first == second == "/usr/bin:/bin"
    run_mock.assert_called_once()  # second call was a cache hit, no re-spawn


async def test_login_shell_path_timeout_returns_empty(monkeypatch):
    from unity_mcp import backend_def
    backend_def._LOGIN_PATH_CACHE = None
    monkeypatch.setattr(backend_def.sys, "platform", "darwin")
    monkeypatch.setattr(
        backend_def.subprocess, "run",
        MagicMock(side_effect=_subprocess.TimeoutExpired(cmd="zsh", timeout=3)),
    )

    assert await backend_def.login_shell_path() == ""


async def test_login_shell_path_retries_after_ttl_on_failure(monkeypatch):
    from unity_mcp import backend_def
    backend_def._LOGIN_PATH_CACHE = None
    backend_def._LOGIN_PATH_CACHE_TS = 0.0
    monkeypatch.setattr(backend_def.sys, "platform", "darwin")

    calls = {"n": 0}

    def _boom(*a, **kw):
        calls["n"] += 1
        raise _subprocess.TimeoutExpired(cmd="zsh", timeout=3)

    monkeypatch.setattr(backend_def.subprocess, "run", _boom)

    clock = {"t": 1000.0}
    monkeypatch.setattr(backend_def.time, "monotonic", lambda: clock["t"])

    assert await backend_def.login_shell_path() == ""
    assert calls["n"] == 1

    # still within retry cooldown — must NOT re-spawn
    assert await backend_def.login_shell_path() == ""
    assert calls["n"] == 1

    # cooldown elapsed — must retry
    clock["t"] += backend_def._LOGIN_PATH_RETRY_TTL + 1
    assert await backend_def.login_shell_path() == ""
    assert calls["n"] == 2


# ─── probe_capabilities (5 tests) ───────────────────────────────────────────

async def test_probe_capabilities_claude_has_resume(monkeypatch):
    from unity_mcp import backend_def
    backend_def._PROBE_CACHE.clear()

    b = ClaudeDef()
    monkeypatch.setattr(backend_def, "_run_login_shell", AsyncMock(return_value="claude 0.2.55\n"))
    result = await b.probe_capabilities()
    assert result["has_resume"] is True


async def test_probe_capabilities_cached_second_call_no_spawn(monkeypatch):
    from unity_mcp import backend_def
    backend_def._PROBE_CACHE.clear()

    b = ClaudeDef()
    call_count = {"n": 0}

    original_probe = b._probe.__func__

    async def counting_probe(self):
        call_count["n"] += 1
        return await original_probe(self)

    monkeypatch.setattr(ClaudeDef, "_probe", counting_probe)
    monkeypatch.setattr(backend_def, "_run_login_shell", AsyncMock(return_value=""))

    await b.probe_capabilities()
    await b.probe_capabilities()  # second call — should be cache hit

    assert call_count["n"] == 1


async def test_probe_capabilities_timeout_returns_defaults(monkeypatch):
    from unity_mcp import backend_def
    backend_def._PROBE_CACHE.clear()

    b = ClaudeDef()

    async def slow_probe(self):
        await asyncio.sleep(10)
        return {}

    monkeypatch.setattr(ClaudeDef, "_probe", slow_probe)
    monkeypatch.setattr(backend_def, "_PROBE_TIMEOUT", 0.05)

    result = await b.probe_capabilities()

    assert result["has_resume"] is True  # default for ClaudeDef
    assert result["has_cancel"] is False
    assert "has_modes" in result
    assert result["binary_version"] is None


async def test_probe_capabilities_exception_returns_defaults(monkeypatch):
    from unity_mcp import backend_def
    backend_def._PROBE_CACHE.clear()

    b = ClaudeDef()

    async def broken_probe(self):
        raise RuntimeError("probe boom")

    monkeypatch.setattr(ClaudeDef, "_probe", broken_probe)

    result = await b.probe_capabilities()

    assert result["has_resume"] is True  # default for ClaudeDef
    assert result["has_cancel"] is False


async def test_probe_capabilities_kimi_no_resume(monkeypatch):
    from unity_mcp import backend_def
    backend_def._PROBE_CACHE.clear()

    result = await KimiDef().probe_capabilities()
    assert result["has_resume"] is False
