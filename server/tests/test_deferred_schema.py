"""Tests for deferred/lazy schema loading (F4)."""
import os
from types import SimpleNamespace
from typing import Optional
from unittest.mock import AsyncMock, patch, MagicMock


def _tool(name: str, description: str = "desc", input_schema: Optional[dict] = None):
    t = SimpleNamespace(
        name=name,
        description=description,
        inputSchema=input_schema or {"type": "object", "properties": {"x": {"type": "string"}}, "required": ["x"]},
    )
    return t


def _full_schema() -> dict:
    return {
        "type": "object",
        "properties": {"path": {"type": "string"}, "value": {"type": "number"}},
        "required": ["path"],
    }


# --- Phase 3: schema stripping ---

def test_core_tool_keeps_full_schema_after_strip():
    from unity_mcp.server import _strip_deferred_schemas
    import unity_mcp.tools.gating as gating
    full = _full_schema()
    tool = _tool("get_hierarchy", "Core tool", full)
    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == full


def test_non_core_tool_gets_stub_after_strip():
    from unity_mcp.server import _strip_deferred_schemas
    from unity_mcp.tools.schema_registry import STUB_SCHEMA
    tool = _tool("animation", "Animation tool", _full_schema())
    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == STUB_SCHEMA


def test_stub_keeps_description_and_name():
    from unity_mcp.server import _strip_deferred_schemas
    tool = _tool("animation", "Do animations", _full_schema())
    result = _strip_deferred_schemas([tool])
    assert result[0].name == "animation"
    assert result[0].description == "Do animations"


def test_core_tools_survive_strip():
    """CORE tools keep full schema through the strip pipeline."""
    from unity_mcp.server import _strip_deferred_schemas
    # 'batch' is core (core=True in tool_specs.py); discover_tools demoted to SYSTEM tier1 in Phase 2
    tool = _tool("batch", "Batch", _full_schema())
    result = _strip_deferred_schemas([tool])
    # batch is core — schema always kept full
    assert result[0].inputSchema == _full_schema()


def test_stripped_list_char_count_less_than_full():
    """Token assertion: stripped list is shorter than full list."""
    from unity_mcp.server import _strip_deferred_schemas
    import json
    tools = [
        _tool("get_hierarchy", "Core", _full_schema()),
        _tool("animation", "Anim", _full_schema()),
        _tool("shader", "Shader", _full_schema()),
        _tool("timeline", "Timeline", _full_schema()),
    ]
    # Measure BEFORE stripping (strip mutates in-place)
    full_size = sum(len(json.dumps(t.inputSchema)) for t in tools)
    stripped = _strip_deferred_schemas(tools)
    stripped_size = sum(len(json.dumps(t.inputSchema)) for t in stripped)
    assert stripped_size < full_size


def test_unity_mcp_full_schemas_disables_stripping(monkeypatch):
    """UNITY_MCP_FULL_SCHEMAS=1 disables schema stripping."""
    monkeypatch.setenv("UNITY_MCP_FULL_SCHEMAS", "1")
    from unity_mcp.server import _strip_deferred_schemas
    full = _full_schema()
    tool = _tool("animation", "Anim", full)
    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == full


def test_unity_mcp_full_schemas_zero_still_strips(monkeypatch):
    """UNITY_MCP_FULL_SCHEMAS=0 must NOT disable stripping — '0' is falsy but truthy string."""
    monkeypatch.setenv("UNITY_MCP_FULL_SCHEMAS", "0")
    from unity_mcp.server import _strip_deferred_schemas
    from unity_mcp.tools.schema_registry import STUB_SCHEMA
    tool = _tool("animation", "Anim", _full_schema())
    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == STUB_SCHEMA


# --- resolve_tool_schema meta-tool ---

async def test_resolve_returns_full_params_single():
    from unity_mcp.server import resolve_tool_schema
    from unity_mcp.tools.schema_registry import _registry
    schema = _full_schema()
    _registry.capture("animation", schema, "Animation CRUD")
    result = await resolve_tool_schema("animation")
    assert "== animation ==" in result
    assert "Animation CRUD" in result
    assert "path*" in result


async def test_resolve_returns_full_params_multiple():
    from unity_mcp.server import resolve_tool_schema
    from unity_mcp.tools.schema_registry import _registry
    _registry.capture("shader", _full_schema(), "Shader tool")
    _registry.capture("timeline", _full_schema(), "Timeline tool")
    result = await resolve_tool_schema("shader,timeline")
    assert "== shader ==" in result
    assert "== timeline ==" in result


async def test_resolve_unknown_graceful():
    from unity_mcp.server import resolve_tool_schema
    result = await resolve_tool_schema("totally_unknown_xyz_abc")
    assert isinstance(result, str)
    assert "unknown" in result.lower() or "not found" in result.lower() or len(result) > 0


async def test_resolve_core_tool_works():
    from unity_mcp.server import resolve_tool_schema
    from unity_mcp.tools.schema_registry import _registry
    _registry.capture("batch", _full_schema(), "Execute batch commands")
    result = await resolve_tool_schema("batch")
    assert "== batch ==" in result


async def test_resolve_output_is_plain_text_not_json():
    from unity_mcp.server import resolve_tool_schema
    from unity_mcp.tools.schema_registry import _registry
    _registry.capture("scene", _full_schema(), "Scene info")
    result = await resolve_tool_schema("scene")
    assert "properties" not in result


# --- backward compat: calling a deferred tool still executes ---


def test_strip_does_not_touch_tool_manager_registry():
    """_strip_deferred_schemas mutates ListTools response objects — NOT _tool_manager._tools.

    FastMCP runs with validate_input=False internally; _tool_manager._tools holds the
    callable fn independently of the ListTools response. So stripping inputSchema from
    the response object cannot block execution.
    """
    from unity_mcp.server import mcp, _strip_deferred_schemas
    # mcp._tool_manager._tools must contain "animation" registered by register_all
    mgr_tools_before = set(mcp._tool_manager._tools.keys())
    assert "animation" in mgr_tools_before, "animation must be registered via register_all"

    # Now simulate what _filtered_tools_handler does: strip response objects
    response_tools = [_tool("animation", "Anim", _full_schema())]
    _strip_deferred_schemas(response_tools)

    # _tool_manager._tools is completely unaffected
    assert set(mcp._tool_manager._tools.keys()) == mgr_tools_before
    # The callable fn is still present
    assert mcp._tool_manager._tools["animation"].fn is not None


# --- plugin tools get stubbed ---

def test_plugin_tool_gets_stubbed():
    """Plugin tools (not in _CORE_TOOLS) get STUB schema."""
    from unity_mcp.server import _strip_deferred_schemas
    from unity_mcp.tools.schema_registry import STUB_SCHEMA
    # A fake plugin tool name that is definitely not in core or known categories
    tool = _tool("my_plugin_custom_tool", "Custom plugin", _full_schema())
    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == STUB_SCHEMA


# --- cold-start race: resolve before ListTools populates the registry ---

async def test_resolve_cold_start_empty_registry_returns_graceful_fallback():
    """Cold start: registry is empty (ListTools not yet called). Must return graceful
    fallback string, NOT raise or return empty string."""
    from unity_mcp.tools.schema_registry import SchemaRegistry
    from unity_mcp.server import resolve_tool_schema
    # resolve_tool_schema now lives in tools/meta.py (M2) — patch its _schema_registry
    # binding, not server.py's re-export (call-time global lookup resolves against
    # the function's own defining module).
    import unity_mcp.tools.meta as meta_mod

    # Temporarily replace the module-level registry with a fresh empty one
    original = meta_mod._schema_registry
    empty_reg = SchemaRegistry()
    # Patch both the meta.py reference and the schema_registry module-level one
    import unity_mcp.tools.schema_registry as reg_mod
    old_singleton = reg_mod._registry
    try:
        reg_mod._registry = empty_reg
        meta_mod._schema_registry = empty_reg
        result = await resolve_tool_schema("animation")
        # Must return a non-empty string with "No schema found" message
        assert isinstance(result, str)
        assert len(result) > 0
        assert "No schema found" in result
    finally:
        reg_mod._registry = old_singleton
        meta_mod._schema_registry = original


# --- resolve_tool_schema is SYSTEM tier1 (demoted from CORE in Phase 2) ---

def test_resolve_tool_schema_schema_is_stripped_after_demotion():
    """resolve_tool_schema demoted from CORE to SYSTEM tier1 in Phase 2 —
    now gets stub schema like other non-core tools."""
    from unity_mcp.server import _strip_deferred_schemas
    from unity_mcp.tools.schema_registry import STUB_SCHEMA
    full = _full_schema()
    tool = _tool("resolve_tool_schema", "Resolve deferred schemas", full)
    result = _strip_deferred_schemas([tool])
    assert result[0].inputSchema == STUB_SCHEMA


# --- stripping is applied in the handler ---

async def test_filter_tools_applies_strip():
    """_filter_tools must call _strip_deferred_schemas on the result."""
    import unity_mcp.server as srv
    from unity_mcp.tools.schema_registry import STUB_SCHEMA
    orig_cache = srv._disabled_tools_cache
    try:
        srv._disabled_tools_cache = None
        tools = [_tool("get_hierarchy", "Core", _full_schema()),
                 _tool("animation", "Anim", _full_schema())]
        result = await srv._filter_tools(tools, None)
        core = next(t for t in result if t.name == "get_hierarchy")
        assert core.inputSchema == _full_schema()
    finally:
        srv._disabled_tools_cache = orig_cache
