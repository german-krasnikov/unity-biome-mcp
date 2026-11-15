"""uitk_element tool unit tests — command forwarding and schema verification."""
import typing
from unittest.mock import AsyncMock


async def test_uitk_element_sends_action(mock_bridge):
    """action="query" → args["action"] == "query".
    Breaks if action is not forwarded or the command name changes.
    """
    from unity_mcp.server import uitk_element

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0 matches"})
    await uitk_element(action="query", path="/HUD", selector=".item")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "query"
    # Double-red:
    # 1. Change to args["action"] == "wrong" → fails
    # 2. Delete uitk_element → ImportError → RED


async def test_uitk_element_ref_forwarded(mock_bridge):
    """ref="~3" → bridge args["ref"] == "~3".
    Breaks if ref param is dropped or renamed.
    """
    from unity_mcp.server import uitk_element

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await uitk_element(action="get", ref="~3")
    args = mock_bridge.send.call_args[0][1]
    assert args["ref"] == "~3"
    # Double-red:
    # 1. Change to args["ref"] == "~99" → fails
    # 2. Remove ref from _args() → KeyError → RED


async def test_uitk_element_class_name_forwarded(mock_bridge):
    """class_name="highlighted" → args["class_name"] == "highlighted".
    Breaks if class_name param is renamed or dropped.
    """
    from unity_mcp.server import uitk_element

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await uitk_element(action="add_class", path="/HUD", class_name="highlighted")
    args = mock_bridge.send.call_args[0][1]
    assert args["class_name"] == "highlighted"
    # Double-red:
    # 1. Change to args["class_name"] == "other" → fails
    # 2. Remove class_name from _args() → KeyError → RED


def test_uitk_element_action_enum_in_schema():
    """action parameter uses Literal — FastMCP generates an enum in JSON schema.
    Breaks if Literal annotation is replaced with plain str.
    """
    from unity_mcp.server import uitk_element

    hints = typing.get_type_hints(uitk_element)
    action_type = hints.get("action")
    args = typing.get_args(action_type)
    assert "query" in args, "Literal must include 'query'"
    assert "get" in args, "Literal must include 'get'"
    assert "add_class" in args, "Literal must include 'add_class'"
    # Double-red:
    # 1. Change to assert "nonexistent" in args → fails
    # 2. Replace Literal with str → get_args returns () → RED
