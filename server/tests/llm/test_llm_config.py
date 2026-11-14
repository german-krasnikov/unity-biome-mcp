"""TDD tests for llm_config.py — LLM profile system."""
import os
import pytest


@pytest.fixture(autouse=True)
def reset_overrides():
    from unity_mcp.llm_config import reset
    reset()
    yield
    reset()


def test_default_profile_returns_haiku():
    from unity_mcp.llm_config import get_profile
    assert get_profile("unknown_feature").model == "haiku"


def test_default_profile_per_feature_timeout():
    from unity_mcp.llm_config import get_profile
    assert get_profile("screenshot_describe").timeout == 20.0


def test_default_profile_visual_diff_timeout():
    from unity_mcp.llm_config import get_profile
    assert get_profile("visual_diff").timeout == 25.0


def test_apply_config_overrides_model():
    from unity_mcp.llm_config import apply_config, get_profile
    apply_config({"screenshot_describe": {"model": "sonnet", "max_turns": 2, "timeout": 20.0}})
    assert get_profile("screenshot_describe").model == "sonnet"


def test_apply_config_preserves_timeout():
    from unity_mcp.llm_config import apply_config, get_profile
    apply_config({"visual_verify": {"model": "sonnet", "max_turns": 2, "timeout": 99.0}})
    assert get_profile("visual_verify").timeout == 99.0


def test_env_override_takes_precedence(monkeypatch):
    from unity_mcp.llm_config import get_profile
    monkeypatch.setenv("UNITY_MCP_LLM_MODEL_VISUAL_VERIFY", "sonnet")
    assert get_profile("visual_verify").model == "sonnet"


def test_env_override_preserves_base_timeout(monkeypatch):
    from unity_mcp.llm_config import get_profile
    monkeypatch.setenv("UNITY_MCP_LLM_MODEL_VISUAL_VERIFY", "sonnet")
    assert get_profile("visual_verify").timeout == 15.0


def test_env_model_override_preserves_non_claude_backend_for_fail_closed_sampling(monkeypatch):
    from unity_mcp.llm_config import apply_config, get_profile

    apply_config({
        "visual_verify": {
            "model": "gemini-old",
            "max_turns": 2,
            "timeout": 15.0,
            "backend": "gemini",
        }
    })
    monkeypatch.setenv("UNITY_MCP_LLM_MODEL_VISUAL_VERIFY", "gemini-new")

    profile = get_profile("visual_verify")
    assert profile.model == "gemini-new"
    assert profile.backend == "gemini"


def test_to_cli_args_basic():
    from unity_mcp.llm_config import LlmProfile
    args = LlmProfile("haiku", max_turns=2, timeout=15.0).to_cli_args()
    assert args == ["--model", "haiku", "--max-turns", "2"]


def test_to_cli_args_with_max_tokens():
    from unity_mcp.llm_config import LlmProfile
    args = LlmProfile("haiku", max_turns=1, timeout=15.0, max_tokens=512).to_cli_args()
    assert "--max-tokens" in args
    assert "512" in args


def test_to_cli_args_no_max_tokens_when_none():
    from unity_mcp.llm_config import LlmProfile
    args = LlmProfile("haiku", max_turns=1, timeout=15.0).to_cli_args()
    assert "--max-tokens" not in args


def test_parse_tcp_config_basic():
    from unity_mcp.llm_config import parse_tcp_config
    result = parse_tcp_config("visual_verify:haiku,2,15.0,0")
    assert result["visual_verify"]["model"] == "haiku"
    assert result["visual_verify"]["max_turns"] == 2
    assert result["visual_verify"]["timeout"] == 15.0
    assert result["visual_verify"]["max_tokens"] is None


def test_parse_tcp_config_with_max_tokens():
    from unity_mcp.llm_config import parse_tcp_config
    result = parse_tcp_config("summarize:sonnet,1,10.0,512")
    assert result["summarize"]["max_tokens"] == 512


def test_parse_tcp_config_multiple_lines():
    from unity_mcp.llm_config import parse_tcp_config
    payload = "visual_verify:haiku,2,15.0,0\nsummarize:sonnet,1,10.0,0"
    result = parse_tcp_config(payload)
    assert "visual_verify" in result
    assert "summarize" in result


def test_reset_clears_overrides():
    from unity_mcp.llm_config import apply_config, get_profile, reset
    apply_config({"screenshot_describe": {"model": "opus", "max_turns": 3, "timeout": 30.0}})
    reset()
    assert get_profile("screenshot_describe").model == "haiku"


def test_set_profile_overrides():
    from unity_mcp.llm_config import set_profile, get_profile, LlmProfile
    set_profile("summarize", LlmProfile("sonnet", max_turns=3, timeout=20.0))
    assert get_profile("summarize").model == "sonnet"
    assert get_profile("summarize").max_turns == 3


# --- Backend field tests ---

def test_default_profile_backend_is_claude():
    from unity_mcp.llm_config import get_profile
    assert get_profile("visual_verify").backend == "claude"


def test_parse_tcp_config_with_backend():
    from unity_mcp.llm_config import parse_tcp_config
    result = parse_tcp_config("visual_verify:gemini-2.5-flash,2,15.0,0,gemini")
    assert result["visual_verify"]["backend"] == "gemini"
    assert result["visual_verify"]["model"] == "gemini-2.5-flash"


def test_parse_tcp_config_missing_backend_defaults_claude():
    """Backward compat: old 4-field format → backend='claude'."""
    from unity_mcp.llm_config import parse_tcp_config
    result = parse_tcp_config("visual_verify:haiku,2,15.0,0")
    assert result["visual_verify"]["backend"] == "claude"


def test_parse_tcp_config_empty_backend_defaults_claude():
    from unity_mcp.llm_config import parse_tcp_config
    result = parse_tcp_config("visual_verify:haiku,2,15.0,0,")
    assert result["visual_verify"]["backend"] == "claude"


def test_apply_config_sets_backend():
    from unity_mcp.llm_config import apply_config, get_profile
    apply_config({"visual_verify": {"model": "codex-mini-latest", "max_turns": 2, "timeout": 15.0, "backend": "codex"}})
    assert get_profile("visual_verify").backend == "codex"


def test_apply_config_missing_backend_defaults_claude():
    from unity_mcp.llm_config import apply_config, get_profile
    apply_config({"visual_verify": {"model": "haiku", "max_turns": 2, "timeout": 15.0}})
    assert get_profile("visual_verify").backend == "claude"
