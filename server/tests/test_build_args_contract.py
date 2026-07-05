"""Contract tests for BackendDef.build_args — assert exact flag presence in argv.

Regression anchor: if any mandatory flag is removed from build_args, the
corresponding test fails immediately before any subprocess runs.
"""
import pytest

from unity_mcp.backend_def import (
    AgyDef, ClaudeDef, CodexDef, KimiDef, OpenCodeDef,
    MCP_BLANKET, MCP_PERMISSION_TOOL, MCP_TOOL_PREFIX,
)

_TEST_PORT = 19500

# ── Claude: mandatory streaming flags ────────────────────────────────────────

def test_claude_has_verbose(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert "--verbose" in argv


def test_claude_has_output_format_stream_json(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    idx = argv.index("--output-format")
    assert argv[idx + 1] == "stream-json"


def test_claude_has_input_format_stream_json(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert "--input-format" in argv
    assert argv[argv.index("--input-format") + 1] == "stream-json"


def test_claude_dash_p_is_first(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert argv[0] == "-p"


def test_claude_has_include_partial_messages(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert "--include-partial-messages" in argv


# ── Claude: permission flags ─────────────────────────────────────────────────

def test_claude_permission_prompt_tool_value(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    idx = argv.index("--permission-prompt-tool")
    assert argv[idx + 1] == MCP_PERMISSION_TOOL


def test_claude_allowed_tools_default(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    idx = argv.index("--allowedTools")
    assert argv[idx + 1] == MCP_BLANKET


def test_claude_allowed_tools_subset_prefixed(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path),
                                        allowed_mcp_tools=["get_hierarchy", "batch"])
    idx = argv.index("--allowedTools")
    assert argv[idx + 1] == f"{MCP_TOOL_PREFIX}get_hierarchy,{MCP_TOOL_PREFIX}batch"


# ── Claude: env_strip ────────────────────────────────────────────────────────

def test_claude_env_strip_all_keys(tmp_path):
    _, _, strip = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                         config_dir=str(tmp_path))
    for key in ("CLAUDECODE", "UNITY_MCP_PORT", "UNITY_MCP_CHAT"):
        assert key in strip, f"expected {key!r} in env_strip"


def test_claude_no_env_set(tmp_path):
    _, env_set, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                            config_dir=str(tmp_path))
    assert env_set == {}


# ── Claude: mode + resume + model ───────────────────────────────────────────

def test_claude_ask_mode_plan(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert argv[argv.index("--permission-mode") + 1] == "plan"


def test_claude_agent_mode_accept_edits(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert argv[argv.index("--permission-mode") + 1] == "acceptEdits"


def test_claude_resume_adds_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        session_id="sess-123", config_dir=str(tmp_path))
    assert "--resume" in argv
    assert argv[argv.index("--resume") + 1] == "sess-123"


def test_claude_model_adds_flag(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model="haiku", mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    assert "--model" in argv
    assert argv[argv.index("--model") + 1] == "haiku"


def test_claude_mcp_config_path(tmp_path):
    argv, _, _ = ClaudeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        config_dir=str(tmp_path))
    idx = argv.index("--mcp-config")
    assert idx >= 0
    assert argv[idx + 1].endswith(".json")


def test_claude_uses_stream_json_flag():
    assert ClaudeDef().uses_stream_json is True


# ── Codex ────────────────────────────────────────────────────────────────────

def test_codex_exec_first():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT, prompt="x")
    assert argv[0] == "exec"


def test_codex_json_flag():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT, prompt="x")
    assert "--json" in argv


def test_codex_skip_git_repo_check():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT, prompt="x")
    assert "--skip-git-repo-check" in argv


def test_codex_resume():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                        prompt="x", session_id="s-42")
    assert "resume" in argv
    assert "s-42" in argv


def test_codex_model():
    argv, _, _ = CodexDef().build_args(mode="ask", model="gpt-4", mcp_port=_TEST_PORT, prompt="x")
    assert argv[argv.index("--model") + 1] == "gpt-4"


def test_codex_mcp_server_config():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT, prompt="x")
    toml_flags = [a for a in argv if "mcp_servers.unity" in a]
    assert len(toml_flags) >= 3


def test_codex_env_set_unity_mcp_port():
    _, env_set, strip = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT, prompt="x")
    assert env_set.get("UNITY_MCP_PORT") == str(_TEST_PORT)
    assert "UNITY_MCP_PORT" not in strip


def test_codex_prompt_appended():
    argv, _, _ = CodexDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT, prompt="hello")
    assert argv[-1] == "hello"


def test_codex_uses_stream_json_false():
    assert CodexDef().uses_stream_json is False


# ── Kimi ─────────────────────────────────────────────────────────────────────

def test_kimi_p_first(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                       prompt="hello", config_dir=str(tmp_path))
    assert argv[0] == "-p"


def test_kimi_output_format(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                       prompt="hello", config_dir=str(tmp_path))
    assert argv[argv.index("--output-format") + 1] == "stream-json"


def test_kimi_prompt_in_argv(tmp_path):
    """Kimi v0.20.1+: -p <prompt> as argv arg, NOT via stdin."""
    argv, _, _ = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                       prompt="test-prompt", config_dir=str(tmp_path))
    assert argv[1] == "test-prompt"
    assert "--input-format" not in argv


def test_kimi_reads_stdin_false():
    assert KimiDef().reads_stdin is False


def test_kimi_model(tmp_path):
    argv, _, _ = KimiDef().build_args(mode="ask", model="k1-32k", mcp_port=_TEST_PORT,
                                       prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--model") + 1] == "k1-32k"


def test_kimi_env_set_unity_mcp_port(tmp_path):
    _, env_set, strip = KimiDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                              prompt="x", config_dir=str(tmp_path))
    assert env_set.get("UNITY_MCP_PORT") == str(_TEST_PORT)
    assert "UNITY_MCP_PORT" not in strip


def test_kimi_uses_stream_json_flag():
    assert KimiDef().uses_stream_json is False


# ── Agy ──────────────────────────────────────────────────────────────────────

def test_agy_p_flag(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                      prompt="x", config_dir=str(tmp_path))
    assert argv[0] == "-p"


def test_agy_prompt(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                      prompt="test", config_dir=str(tmp_path))
    assert argv[1] == "test"


def test_agy_ask_no_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                      prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" not in argv


def test_agy_agent_skip_permissions(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="agent", model=None, mcp_port=_TEST_PORT,
                                      prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" in argv


def test_agy_model(tmp_path):
    argv, _, _ = AgyDef().build_args(mode="ask", model="gemini-2", mcp_port=_TEST_PORT,
                                      prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--model") + 1] == "gemini-2"


def test_agy_env_set_unity_mcp_port(tmp_path):
    _, env_set, strip = AgyDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                             prompt="x", config_dir=str(tmp_path))
    assert env_set.get("UNITY_MCP_PORT") == str(_TEST_PORT)
    assert "UNITY_MCP_PORT" not in strip


def test_agy_uses_stream_json_false():
    assert AgyDef().uses_stream_json is False


# ── OpenCode ─────────────────────────────────────────────────────────────────

def test_opencode_run_first(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                           prompt="x", config_dir=str(tmp_path))
    assert argv[0] == "run"


def test_opencode_format_json(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                           prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--format") + 1] == "json"


def test_opencode_skip_permissions(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                           prompt="x", config_dir=str(tmp_path))
    assert "--dangerously-skip-permissions" in argv


def test_opencode_resume(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                           prompt="x", session_id="s-99",
                                           config_dir=str(tmp_path))
    assert "-s" in argv
    assert argv[argv.index("-s") + 1] == "s-99"


def test_opencode_model(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model="o3", mcp_port=_TEST_PORT,
                                           prompt="x", config_dir=str(tmp_path))
    assert argv[argv.index("--model") + 1] == "o3"


def test_opencode_config_in_env(tmp_path):
    _, env_set, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                              prompt="x", config_dir=str(tmp_path))
    assert "OPENCODE_CONFIG" in env_set


def test_opencode_prompt_last(tmp_path):
    argv, _, _ = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                           prompt="my-prompt", config_dir=str(tmp_path))
    assert argv[-1] == "my-prompt"


def test_opencode_env_set_unity_mcp_port(tmp_path):
    _, env_set, strip = OpenCodeDef().build_args(mode="ask", model=None, mcp_port=_TEST_PORT,
                                                  prompt="x", config_dir=str(tmp_path))
    assert env_set.get("UNITY_MCP_PORT") == str(_TEST_PORT)
    assert "UNITY_MCP_PORT" not in strip


def test_opencode_uses_stream_json_false():
    assert OpenCodeDef().uses_stream_json is False


# ── _sanitize_extra_args: equals-style blocked flags ────────────────────────

def test_sanitize_blocks_equals_style_permission_mode(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=_TEST_PORT,
        config_dir=str(tmp_path),
        extra_args="--permission-mode=acceptEdits --verbose")
    vals = [a for a in argv if "acceptEdits" in a]
    assert len(vals) == 0  # blocked


def test_sanitize_blocks_equals_style_output_format(tmp_path):
    argv, _, _ = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=_TEST_PORT,
        config_dir=str(tmp_path),
        extra_args="--output-format=text")
    vals = [a for a in argv if "output-format=text" in a]
    assert len(vals) == 0


# ── env contract: Claude strips UNITY_MCP_PORT; non-Claude sets it in env_set ─

def test_claude_env_strip_includes_unity_mcp_port(tmp_path):
    """Claude uses --mcp-config JSON, so UNITY_MCP_PORT must be stripped."""
    _, _, strip = ClaudeDef().build_args(
        mode="ask", model=None, mcp_port=9601,
        prompt="x", config_dir=str(tmp_path),
    )
    assert "UNITY_MCP_PORT" in strip


@pytest.mark.parametrize("DefCls", [CodexDef, KimiDef, AgyDef, OpenCodeDef])
def test_non_claude_env_set_includes_unity_mcp_port(DefCls, tmp_path):
    """Non-Claude backends pass UNITY_MCP_PORT via env_set, not env_strip."""
    _, env_set, strip = DefCls().build_args(
        mode="ask", model=None, mcp_port=9601,
        prompt="x", config_dir=str(tmp_path),
    )
    assert env_set.get("UNITY_MCP_PORT") == "9601"
    assert "UNITY_MCP_PORT" not in strip


# ── output_format discriminator ──────────────────────────────────────────────

def test_claude_output_format_stream_json():
    from unity_mcp.backend_def import OUTPUT_FORMAT_STREAM_JSON
    assert ClaudeDef().output_format == OUTPUT_FORMAT_STREAM_JSON


def test_kimi_output_format_kimi_json():
    from unity_mcp.backend_def import OUTPUT_FORMAT_KIMI_JSON
    assert KimiDef().output_format == OUTPUT_FORMAT_KIMI_JSON


def test_agy_output_format_plain_text():
    from unity_mcp.backend_def import OUTPUT_FORMAT_PLAIN_TEXT
    assert AgyDef().output_format == OUTPUT_FORMAT_PLAIN_TEXT


def test_codex_output_format_codex_json():
    from unity_mcp.backend_def import OUTPUT_FORMAT_CODEX_JSON
    assert CodexDef().output_format == OUTPUT_FORMAT_CODEX_JSON


def test_opencode_output_format_opencode_json():
    from unity_mcp.backend_def import OUTPUT_FORMAT_OPENCODE_JSON
    assert OpenCodeDef().output_format == OUTPUT_FORMAT_OPENCODE_JSON


# backward compat: uses_stream_json property still works
def test_uses_stream_json_property_true_for_stream_json_backends():
    assert ClaudeDef().uses_stream_json is True


def test_uses_stream_json_property_false_for_other_backends():
    assert AgyDef().uses_stream_json is False
    assert CodexDef().uses_stream_json is False
    assert OpenCodeDef().uses_stream_json is False
    assert KimiDef().uses_stream_json is False


# ── MCP_BLANKET must derive from SERVER_NAME, not a hardcoded literal ────────
# Drift guard: a plain `assert MCP_BLANKET == "mcp__unity-mcp"` would pass even
# if MCP_BLANKET were hardcoded independently of SERVER_NAME (as it was before
# this fix — "mcp__unity", wrong under both the old AND new naming schemes).
# Deriving the expected value FROM the shared constant proves the DRY coupling.
def test_mcp_blanket_matches_server_name_exactly():
    from unity_mcp.config.merger import SERVER_NAME
    assert MCP_BLANKET == f"mcp__{SERVER_NAME}"
