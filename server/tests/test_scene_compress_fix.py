"""TDD tests for S3: get_hierarchy compress passthrough + collapse_empty_sections."""
import pytest
from unittest.mock import AsyncMock, patch
from unity_mcp.compressor import collapse_empty_sections


# ── collapse_empty_sections ────────────────────────────────────────────────────

def test_collapse_removes_empty_section():
    """Header followed immediately by another header (empty body) is removed."""
    text = "[Transform]\n[Rigidbody]\nmass: 5.0"
    result = collapse_empty_sections(text)
    assert "[Transform]" not in result
    assert "[Rigidbody]" in result
    assert "mass: 5.0" in result


def test_collapse_removes_section_before_eof():
    """Header at end of string with no content is removed."""
    text = "[Rigidbody]\nmass: 5.0\n[EmptyAtEnd]"
    result = collapse_empty_sections(text)
    assert "[EmptyAtEnd]" not in result
    assert "mass: 5.0" in result


def test_collapse_keeps_section_with_content():
    """Header followed by content is kept."""
    text = "[Transform]\nposition: (1, 0, 0)"
    result = collapse_empty_sections(text)
    assert "[Transform]" in result
    assert "position: (1, 0, 0)" in result


def test_collapse_noop_no_sections():
    """Plain text without headers is unchanged."""
    text = "field: value\nother: data"
    result = collapse_empty_sections(text)
    assert result == text


def test_collapse_multiple_empty_sections():
    """Multiple consecutive empty headers all removed."""
    text = "[A]\n[B]\n[C]\ndata: 1"
    result = collapse_empty_sections(text)
    assert "[A]" not in result
    assert "[B]" not in result
    assert "[C]" in result


# ── get_hierarchy compress passthrough ────────────────────────────────────────

def _make_args(**kwargs):
    return {k: v for k, v in kwargs.items() if v is not None}


@pytest.mark.asyncio
async def test_get_hierarchy_compress_passes_to_bridge():
    """get_hierarchy(compress=True) must include compress='true' in bridge args."""
    from unity_mcp.tools import scene as scene_mod

    captured_args = {}

    async def fake_send(cmd, args, **kw):
        captured_args.update(args)
        return "Hierarchy output"

    orig_send, orig_args = scene_mod._send, scene_mod._args
    scene_mod._send = fake_send
    scene_mod._args = _make_args
    try:
        await scene_mod.get_hierarchy(compress=True)
    finally:
        scene_mod._send = orig_send
        scene_mod._args = orig_args

    assert captured_args.get("compress") == "true"


@pytest.mark.asyncio
async def test_get_hierarchy_no_compress_no_key():
    """get_hierarchy(compress=False) must NOT include compress key in args."""
    from unity_mcp.tools import scene as scene_mod

    captured_args = {}

    async def fake_send(cmd, args, **kw):
        captured_args.update(args)
        return "Hierarchy output"

    orig_send, orig_args = scene_mod._send, scene_mod._args
    scene_mod._send = fake_send
    scene_mod._args = _make_args
    try:
        await scene_mod.get_hierarchy(compress=False)
    finally:
        scene_mod._send = orig_send
        scene_mod._args = orig_args

    assert "compress" not in captured_args
