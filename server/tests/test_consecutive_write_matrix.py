"""MCP-GUARD-007: consecutive-write guard full sequence matrix.

Tests the transition matrix — not just individual transitions — covering:
- read interrupts a write run (counter resets)
- batch counts as one write, not N mutations
- playtest resets the counter between write runs
- four-write run: guard fires at 3rd and continues through 4th
- mcp_status is NOT a read boundary (Python-only, never calls transition)
- query_state IS a read boundary (TCP call, calls transition)
- guard warning is prepended, not a replacement of the inner tool result
"""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.middleware import Middleware, wrap_send

_W = "set_property"   # shorthand for the most common scene-mutation command


def _mw() -> Middleware:
    return Middleware()


def test_guard_write_read_write_write_never_triggers():
    """Counter resets on get_component; subsequent write pair (max count=2) never warns."""
    mw = _mw()
    r1 = mw.transition(_W, {})              # count=1
    r2 = mw.transition("get_component", {}) # count→0 (read)
    r3 = mw.transition(_W, {})              # count=1
    r4 = mw.transition(_W, {})              # count=2
    assert r1 is None, "1st write: no warning"
    assert r2 is None, "read: no warning"
    assert r3 is None, "post-read write 1: count=1, no warning"
    assert r4 is None, "post-read write 2: count=2, still below threshold"


def test_guard_four_consecutive_writes_triggers_on_fourth():
    """Guard fires at count>=3; the 4th write also warns and reports count=4."""
    mw = _mw()
    r1 = mw.transition(_W, {})  # count=1
    r2 = mw.transition(_W, {})  # count=2
    r3 = mw.transition(_W, {})  # count=3 → fires
    r4 = mw.transition(_W, {})  # count=4 → still fires
    assert r1 is None, "1st write: no warning"
    assert r2 is None, "2nd write: no warning"
    assert r3 is not None, "3rd write triggers guard"
    assert "consecutive writes" in r3
    assert r4 is not None, "4th write also triggers guard"
    assert "4" in r4, "reported count is 4 on the 4th write"


def test_guard_playtest_after_three_writes_resets_counter():
    """run_playtest resets the consecutive-write counter; next write is silent."""
    mw = _mw()
    mw.transition(_W, {})                                      # count=1
    mw.transition(_W, {})                                      # count=2
    mw.transition(_W, {})                                      # count=3 → fires
    mw.transition("run_playtest", {"script": "ASSERT /A|H"})  # count→0 (neutral write)
    result = mw.transition(_W, {})                             # count=1 → silent
    assert result is None, "post-playtest write should not warn (counter was reset)"
    assert mw._consecutive_writes == 1


def test_guard_batch_write_counts_as_single_write():
    """A non-readonly batch counts as exactly 1 consecutive write, not N per mutation."""
    mw = _mw()
    two_mutations = (
        "set_property path=/Cube component=Transform prop=localPositionX value=1\n"
        "set_property path=/Cube component=Transform prop=localPositionY value=2"
    )
    r1 = mw.transition("batch", {"commands": two_mutations})  # count=1
    r2 = mw.transition("batch", {"commands": two_mutations})  # count=2
    r3 = mw.transition("batch", {"commands": two_mutations})  # count=3 → fires
    assert r1 is None, "1st batch: no warning"
    assert r2 is None, "2nd batch: no warning"
    assert r3 is not None, "guard fires at 3rd batch (not at 6th individual mutation)"
    assert "consecutive writes" in r3


def test_guard_get_hierarchy_resets_counter_after_writes():
    """get_hierarchy (read) resets counter; subsequent pair stays below threshold."""
    mw = _mw()
    mw.transition(_W, {})               # count=1
    mw.transition(_W, {})               # count=2
    mw.transition("get_hierarchy", {})  # count→0 (read)
    r3 = mw.transition(_W, {})          # count=1
    r4 = mw.transition(_W, {})          # count=2
    assert r3 is None, "1st post-hierarchy write: count=1, no warning"
    assert r4 is None, "2nd post-hierarchy write: count=2, no warning"
    assert mw._consecutive_writes == 2


# ─── GUARD-007: mcp_status vs query_state discriminator ──────────────────────

def test_mcp_status_does_not_reset_consecutive_write_counter():
    """mcp_status is Python-only (direct_only=True); it never calls transition().

    Contract: mcp_status between writes is NOT a read boundary.
    Guard fires on the 3rd write because no reset happened.
    """
    mw = _mw()
    mw.transition(_W, {})  # count=1
    mw.transition(_W, {})  # count=2
    # mcp_status never calls transition() — no reset happens
    result = mw.transition(_W, {})  # count=3 → guard fires
    assert result is not None, "Guard must fire: mcp_status did not reset the counter"
    assert "consecutive writes" in result
    assert mw._consecutive_writes == 3


def test_query_state_resets_consecutive_write_counter():
    """query_state is a TCP read; transition() resets _consecutive_writes to 0.

    After the reset: 2 more writes stay below the threshold (no warning).
    """
    mw = _mw()
    mw.transition(_W, {})                     # count=1
    mw.transition(_W, {})                     # count=2
    r = mw.transition("query_state", {})      # count→0 (TCP read boundary)
    assert r is None, "query_state must not produce a warning"
    assert mw._consecutive_writes == 0
    r3 = mw.transition(_W, {})                # count=1
    r4 = mw.transition(_W, {})                # count=2
    assert r3 is None, "1st write after reset: count=1, no warning"
    assert r4 is None, "2nd write after reset: count=2, no warning"


async def test_guard_warning_does_not_overwrite_inner_tool_result():
    """Guard warning is prepended to, not a replacement of, the inner tool result.

    When the guard fires on the 3rd consecutive write, both the warning AND
    the Unity TCP response must be present in the final result.
    """
    mw = _mw()
    inner = "ok written=true"
    wrapped = wrap_send(AsyncMock(return_value=inner), mw)

    # Use distinct paths so the retry guard (identical-args check) doesn't fire early.
    await wrapped(_W, {"path": "/A", "value": "1"})  # count=1
    await wrapped(_W, {"path": "/B", "value": "1"})  # count=2
    result = await wrapped(_W, {"path": "/C", "value": "1"})  # count=3 → warning prepended

    assert "consecutive writes" in result, "Guard warning must be present"
    assert inner in result, "Inner tool result must be preserved alongside warning"
