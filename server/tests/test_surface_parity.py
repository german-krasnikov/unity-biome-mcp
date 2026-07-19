"""Surface parity: direct_only tools excluded from TCP catalog."""
import asyncio
import pytest
from unity_mcp.tools.gating import get_catalog, _DIRECT_ONLY
from unity_mcp.tools.tool_specs import _SPECS


def test_direct_only_excluded_from_catalog():
    catalog = get_catalog()
    for cat, tools in catalog["categories"].items():
        for tool in tools:
            spec = _SPECS.get(tool)
            assert not (spec and spec.direct_only), \
                f"direct_only tool '{tool}' leaked into catalog['{cat}']"


def test_discover_tools_is_direct_only():
    assert _SPECS["discover_tools"].direct_only


def test_console_mark_is_direct_only():
    assert _SPECS["console_mark"].direct_only


def test_get_console_since_is_direct_only():
    assert _SPECS["get_console_since"].direct_only


def test_mcp_status_is_direct_only():
    assert _SPECS["mcp_status"].direct_only


def test_schema_keep_full_includes_run_playtest():
    from unity_mcp.server_filtering import _SCHEMA_KEEP_FULL
    for name in ("run_playtest", "run_tests", "run_tests_wait", "resolve_tool_schema"):
        assert name in _SCHEMA_KEEP_FULL, f"'{name}' not in _SCHEMA_KEEP_FULL"


def test_specific_tools_are_direct_only():
    expected = {"discover_tools", "console_mark", "get_console_since", "mcp_status",
                "release_smoke", "resolve_tool_schema", "run_tests_wait", "ask",
                "await_compile", "budget_status", "debug", "doctor"}
    for name in expected:
        assert name in _DIRECT_ONLY, f"'{name}' should be in _DIRECT_ONLY"


def test_tcp_tools_not_direct_only():
    tcp_tools = {"get_hierarchy", "set_property", "create_object", "batch",
                 "get_component", "screenshot", "run_tests", "get_console"}
    for name in tcp_tools:
        assert name not in _DIRECT_ONLY, f"'{name}' should NOT be direct_only"


def test_deprecated_tools_raise_with_hint():
    from mcp.server.fastmcp.exceptions import ToolError
    from unity_mcp.tools.testing import get_perf, run_playtest_file

    with pytest.raises(ToolError, match="get_frame_stats"):
        asyncio.run(get_perf())
    with pytest.raises(ToolError, match="run_playtest"):
        asyncio.run(run_playtest_file("test.playtest"))


def test_deprecated_in_all_known_but_not_visible():
    """MCP091-011: deprecated stubs are in _ALL_KNOWN (so filter_by_tier hides them)
    but NOT in TIER1 or session_enabled (so is_visible returns False → hidden in ListTools)."""
    from unity_mcp.tools.gating import _ALL_KNOWN, is_visible
    for name in ("get_perf", "run_playtest_file"):
        assert name in _ALL_KNOWN, f"{name} must be in _ALL_KNOWN for visibility gating"
        assert not is_visible(name), f"{name} must not be visible in ListTools"


# ---------------------------------------------------------------------------
# MCP091-014: configure_objects / setup_objects are Python-only macros
# ---------------------------------------------------------------------------

def test_configure_objects_is_direct_only():
    assert _SPECS["configure_objects"].direct_only, \
        "configure_objects must be direct_only=True (Python macro, not a C# command)"


def test_setup_objects_is_direct_only():
    assert _SPECS["setup_objects"].direct_only, \
        "setup_objects must be direct_only=True (Python macro, not a C# command)"


def test_configure_objects_not_in_tcp_catalog():
    catalog = get_catalog()
    for cat, tools in catalog["categories"].items():
        assert "configure_objects" not in tools, \
            f"configure_objects (direct_only) leaked into TCP catalog['{cat}']"


def test_setup_objects_not_in_tcp_catalog():
    catalog = get_catalog()
    for cat, tools in catalog["categories"].items():
        assert "setup_objects" not in tools, \
            f"setup_objects (direct_only) leaked into TCP catalog['{cat}']"


# ---------------------------------------------------------------------------
# MCP091-011: deprecated stubs registered with FastMCP
# ---------------------------------------------------------------------------

def test_deprecated_stubs_registered_with_fastmcp():
    from unity_mcp.server import mcp
    for name in ("get_perf", "run_playtest_file"):
        assert name in mcp._tool_manager._tools, \
            f"deprecated stub '{name}' not registered with FastMCP (MCP091-011)"
