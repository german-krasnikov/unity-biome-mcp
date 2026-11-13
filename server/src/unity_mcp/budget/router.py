"""Adaptive routing: decide skip/run based on budget + priority + hit rate."""
import os
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass

from ..decisions.budget_facts import Route, budget_route, budget_run


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
        from .registry import get_feature
        priority = get_feature(feature).priority
        hit_rate = self._hit_rate(feature)
        disabled = os.environ.get("UNITY_MCP_BUDGET_DISABLED") == "1"
        facts = {
            "disabled": disabled, "priority": priority, "feature": feature, "hit_rate": hit_rate,
            "day_cap_exceeded": False if disabled else self._tracker.day_cap_exceeded(),
            "session_pct": 0.0 if disabled else self._tracker.session_pct(),
        }
        route = budget_route(**facts)
        run = budget_run(**facts)
        if not run:
            self._tracker.record_skip(feature)
        return RouteDecision(run, _reason(route, priority, hit_rate))


def _reason(route: str, priority: str, hit_rate: float | None) -> str:
    if route == Route.LOW_HIT_RATE:
        return f"low_hit_rate_{hit_rate:.2f}"
    if route == Route.BUDGET_95_SKIPPED:
        return f"budget_95_{priority}_skipped"
    return route
