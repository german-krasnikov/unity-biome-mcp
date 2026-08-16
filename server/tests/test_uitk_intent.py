"""TDD tests for uitk_intent — NL → DSL → UXML+USS pipeline. $0, no Unity."""
import xml.etree.ElementTree as ET
from unittest.mock import AsyncMock, patch

import pytest
from mcp.server.fastmcp.exceptions import ToolError


# ---------------------------------------------------------------------------
# 1. DSL Parser (6 tests)
# ---------------------------------------------------------------------------

def test_parse_dsl_valid_tree_and_style():
    from unity_mcp.tools.uitk_intent_tool import parse_uitk_dsl
    dsl = "=TREE=\npanel root\n  label title\n=STYLE=\n.root { flex-grow: 1; }"
    data = parse_uitk_dsl(dsl)
    assert len(data["tree"]) == 2
    assert data["style"] == ".root { flex-grow: 1; }"


def test_parse_dsl_missing_tree_section():
    from unity_mcp.tools.uitk_intent_tool import parse_uitk_dsl
    with pytest.raises(ToolError, match="Missing =TREE="):
        parse_uitk_dsl("just some text with no markers")


def test_parse_dsl_empty_tree():
    from unity_mcp.tools.uitk_intent_tool import parse_uitk_dsl
    dsl = "=TREE=\n=STYLE=\n.cls { color: red; }"
    data = parse_uitk_dsl(dsl)
    assert data["tree"] == []
    assert ".cls" in data["style"]


def test_parse_dsl_nested_elements():
    from unity_mcp.tools.uitk_intent_tool import parse_uitk_dsl
    dsl = "=TREE=\npanel root\n  label child\n    button grandchild"
    data = parse_uitk_dsl(dsl)
    assert data["tree"][0]["depth"] == 0
    assert data["tree"][1]["depth"] == 1
    assert data["tree"][2]["depth"] == 2
    assert data["tree"][1]["parent"] is data["tree"][0]


def test_parse_dsl_with_attributes():
    from unity_mcp.tools.uitk_intent_tool import parse_uitk_dsl
    dsl = '=TREE=\npanel root class="my-panel"\n  label title text="Hello"'
    data = parse_uitk_dsl(dsl)
    assert "my-panel" in data["tree"][0]["line"]
    assert "Hello" in data["tree"][1]["line"]


def test_parse_dsl_style_only():
    from unity_mcp.tools.uitk_intent_tool import parse_uitk_dsl
    with pytest.raises(ToolError, match="Missing =TREE="):
        parse_uitk_dsl("=STYLE=\n.foo { color: red; }")


# ---------------------------------------------------------------------------
# 2. USS Validator (8 tests)
# ---------------------------------------------------------------------------

def test_validate_clean_uss_passes():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    data = {"style": ".panel {\n    flex-grow: 1;\n    padding: 16px;\n    opacity: 0.9;\n}"}
    assert validate_uitk_dsl(data) is None


def test_validate_grid_rejected():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    data = {"style": ".grid { display: grid; grid-template-columns: 1fr 1fr; }"}
    result = validate_uitk_dsl(data)
    assert result is not None
    assert "display: grid" in result


def test_validate_calc_rejected():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    data = {"style": ".box { width: calc(100% - 20px); }"}
    result = validate_uitk_dsl(data)
    assert result is not None
    assert "calc(" in result


def test_validate_box_shadow_rejected():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    data = {"style": ".card { box-shadow: 0 2px 4px rgba(0,0,0,0.3); }"}
    result = validate_uitk_dsl(data)
    assert result is not None
    assert "box-shadow" in result


def test_validate_media_query_rejected():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    data = {"style": "@media (max-width: 600px) { .panel { display: none; } }"}
    result = validate_uitk_dsl(data)
    assert result is not None
    assert "@media" in result


def test_validate_keyframes_rejected():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    data = {"style": "@keyframes spin { from { rotate: 0; } to { rotate: 360deg; } }"}
    result = validate_uitk_dsl(data)
    assert result is not None


def test_validate_nth_child_rejected():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    data = {"style": ".list > *:nth-child(2n) { background: #eee; }"}
    result = validate_uitk_dsl(data)
    assert result is not None
    assert ":nth-child" in result


def test_validate_multiple_violations():
    from unity_mcp.tools.uitk_intent_tool import validate_uitk_dsl
    # Two banned tokens — validator returns first hit, not None
    data = {"style": ".box { display: grid; box-shadow: 0 0 4px black; }"}
    result = validate_uitk_dsl(data)
    assert result is not None


# ---------------------------------------------------------------------------
# 3. UXML Builder (6 tests)
# ---------------------------------------------------------------------------

def _make_nodes(dsl_tree: str):
    from unity_mcp.tools.uitk_intent_tool import parse_uitk_dsl
    return parse_uitk_dsl(f"=TREE=\n{dsl_tree}")["tree"]


def test_tree_to_uxml_single_element():
    from unity_mcp.tools.uitk_intent_tool import build_uitk_uxml
    nodes = _make_nodes("panel root")
    uxml = build_uitk_uxml("Test", nodes, "Test.uss")
    assert "ui:VisualElement" in uxml
    assert 'name="root"' in uxml


def test_tree_to_uxml_nested():
    from unity_mcp.tools.uitk_intent_tool import build_uitk_uxml
    nodes = _make_nodes("scroll scroller\n  label item")
    uxml = build_uitk_uxml("Test", nodes, "Test.uss")
    assert "ui:ScrollView" in uxml
    assert "ui:Label" in uxml
    # Nested means Label is inside ScrollView — verify label comes after scrollview open
    sv_idx = uxml.index("ui:ScrollView")
    lbl_idx = uxml.index("ui:Label")
    assert lbl_idx > sv_idx


def test_tree_to_uxml_with_classes():
    from unity_mcp.tools.uitk_intent_tool import build_uitk_uxml
    nodes = _make_nodes('panel root class="my-panel"')
    uxml = build_uitk_uxml("Test", nodes, "Test.uss")
    assert 'class="my-panel"' in uxml


def test_tree_to_uxml_with_text():
    from unity_mcp.tools.uitk_intent_tool import build_uitk_uxml
    nodes = _make_nodes('label title text="Hello World"')
    uxml = build_uitk_uxml("Test", nodes, "Test.uss")
    assert 'text="Hello World"' in uxml


def test_tree_to_uxml_button_with_name():
    from unity_mcp.tools.uitk_intent_tool import build_uitk_uxml
    nodes = _make_nodes('button play-btn text="Play"')
    uxml = build_uitk_uxml("Test", nodes, "Test.uss")
    assert "ui:Button" in uxml
    assert 'name="play-btn"' in uxml


def test_tree_to_uxml_includes_namespace():
    from unity_mcp.tools.uitk_intent_tool import build_uitk_uxml
    nodes = _make_nodes("panel root")
    uxml = build_uitk_uxml("Test", nodes, "Test.uss")
    assert 'xmlns:ui="UnityEngine.UIElements"' in uxml


# ---------------------------------------------------------------------------
# 4. Templates (5 tests)
# ---------------------------------------------------------------------------

def test_template_hud():
    from unity_mcp.tools.uitk_intent_tool import get_template_dsl
    dsl = get_template_dsl("hud")
    assert dsl is not None
    assert "=TREE=" in dsl
    assert "hud" in dsl.lower()


def test_template_menu():
    from unity_mcp.tools.uitk_intent_tool import get_template_dsl
    dsl = get_template_dsl("menu")
    assert dsl is not None
    assert "button" in dsl.lower()


async def test_template_dialog():
    """template='dialog' must bypass Haiku (no generate call)."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok: Assets/UI/Test.uxml"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            result = await uitk_intent(intent="dialog", name="Test", template="dialog")
            mock_svc.generate.assert_not_called()
            assert "Test.uxml" in result


def test_template_editor_window():
    from unity_mcp.tools.uitk_intent_tool import get_template_dsl
    dsl = get_template_dsl("editor_window")
    assert dsl is not None
    assert "=TREE=" in dsl


async def test_template_default_fallback():
    """Unknown template raises ToolError, sampling never called."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock):
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            with pytest.raises(ToolError, match="Unknown template"):
                await uitk_intent(intent="x", name="X", template="nonexistent")
            mock_svc.generate.assert_not_called()


# ---------------------------------------------------------------------------
# 5. E2E with mocked sampling + send (8 tests)
# ---------------------------------------------------------------------------

_VALID_DSL = """\
=TREE=
panel root class="panel"
  label title text="Hello"

=STYLE=
.panel {
    flex-grow: 1;
    padding: 16px;
}
"""

_INVALID_DSL = """\
=TREE=
panel root

=STYLE=
.root { display: grid; }
"""


async def test_e2e_valid_prompt_creates_files():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=_VALID_DSL)
            result = await uitk_intent(intent="simple panel", name="MyPanel", path="Assets/UI")
            assert "MyPanel.uxml" in result
            assert "MyPanel.uss" in result


async def test_e2e_calls_create_uss_before_create_uxml():
    """R04: USS must be sent before UXML."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=_VALID_DSL)
            await uitk_intent(intent="test", name="Panel", path="Assets/UI")
            calls = mock_send.call_args_list
            # Find uitk_file calls
            uitk_calls = [c for c in calls if c[0][0] == "uitk_file"]
            assert len(uitk_calls) >= 2
            assert uitk_calls[0][0][1]["action"] == "create_uss"
            assert uitk_calls[1][0][1]["action"] == "create_uxml"


async def test_e2e_validation_failure_retries_once():
    """First response invalid → retry → second response valid → 2 generate calls total."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(side_effect=[_INVALID_DSL, _VALID_DSL])
            await uitk_intent(intent="test", name="Panel", path="Assets/UI")
            assert mock_svc.generate.call_count == 2


async def test_e2e_double_failure_raises_error():
    """Both attempts invalid → ToolError('DSL validation failed after retry')."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock):
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(side_effect=[_INVALID_DSL, _INVALID_DSL])
            with pytest.raises(ToolError, match="DSL validation failed after retry"):
                await uitk_intent(intent="test", name="Panel", path="Assets/UI")
            assert mock_svc.generate.call_count == 2


async def test_e2e_output_dir_forwarded():
    """path argument forwarded to both uitk_file calls."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=_VALID_DSL)
            await uitk_intent(intent="test", name="HUD", path="Assets/UI/Game")
            calls = mock_send.call_args_list
            paths = [c[0][1]["path"] for c in calls if c[0][0] == "uitk_file"]
            assert all("Assets/UI/Game" in p for p in paths)


async def test_e2e_sampling_called_with_system_prompt():
    """sampling.generate is called with the full prompt string."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=_VALID_DSL)
            await uitk_intent(intent="my intent", name="Panel", path="Assets/UI")
            assert mock_svc.generate.called
            prompt = mock_svc.generate.call_args[0][0]
            assert "my intent" in prompt


async def test_e2e_system_prompt_contains_uss_restrictions():
    """System prompt lists USS banned tokens so Haiku knows constraints."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=_VALID_DSL)
            await uitk_intent(intent="test", name="P", path="Assets/UI")
            prompt = mock_svc.generate.call_args[0][0]
            assert "display: grid" in prompt


async def test_e2e_system_prompt_contains_bem():
    """System prompt mentions BEM naming convention."""
    from unity_mcp.tools.uitk_intent_tool import uitk_intent
    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        with patch("unity_mcp.tools.uitk_intent_tool._sampling") as mock_svc:
            mock_svc.generate = AsyncMock(return_value=_VALID_DSL)
            await uitk_intent(intent="test", name="P", path="Assets/UI")
            prompt = mock_svc.generate.call_args[0][0]
            assert "BEM" in prompt or "block__element" in prompt


async def test_attach_uses_public_attach_uitk_keys_and_counts_operation():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent

    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "ok"
        result = await uitk_intent(
            intent="dialog",
            name="Panel",
            template="dialog",
            attach_to="/UIRoot",
        )

    attach_call = [c for c in mock_send.call_args_list if c.args[0] == "attach_uitk"]
    assert len(attach_call) == 1
    assert attach_call[0].args[1] == {
        "path": "/UIRoot",
        "uxml": "Assets/UI/Panel.uxml",
    }
    assert "uitk_intent: 3 ops completed" in result
    assert "Attached Assets/UI/Panel.uxml to /UIRoot" in result


async def test_first_file_error_reports_attempted_file_as_possible_residual():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent

    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.return_value = "err: USS import failed"
        with pytest.raises(ToolError, match="failed at create_uss: 0/3 ops completed") as exc:
            await uitk_intent(
                intent="dialog",
                name="Panel",
                template="dialog",
                attach_to="/UIRoot",
            )

    assert mock_send.call_count == 1
    message = str(exc.value)
    assert "may have been created or modified" in message
    assert "cleanup unconfirmed" in message
    assert "Assets/UI/Panel.uss" in message
    assert "No files were processed" not in message


async def test_new_uss_auto_reverted_error_reports_confirmed_cleanup():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent

    with patch(
        "unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock
    ) as mock_send:
        mock_send.return_value = "err: USS import failed — auto-reverted."
        with pytest.raises(ToolError) as exc:
            await uitk_intent(
                intent="dialog",
                name="Panel",
                template="dialog",
            )

    message = str(exc.value)
    assert "Attempted file was auto-reverted by Unity:" in message
    assert "Assets/UI/Panel.uss" in message
    assert "cleanup unconfirmed" not in message
    assert "not rolled back" not in message


async def test_first_file_transport_exception_reports_attempted_residual():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent

    with patch(
        "unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock
    ) as mock_send:
        mock_send.side_effect = ConnectionError("Unity disconnected during import")
        with pytest.raises(ToolError) as exc:
            await uitk_intent(
                intent="dialog",
                name="Panel",
                template="dialog",
            )

    message = str(exc.value)
    assert "failed at create_uss: 0/2 ops completed" in message
    assert "ConnectionError" in message
    assert "Assets/UI/Panel.uss" in message
    assert "may have been created or modified" in message


async def test_second_file_error_reports_completed_and_attempted_files():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent

    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.side_effect = ["ok: create_uss", "error: invalid XML"]
        with pytest.raises(ToolError, match="failed at create_uxml: 1/3 ops completed") as exc:
            await uitk_intent(
                intent="dialog",
                name="Panel",
                template="dialog",
                attach_to="/UIRoot",
            )

    assert [call.args[0] for call in mock_send.call_args_list] == ["uitk_file", "uitk_file"]
    message = str(exc.value)
    assert "Assets/UI/Panel.uss" in message
    assert "Assets/UI/Panel.uxml" in message
    assert "may have been created or modified" in message
    assert "cleanup unconfirmed" in message
    assert "not rolled back" in message


async def test_new_uxml_auto_reverted_preserves_only_prior_uss_as_residual():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent

    with patch(
        "unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock
    ) as mock_send:
        mock_send.side_effect = [
            "ok: create_uss",
            "err: UXML import failed (asset null) — auto-reverted. "
            "Check get_console for [UXML Import Error].",
        ]
        with pytest.raises(ToolError) as exc:
            await uitk_intent(
                intent="dialog",
                name="Panel",
                template="dialog",
            )

    message = str(exc.value)
    retained, reverted = message.split("Attempted file was auto-reverted by Unity:")
    assert "Files already created or modified (not rolled back):" in retained
    assert "Assets/UI/Panel.uss" in retained
    assert "Assets/UI/Panel.uxml" not in retained
    assert "Assets/UI/Panel.uxml" in reverted
    assert "cleanup unconfirmed" not in message


async def test_attach_error_reports_both_processed_files():
    from unity_mcp.tools.uitk_intent_tool import uitk_intent

    with patch("unity_mcp.tools.uitk_intent_tool._send", new_callable=AsyncMock) as mock_send:
        mock_send.side_effect = ["ok", "ok", "err: target not found"]
        with pytest.raises(ToolError, match="failed at attach_uitk: 2/3 ops completed") as exc:
            await uitk_intent(
                intent="dialog",
                name="Panel",
                template="dialog",
                attach_to="/Missing",
            )

    message = str(exc.value)
    assert "Assets/UI/Panel.uss" in message
    assert "Assets/UI/Panel.uxml" in message
