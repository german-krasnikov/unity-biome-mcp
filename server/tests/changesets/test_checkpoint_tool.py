"""T19: checkpoint_tool MCP tool unit tests (no Unity, no TCP)."""

import uuid
from pathlib import Path
from unittest.mock import patch

import pytest


def _make_content_store(tmp_path: Path):
    from unity_mcp.changeset_store import ContentStore

    return ContentStore(str(tmp_path / "blobs"))


def _make_cp_store(tmp_path: Path, content_store):
    from unity_mcp.checkpoint_store import CheckpointStore

    return CheckpointStore(
        fingerprint="fp123456789012",
        content_store=content_store,
        _dir=tmp_path / "checkpoints",
    )


def _setup_tool(send_fn):
    from unity_mcp.tools import checkpoint_tool as mod

    mod._send = send_fn
    mod._args = lambda **kw: {k: v for k, v in kw.items() if v is not None}


@pytest.mark.asyncio
async def test_checkpoint_create_returns_ready_response(tmp_path):
    """checkpoint_create returns text line with checkpoint_id and state=ready."""
    from unity_mcp.tools import checkpoint_tool as mod

    content_store = _make_content_store(tmp_path)
    cp_store = _make_cp_store(tmp_path, content_store)

    async def fake_send(cmd, args):
        return "Checkpoint: before_1\ngroup_id=42\ndomain_stamp=abc123"

    _setup_tool(fake_send)

    with patch("unity_mcp.tools.checkpoint_tool._get_cp_store", return_value=cp_store):
        with patch("unity_mcp.tools.checkpoint_tool._get_content_store", return_value=content_store):
            # Pass a non-existent path → scan_manifest skips it → files=0
            result = await mod.checkpoint_create(paths="/nonexistent/file.cs")

    assert "checkpoint_id=" in result
    assert "state=ready" in result
    assert "files=0" in result


@pytest.mark.asyncio
async def test_checkpoint_restore_uses_undo_on_same_domain(tmp_path):
    """When domain stamp matches, Unity Undo is used (method=undo)."""
    from unity_mcp.checkpoint import Checkpoint, FileManifest
    from unity_mcp.tools import checkpoint_tool as mod

    content_store = _make_content_store(tmp_path)
    cp_store = _make_cp_store(tmp_path, content_store)

    cp = Checkpoint(
        checkpoint_id=str(uuid.uuid4()),
        turn_id=1,
        state="ready",
        fingerprint="fp123456789012",
        manifest=FileManifest(entries=()),
        undo_group_id=42,
        domain_stamp="stamp-abc",
        created_at="2026-08-14T00:00:00+00:00",
        finalized_at=None,
    )
    cp_store.save(cp)

    async def fake_send(cmd, args):
        if cmd == "diagnose":
            return "mvid=abc\nstamp=stamp-abc\ncompile=idle"
        if cmd == "checkpoint_undo_restore":
            return "ok"
        return ""

    _setup_tool(fake_send)

    with patch("unity_mcp.tools.checkpoint_tool._get_cp_store", return_value=cp_store):
        with patch("unity_mcp.tools.checkpoint_tool._get_content_store", return_value=content_store):
            result = await mod.checkpoint_restore(checkpoint_id=cp.checkpoint_id)

    assert "method=undo" in result


@pytest.mark.asyncio
async def test_checkpoint_restore_uses_file_on_stale_domain(tmp_path):
    """When domain stamp differs, file restore is used (method=file)."""
    from unity_mcp.changeset import ContentRef
    from unity_mcp.checkpoint import Checkpoint, FileManifest
    from unity_mcp.tools import checkpoint_tool as mod

    f = tmp_path / "a.cs"
    f.write_text("after", encoding="utf-8")

    content_store = _make_content_store(tmp_path)
    before_ref = content_store.put("before")
    cp_store = _make_cp_store(tmp_path, content_store)

    cp = Checkpoint(
        checkpoint_id=str(uuid.uuid4()),
        turn_id=1,
        state="ready",
        fingerprint="fp123456789012",
        manifest=FileManifest.of({str(f): before_ref}),
        undo_group_id=42,
        domain_stamp="old-stamp",
        created_at="2026-08-14T00:00:00+00:00",
        finalized_at=None,
    )
    cp_store.save(cp)

    async def fake_send(cmd, args):
        if cmd == "diagnose":
            return "mvid=xyz\nstamp=new-stamp\ncompile=idle"
        return ""

    _setup_tool(fake_send)

    with patch("unity_mcp.tools.checkpoint_tool._get_cp_store", return_value=cp_store):
        with patch("unity_mcp.tools.checkpoint_tool._get_content_store", return_value=content_store):
            result = await mod.checkpoint_restore(checkpoint_id=cp.checkpoint_id)

    assert "method=file" in result
    assert f.read_text(encoding="utf-8") == "before"


@pytest.mark.asyncio
async def test_checkpoint_restore_not_found(tmp_path):
    """Unknown checkpoint_id returns error string."""
    from unity_mcp.tools import checkpoint_tool as mod

    content_store = _make_content_store(tmp_path)
    cp_store = _make_cp_store(tmp_path, content_store)

    async def fake_send(cmd, args):
        return ""

    _setup_tool(fake_send)

    with patch("unity_mcp.tools.checkpoint_tool._get_cp_store", return_value=cp_store):
        with patch("unity_mcp.tools.checkpoint_tool._get_content_store", return_value=content_store):
            result = await mod.checkpoint_restore(checkpoint_id="nonexistent-id")

    assert "err: checkpoint_not_found" in result
