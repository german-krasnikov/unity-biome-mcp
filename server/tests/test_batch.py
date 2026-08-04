import pytest
from unittest.mock import AsyncMock
from mcp.server.fastmcp.exceptions import ToolError

from unity_mcp.server import batch
from unity_mcp.tools.batch import validate_references


async def test_batch_text_forwarded(mock_bridge, bridge_response):
    """Text commands sent directly to bridge without JSON parsing."""
    bridge_response(data="[0] ok: /A\n[1] ok")
    commands = "create_object name=A primitive=Cube\nset_material path=/A color=#FF0000"
    result = await batch(commands=commands)
    # A4: default timeout=75.0 -> timeout_ms=70000, which no longer matches
    # C#'s hardcoded 25000ms default, so it's now sent explicitly.
    mock_bridge.send.assert_called_once_with(
        "batch",
        {"commands": commands, "timeout_ms": 70000},
        timeout=75.0,
    )
    assert result == "[0] ok: /A\n[1] ok"


async def test_batch_on_error_stop(mock_bridge, bridge_response):
    """on_error=stop forwarded to bridge."""
    bridge_response(data="[0] ok: /A\n[1] err: Not found\n[2] skip")
    result = await batch(commands="create_object name=A", on_error="stop")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["on_error"] == "stop"


async def test_batch_non_default_timeout_sent(mock_bridge, bridge_response):
    """Non-default timeout is included in args."""
    bridge_response(data="[0] ok: /A")
    await batch(commands="create_object name=A", timeout=60.0)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["timeout_ms"] == 55000  # (60-5)*1000


async def test_batch_default_timeout_omitted(mock_bridge, bridge_response):
    """A4: timeout_ms is only omitted when it matches C#'s own hardcoded
    internal batch-executor default (25000ms) -- NOT Python's local default.
    Post-A4 the two deliberately diverge (75s client default -> 70000ms), so
    timeout_ms is present at the default now; see
    test_batch_timeout.py::test_batch_default_timeout_75s for the authoritative
    coverage of that value. This test locks in the *mechanism*: a timeout that
    genuinely resolves to 25000ms (Unity's own default) still omits the key."""
    bridge_response(data="[0] ok: /A")
    await batch(commands="create_object name=A", timeout=30.0)  # (30-5)*1000 == 25000
    call_args = mock_bridge.send.call_args[0]
    assert "timeout_ms" not in call_args[1]


async def test_batch_on_error_continue(mock_bridge, bridge_response):
    """Default on_error=continue is omitted from args (token economy)."""
    bridge_response(data="[0] ok: /A")
    result = await batch(commands="create_object name=A")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert "on_error" not in call_args[1]


async def test_batch_empty_commands(mock_bridge):
    """Empty string handled."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": ""})
    result = await batch(commands="")
    mock_bridge.send.assert_called_once()
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["commands"] == ""


async def test_batch_error_raises_tool_error(mock_bridge):
    """Bridge error raises ToolError."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Connection lost"})
    with pytest.raises(ToolError, match="Connection lost"):
        await batch(commands="create_object name=A")


async def test_batch_vector_with_spaces_forwarded(mock_bridge):
    """Vector3 with spaces in parens forwarded as-is to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok:2"})
    commands = "create_object name=A primitive=Cube\nset_property path=/A component=Transform prop=m_LocalPosition value=(0, 6.8, 0)"
    result = await batch(commands=commands)
    sent_commands = mock_bridge.send.call_args[0][1]["commands"]
    assert "(0, 6.8, 0)" in sent_commands


async def test_batch_single_command(mock_bridge):
    """Single line command works."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "[0] ok: /A"})
    result = await batch(commands="create_object name=A primitive=Cube")
    # A4: default timeout=75.0 -> timeout_ms=70000, no longer matches C#'s
    # hardcoded 25000ms default, so it's now sent explicitly.
    mock_bridge.send.assert_called_once_with(
        "batch",
        {"commands": "create_object name=A primitive=Cube", "timeout_ms": 70000},
        timeout=75.0,
    )
    assert result == "[0] ok: /A"


async def test_batch_rejects_dsl_tools_python_side(mock_bridge):
    """batch() rejects DSL tools registered via register_dsl_tools."""
    from unity_mcp.tools.batch import _dsl_tools
    _dsl_tools.add("test_dsl_cmd")
    try:
        with pytest.raises(ToolError, match="requires typed MCP tool"):
            await batch(commands="test_dsl_cmd path=/NPC")
    finally:
        _dsl_tools.discard("test_dsl_cmd")


# F27: atomic mode tests

async def test_batch_atomic_true_forwarded(mock_bridge, bridge_response):
    """atomic=True is forwarded as 'true' string in command dict."""
    bridge_response(data="ok:1")
    await batch(commands="create_object name=A", atomic=True)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1].get("atomic") == "true"


async def test_batch_atomic_false_not_sent(mock_bridge, bridge_response):
    """atomic=False (default) means atomic key is absent from command dict."""
    bridge_response(data="ok:1")
    await batch(commands="create_object name=A")
    call_args = mock_bridge.send.call_args[0]
    assert "atomic" not in call_args[1]


async def test_batch_atomic_with_on_error(mock_bridge, bridge_response):
    """atomic=True is forwarded; on_error omitted when default (not sent to C#)."""
    bridge_response(data="ok:1")
    await batch(commands="create_object name=A", atomic=True, on_error="continue")
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1].get("atomic") == "true"
    assert "on_error" not in call_args[1]  # default value omitted


async def test_batch_empty_commands_list(mock_bridge, bridge_response):
    """batch with only whitespace/newlines sends empty commands string."""
    bridge_response(data="ok:0")
    result = await batch(commands="\n\n  \n")
    call_args = mock_bridge.send.call_args[0]
    # commands forwarded as-is (whitespace only), no dsl rejection triggered
    assert call_args[0] == "batch"


async def test_batch_minimum_timeout_clamped_to_1000ms(mock_bridge, bridge_response):
    """timeout<=6.0 clamps timeout_ms to 1000 (minimum floor)."""
    bridge_response(data="[0] ok")
    await batch(commands="x", timeout=5.0)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1]["timeout_ms"] == 1000  # max(1000, (5-5)*1000) = max(1000, 0) = 1000


async def test_validate_references_ignore_optional_sent(mock_bridge):
    """validate_references with ignore_optional=True sends ignore_optional flag."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0 ERROR, 3 OK"})
    await validate_references(path="/Root", ignore_optional=True)
    mock_bridge.send.assert_called_once_with(
        "validate_references",
        {"path": "/Root", "depth": 3, "ignore_optional": "true"},
        timeout=30.0,
    )


async def test_validate_references_ignore_optional_false_omitted(mock_bridge):
    """validate_references with ignore_optional=False (default) omits the key."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "0 ERROR, 3 OK"})
    await validate_references(path="/Root")
    call_args = mock_bridge.send.call_args[0][1]
    assert "ignore_optional" not in call_args


async def test_batch_fields_forwarded_to_csharp(mock_bridge, bridge_response):
    """fields= in batch DSL is forwarded as-is to C# (not consumed by Python)."""
    bridge_response(data="[0] ok: m_Mass: 2")
    await batch(commands="inspect paths=/Player fields=mass")
    call_args = mock_bridge.send.call_args[0]
    assert "fields=mass" in call_args[1]["commands"]


async def test_batch_compress_forwarded_to_csharp(mock_bridge, bridge_response):
    """compress= in batch DSL is forwarded as-is to C# (not consumed by Python)."""
    bridge_response(data="[0] ok: m_Mass: 2")
    await batch(commands="get_component path=/Player type=Rigidbody compress=true")
    call_args = mock_bridge.send.call_args[0]
    assert "compress=true" in call_args[1]["commands"]


async def test_batch_no_python_postprocessing_for_fields(mock_bridge, bridge_response):
    """batch() does NOT call project_fields or strip_defaults — C# handles it."""
    bridge_response(data="[0] ok: m_Mass: 2\nm_Drag: 0")
    result = await batch(commands="inspect paths=/Player fields=mass")
    # Result returned as-is from bridge — Python didn't filter
    assert "m_Drag: 0" in result


async def test_batch_validate_aliases_forwarded(mock_bridge, bridge_response):
    """validate_aliases=True is forwarded as 'true' string in command dict."""
    bridge_response(data="ok: all aliases resolved")
    await batch(commands="ping\nset_property path=$hero value=1", validate_aliases=True)
    call_args = mock_bridge.send.call_args[0]
    assert call_args[1].get("validate_aliases") == "true"


async def test_batch_validate_aliases_false_not_sent(mock_bridge, bridge_response):
    """validate_aliases=False (default) means key is absent from command dict."""
    bridge_response(data="ok:1")
    await batch(commands="ping")
    call_args = mock_bridge.send.call_args[0]
    assert "validate_aliases" not in call_args[1]


# P0-2: direct_only rejection

async def test_batch_rejects_direct_only_tool(mock_bridge):
    """direct_only tools raise ToolError before any TCP call."""
    with pytest.raises(ToolError, match="direct-only"):
        await batch(commands="run_playtest_suite paths=Tests/MyTest.playtest")
    mock_bridge.send.assert_not_called()


async def test_batch_rejects_all_direct_only_tools(mock_bridge):
    """Every direct_only=True tool in _SPECS raises ToolError in batch."""
    from unity_mcp.tools.tool_specs import _SPECS
    direct_only = [name for name, spec in _SPECS.items() if spec.direct_only]
    assert direct_only, "Expected at least one direct_only tool"
    for cmd in direct_only:
        with pytest.raises(ToolError, match="direct-only"):
            await batch(commands=f"{cmd} path=/foo")
    mock_bridge.send.assert_not_called()


async def test_batch_remaps_line_numbers_after_direct_only_filter(mock_bridge, bridge_response):
    """C# result line numbers are remapped to original positions when direct_only lines are filtered."""
    bridge_response(data="[1] ok: /A\n[2] ok: root")
    # Line 1: valid, Line 2: direct_only (filtered out), Line 3: valid
    result = await batch(commands="create_object name=A\nrun_playtest_suite paths=x\nget_hierarchy")
    assert "[2] err:" in result          # pre_error at original line 2
    assert "[1] ok: /A" in result        # C# [1] → original line 1 (unchanged)
    assert "[3] ok: root" in result      # C# [2] → original line 3 (remapped)
    assert "[2] ok:" not in result       # C# [2] was remapped away


async def test_batch_strips_python_only_full_param(mock_bridge, bridge_response):
    """full=true on get_component is stripped before forwarding to C# (Python-only param)."""
    bridge_response(data="[0] ok: Transform")
    await batch(commands="get_component path=/A type=Transform full=true")
    sent = mock_bridge.send.call_args[0][1]["commands"]
    assert "full=" not in sent
    assert "path=/A" in sent
    assert "type=Transform" in sent


async def test_batch_strips_python_only_full_on_error_stop(mock_bridge, bridge_response):
    """full=true stripped in on_error=stop path too."""
    bridge_response(data="[0] ok: Transform")
    await batch(commands="get_component path=/A type=Transform full=true", on_error="stop")
    sent = mock_bridge.send.call_args[0][1]["commands"]
    assert "full=" not in sent


async def test_batch_preserves_non_python_params(mock_bridge, bridge_response):
    """compress=true is NOT stripped (it's a valid C# param)."""
    bridge_response(data="[0] ok: Transform")
    await batch(commands="get_component path=/A type=Transform compress=true")
    sent = mock_bridge.send.call_args[0][1]["commands"]
    assert "compress=true" in sent
