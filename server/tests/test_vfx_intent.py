"""TDD tests for vfx_intent tool."""
from unittest.mock import AsyncMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError


# ---------------------------------------------------------------------------
# 1. Preset bypass — no Haiku needed
# ---------------------------------------------------------------------------

def test_vfx_preset_fire_explosion_returns_commands():
    from unity_mcp.tools.vfx_intent_tool import get_preset_config
    config = get_preset_config("fire_explosion")
    assert config is not None
    assert len(config) > 0


def test_vfx_preset_magic_burst_exists():
    from unity_mcp.tools.vfx_intent_tool import get_preset_config
    assert get_preset_config("magic_burst") is not None


def test_vfx_preset_dissolve_exists():
    from unity_mcp.tools.vfx_intent_tool import get_preset_config
    assert get_preset_config("dissolve") is not None


def test_vfx_preset_glow_outline_exists():
    from unity_mcp.tools.vfx_intent_tool import get_preset_config
    assert get_preset_config("glow_outline") is not None


def test_vfx_preset_smoke_trail_exists():
    from unity_mcp.tools.vfx_intent_tool import get_preset_config
    assert get_preset_config("smoke_trail") is not None


def test_vfx_preset_unknown_returns_none():
    from unity_mcp.tools.vfx_intent_tool import get_preset_config
    assert get_preset_config("nonexistent") is None


# ---------------------------------------------------------------------------
# 2. Auto-detect kind from keywords
# ---------------------------------------------------------------------------

def test_detect_kind_always_particle():
    """All inputs, including shader keywords, return particle."""
    from unity_mcp.tools.vfx_intent_tool import detect_kind
    for intent in ["dissolve effect", "glow outline", "shader magic", "material swap", "big explosion", "random text"]:
        assert detect_kind(intent) == "particle", f"Failed for: {intent}"


def test_vfx_shader_keyword_produces_particle_batch():
    """Shader keywords go through particle path without crashing."""
    from unity_mcp.tools.vfx_intent_tool import detect_kind, build_vfx_batch, parse_vfx_dsl
    kind = detect_kind("add dissolve effect to enemy")
    assert kind == "particle"
    dsl = "SET startColor = #FFFFFF\nMODULE sizeOverLifetime ENABLED"
    data = parse_vfx_dsl(dsl)
    lines = build_vfx_batch("/Enemy", data)
    assert len(lines) == 2
    assert all("particle" in l for l in lines)


# ---------------------------------------------------------------------------
# 3. DSL Parser
# ---------------------------------------------------------------------------

def test_vfx_parse_dsl_set():
    from unity_mcp.tools.vfx_intent_tool import parse_vfx_dsl
    dsl = "SET startColor = #FF2200\nSET startSize = 0.5,1.0"
    result = parse_vfx_dsl(dsl)
    assert result["sets"] == [("startColor", "#FF2200"), ("startSize", "0.5,1.0")]


def test_vfx_parse_dsl_module():
    from unity_mcp.tools.vfx_intent_tool import parse_vfx_dsl
    dsl = "MODULE colorOverLifetime ENABLED"
    result = parse_vfx_dsl(dsl)
    assert result["modules"] == [("colorOverLifetime", "ENABLED")]


def test_vfx_parse_dsl_gradient():
    from unity_mcp.tools.vfx_intent_tool import parse_vfx_dsl
    dsl = "GRADIENT color = #FF8800@0;#FF2200@1"
    result = parse_vfx_dsl(dsl)
    assert result["gradients"] == [("color", "#FF8800@0;#FF2200@1")]


def test_vfx_parse_dsl_mixed():
    from unity_mcp.tools.vfx_intent_tool import parse_vfx_dsl
    dsl = "SET startColor = #FF2200\nMODULE colorOverLifetime ENABLED\nGRADIENT color = #FF8800@0;#FF2200@1"
    result = parse_vfx_dsl(dsl)
    assert len(result["sets"]) == 1
    assert len(result["modules"]) == 1
    assert len(result["gradients"]) == 1


# ---------------------------------------------------------------------------
# 4. Builder
# ---------------------------------------------------------------------------

def test_vfx_builder_set_produces_particle_command():
    from unity_mcp.tools.vfx_intent_tool import build_vfx_batch
    data = {"sets": [("startColor", "#FF2200")], "modules": [], "gradients": []}
    lines = build_vfx_batch("/Fx", data)
    assert any("particle" in l and "startColor" in l for l in lines)


def test_vfx_builder_module_produces_particle_command():
    from unity_mcp.tools.vfx_intent_tool import build_vfx_batch
    data = {"sets": [], "modules": [("colorOverLifetime", "ENABLED")], "gradients": []}
    lines = build_vfx_batch("/Fx", data)
    assert any("colorOverLifetime" in l for l in lines)


def test_vfx_preset_build_fire_explosion():
    from unity_mcp.tools.vfx_intent_tool import get_preset_config, build_vfx_batch
    data = get_preset_config("fire_explosion")
    lines = build_vfx_batch("/Fx", data)
    assert len(lines) > 0


# ---------------------------------------------------------------------------
# 5. E2E mock
# ---------------------------------------------------------------------------

async def test_vfx_intent_preset_bypass_no_haiku():
    """fire_explosion preset should NOT call Haiku."""
    from unity_mcp.tools.vfx_intent_tool import vfx_intent
    with patch("unity_mcp.tools.vfx_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: 3 ops"
        with patch("unity_mcp.tools.vfx_intent_tool._sampling") as mock_svc:
            result = await vfx_intent(target="/Fx", intent="fire_explosion")
            mock_svc.generate.assert_not_called()
            assert "ok" in result


# ---------------------------------------------------------------------------
# 6. Bug fixes (Q1)
# ---------------------------------------------------------------------------

def test_vfx_batch_set_includes_module_main():
    """SET lines must include module=main so C# ParticleHelper dispatches correctly."""
    from unity_mcp.tools.vfx_intent_tool import build_vfx_batch, parse_vfx_dsl
    data = parse_vfx_dsl("SET startColor = #FF2200")
    lines = build_vfx_batch("/Obj", data)
    assert any("module=main" in l for l in lines)


def test_vfx_batch_gradient_emits_enabled_not_color():
    """GRADIENT lines must emit prop=enabled + prop=gradient with value, not prop=color."""
    from unity_mcp.tools.vfx_intent_tool import build_vfx_batch, parse_vfx_dsl
    grad_value = "#FF8800@0;#FF2200@1"
    data = parse_vfx_dsl(f"GRADIENT color = {grad_value}")
    lines = build_vfx_batch("/Obj", data)
    assert any("prop=enabled" in l for l in lines)
    assert not any("prop=color" in l for l in lines)
    gradient_lines = [l for l in lines if "prop=gradient" in l]
    assert gradient_lines, "expected a line with prop=gradient"
    assert grad_value in gradient_lines[0]


def test_vfx_preset_fire_no_unsupported_color_props():
    """fire_explosion preset gradient lines must not contain prop=color."""
    from unity_mcp.tools.vfx_intent_tool import build_vfx_batch, get_preset_config
    data = get_preset_config("fire_explosion")
    lines = build_vfx_batch("/Fx", data)
    gradient_lines = [l for l in lines if "colorOverLifetime" in l]
    assert not any("prop=color" in l for l in gradient_lines)


async def test_vfx_intent_no_sampling_returns_error():
    from unity_mcp.tools.vfx_intent_tool import vfx_intent
    with patch("unity_mcp.tools.vfx_intent_tool._send", new_callable=AsyncMock):
        with patch("unity_mcp.tools.vfx_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=None)
            with pytest.raises(ToolError, match="Haiku unavailable"):
                await vfx_intent(target="/Fx", intent="big explosion effect")


async def test_vfx_intent_dry_run():
    from unity_mcp.tools.vfx_intent_tool import vfx_intent
    dsl = "SET startColor = #FF2200\nSET startSize = 0.5"
    with patch("unity_mcp.tools.vfx_intent_tool._send", new_callable=AsyncMock) as mock_send:
        with patch("unity_mcp.tools.vfx_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await vfx_intent(target="/Fx", intent="fire effect", dry_run=True)
            mock_send.assert_not_called()
            assert "particle" in result


async def test_vfx_intent_e2e_executes_batch():
    from unity_mcp.tools.vfx_intent_tool import vfx_intent
    dsl = "SET startColor = #FF2200\nSET startSize = 0.5"
    with patch("unity_mcp.tools.vfx_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: 2 ops"
        with patch("unity_mcp.tools.vfx_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await vfx_intent(target="/Fx", intent="fire effect")
            mock_send.assert_called_once()
            assert mock_send.call_args[0][0] == "batch"
