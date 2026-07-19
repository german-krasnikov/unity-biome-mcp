"""Registration parity: every spec has a tool, every tool has a spec."""
from unity_mcp.tools.tool_specs import _SPECS

_SPEC_MCP_NAMES = frozenset(n for n, s in _SPECS.items()
                            if s.category not in ("_INTERNAL", "DEPRECATED"))


def _get_registered() -> frozenset[str]:
    from unity_mcp.server import mcp
    return frozenset(t.name for t in mcp._tool_manager.list_tools())


def _get_first_party_registered() -> frozenset[str]:
    """Registered tools from unity_mcp.* modules (excludes external plugins)."""
    from unity_mcp.server import mcp
    tools = mcp._tool_manager._tools
    return frozenset(
        name for name, tool in tools.items()
        if hasattr(tool, 'fn') and tool.fn.__module__.startswith("unity_mcp.")
    )


def test_every_spec_has_registered_tool():
    """Every non-_INTERNAL _SPECS entry must be a registered MCP tool."""
    registered = _get_registered()
    missing = _SPEC_MCP_NAMES - registered
    assert not missing, f"In _SPECS but not registered: {sorted(missing)}"


def test_every_registered_tool_has_spec():
    """Every first-party registered MCP tool must have a _SPECS entry (no orphan registrations).

    MCP091-011: deprecated stubs (get_perf, run_playtest_file) are intentionally registered
    so MCP returns ToolError+migration-hint instead of 'tool not found'. They have _SPECS
    entries (DEPRECATED category), so they're not orphans.
    """
    registered = _get_first_party_registered()
    # Include DEPRECATED in allowed set — deprecated stubs are intentionally registered (MCP091-011)
    _ALL_SPEC_NAMES = frozenset(n for n, s in _SPECS.items() if s.category != "_INTERNAL")
    orphans = registered - _ALL_SPEC_NAMES
    assert not orphans, f"Registered but no _SPECS entry: {sorted(orphans)}"


def test_deprecated_tools_registered_with_migration_hint():
    """MCP091-011: deprecated stubs are registered (return ToolError hint, not 'tool not found')."""
    registered = _get_registered()
    for name in ("get_perf", "run_playtest_file"):
        assert name in registered, f"Deprecated stub not registered: {name}"
