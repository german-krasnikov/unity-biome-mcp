"""The calls of the decisions budgetRoute, budgetRun and hitRateOk from the Python side.

The rules are dsl/decisions/budget.logic, printed into generated/budget_logic.py; the facts are budget.contract. This module is the
mechanical half: it folds the tracker, the feature registry and the hit/miss counters into those facts and nothing here decides. The
texts of the reasons stay with the router.
"""
from .generated import budget_logic as logic

Route = logic.BudgetRouteValues

_NO_SAMPLES = 0  # hitTotal under the warmup: the hit rate is not judged
_JUDGED = 50  # hitTotal at the warmup: a known rate is judged (today's budget has no warmup, characterized-no-warmup-today, Q-B1)
_MAX_PCT = 1000  # the domain of budget.sessionPct; beyond it every threshold (<= 0.95) answers the same


def hit_rate_ok(hits: int, misses: int) -> bool:
    """Is the hit rate of `hits` and `misses` good enough to keep spending (below 50 samples nothing is judged)."""
    total = hits + misses
    return logic.hit_rate_ok(logic.HitRateOkInput(hit_total=total, hit_ratio=hits / total if total else 1.0))


def _facts(cls, *, disabled: bool, priority: str, feature: str, day_cap_exceeded: bool, session_pct: float, hit_rate: float | None):
    return cls(budget_disabled=disabled, budget_priority=priority, budget_feature=feature, budget_day_cap_exceeded=day_cap_exceeded,
               budget_session_pct=min(session_pct, _MAX_PCT), hit_total=_NO_SAMPLES if hit_rate is None else _JUDGED, hit_ratio=1.0 if hit_rate is None else hit_rate)


def budget_route(**facts) -> str:
    """The route (a Route value) of a feature; `hit_rate` is the ratio the provider knows, or None."""
    return logic.budget_route(_facts(logic.BudgetRouteInput, **facts))


def budget_run(**facts) -> bool:
    """Does the feature run (the route is ok, ok_disabled or critical_at_95); a False is a skip the host records."""
    return logic.budget_run(_facts(logic.BudgetRunInput, **facts))
