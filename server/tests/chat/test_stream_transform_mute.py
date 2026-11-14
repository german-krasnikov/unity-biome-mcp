"""Tests for acc.muted path in _transform_line — tool_result block suppression.

The muted flag was implemented in stream_transform but had no dedicated tests.
These cover the gap identified in commit 2155c73 review.
"""
import json

from unity_mcp.stream_transform import _ToolCallAcc, _transform_line

# ── Helpers ──────────────────────────────────────────────────────────────────

def _cb_start(block_type: str, name: str = "", id_: str = "") -> str:
    blk: dict = {"type": block_type}
    if name:
        blk["name"] = name
    if id_:
        blk["id"] = id_
    return json.dumps({"type": "stream_event",
                        "event": {"type": "content_block_start", "content_block": blk}})


def _cb_text(text: str) -> str:
    return json.dumps({"type": "stream_event",
                        "event": {"type": "content_block_delta",
                                  "delta": {"type": "text_delta", "text": text}}})


def _cb_input(partial: str) -> str:
    return json.dumps({"type": "stream_event",
                        "event": {"type": "content_block_delta",
                                  "delta": {"type": "input_json_delta",
                                            "partial_json": partial}}})


_CB_STOP = '{"type":"stream_event","event":{"type":"content_block_stop"}}'

# ── Tests ────────────────────────────────────────────────────────────────────

def test_tool_result_start_sets_muted():
    acc = _ToolCallAcc()
    _transform_line(_cb_start("tool_result"), acc)
    assert acc.muted


def test_tool_result_text_delta_suppressed():
    acc = _ToolCallAcc()
    _transform_line(_cb_start("tool_result"), acc)
    result = _transform_line(_cb_text("secret output"), acc)
    assert result == []


def test_tool_result_mute_clears_after_block_stop():
    acc = _ToolCallAcc()
    _transform_line(_cb_start("tool_result"), acc)
    _transform_line(_CB_STOP, acc)
    assert not acc.muted
    result = _transform_line(_cb_text("visible"), acc)
    assert result == ["t|visible"]


def test_text_delta_suppressed_during_active_tool_use():
    """acc.active also suppresses text_delta (parallel guard to acc.muted)."""
    acc = _ToolCallAcc()
    acc.active = True
    assert _transform_line(_cb_text("hidden"), acc) == []


def test_muted_false_for_non_tool_result_block_start():
    """content_block_start with type != tool_use/tool_result clears muted."""
    acc = _ToolCallAcc()
    acc.muted = True
    _transform_line(_cb_start("text"), acc)
    assert not acc.muted


def test_full_tool_use_then_tool_result_then_text():
    """Full round-trip: tool_use → tool_result (muted) → text (visible)."""
    acc = _ToolCallAcc()

    # tool_use produces tc| event
    _transform_line(_cb_start("tool_use", name="bash", id_="t1"), acc)
    _transform_line(_cb_input("{}"), acc)
    tc = _transform_line(_CB_STOP, acc)
    assert tc == ["tc|bash|t1|{}"]

    # tool_result block: all text_deltas suppressed
    _transform_line(_cb_start("tool_result"), acc)
    assert _transform_line(_cb_text("stdout output"), acc) == []
    assert _transform_line(_cb_text("more output"), acc) == []
    _transform_line(_CB_STOP, acc)

    # text after tool_result is visible
    assert _transform_line(_cb_text("AI response"), acc) == ["t|AI response"]
