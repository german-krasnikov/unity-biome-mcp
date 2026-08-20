"""Tests for MCP-PLANE-036: plane metadata distinguishing control-plane from Unity TCP tools."""
import pytest
from unity_mcp.tools.tool_specs import _SPECS

# Tools that execute entirely in Python — never touch Unity TCP
KNOWN_PYTHON_TOOLS = {
    'mcp_status',
    'discover_tools',
    'reconnect_unity',
    'resolve_tool_schema',
    'permission_prompt',
    'sync_unity',
    'list_connections',
    'doctor',
    'list_skills',
    'list_templates',
    'budget_status',
    'load_session',
    'save_session',
    'run_tests_wait',
    'run_playtest_suite',
    'verify_after_change',
    'scene_change_plan',
    'ui_intent',
    'vfx_intent',
    'uitk_intent',
    'release_smoke',
}

# Tools that always go to Unity via TCP
KNOWN_UNITY_TOOLS = {
    'create_object',
    'get_hierarchy',
    'batch',
    'editor',
    'get_compile_errors',
    'set_property',
    'run_tests',
    'screenshot',
    'ask_user',
    'get_console',
    'inspect',
}


def test_all_tools_have_plane_field():
    """Every entry in _SPECS exposes a .plane attribute with a valid value."""
    valid = {'unity', 'python'}
    for name, spec in _SPECS.items():
        plane = spec.plane
        assert plane in valid, f"{name}.plane must be 'unity' or 'python', got {plane!r}"


def test_python_only_tools_have_python_plane():
    """Known Python-only control-plane tools are marked plane='python'."""
    for name in KNOWN_PYTHON_TOOLS:
        assert name in _SPECS, f"{name} not in _SPECS"
        spec = _SPECS[name]
        assert spec.plane == 'python', (
            f"{name} is Python-only (direct_only={spec.direct_only}, "
            f"unity_transport={spec.unity_transport}) but plane={spec.plane!r}"
        )


def test_unity_tools_have_unity_plane():
    """Tools that dispatch to Unity TCP are marked plane='unity'."""
    for name in KNOWN_UNITY_TOOLS:
        assert name in _SPECS, f"{name} not in _SPECS"
        spec = _SPECS[name]
        assert spec.plane == 'unity', f"{name} should be plane='unity', got {spec.plane!r}"


async def test_plane_metadata_in_discover_response():
    """discover_tools(structured=True) includes plane= tags in its output."""
    from unity_mcp.tools.gating import discover_tools
    result = await discover_tools(category='SYSTEM', enable=False, structured=True)
    assert 'plane=python' in result, (
        "discover_tools structured output must include 'plane=python' for Python-only tools; "
        f"got:\n{result}"
    )


def test_direct_only_non_transport_tools_are_python_plane():
    """All direct_only=True, unity_transport=False tools are automatically python plane."""
    for name, spec in _SPECS.items():
        if spec.category == '_INTERNAL':
            continue
        if spec.direct_only and not spec.unity_transport:
            assert spec.plane == 'python', (
                f"{name}: direct_only=True, unity_transport=False must imply plane='python'"
            )


def test_unity_transport_tools_are_unity_plane():
    """Tools with unity_transport=True dispatch to Unity, so plane must be 'unity'."""
    for name, spec in _SPECS.items():
        if spec.unity_transport:
            assert spec.plane == 'unity', (
                f"{name}: unity_transport=True must imply plane='unity'"
            )
