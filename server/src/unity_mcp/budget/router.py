"""Adaptive routing: decide skip/run based on budget + priority + hit rate."""
import os
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass


@dataclass
class RouteDecision:
    run: bool
    reason: str


class BudgetRouter:
    def __init__(self, tracker, hit_rate_provider: Callable | None = None):
        """hit_rate_provider: callable(feature) -> float | None"""
        self._tracker = tracker
        self._hit_rate = hit_rate_provider or (lambda _f: None)

    def should_run(self, feature: str, difficulty: float,
                   confidence_hint: float | None = None) -> RouteDecision:
        if os.environ.get("UNITY_MCP_BUDGET_DISABLED") == "1":
            return RouteDecision(True, "ok_disabled")

        from .registry import get_feature
        meta = get_feature(feature)

        # Hit rate gate: if provider says <40%, skip non-critical
        hit_rate = self._hit_rate(feature)
        if hit_rate is not None and hit_rate < 0.40 and meta.priority != "critical":
            self._tracker.record_skip(feature)
            return RouteDecision(False, f"low_hit_rate_{hit_rate:.2f}")

        # Day cap hard stop
        if self._tracker.day_cap_exceeded():
            self._tracker.record_skip(feature)
            return RouteDecision(False, "day_cap_exceeded")

        pct = self._tracker.session_pct()

        # 95%+: only critical
        if pct >= 0.95:
            if meta.priority != "critical":
                self._tracker.record_skip(feature)
                return RouteDecision(False, f"budget_95_{meta.priority}_skipped")
            return RouteDecision(True, "critical_at_95")

        # 80-95%: kill low-priority
        if pct >= 0.80 and meta.priority == "low":
            self._tracker.record_skip(feature)
            return RouteDecision(False, "budget_80_low_skipped")

        # 50-80%: skip low priority watchdog + summarize
        if pct >= 0.50 and meta.priority == "low" and feature in ("watchdog", "summarize"):
            self._tracker.record_skip(feature)
            return RouteDecision(False, "budget_50_low_skipped")

        return RouteDecision(True, "ok")
