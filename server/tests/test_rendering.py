"""TDD tests for render_analyze MCP tool — Phases 3+4+6+7."""
import asyncio
import pytest
from unittest.mock import AsyncMock, MagicMock
from unity_mcp.tools.rendering import render_analyze
from unity_mcp.tools import gating

_ARGS = lambda **kw: {k: v for k, v in kw.items() if v is not None}


def _run(coro):
    return asyncio.run(coro)


# ── action forwarding ────────────────────────────────────────────────────────

async def test_render_analyze_sends_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "RENDER STATS\n  draw calls: 0"}
    await render_analyze(action="stats")
    sent = mock_bridge.send.call_args[0]
    assert sent[0] == "render_analyze"
    assert sent[1]["action"] == "stats"


async def test_render_analyze_default_detail_brief(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "RENDER STATS"}
    await render_analyze(action="stats")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["detail"] == "brief"


async def test_render_analyze_path_optional(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "OVERDRAW ESTIMATE"}
    await render_analyze(action="overdraw")
    sent = mock_bridge.send.call_args[0][1]
    assert "path" not in sent


async def test_render_analyze_path_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "OVERDRAW ESTIMATE"}
    await render_analyze(action="overdraw", path="/Env")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["path"] == "/Env"


async def test_render_analyze_compare_sends_baseline_id(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "COMPARE: draw_calls 0 (+0%)"}
    await render_analyze(action="compare", baseline_id="snap1")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["baseline_id"] == "snap1"


async def test_render_analyze_max_events_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "FRAME DEBUG:"}
    await render_analyze(action="frame_debug", max_events=50)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["max_events"] == 50


# ── gating ───────────────────────────────────────────────────────────────────

def test_render_analyze_in_rendering_category():
    # Phase 2: RENDERING merged into MEDIA
    assert "render_analyze" in gating._THEMED_CATEGORIES["MEDIA"]


def test_render_analyze_registered_read_only():
    """Tool is registered with readOnlyHint=True."""
    import unity_mcp.tools.rendering as m
    from mcp.types import ToolAnnotations
    registered = {}

    def mock_tool(annotations=None):
        def decorator(fn):
            registered[fn.__name__] = annotations
            return fn
        return decorator

    mock_mcp = MagicMock()
    mock_mcp.tool = mock_tool
    orig_send, orig_args = m._send, m._args
    try:
        m.register(mock_mcp, AsyncMock(), _ARGS)
        ann = registered.get("render_analyze")
        assert isinstance(ann, ToolAnnotations)
        assert ann.readOnlyHint is True
    finally:
        m._send, m._args = orig_send, orig_args


# ── response handling ────────────────────────────────────────────────────────

async def test_render_analyze_stats_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "RENDER STATS\n  draw calls: 0"}
    result = await render_analyze(action="stats")
    assert "RENDER STATS" in result


async def test_render_analyze_unknown_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "err:Unknown action 'blah'."}
    result = await render_analyze(action="blah")
    assert "blah" in result


async def test_render_analyze_error_raises_tool_error(mock_bridge):
    from mcp.server.fastmcp.exceptions import ToolError
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Not in editor"})
    with pytest.raises(ToolError, match="Not in editor"):
        await render_analyze(action="stats")


# ── batching action ──────────────────────────────────────────────────────────

async def test_batching_forwards_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "BATCHING:"}
    await render_analyze(action="batching")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "batching"


async def test_batching_detail_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "BATCHING:"}
    await render_analyze(action="batching", detail="full")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["detail"] == "full"


async def test_batching_path_scoped(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "BATCHING:"}
    await render_analyze(action="batching", path="/Level")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["path"] == "/Level"


# ── lights actions ───────────────────────────────────────────────────────────

async def test_lights_forwards_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "LIGHTS:"}
    await render_analyze(action="lights")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "lights"


async def test_shadow_audit_forwards_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "SHADOW AUDIT:"}
    await render_analyze(action="shadow_audit")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "shadow_audit"


async def test_probe_audit_forwards_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PROBE AUDIT:"}
    await render_analyze(action="probe_audit")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "probe_audit"


async def test_light_optimize_forwards_action(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "LIGHT OPTIMIZE:"}
    await render_analyze(action="light_optimize")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["action"] == "light_optimize"


async def test_lights_detail_defaults_brief(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "LIGHTS:"}
    await render_analyze(action="lights")
    sent = mock_bridge.send.call_args[0][1]
    assert sent["detail"] == "brief"


# ── frame_debug action (Phase 6) ─────────────────────────────────────────────

def test_frame_debug_forwards_action():
    import unity_mcp.tools.rendering as m
    orig_send, orig_args = m._send, m._args
    m._args = _ARGS
    m._send = AsyncMock(return_value="FRAME EVENTS total=0")
    try:
        _run(m.render_analyze(action="frame_debug"))
        cmd, sent = m._send.call_args[0]
        assert cmd == "render_analyze"
        assert sent.get("action") == "frame_debug"
    finally:
        m._send, m._args = orig_send, orig_args


# ── analyze_lod_culling (spatial.py, Phase 7) ────────────────────────────────

def test_analyze_lod_culling_forwards_focus():
    import unity_mcp.tools.spatial as m
    orig_send, orig_args = m._send, m._args
    m._args = _ARGS
    m._send = AsyncMock(return_value="LOD ANALYSIS")
    try:
        _run(m.analyze_lod_culling(focus="lod"))
        cmd, sent = m._send.call_args[0]
        assert cmd == "analyze_lod_culling"
        assert sent.get("focus") == "lod"
    finally:
        m._send, m._args = orig_send, orig_args


def test_analyze_lod_culling_null_focus_not_in_args():
    import unity_mcp.tools.spatial as m
    orig_send, orig_args = m._send, m._args
    m._args = _ARGS
    m._send = AsyncMock(return_value="ok")
    try:
        _run(m.analyze_lod_culling())
        _, sent = m._send.call_args[0]
        assert "focus" not in sent
    finally:
        m._send, m._args = orig_send, orig_args


def test_analyze_lod_culling_culling_focus():
    import unity_mcp.tools.spatial as m
    orig_send, orig_args = m._send, m._args
    m._args = _ARGS
    m._send = AsyncMock(return_value="ok")
    try:
        _run(m.analyze_lod_culling(focus="culling"))
        _, sent = m._send.call_args[0]
        assert sent.get("focus") == "culling"
    finally:
        m._send, m._args = orig_send, orig_args


def test_analyze_lod_culling_registered_readonly():
    """analyze_lod_culling must be registered with readOnlyHint=True."""
    import unity_mcp.tools.spatial as m
    from mcp.types import ToolAnnotations

    registered = {}

    def mock_tool(annotations=None):
        def decorator(fn):
            registered[fn.__name__] = annotations
            return fn
        return decorator

    mock_mcp = MagicMock()
    mock_mcp.tool = mock_tool
    orig_send, orig_args = m._send, m._args
    try:
        m.register(mock_mcp, AsyncMock(), _ARGS)
        assert "analyze_lod_culling" in registered, "analyze_lod_culling not registered"
        ann = registered["analyze_lod_culling"]
        assert isinstance(ann, ToolAnnotations)
        assert ann.readOnlyHint is True
    finally:
        m._send, m._args = orig_send, orig_args


def test_analyze_lod_culling_in_rendering_category():
    from unity_mcp.tools.gating import _THEMED_CATEGORIES
    # Phase 2: RENDERING merged into MEDIA
    assert "analyze_lod_culling" in _THEMED_CATEGORIES["MEDIA"], (
        "analyze_lod_culling must be in MEDIA gating category"
    )
