"""Surface parity for typed-only, batch, and Unity transport contracts."""
import asyncio
import re
from pathlib import Path
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


def test_batchable_tcp_tools_not_direct_only():
    batchable_tools = {"get_hierarchy", "set_property", "create_object", "batch",
                       "get_component", "get_console"}
    for name in batchable_tools:
        assert name not in _DIRECT_ONLY, f"'{name}' should NOT be direct_only"


def test_direct_only_unity_wrappers_match_csharp_non_batchable_surface():
    """Async/special/file-effect C# commands must stay direct-only in Python."""
    registration = (
        Path(__file__).parents[2]
        / "unity-plugin" / "Editor" / "CommandRouter.Registration.cs"
    ).read_text(encoding="utf-8")
    async_commands = set(re.findall(r'RegisterAsync\("([^"]+)"', registration))
    assert "specialDispatch: true" in registration
    assert 'Register("screenshot"' in registration

    expected = async_commands | {"screenshot", "uitk_file"}
    actual = {
        name for name, spec in _SPECS.items()
        if spec.direct_only and spec.unity_transport
    }
    assert actual == expected


def test_direct_only_unity_wrappers_still_delegate_to_transport():
    for name in ("run_tests", "ask_user", "screenshot", "uitk_file"):
        spec = _SPECS[name]
        assert spec.direct_only
        assert spec.unity_transport


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


def test_console_epoch_tools_are_tier1():
    """console_mark and get_console_since must be always-visible (self-healing)."""
    assert _SPECS["console_mark"].tier1
    assert _SPECS["get_console_since"].tier1


def test_media_required_param_tools_have_full_schema():
    """timeline/animation/animator must not get stub schema after MEDIA discovery."""
    from unity_mcp.server_filtering import _SCHEMA_KEEP_FULL
    assert "timeline" in _SCHEMA_KEEP_FULL
    assert "animation" in _SCHEMA_KEEP_FULL
    assert "animator" in _SCHEMA_KEEP_FULL


def test_checkpoint_in_schema_keep_full():
    """checkpoint must serve full schema when SYSTEM is enabled."""
    from unity_mcp.server_filtering import _SCHEMA_KEEP_FULL
    assert "checkpoint" in _SCHEMA_KEEP_FULL
