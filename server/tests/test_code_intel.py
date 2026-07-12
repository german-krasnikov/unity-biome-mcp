"""Unit tests for code_intel tools — mock_bridge with canned C# response format."""
from unittest.mock import AsyncMock, MagicMock


# --- compile_preflight ---

async def test_compile_preflight_clean(mock_bridge):
    from unity_mcp.tools.code_intel import compile_preflight

    canned = "OK preflight Assets/Scripts/Player.cs (3 asms recompiled, 142ms)"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await compile_preflight("Assets/Scripts/Player.cs", "public class Player {}")

    args = mock_bridge.send.call_args[0][1]
    assert mock_bridge.send.call_args[0][0] == "compile_preflight"
    assert args["file_path"] == "Assets/Scripts/Player.cs"
    assert args["new_content"] == "public class Player {}"
    assert "OK preflight" in result


async def test_compile_preflight_with_errors(mock_bridge):
    from unity_mcp.tools.code_intel import compile_preflight

    canned = (
        "ERR preflight Assets/Scripts/Player.cs (2 errors, 89ms)\n"
        "Player.cs(42,13): CS0103 The name 'helath' does not exist in the current context\n"
        "Player.cs(58,5): CS1002 ; expected"
    )
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": canned})

    result = await compile_preflight("Assets/Scripts/Player.cs", "broken code")

    assert "ERR preflight" in result
    assert "CS0103" in result
    assert "helath" in result


async def test_compile_preflight_uses_15s_timeout(mock_bridge):
    from unity_mcp.tools.code_intel import compile_preflight

    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "OK preflight ..."})

    await compile_preflight("Assets/x.cs", "content")

    assert mock_bridge.send.call_args[1]["timeout"] == 15.0


# --- removed dead wrappers (S2) ---

def test_removed_tools_not_registered():
    """find_references/semantic_at had no C# handler — every call raised
    ToolError("Command not registered: ..."). Pure token waste; removed (S2).
    Guards against re-adding them as module functions or MCP tool registrations."""
    import unity_mcp.tools.code_intel as mod

    assert not hasattr(mod, "find_references")
    assert not hasattr(mod, "semantic_at")

    registered = []
    mcp = MagicMock()
    mcp.tool = MagicMock(return_value=lambda fn: registered.append(fn.__name__) or fn)
    mod.register(mcp, AsyncMock(), MagicMock())

    assert registered == ["compile_preflight", "await_compile"]


# --- gating ---

def test_live_tools_registered_in_tier1():
    """gating.py must expose the surviving tools in TIER1; dead wrappers must be gone."""
    from unity_mcp.tools.gating import TIER1
    assert "compile_preflight" in TIER1
    assert "await_compile" in TIER1
    assert "find_references" not in TIER1
    assert "semantic_at" not in TIER1


