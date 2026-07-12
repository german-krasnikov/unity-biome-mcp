"""TDD tests for permission_prompt_tool — --permission-prompt-tool MCP handler."""
import json
import pytest
from unittest.mock import AsyncMock, MagicMock


@pytest.fixture(autouse=True)
def _clean_send(monkeypatch):
    import unity_mcp.tools.permission_prompt_tool as mod
    monkeypatch.setattr(mod, "_send", None)


async def test_ask_user_routes_to_send(monkeypatch):
    import unity_mcp.tools.permission_prompt_tool as mod
    questions = [{"question": "Color?", "options": [{"label": "Red"}]}]
    send = AsyncMock(return_value=json.dumps({"Color?": "Red"}))
    monkeypatch.setattr(mod, "_send", send)
    await mod.permission_prompt("AskUserQuestion", {"questions": questions}, "tu-1")
    send.assert_awaited_once_with(
        "ask_user", {"questions": json.dumps(questions)}, timeout=300.0,
    )


async def test_ask_user_returns_allow_with_answers(monkeypatch):
    import unity_mcp.tools.permission_prompt_tool as mod
    questions = [{"question": "Go?"}]
    answers = {"Go?": "Yes"}
    monkeypatch.setattr(mod, "_send", AsyncMock(return_value=json.dumps(answers)))
    result = await mod.permission_prompt("AskUserQuestion", {"questions": questions}, "tu-2")
    data = json.loads(result)
    assert data["behavior"] == "allow"
    assert data["updatedInput"]["answers"] == answers
    assert data["updatedInput"]["questions"] == questions


async def test_non_ask_user_returns_allow(monkeypatch):
    import unity_mcp.tools.permission_prompt_tool as mod
    send = AsyncMock()
    monkeypatch.setattr(mod, "_send", send)
    result = await mod.permission_prompt("Bash", {"command": "ls"}, "tu-3")
    data = json.loads(result)
    assert data["behavior"] == "allow"
    assert data["updatedInput"] == {"command": "ls"}  # new schema: allow requires updatedInput
    send.assert_not_awaited()



async def test_send_raises_returns_deny_sanitized(monkeypatch):
    import unity_mcp.tools.permission_prompt_tool as mod
    monkeypatch.setattr(mod, "_send", AsyncMock(side_effect=Exception("Tool 'ask_user' is disabled")))
    result = await mod.permission_prompt(
        "AskUserQuestion", {"questions": []}, "tu-err",
    )
    data = json.loads(result)
    assert data["behavior"] == "deny"
    assert data["message"] == "ask_user unavailable"


async def test_send_raises_connection_error_returns_not_connected(monkeypatch):
    import unity_mcp.tools.permission_prompt_tool as mod
    monkeypatch.setattr(mod, "_send", AsyncMock(side_effect=ConnectionError("connection refused")))
    result = await mod.permission_prompt(
        "AskUserQuestion", {"questions": []}, "tu-conn",
    )
    data = json.loads(result)
    assert data["behavior"] == "deny"
    assert data["message"] == "Unity not connected"


def test_register_wires_send(monkeypatch):
    import unity_mcp.tools.permission_prompt_tool as mod
    mcp = MagicMock()
    mcp.tool = MagicMock(return_value=lambda fn: fn)
    send = AsyncMock()
    mod.register(mcp, send, MagicMock())
    assert mod._send is send
    mcp.tool.assert_called_once()
