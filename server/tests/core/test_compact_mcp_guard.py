"""Guard tests: the production CompactMCP instance must never emit output_schema for any tool."""
from unity_mcp.server import _force_additional_properties_false, mcp


def test_tool_count_not_zero():
    assert len(mcp._tool_manager._tools) >= 90


def test_no_tool_has_output_schema():
    schemas = {
        name: tool.output_schema
        for name, tool in mcp._tool_manager._tools.items()
        if tool.output_schema is not None
    }
    assert schemas == {}, f"Tools with output_schema: {list(schemas)}"


async def test_list_tools_response_has_no_output_schema():
    tools = await mcp.list_tools()
    assert tools, "list_tools returned empty"
    bad = [t.name for t in tools if t.output_schema is not None]
    assert bad == [], f"Tools with outputSchema in list_tools: {bad}"


def test_force_additional_properties_false_covers_empty_properties():
    """biome-mcp's forbid_extra_properties skips an empty {} (falsy check) -- our local
    hook must still set additionalProperties=False, or reject_unknown_args silently
    stops guarding every zero-param tool (P-321 regression, found during the mcp v2
    migration: recompile/get_selection/alias_status/etc. lost the guard)."""
    schema = {"type": "object", "properties": {}}
    _force_additional_properties_false("zero_param_tool", schema)
    assert schema["additionalProperties"] is False


def test_force_additional_properties_false_noop_without_properties_key():
    """A schema with no 'properties' key at all (e.g. a resource, not a tool-args
    schema) must be left untouched."""
    schema = {"type": "object"}
    _force_additional_properties_false("no_properties_key", schema)
    assert "additionalProperties" not in schema


def test_zero_param_production_tool_still_rejects_unknown_arguments():
    """End-to-end: a real zero-param tool (recompile) must still carry
    additionalProperties=False on the live production mcp instance."""
    tool = mcp._tool_manager._tools["recompile"]
    assert tool.parameters.get("additionalProperties") is False

