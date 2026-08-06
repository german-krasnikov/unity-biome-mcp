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


# ── P-317: dimension-mismatch error passthrough ──────────────────────────────

async def test_screenshot_dimension_mismatch_error_surfaced_as_is():
    """C# dimension mismatch error must not be masked by describe logic."""
    import unity_mcp.tools.screenshot as mod

    def _args(**kwargs):
        return {k: v for k, v in kwargs.items() if v is not None}

    error_msg = "Error: Screenshot dimensions mismatch: got 3456x40, expected 640x480"

    async def _fail_send(cmd, args, **_kw):
        return error_msg

    orig_send, orig_args = mod._send, mod._args
    mod._send = _fail_send
    mod._args = _args
    try:
        result = await mod.screenshot(width=640, height=480, describe="scene")
        # When C# returns an error (no "Data saved to:"), it must be returned unchanged
        assert result == error_msg
    finally:
        mod._send = orig_send
        mod._args = orig_args


async def test_screenshot_dimension_mismatch_not_described():
    """describe= must NOT trigger Haiku call when C# returns error (no file path)."""
    import unity_mcp.tools.screenshot as mod

    calls = []

    def _args(**kwargs):
        return {k: v for k, v in kwargs.items() if v is not None}

    async def _fail_send(cmd, args, **_kw):
        calls.append(cmd)
        return "Error: dimensions mismatch: got 3456x40, expected 640x480"

    orig_send, orig_args = mod._send, mod._args
    mod._send = _fail_send
    mod._args = _args
    try:
        await mod.screenshot(width=640, height=480, describe="all")
        # Only the screenshot call should happen, no fingerprint/describe round-trips
        assert calls == ["screenshot"]
    finally:
        mod._send = orig_send
        mod._args = orig_args
