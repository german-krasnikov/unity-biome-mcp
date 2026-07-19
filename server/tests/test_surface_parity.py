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


def test_deprecated_not_in_all_known():
    from unity_mcp.tools.gating import _ALL_KNOWN
    assert "get_perf" not in _ALL_KNOWN
    assert "run_playtest_file" not in _ALL_KNOWN
