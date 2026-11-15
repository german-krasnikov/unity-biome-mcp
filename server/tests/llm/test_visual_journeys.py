"""Critical end-to-end visual pipeline journeys. P0 from 10-architect review.

Mock boundary: SamplingService.verify_visual_diff (high-level).
Real layers: visual_diff dispatch, degrade ladder, budget gating, cache.
NO Unity bridge required. Cost: $0.
"""
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import AsyncMock, MagicMock

import pytest


def _png(path: Path, color):
    """Generate solid-color PNG via PIL (deterministic test data)."""
    from PIL import Image
    Image.new("RGB", (10, 10), color).save(path)


@pytest.fixture
def journey_context(tmp_path, monkeypatch):
    """Shared setup: mock sampling, fresh cache helper."""
    from unity_mcp import visual_diff as vd_mod
    from unity_mcp.visual_diff import DiffCache

    ns = SimpleNamespace()
    ns.mock_sampling = MagicMock()
    ns.mock_sampling.verify_visual_diff = AsyncMock()

    def fresh_cache():
        monkeypatch.setattr(vd_mod, "_cache", DiffCache(max_entries=64, ttl=300.0))

    ns.fresh_cache = fresh_cache
    ns.tmp_path = tmp_path
    return ns


async def test_journey_build_screenshot_diff(journey_context):
    """Journey #1: mutation → 2 screenshots → visual_diff returns Haiku result.

    Also catches: same-file diff (production bug — overwritten path) returns IDENTICAL.
    """
    from unity_mcp.visual_diff import visual_diff

    ctx = journey_context
    ctx.fresh_cache()

    before = ctx.tmp_path / "before.png"
    _png(before, (255, 0, 0))
    after = ctx.tmp_path / "after.png"
    _png(after, (0, 0, 255))

    ctx.mock_sampling.verify_visual_diff = AsyncMock(return_value="Color shifted red to blue.")

    result = await visual_diff(str(before), str(after), mode="structural", sampling=ctx.mock_sampling)

    assert "Color shifted red to blue." in result
    assert "[DEGRADED:" not in result
    assert result.startswith("PIXEL:")
    ctx.mock_sampling.verify_visual_diff.assert_called_once()

    # Critical: same-file diff (production bug — overwritten path)
    same_result = await visual_diff(str(before), str(before), mode="auto", sampling=ctx.mock_sampling)
    assert "IDENTICAL" in same_result
    # Haiku NOT re-called for identical files
    assert ctx.mock_sampling.verify_visual_diff.call_count == 1


async def test_journey_budget_exhaustion_marker(journey_context, monkeypatch):
    """Journey #3: budget exhausted → degrade kicks in → [DEGRADED:budget_*] marker."""
    from unity_mcp.visual_diff import visual_diff
    from unity_mcp.budget.cost_tracker import CostTracker
    from unity_mcp.budget.router import BudgetRouter
    from unity_mcp import sampling as sampling_mod

    ctx = journey_context
    ctx.fresh_cache()

    # CRITICAL: enable visual_verify so the budget gate is actually reached.
    # Without this, SamplingService.enabled=False short-circuits before gate.
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")

    # Set up tracker with tiny day cap → easy to exhaust
    tracker_path = ctx.tmp_path / "budget.json"
    tracker = CostTracker(path=tracker_path, day_cap=0.0001, session_cap=100.0)

    # Saturate via direct record
    for _ in range(50):
        tracker.record("visual_diff", in_tok=1000, out_tok=200, has_image=True)
    assert tracker.day_cap_exceeded(), "Setup failed: tracker not exhausted"

    # Wire tracker + router into sampling module
    monkeypatch.setattr(sampling_mod, "_budget_tracker", tracker)
    router = BudgetRouter(tracker)
    monkeypatch.setattr(sampling_mod, "_budget_router", router)

    before = ctx.tmp_path / "b.png"
    _png(before, (1, 2, 3))
    after = ctx.tmp_path / "a.png"
    _png(after, (200, 100, 50))

    # Real SamplingService — gate must block before subprocess
    from unity_mcp.sampling import SamplingService
    real_svc = SamplingService()
    real_svc._run = AsyncMock(return_value="should-not-be-used")

    result = await visual_diff(str(before), str(after), mode="structural", sampling=real_svc)

    # Must have degraded marker (budget blocks → pixel_only or feature_unavailable)
    assert "[DEGRADED:" in result
    real_svc._run.assert_not_called()


async def test_journey_recovery_after_haiku_timeout(journey_context):
    """Pre-emptive guard against future circuit-breaker / sticky-disable additions.
    Currently degrade() is stateless — no current sticky-disable mechanism exists.
    This test passes trivially today but would fail if anyone adds a sticky-disable
    without proper recovery semantics.
    """
    from unity_mcp.visual_diff import visual_diff

    ctx = journey_context
    ctx.fresh_cache()

    before = ctx.tmp_path / "b.png"
    _png(before, (10, 20, 30))
    a1 = ctx.tmp_path / "a1.png"
    _png(a1, (200, 100, 50))

    # 3 sequential calls: first 2 fail, 3rd succeeds
    ctx.mock_sampling.verify_visual_diff = AsyncMock(
        side_effect=[None, None, "Recovered: red box."]
    )

    r1 = await visual_diff(str(before), str(a1), mode="structural", sampling=ctx.mock_sampling)
    assert "[DEGRADED:visual_diff:pixel_only]" in r1

    # Bust cache for 2nd attempt with different file
    a2 = ctx.tmp_path / "a2.png"
    _png(a2, (201, 100, 50))
    r2 = await visual_diff(str(before), str(a2), mode="structural", sampling=ctx.mock_sampling)
    assert "[DEGRADED:visual_diff:pixel_only]" in r2

    # 3rd call: Haiku alive — MUST succeed (no sticky disable)
    a3 = ctx.tmp_path / "a3.png"
    _png(a3, (202, 100, 50))
    r3 = await visual_diff(str(before), str(a3), mode="structural", sampling=ctx.mock_sampling)
    assert "[DEGRADED:" not in r3
    assert "Recovered: red box." in r3
    assert ctx.mock_sampling.verify_visual_diff.call_count == 3
