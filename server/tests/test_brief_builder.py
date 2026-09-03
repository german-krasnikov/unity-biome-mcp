"""T21: ContextBuilder + built-in AttachmentProvider unit tests (no Unity, no TCP)."""

from unittest.mock import AsyncMock, MagicMock

import pytest

from unity_mcp import editor_log
from unity_mcp.brief_builder import (
    CompileErrorsProvider,
    ConsoleProvider,
    ContextBuilder,
    HierarchyProvider,
)


# ── Provider fetch tests ──────────────────────────────────────────────────────

async def test_compile_errors_provider_returns_clean_label():
    send = AsyncMock(return_value="No compilation errors")
    provider = CompileErrorsProvider()
    result = await provider.fetch(send)
    assert result == "clean"


async def test_compile_errors_provider_returns_errors_unchanged():
    raw = "2 compilation error(s):\nfile.cs:10: error CS0001"
    send = AsyncMock(return_value=raw)
    provider = CompileErrorsProvider()
    result = await provider.fetch(send)
    assert "error" in result
    assert result == raw


async def test_console_provider_fetch_returns_raw():
    raw = "error: NullReferenceException\nwarning: deprecated"
    send = AsyncMock(return_value=raw)
    provider = ConsoleProvider()
    result = await provider.fetch(send)
    assert result == raw


async def test_hierarchy_provider_fetch_returns_raw():
    raw = "SampleScene  objects=42\n/Player (Rigidbody)"
    send = AsyncMock(return_value=raw)
    provider = HierarchyProvider()
    result = await provider.fetch(send)
    assert result == raw


# ── ContextBuilder tests ──────────────────────────────────────────────────────

async def test_context_builder_critical_always_included():
    """Critical provider included even when budget is tight; medium skipped."""
    send = AsyncMock(return_value="")
    builder = ContextBuilder(total_budget=50, send=send)

    critical = MagicMock()
    critical.kind = "compile_errors"
    critical.priority = "critical"
    critical.token_budget = 50
    critical.fetch = AsyncMock(return_value="x" * 200)  # 50 tokens

    medium = MagicMock()
    medium.kind = "hierarchy"
    medium.priority = "medium"
    medium.token_budget = 800
    medium.fetch = AsyncMock(return_value="hierarchy content")

    builder.register(critical)
    builder.register(medium)
    brief = await builder.build()

    kinds = {s.kind for s in brief.slots}
    assert "compile_errors" in kinds
    assert "hierarchy" not in kinds  # threshold=80, remaining≈0


async def test_context_builder_medium_included_when_budget_allows():
    send = AsyncMock(return_value="")
    builder = ContextBuilder(total_budget=2000, send=send)

    medium = MagicMock()
    medium.kind = "hierarchy"
    medium.priority = "medium"
    medium.token_budget = 800
    medium.fetch = AsyncMock(return_value="hierarchy content")

    builder.register(medium)
    brief = await builder.build()

    assert "hierarchy" in {s.kind for s in brief.slots}


async def test_context_builder_low_skipped_when_budget_tight():
    send = AsyncMock(return_value="")
    builder = ContextBuilder(total_budget=5, send=send)  # 5 < threshold=15

    low = MagicMock()
    low.kind = "selection"
    low.priority = "low"
    low.token_budget = 150
    low.fetch = AsyncMock(return_value="selection content")

    builder.register(low)
    brief = await builder.build()

    assert "selection" not in {s.kind for s in brief.slots}


async def test_context_builder_provider_order_deterministic():
    """Slots in (priority_rank, kind) order — sort is stable."""
    send = AsyncMock(return_value="")
    builder = ContextBuilder(total_budget=2000, send=send)

    for kind, priority, budget in [
        ("selection", "low", 150),
        ("hierarchy", "medium", 800),
        ("compile_errors", "critical", 200),
        ("console", "critical", 300),
    ]:
        p = MagicMock()
        p.kind = kind
        p.priority = priority
        p.token_budget = budget
        p.fetch = AsyncMock(return_value=f"{kind} content")
        builder.register(p)

    brief = await builder.build()
    kinds = [s.kind for s in brief.slots]
    assert kinds.index("compile_errors") < kinds.index("console")
    assert kinds.index("console") < kinds.index("hierarchy")
    assert kinds.index("hierarchy") < kinds.index("selection")


async def test_context_builder_fetch_exception_omits_slot():
    send = AsyncMock(return_value="")
    builder = ContextBuilder(total_budget=2000, send=send)

    bad = MagicMock()
    bad.kind = "console"
    bad.priority = "critical"
    bad.token_budget = 300
    bad.fetch = AsyncMock(side_effect=RuntimeError("bridge error"))

    builder.register(bad)
    brief = await builder.build()

    assert "console" not in {s.kind for s in brief.slots}


async def test_context_builder_connection_error_yields_unreachable_slot():
    """A dead-Unity provider gets an explicit slot, not silent omission. (ARC-6 T4)"""
    builder = ContextBuilder(total_budget=2000, send=AsyncMock(return_value=""))

    dead = MagicMock()
    dead.kind = "compile_errors"
    dead.priority = "critical"
    dead.token_budget = 200
    dead.fetch = AsyncMock(side_effect=ConnectionError("dead TCP"))

    builder.register(dead)
    brief = await builder.build()

    assert len(brief.slots) == 1
    assert brief.slots[0].kind == "compile_errors"
    assert brief.slots[0].content == editor_log.UNITY_UNREACHABLE


async def test_context_builder_timeout_error_yields_unreachable_slot():
    """OSError family (TimeoutError) gets the same honest slot as ConnectionError."""
    builder = ContextBuilder(total_budget=2000, send=AsyncMock(return_value=""))

    dead = MagicMock()
    dead.kind = "console"
    dead.priority = "critical"
    dead.token_budget = 300
    dead.fetch = AsyncMock(side_effect=TimeoutError("no response"))

    builder.register(dead)
    brief = await builder.build()

    assert len(brief.slots) == 1
    assert brief.slots[0].content == editor_log.UNITY_UNREACHABLE


async def test_context_builder_provider_returns_sentinel_content_preserved():
    """A provider that already resolved to the sentinel (no exception) passes through untouched."""
    builder = ContextBuilder(total_budget=2000, send=AsyncMock(return_value=""))

    p = MagicMock()
    p.kind = "compile_errors"
    p.priority = "critical"
    p.token_budget = 200
    p.fetch = AsyncMock(return_value=editor_log.UNITY_UNREACHABLE)

    builder.register(p)
    brief = await builder.build()

    assert len(brief.slots) == 1
    assert brief.slots[0].content == editor_log.UNITY_UNREACHABLE


async def test_context_builder_empty_content_still_skipped():
    """Genuinely empty content (no exception) is still omitted — unchanged behavior."""
    builder = ContextBuilder(total_budget=2000, send=AsyncMock(return_value=""))

    p = MagicMock()
    p.kind = "selection"
    p.priority = "low"
    p.token_budget = 150
    p.fetch = AsyncMock(return_value="")

    builder.register(p)
    brief = await builder.build()

    assert "selection" not in {s.kind for s in brief.slots}


async def test_context_builder_kinds_filter():
    """kinds=["console"] only runs console provider."""
    send = AsyncMock(return_value="")
    builder = ContextBuilder(total_budget=2000, send=send)

    for kind, priority in [("console", "critical"), ("compile_errors", "critical")]:
        p = MagicMock()
        p.kind = kind
        p.priority = priority
        p.token_budget = 200
        p.fetch = AsyncMock(return_value=f"{kind} content")
        builder.register(p)

    brief = await builder.build(kinds=["console"])
    assert {s.kind for s in brief.slots} == {"console"}


async def test_context_builder_unknown_kind_ignored():
    """Unknown kind in filter is silently skipped."""
    send = AsyncMock(return_value="")
    builder = ContextBuilder(total_budget=2000, send=send)

    p = MagicMock()
    p.kind = "console"
    p.priority = "critical"
    p.token_budget = 300
    p.fetch = AsyncMock(return_value="console content")
    builder.register(p)

    brief = await builder.build(kinds=["unknown_kind"])
    assert len(brief.slots) == 0
