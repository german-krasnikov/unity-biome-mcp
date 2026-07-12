"""TDD tests for ui_intent tool."""
from unittest.mock import AsyncMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError


# ---------------------------------------------------------------------------
# 1. DSL Parser — indent-based
# ---------------------------------------------------------------------------

def test_ui_parse_dsl_flat_canvas():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "canvas Canvas"
    nodes = parse_ui_dsl(dsl)
    assert len(nodes) == 1
    assert nodes[0]["type"] == "canvas"
    assert nodes[0]["name"] == "Canvas"
    assert nodes[0]["parent"] is None


def test_ui_parse_dsl_nested():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "canvas Canvas\n  panel HUD anchor=stretch"
    nodes = parse_ui_dsl(dsl)
    assert nodes[1]["type"] == "panel"
    assert nodes[1]["name"] == "HUD"
    assert nodes[1]["attrs"]["anchor"] == "stretch"
    assert nodes[1]["parent"] == "Canvas"


def test_ui_parse_dsl_deep_nesting():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "canvas Canvas\n  panel HUD\n    image HealthBar anchor=top-left pos=20,-20 size=200,30"
    nodes = parse_ui_dsl(dsl)
    assert nodes[2]["parent"] == "HUD"
    assert nodes[2]["attrs"]["anchor"] == "top-left"
    assert nodes[2]["attrs"]["pos"] == "20,-20"


def test_ui_parse_dsl_quoted_text():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = '    button Play text="Play Game"'
    nodes = parse_ui_dsl(dsl)
    assert nodes[0]["attrs"]["text"] == "Play Game"


def test_ui_parse_dsl_color_attr():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl
    dsl = "    image HealthBar color=#c33"
    nodes = parse_ui_dsl(dsl)
    assert nodes[0]["attrs"]["color"] == "#c33"


# ---------------------------------------------------------------------------
# 2. Templates — bypass Haiku
# ---------------------------------------------------------------------------

def test_ui_template_hud_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    dsl = get_template_dsl("hud")
    assert dsl is not None
    assert "canvas" in dsl.lower()


def test_ui_template_menu_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    dsl = get_template_dsl("menu")
    assert dsl is not None
    assert "button" in dsl.lower()


def test_ui_template_dialog_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    assert get_template_dsl("dialog") is not None


def test_ui_template_grid_returns_dsl():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    assert get_template_dsl("grid") is not None


def test_ui_template_unknown_returns_none():
    from unity_mcp.tools.ui_intent_tool import get_template_dsl
    assert get_template_dsl("nonexistent") is None


# ---------------------------------------------------------------------------
# 3. Builder
# ---------------------------------------------------------------------------

def test_ui_builder_canvas():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    assert any("create_ui" in l and "Canvas" in l for l in lines)


def test_ui_builder_image_with_attrs():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  image HealthBar anchor=top-left pos=20,-20 size=200,30 color=#c33"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    # Should produce create_ui and set_rect
    assert any("HealthBar" in l for l in lines)


def test_ui_builder_layout_group():
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  layout Menu dir=vertical spacing=10"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    assert any("VerticalLayoutGroup" in l or "layout" in l.lower() for l in lines)


# ---------------------------------------------------------------------------
# 4. E2E mock
# ---------------------------------------------------------------------------

async def test_ui_intent_template_bypass_no_haiku():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: 5 ops"
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            result = await ui_intent(intent="build UI", template="hud")
            mock_svc.generate.assert_not_called()
            assert "ok" in result


async def test_ui_intent_no_sampling_returns_error():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock):
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=None)
            with pytest.raises(ToolError, match="Haiku unavailable"):
                await ui_intent(intent="create a health bar UI")


async def test_ui_intent_dry_run():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    dsl = "canvas Canvas\n  image HealthBar anchor=top-left"
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await ui_intent(intent="health bar", dry_run=True)
            mock_send.assert_not_called()
            assert "create_ui" in result


async def test_ui_intent_e2e_executes_batch():
    from unity_mcp.tools.ui_intent_tool import ui_intent
    dsl = "canvas Canvas\n  image HealthBar anchor=top-left pos=20,-20 size=200,30"
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: 3 ops"
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=dsl)
            result = await ui_intent(intent="health bar HUD")
            mock_send.assert_called_once()
            assert mock_send.call_args[0][0] == "batch"


# ---------------------------------------------------------------------------
# 5. Nested path resolution — #23
# ---------------------------------------------------------------------------

def test_ui_builder_nested_set_rect_uses_full_path():
    """set_rect for nested child must use full path, not just name."""
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  panel HUD\n    image HealthBar anchor=top-left pos=20,-20 size=200,30"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    set_rect_lines = [l for l in lines if "set_rect" in l and "HealthBar" in l]
    assert set_rect_lines, "Expected a set_rect line for HealthBar"
    assert "/Canvas/HUD/HealthBar" in set_rect_lines[0], (
        f"Expected full path /Canvas/HUD/HealthBar, got: {set_rect_lines[0]}"
    )


def test_ui_builder_nested_manage_component_uses_full_path():
    """manage_component for nested layout must use full path."""
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  panel HUD\n    layout Menu dir=vertical spacing=10"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    comp_lines = [l for l in lines if "manage_component" in l and "Menu" in l]
    assert comp_lines, "Expected a manage_component line for Menu"
    assert "/Canvas/HUD/Menu" in comp_lines[0], (
        f"Expected full path /Canvas/HUD/Menu, got: {comp_lines[0]}"
    )


# ---------------------------------------------------------------------------
# 6. E2E edge cases
# ---------------------------------------------------------------------------

async def test_ui_intent_unknown_template_returns_error():
    """Unknown template name raises ToolError without calling bridge."""
    from unity_mcp.tools.ui_intent_tool import ui_intent
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        with pytest.raises(ToolError, match="nonexistent_xyz"):
            await ui_intent(intent="anything", template="nonexistent_xyz")
        mock_send.assert_not_called()


async def test_ui_intent_empty_dsl_from_llm_returns_error():
    """Empty/whitespace DSL from LLM (parses to zero nodes) raises ToolError, no bridge call."""
    from unity_mcp.tools.ui_intent_tool import ui_intent
    with patch("unity_mcp.tools.ui_intent_tool._send", new_callable=AsyncMock) as mock_send:
        with patch("unity_mcp.tools.ui_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value="   ")
            with pytest.raises(ToolError, match="DSL produced no commands"):
                await ui_intent(intent="make a HUD")
            mock_send.assert_not_called()


def test_ui_builder_external_parent_applied_to_root_node():
    """build_ui_batch with external parent= wires top-level node under that parent."""
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent="/ExistingUI")
    create_line = next(l for l in lines if "Canvas" in l and "create_ui" in l)
    assert "parent=/ExistingUI" in create_line


# ---------------------------------------------------------------------------
# 7. manage_component must use 'type=' key, not 'component=' (PY5.arch.1)
# ---------------------------------------------------------------------------

def test_ui_builder_manage_component_uses_type_key():
    """manage_component for layout node must use type=VerticalLayoutGroup, not component=."""
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  layout Menu dir=vertical spacing=10"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    mc_lines = [l for l in lines if "manage_component" in l]
    assert mc_lines, "Expected at least one manage_component line"
    assert any("type=VerticalLayoutGroup" in l for l in mc_lines), mc_lines
    assert not any("component=" in l for l in mc_lines), f"'component=' key found: {mc_lines}"


# ---------------------------------------------------------------------------
# 8. FIX-17: spacing emits set_property, not manage_component arg
# ---------------------------------------------------------------------------

def test_build_ui_batch_spacing_emits_set_property():
    """spacing=10 must produce a separate set_property line, not a manage_component arg."""
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  layout Menu dir=vertical spacing=10"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    mc_lines = [l for l in lines if "manage_component" in l]
    sp_lines = [l for l in lines if "set_property" in l and "spacing" in l]
    # manage_component must NOT contain spacing
    assert not any("spacing" in l for l in mc_lines), f"spacing leaked into manage_component: {mc_lines}"
    # set_property must contain spacing=10
    assert sp_lines, f"Expected set_property for spacing, got lines: {lines}"
    assert "prop=spacing" in sp_lines[0], sp_lines[0]
    assert "value=10" in sp_lines[0], sp_lines[0]
    assert "component=VerticalLayoutGroup" in sp_lines[0], sp_lines[0]


def test_build_ui_batch_no_spacing_no_extra_line():
    """Layout without spacing must NOT emit set_property."""
    from unity_mcp.tools.ui_intent_tool import parse_ui_dsl, build_ui_batch
    dsl = "canvas Canvas\n  layout Menu dir=vertical"
    nodes = parse_ui_dsl(dsl)
    lines = build_ui_batch(nodes, parent=None)
    sp_lines = [l for l in lines if "set_property" in l and "spacing" in l]
    assert not sp_lines, f"Unexpected set_property for spacing: {sp_lines}"
