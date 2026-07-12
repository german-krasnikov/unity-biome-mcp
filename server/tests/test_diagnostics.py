"""TDD tests for diagnostics: get_perf, debug_animator, debug_physics."""
import pytest
from unittest.mock import AsyncMock


@pytest.mark.asyncio
async def test_get_perf_sends_correct_command():
    import unity_mcp.tools.diagnostics as mod
    mod._send = AsyncMock(return_value="fps=60 dt=16.7ms\nmono=12MB/32MB")
    result = await mod.get_perf()
    mod._send.assert_called_once_with("get_frame_stats", {})
    assert isinstance(result, str)


@pytest.mark.asyncio
async def test_debug_animator_sends_path():
    import unity_mcp.tools.diagnostics as mod
    mod._send = AsyncMock(return_value="layer0: hash=123 time=0.50")
    result = await mod.debug_animator("/Player")
    mod._send.assert_called_once_with("debug_animator", {"path": "/Player"})
    assert isinstance(result, str)


@pytest.mark.asyncio
async def test_debug_physics_default_radius():
    import unity_mcp.tools.diagnostics as mod
    mod._send = AsyncMock(return_value="rb: vel=(0,0,0)")
    await mod.debug_physics("/Enemy")
    call_args = mod._send.call_args[0]
    assert call_args[0] == "debug_physics"
    assert call_args[1]["path"] == "/Enemy"
    assert call_args[1]["radius"] == 5.0


@pytest.mark.asyncio
async def test_debug_physics_custom_radius():
    import unity_mcp.tools.diagnostics as mod
    mod._send = AsyncMock(return_value="nearby(10.0m): 3 colliders")
    await mod.debug_physics("/Boss", radius=10.0)
    sent = mod._send.call_args[0][1]
    assert sent["radius"] == 10.0


@pytest.mark.asyncio
async def test_get_perf_returns_data():
    import unity_mcp.tools.diagnostics as mod
    expected = "fps=120 dt=8.3ms\nmono=8MB/24MB\ngc_gen0=5"
    mod._send = AsyncMock(return_value=expected)
    result = await mod.get_perf()
    assert result == expected


@pytest.mark.asyncio
async def test_debug_animator_path_forwarded():
    import unity_mcp.tools.diagnostics as mod
    mod._send = AsyncMock(return_value="layer0: hash=0 time=0.00")
    await mod.debug_animator("/NPC/Guard")
    sent = mod._send.call_args[0][1]
    assert sent["path"] == "/NPC/Guard"


@pytest.mark.asyncio
async def test_debug_physics_returns_string():
    import unity_mcp.tools.diagnostics as mod
    mod._send = AsyncMock(return_value="layer=Default collides=[Default,Water]")
    result = await mod.debug_physics("/Cube", radius=3.0)
    assert isinstance(result, str)
