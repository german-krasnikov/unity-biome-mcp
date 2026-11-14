"""TDD: Budget integration — SamplingService gating + budget_status tool."""
import os
from pathlib import Path
from unittest.mock import AsyncMock, patch, MagicMock
from unity_mcp.budget.cost_tracker import CostTracker
from unity_mcp.budget.router import BudgetRouter


def make_tracker(tmp_path, session_cap=1.0, day_cap=5.0):
    return CostTracker(path=tmp_path / "b.json", session_cap=session_cap, day_cap=day_cap)


async def test_sampling_records_cost_on_success(tmp_path):
    """After a successful sampling call, budget tracker should have > 0 spent."""
    import unity_mcp.sampling as sm
    tracker = make_tracker(tmp_path)
    router = BudgetRouter(tracker)
    sm.init_budget(tracker, router)

    try:
        svc = sm.SamplingService()
        with patch.dict(os.environ, {"UNITY_MCP_VISUAL_VERIFY": "1"}):
            with patch("unity_mcp.sampling.asyncio.create_subprocess_exec") as mock_exec:
                mock_proc = MagicMock()
                mock_proc.communicate = AsyncMock(
                    return_value=(b"PASS: looks good", b""))
                mock_proc.returncode = 0
                mock_exec.return_value = mock_proc
                result = await svc.verify_visual("Check scene", feature="visual_verify")

        assert result == "PASS: looks good"
        assert tracker.session_spent() > 0
    finally:
        sm.init_budget(None, None)


async def test_sampling_skips_on_budget_exceeded(tmp_path):
    """When budget is 95%+ and feature is low-priority, skip call entirely."""
    import unity_mcp.sampling as sm
    tracker = CostTracker(path=tmp_path / "b.json", session_cap=0.000001, day_cap=5.0)
    tracker.record("x", 5000, 5000)  # blow past 95%
    router = BudgetRouter(tracker)
    sm.init_budget(tracker, router)

    try:
        svc = sm.SamplingService()
        with patch.dict(os.environ, {"UNITY_MCP_VISUAL_VERIFY": "1"}):
            with patch("unity_mcp.sampling.asyncio.create_subprocess_exec") as mock_exec:
                result = await svc.summarize("data", feature="summarize")
        assert result is None
        mock_exec.assert_not_called()
    finally:
        sm.init_budget(None, None)


async def test_budget_status_tool_returns_text(tmp_path):
    """budget_status returns formatted string after init."""
    from unity_mcp.tools import budget_tool
    tracker = make_tracker(tmp_path, session_cap=0.50)
    tracker.record("a", 1000, 100)
    budget_tool._tracker = tracker

    result = await budget_tool.budget_status()
    assert "sess=$" in result
    assert "day=$" in result


async def test_budget_status_disabled_message():
    """budget_status without init returns disabled message."""
    from unity_mcp.tools import budget_tool
    budget_tool._tracker = None
    result = await budget_tool.budget_status()
    assert "disabled" in result.lower() or "budget" in result.lower()


async def test_verify_visual_without_image_charges_text_only(tmp_path):
    """verify_visual with no screenshot must NOT add image token overhead."""
    import unity_mcp.sampling as sm
    from unity_mcp.metrics import HAIKU_IN_PER_MTOK, HAIKU_OUT_PER_MTOK
    from unity_mcp.budget.registry import get_feature
    tracker = make_tracker(tmp_path)
    router = BudgetRouter(tracker)
    sm.init_budget(tracker, router)

    try:
        svc = sm.SamplingService()
        with patch.dict(os.environ, {"UNITY_MCP_VISUAL_VERIFY": "1"}):
            with patch("unity_mcp.sampling.asyncio.create_subprocess_exec") as mock_exec:
                mock_proc = MagicMock()
                mock_proc.communicate = AsyncMock(return_value=(b"PASS", b""))
                mock_proc.returncode = 0
                mock_exec.return_value = mock_proc
                # No screenshot_path passed → has_image should be False
                await svc.verify_visual("Check scene", feature="visual_verify")

        meta = get_feature("visual_verify")
        text_only_cost = meta.est_in * HAIKU_IN_PER_MTOK / 1e6 + meta.est_out * HAIKU_OUT_PER_MTOK / 1e6
        image_cost = (meta.est_in + 1500) * HAIKU_IN_PER_MTOK / 1e6 + meta.est_out * HAIKU_OUT_PER_MTOK / 1e6
        actual = tracker.session_spent()
        assert abs(actual - text_only_cost) < 1e-10, \
            f"Expected text-only ${text_only_cost:.6f}, got ${actual:.6f} (image would be ${image_cost:.6f})"
    finally:
        sm.init_budget(None, None)


async def test_budget_disabled_via_env(tmp_path):
    """UNITY_MCP_BUDGET_DISABLED=1 bypasses routing entirely."""
    import unity_mcp.sampling as sm
    tracker = CostTracker(path=tmp_path / "b.json", session_cap=0.000001, day_cap=5.0)
    tracker.record("x", 5000, 5000)  # blow past 95%
    router = BudgetRouter(tracker)
    sm.init_budget(tracker, router)

    try:
        svc = sm.SamplingService()
        with patch.dict(os.environ, {"UNITY_MCP_VISUAL_VERIFY": "1",
                                      "UNITY_MCP_BUDGET_DISABLED": "1"}):
            with patch("unity_mcp.sampling.asyncio.create_subprocess_exec") as mock_exec:
                mock_proc = MagicMock()
                mock_proc.communicate = AsyncMock(return_value=(b"done", b""))
                mock_proc.returncode = 0
                mock_exec.return_value = mock_proc
                result = await svc.summarize("data", feature="summarize")
        assert result == "done"
        mock_exec.assert_called_once()
    finally:
        sm.init_budget(None, None)
