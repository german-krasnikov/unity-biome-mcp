"""TDD: highlight param for multi_view anti-hallucination (Phase Cycle 10a)."""
import pytest


@pytest.fixture
def screenshot_tool():
    import unity_mcp.tools.screenshot as scene_mod

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


@pytest.fixture
def screenshot_tool_with_manifest():
    """Screenshot fixture returning manifest+file combined response."""
    import unity_mcp.tools.screenshot as scene_mod

    def _args(**kwargs):
        return {k: v for k, v in kwargs.items() if v is not None}

    captured = {}

    async def _send(cmd, args, **_kw):
        captured['cmd'] = cmd
        captured['args'] = args
        return "FRONT:Player(vis)\nLEFT:Player(vis)\nData saved to: /tmp/test.png"

    orig_send, orig_args = scene_mod._send, scene_mod._args
    scene_mod._send = _send
    scene_mod._args = _args
    yield scene_mod.screenshot, captured
    scene_mod._send = orig_send
    scene_mod._args = orig_args


async def test_screenshot_highlight_param_passed(screenshot_tool):
    """highlight flows through to bridge.send args."""
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/Player", highlight="/Player,/Enemy")
    assert captured['args'].get('highlight') == "/Player,/Enemy"


async def test_screenshot_highlight_none_not_in_args(screenshot_tool):
    """highlight=None is omitted from args (backward compat)."""
    fn, captured = screenshot_tool
    await fn(camera="multi_view", path="/Player")
    assert 'highlight' not in captured['args']


# ---------------------------------------------------------------------------
# _send_raw: data + file coexist in response
# ---------------------------------------------------------------------------

async def test_png_path_extraction_with_manifest(screenshot_tool_with_manifest):
    """png_path must be just the path, not manifest prefix (MAJOR review fix)."""
    fn, captured = screenshot_tool_with_manifest
    result = await fn(camera="multi_view", path="/Player", highlight="/Player", raw=True)
    assert "Data saved to:" in result
    png_path = result.split("Data saved to: ")[-1].strip()
    assert png_path == "/tmp/test.png"
    assert "\n" not in png_path


async def test_send_raw_data_and_file_combined():
    """When response has both data and file, return 'data\\nData saved to: file'."""
    import unity_mcp.server as srv
    from unittest.mock import AsyncMock

    fake_bridge = AsyncMock()
    fake_bridge.send = AsyncMock(return_value={
        "ok": True,
        "data": "FRONT:Player(vis)\nLEFT:Player(vis)",
        "file": "/tmp/MCP/multiview.png",
    })

    orig_slot = srv.slot
    fake_slot = type('S', (), {'bridge': fake_bridge})()
    srv.slot = fake_slot
    try:
        result = await srv._send_raw("screenshot", {})
        assert "FRONT:Player(vis)" in result
        assert "Data saved to: /tmp/MCP/multiview.png" in result
    finally:
        srv.slot = orig_slot


async def test_send_raw_file_only_unchanged():
    """When response has only file (no data), keep existing behavior."""
    import unity_mcp.server as srv
    from unittest.mock import AsyncMock

    fake_bridge = AsyncMock()
    fake_bridge.send = AsyncMock(return_value={
        "ok": True,
        "data": "",
        "file": "/tmp/MCP/multiview.png",
    })

    orig_slot = srv.slot
    fake_slot = type('S', (), {'bridge': fake_bridge})()
    srv.slot = fake_slot
    try:
        result = await srv._send_raw("screenshot", {})
        assert result == "Data saved to: /tmp/MCP/multiview.png"
    finally:
        srv.slot = orig_slot


# ---------------------------------------------------------------------------
# annotation_id param
# ---------------------------------------------------------------------------

async def test_screenshot_annotation_id_sets_camera_annotation_frame(screenshot_tool):
    """annotation_id forces camera=annotation_frame."""
    fn, captured = screenshot_tool
    await fn(annotation_id="region_01")
    assert captured['args'].get('camera') == "annotation_frame"


async def test_screenshot_annotation_id_passed_to_args(screenshot_tool):
    """annotation_id is forwarded in bridge args."""
    fn, captured = screenshot_tool
    await fn(annotation_id="region_01")
    assert captured['args'].get('annotation_id') == "region_01"


async def test_screenshot_annotation_id_none_not_in_args(screenshot_tool):
    """annotation_id=None omitted from args (backward compat)."""
    fn, captured = screenshot_tool
    await fn(camera="scene_view")
    assert 'annotation_id' not in captured['args']


async def test_screenshot_annotation_id_overrides_camera(screenshot_tool):
    """annotation_id overrides any explicit camera value."""
    fn, captured = screenshot_tool
    await fn(camera="multi_view", annotation_id="region_42")
    assert captured['args'].get('camera') == "annotation_frame"
