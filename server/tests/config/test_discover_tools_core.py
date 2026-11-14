"""Tests that discover_tools() includes CORE tools in its output (E2 fix)."""
import pytest
from unity_mcp.tools.gating import discover_tools, _CORE_TOOLS, _DIRECT_ONLY


EXPECTED_CORE = [
    "batch", "create_object", "set_property", "get_hierarchy",
    "inspect", "get_component", "manage_component",
    "get_console", "get_compile_errors", "editor",
]


@pytest.mark.asyncio
async def test_discover_tools_includes_core_section():
    """discover_tools() with no args must include 'CORE:' section."""
    result = await discover_tools(enable=False)
    assert "CORE:" in result


@pytest.mark.asyncio
async def test_discover_tools_includes_core_tools():
    """discover_tools() output must list all expected CORE tools."""
    result = await discover_tools(enable=False)
    for tool in EXPECTED_CORE:
        assert tool in result, f"Expected CORE tool '{tool}' missing from discover_tools output"


@pytest.mark.asyncio
async def test_discover_tools_core_appears_before_themed():
    """CORE section appears before SCENE section in output."""
    result = await discover_tools(enable=False)
    assert result.index("CORE:") < result.index("SCENE:")


@pytest.mark.asyncio
async def test_discover_tools_structured_includes_core():
    """structured=True also includes CORE section."""
    result = await discover_tools(enable=False, structured=True)
    assert "CORE:" in result
    assert "batch" in result


@pytest.mark.asyncio
async def test_discover_tools_single_category_unchanged():
    """Single-category call is unaffected (no CORE injected)."""
    result = await discover_tools(category="SCENE", enable=False)
    assert result.startswith("Category 'SCENE':")
    assert "CORE" not in result


@pytest.mark.asyncio
async def test_discover_tools_include_legacy_no_core_duplication():
    """include_legacy=True must not emit CORE section twice."""
    result = await discover_tools(enable=False, include_legacy=True)
    assert result.count("CORE:") == 1, f"CORE appeared {result.count('CORE:')} times:\n{result}"


@pytest.mark.asyncio
async def test_discover_tools_include_legacy_structured_no_core_duplication():
    """include_legacy=True + structured=True must not emit CORE section twice."""
    result = await discover_tools(enable=False, include_legacy=True, structured=True)
    assert result.count("CORE:") == 1, f"CORE appeared {result.count('CORE:')} times:\n{result}"
