"""T22: ProfilerProvider unit tests (no Unity, no TCP)."""
from __future__ import annotations

from unittest.mock import AsyncMock

from unity_mcp.brief_builder import (
    ProfilerProvider,
    make_default_builder,
)

# ── Provider fetch tests ──────────────────────────────────────────────────────

async def test_profiler_provider_fetch_returns_empty_on_no_sessions():
    send = AsyncMock(return_value="no sessions")
    result = await ProfilerProvider().fetch(send)
    assert result == ""


async def test_profiler_provider_fetch_returns_stats_text():
    stats = "session:p1 5.0s 300frames\nfps avg=60.0 min=58 max=62"
    send = AsyncMock(return_value=stats)
    result = await ProfilerProvider().fetch(send)
    assert result == stats


# ── Integration with ContextBuilder ──────────────────────────────────────────

async def test_brief_build_includes_profiler_section():
    stats = "session:p1 5.0s 300frames\nfps avg=60.0 min=58 max=62"

    async def send(cmd, args, **kw):
        if cmd == "get_profile_context":
            return stats
        return ""

    builder = make_default_builder(2000, send)
    brief = await builder.build(kinds=["profiler"])
    text = brief.to_text()
    assert "[Profiler]" in text
    assert "fps avg=60.0" in text


async def test_brief_build_profiler_truncated_at_budget():
    long_stats = "session:p1 5.0s 300frames\n" + "fps avg=60.0\n" * 300

    async def send(cmd, args, **kw):
        return long_stats

    builder = make_default_builder(2000, send)
    brief = await builder.build(kinds=["profiler"])
    profiler_slot = next((s for s in brief.slots if s.kind == "profiler"), None)
    assert profiler_slot is not None
    # Slot must be truncated and well within 2× token_budget ceiling
    assert profiler_slot.truncated is True
    assert profiler_slot.used_tokens <= 210  # 200 budget + truncation marker overhead
