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


# --- P-NEW-1: 15 Python-only tools missing direct_only=True (Arch-Batch-Surface-Metadata) ---

_NEW_DIRECT_ONLY = {
    "apply_scene_change", "apply_template", "auto_fix", "load_session",
    "permission_prompt", "reconnect_unity", "save_session", "save_skill",
    "save_template", "scene_change_plan", "set_llm_config", "smart_build",
    "sync_unity", "use_skill", "verify_after_change",
}


def test_batch_surface_metadata_coverage():
    """All 15 Python-only tools from Arch-Batch-Surface-Metadata are direct_only."""
    for name in _NEW_DIRECT_ONLY:
        assert _SPECS[name].direct_only, \
            f"'{name}' is Python-only but missing direct_only=True in tool_specs.py"


def test_newly_marked_tools_in_direct_only_set():
    """_DIRECT_ONLY (derived frozenset) includes all 15 newly-marked tools."""
    for name in _NEW_DIRECT_ONLY:
        assert name in _DIRECT_ONLY, \
            f"'{name}' not in _DIRECT_ONLY frozenset — tool_specs.py not updated"

