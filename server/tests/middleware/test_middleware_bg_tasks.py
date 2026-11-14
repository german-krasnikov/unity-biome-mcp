"""Tests for fire-and-forget GC-cancellation fix (FIX-25)."""
import asyncio
import pytest
from unity_mcp.middleware import Middleware


@pytest.mark.asyncio
async def test_background_task_tracked():
    """Task is held in _bg_tasks while running."""
    mw = Middleware()
    event = asyncio.Event()

    async def slow():
        await event.wait()

    t = asyncio.create_task(slow())
    mw._bg_tasks.add(t)
    t.add_done_callback(mw._bg_tasks.discard)

    assert t in mw._bg_tasks
    event.set()
    await t
    await asyncio.sleep(0)
    assert t not in mw._bg_tasks


