"""TDD tests for Q6 (rename_track) and L5 (set_clip_in, get_bindings) + M1-M6."""
import pytest
from unittest.mock import AsyncMock

from unity_mcp.server import timeline


# Q6: rename_track
async def test_timeline_rename_track_sends_name(mock_bridge):
    """rename_track action forwards name to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/Dir", action="rename_track", track="OldName", name="NewName")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "rename_track"
    assert args["track"] == "OldName"
    assert args["name"] == "NewName"


# L5: set_clip_in
async def test_timeline_set_clip_in_sends_clip_in(mock_bridge):
    """set_clip_in forwards clip_in to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/GO", action="set_clip_in", track="Body", clip="Intro", clip_in=0.5)
    args = mock_bridge.send.call_args[0][1]
    assert args["clip_in"] == 0.5


# L5: get_bindings
async def test_timeline_get_bindings_sends_path_only(mock_bridge):
    """get_bindings sends only action and path (no extra keys)."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/GO", action="get_bindings")
    args = mock_bridge.send.call_args[0][1]
    assert args == {"action": "get_bindings", "path": "/GO"}


# M1: reorder_track
async def test_timeline_reorder_track_sends_index(mock_bridge):
    """reorder_track forwards track and index to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/Dir", action="reorder_track", track="Body", index=0)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "reorder_track"
    assert args["track"] == "Body"
    assert args["index"] == 0


# M2: duplicate_clip
async def test_timeline_duplicate_clip_sends_offset(mock_bridge):
    """duplicate_clip forwards clip and offset to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/Dir", action="duplicate_clip", track="T", clip="C", offset=2.0)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "duplicate_clip"
    assert args["clip"] == "C"
    assert args["offset"] == 2.0


# M3: add_marker / remove_marker
async def test_timeline_add_marker_sends_params(mock_bridge):
    """add_marker forwards track, name, and start to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/Dir", action="add_marker", track="Signals", name="Hit", start=1.5)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "add_marker"
    assert args["track"] == "Signals"
    assert args["name"] == "Hit"
    assert args["start"] == 1.5


async def test_timeline_remove_marker_sends_params(mock_bridge):
    """remove_marker forwards track and start to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/Dir", action="remove_marker", track="Signals", start=1.5)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "remove_marker"
    assert args["track"] == "Signals"
    assert args["start"] == 1.5


# M5: set_duration
async def test_timeline_set_duration_sends_duration(mock_bridge):
    """set_duration forwards duration to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/Dir", action="set_duration", duration=10.0)
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "set_duration"
    assert args["duration"] == 10.0


# M6: add_sub_track
async def test_timeline_add_sub_track_sends_params(mock_bridge):
    """add_sub_track forwards track, track_type, and name to bridge."""
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": "ok"})
    await timeline(path="/Dir", action="add_sub_track", track="Group1",
                   track_type="Animation", name="SubAnim")
    args = mock_bridge.send.call_args[0][1]
    assert args["action"] == "add_sub_track"
    assert args["track"] == "Group1"
    assert args["track_type"] == "Animation"
    assert args["name"] == "SubAnim"


# Error propagation
async def test_timeline_error_propagates(mock_bridge):
    """ToolError raised when bridge returns ok=False."""
    from mcp.server.fastmcp.exceptions import ToolError
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Track not found"})
    with pytest.raises(ToolError, match="Track not found"):
        await timeline(path="/GO", action="set_clip_in", track="X", clip="Y")
