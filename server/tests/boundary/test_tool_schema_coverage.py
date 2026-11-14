"""Schema coverage tests: tier1 membership, critical param presence,
and FastMCP JSON schema contract (guards against FastMCP silently dropping params)."""
import inspect
from types import SimpleNamespace
import pytest
from unity_mcp.tools.gating import TIER1
from unity_mcp.tools.objects import get_component, inspect as inspect_tool
from unity_mcp.tools.batch import batch


# ---------------------------------------------------------------------------
# Existing signature-level guards (fast, no server import)
# ---------------------------------------------------------------------------

def test_alias_status_not_in_tier1():
    """P-12440 Phase 1: alias_status demoted from TIER1 to themed SYSTEM."""
    assert "alias_status" not in TIER1, "alias_status must NOT be tier1 after P-12440 Phase 1"


def test_compress_param_in_get_component():
    assert "compress" in inspect.signature(get_component).parameters


def test_compress_param_in_inspect():
    assert "compress" in inspect.signature(inspect_tool).parameters


def test_validate_aliases_param_in_batch():
    assert "validate_aliases" in inspect.signature(batch).parameters


# ---------------------------------------------------------------------------
# FastMCP contract tests — verify the JSON Schema FastMCP actually generates.
# These catch regressions where FastMCP drops/misses a param in the schema it
# emits to MCP clients, even if the Python function signature is still correct.
# ---------------------------------------------------------------------------

def _props(tool_name: str) -> dict:
    """Return FastMCP-generated JSON Schema properties for a registered tool."""
    from unity_mcp.server import mcp
    t = mcp._tool_manager._tools[tool_name]
    return t.parameters.get("properties", {})


def test_fastmcp_get_component_schema_has_compress():
    assert "compress" in _props("get_component"), \
        "FastMCP dropped 'compress' from get_component JSON schema"


def test_fastmcp_inspect_schema_has_compress():
    assert "compress" in _props("inspect"), \
        "FastMCP dropped 'compress' from inspect JSON schema"


def test_fastmcp_batch_schema_has_validate_aliases():
    assert "validate_aliases" in _props("batch"), \
        "FastMCP dropped 'validate_aliases' from batch JSON schema"


def test_fastmcp_run_playtest_schema_has_path_defs_script():
    props = _props("run_playtest")
    for param in ("path", "defs", "script"):
        assert param in props, f"FastMCP dropped '{param}' from run_playtest JSON schema"


def test_fastmcp_editor_schema_has_enable():
    assert "enable" in _props("editor"), \
        "FastMCP dropped 'enable' from editor JSON schema (P0-70 mutation_mode)"


def test_fastmcp_alias_status_schema_exists():
    from unity_mcp.server import mcp
    assert "alias_status" in mcp._tool_manager._tools, \
        "alias_status not registered with FastMCP"


def test_every_core_tool_has_nonempty_properties():
    """Core tools with params must have non-empty properties in FastMCP schema.

    Known genuinely param-less tools are excluded — they return plain results
    with no inputs and their empty properties dict is intentional.
    """
    from unity_mcp.server import mcp
    from unity_mcp.tools.gating import _CORE_TOOLS

    # Confirmed param-less core tools (empty properties is expected)
    NO_PARAM_TOOLS = {"get_compile_errors", "get_enabled_tools", "list_connections",
                      "mcp_status"}  # P-12440: mcp_status promoted to core; no params

    failures = []
    for name in sorted(_CORE_TOOLS - NO_PARAM_TOOLS):
        t = mcp._tool_manager._tools.get(name)
        if t is None:
            failures.append(f"{name}:MISSING")
        elif not t.parameters.get("properties"):
            failures.append(f"{name}:EMPTY_PROPS")

    assert failures == [], f"Core tools with missing/empty FastMCP properties: {failures}"


def test_resolve_tool_schema_returns_full_schema():
    """Simulate the resolve_tool_schema pipeline for a tier1 non-core tool.

    resolve_tool_schema reads from SchemaRegistry, which is populated from
    mcp._tool_manager._tools[name].parameters during list_tools.
    This test verifies the source data is present and produces real output.
    """
    from unity_mcp.server import mcp
    from unity_mcp.tools.schema_registry import SchemaRegistry
    from unity_mcp.tools.gating import TIER1, _CORE_TOOLS

    # screenshot: tier1, not core, has multiple params
    tool_name = "screenshot"
    assert tool_name in TIER1 and tool_name not in _CORE_TOOLS

    t = mcp._tool_manager._tools[tool_name]
    params = t.parameters
    assert params.get("properties"), f"{tool_name} has no properties in FastMCP schema"

    # Simulate what install_list_tools_filter captures into the registry
    registry = SchemaRegistry()
    registry.capture(tool_name, params, t.description or "")
    text = registry.format_text([tool_name])

    assert text, f"resolve_tool_schema would return empty for {tool_name}"
    assert f"== {tool_name} ==" in text
    assert "Params:" in text


def test_fastmcp_run_tests_schema_has_params():
    props = _props("run_tests")
    for p in ("mode", "filter", "request_id"):
        assert p in props, f"'run_tests' missing param '{p}'"


# ---------------------------------------------------------------------------
# P-106: fingerprint path must be optional
# ---------------------------------------------------------------------------

def test_fingerprint_path_is_optional():
    """P-106: fingerprint path must NOT appear in JSON schema required[]."""
    from unity_mcp.server import mcp
    t = mcp._tool_manager._tools["fingerprint"]
    required = t.parameters.get("required", [])
    assert "path" not in required, (
        "P-106: 'path' marked required in fingerprint JSON schema — must be optional"
    )


def test_fingerprint_path_is_nullable_in_schema():
    """P-106: fingerprint path schema must accept null (str | None annotation)."""
    from unity_mcp.server import mcp
    t = mcp._tool_manager._tools["fingerprint"]
    path_schema = t.parameters.get("properties", {}).get("path", {})
    # FastMCP emits anyOf with null for str | None = None
    any_of = path_schema.get("anyOf", [])
    has_null = any(s.get("type") == "null" for s in any_of)
    assert has_null, f"P-106: fingerprint 'path' does not accept null. Got: {path_schema}"


def test_fastmcp_run_tests_wait_schema_has_params():
    props = _props("run_tests_wait")
    for p in ("mode", "filter", "timeout", "poll_interval", "request_id"):
        assert p in props, f"'run_tests_wait' missing param '{p}'"


@pytest.mark.parametrize("tool_name,param", [
    ("resolve_test_request", "request_id"),
    ("get_test_run", "run_id"),
    ("cancel_test_run", "run_id"),
    ("list_test_runs", "limit"),
])
def test_fastmcp_durable_test_tools_have_identity_params(tool_name, param):
    assert param in _props(tool_name), f"'{tool_name}' missing param '{param}'"


def test_fastmcp_discover_tools_schema_has_category():
    props = _props("discover_tools")
    assert "category" in props


def test_fastmcp_resolve_tool_schema_has_tools_param():
    props = _props("resolve_tool_schema")
    assert "tools" in props


def test_tier1_tools_visible_in_list_tools():
    """All TIER1 tools that are registered must survive filter_by_tier."""
    from unity_mcp.server import mcp
    from unity_mcp.tools.gating import TIER1, filter_by_tier

    registered = set(mcp._tool_manager._tools.keys())
    tier1_registered = TIER1 & registered

    tools = [SimpleNamespace(name=n) for n in registered]
    visible = {t.name for t in filter_by_tier(tools)}

    missing = tier1_registered - visible
    assert not missing, f"TIER1 tools invisible after filter_by_tier: {sorted(missing)}"


# ---------------------------------------------------------------------------
# MCP091-004 / MCP091-012: schema keep-full expansion
# ---------------------------------------------------------------------------

def test_schema_keep_full_includes_wave2_tools():
    """MCP091-004/012: TIER1 tools with required params must have full schemas in ListTools."""
    from unity_mcp.server_filtering import _SCHEMA_KEEP_FULL
    for name in ("get_console_since", "scene", "await_compile", "console_mark", "screenshot"):
        assert name in _SCHEMA_KEEP_FULL, \
            f"'{name}' must be in _SCHEMA_KEEP_FULL (MCP091-004/012)"


def test_schema_keep_full_matches_specs():
    """All TIER1/core tools from _SPECS must be in _SCHEMA_KEEP_FULL automatically.

    Prevents drift where a new tier1 tool is added to _SPECS but forgotten in the
    hand-maintained _SCHEMA_KEEP_FULL_EXTRA, causing it to receive stub schema.
    """
    from unity_mcp.server_filtering import _SCHEMA_KEEP_FULL
    from unity_mcp.tools.gating import TIER1
    missing = TIER1 - _SCHEMA_KEEP_FULL
    assert not missing, (
        f"TIER1 tools missing from _SCHEMA_KEEP_FULL: {sorted(missing)}\n"
        "Derive _SCHEMA_KEEP_FULL from TIER1 instead of hand-editing _SCHEMA_KEEP_FULL_EXTRA"
    )


def test_fastmcp_get_console_since_schema_has_mark_id():
    assert "mark_id" in _props("get_console_since"), \
        "FastMCP must expose 'mark_id' in get_console_since schema"


def test_fastmcp_scene_schema_has_action():
    assert "action" in _props("scene"), \
        "FastMCP must expose 'action' in scene schema"


# ---------------------------------------------------------------------------
# Schema postprocessor integration guards (MCP-LINT compliance)
# ---------------------------------------------------------------------------

def test_additional_properties_false_on_all_schemas():
    """After postprocessor runs, all tools with properties have additionalProperties: false."""
    from unity_mcp.server import mcp
    missing = [
        name for name, tool in mcp._tool_manager._tools.items()
        if tool.parameters.get("properties")
        and tool.parameters.get("additionalProperties") is not False
    ]
    assert not missing, f"Missing additionalProperties:false: {sorted(missing)}"


def test_all_registered_tools_have_title():
    """After postprocessor hook, every registered tool must have a non-empty title."""
    from unity_mcp.server import mcp
    missing = [
        name for name, tool in mcp._tool_manager._tools.items()
        if not getattr(tool, "title", None)
    ]
    assert not missing, f"Tools missing title: {sorted(missing)}"
