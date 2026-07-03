"""Tests for screenshot offset + fixed_size params (TDD)."""
import pytest


@pytest.fixture
def screenshot_tool():
    """Import screenshot with a mock send/args."""
    import unity_mcp.tools.screenshot as scene_mod

    # minimal _args impl (same as real one)
    def _args(**kwargs):
        return {k: v for k, v in kwargs.items() if v is not None}

    captured = {}

    async def _send(cmd, args, **_kw):
        captured['cmd'] = cmd
        captured['args'] = args
        return "Data saved to: /tmp/test.png"

    orig_send, orig_args = scene_mod._send, scene_mod._args
    scene_mod._send = _send
    scene_mod._args = _args
    yield scene_mod.screenshot, captured
    scene_mod._send = orig_send
    scene_mod._args = orig_args


async def test_screenshot_offset_passthrough(screenshot_tool):
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/Cube", offset="1,2,3")
    assert captured['args'].get('offset') == "1,2,3"


async def test_screenshot_fixed_size_passthrough(screenshot_tool):
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/Cube", fixed_size=5.0)
    assert captured['args'].get('fixed_size') == 5.0


async def test_screenshot_all_params_together(screenshot_tool):
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/Cube", zoom=1.5, offset="0,1,0", fixed_size=3.0)
    assert captured['args'].get('zoom') == 1.5
    assert captured['args'].get('offset') == "0,1,0"
    assert captured['args'].get('fixed_size') == 3.0


async def test_screenshot_omitted_params_not_in_args(screenshot_tool):
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/Cube")
    assert 'offset' not in captured['args']
    assert 'fixed_size' not in captured['args']


async def test_screenshot_angles_passthrough(screenshot_tool):
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/X", angles="45,0,0|_|_|90,0,0")
    assert captured['args'].get('angles') == "45,0,0|_|_|90,0,0"


async def test_screenshot_supersample_passthrough(screenshot_tool):
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/X", supersample=4)
    assert captured['args'].get('supersample') == 4
