# Generated from dsl/decisions/budget.logic
# source-sha256=fcb7a667d8d4785a5293c639a86bb785a0c9cfa4501b21dfa4b09ce781c95067
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


@dataclass(frozen=True, slots=True)
class HitRateOkInput:
    hit_ratio: float
    hit_total: float


def hit_rate_ok(inp: HitRateOkInput) -> bool:
    """Checks every input against the contract, then decides."""
    _check("hit.ratio", inp.hit_ratio, "number", lo=0, hi=1, integer=False)
    _check("hit.total", inp.hit_total, "number", lo=0, hi=100000000, integer=True)
    return _decide_hit_rate_ok(inp)


class BudgetRouteValues:
    BUDGET_50_LOW_SKIPPED = "budget_50_low_skipped"
    BUDGET_80_LOW_SKIPPED = "budget_80_low_skipped"
    BUDGET_95_SKIPPED = "budget_95_skipped"
    CRITICAL_AT_95 = "critical_at_95"
    DAY_CAP_EXCEEDED = "day_cap_exceeded"
    LOW_HIT_RATE = "low_hit_rate"
    OK = "ok"
    OK_DISABLED = "ok_disabled"


@dataclass(frozen=True, slots=True)
class BudgetRouteInput:
    budget_day_cap_exceeded: bool
    budget_disabled: bool
    budget_feature: str
    budget_priority: str
    budget_session_pct: float
    hit_ratio: float
    hit_total: float


def budget_route(inp: BudgetRouteInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("budget.dayCapExceeded", inp.budget_day_cap_exceeded, "bool")
    _check("budget.disabled", inp.budget_disabled, "bool")
    _check("budget.feature", inp.budget_feature, "string")
    _check("budget.priority", inp.budget_priority, "enum", values=("critical", "medium",
            "low",))
    _check("budget.sessionPct", inp.budget_session_pct, "number", lo=0, hi=1000,
            integer=False)
    _check("hit.ratio", inp.hit_ratio, "number", lo=0, hi=1, integer=False)
    _check("hit.total", inp.hit_total, "number", lo=0, hi=100000000, integer=True)
    return _decide_budget_route(inp)


@dataclass(frozen=True, slots=True)
class BudgetRunInput:
    budget_day_cap_exceeded: bool
    budget_disabled: bool
    budget_feature: str
    budget_priority: str
    budget_session_pct: float
    hit_ratio: float
    hit_total: float


def budget_run(inp: BudgetRunInput) -> bool:
    """Checks every input against the contract, then decides."""
    _check("budget.dayCapExceeded", inp.budget_day_cap_exceeded, "bool")
    _check("budget.disabled", inp.budget_disabled, "bool")
    _check("budget.feature", inp.budget_feature, "string")
    _check("budget.priority", inp.budget_priority, "enum", values=("critical", "medium",
            "low",))
    _check("budget.sessionPct", inp.budget_session_pct, "number", lo=0, hi=1000,
            integer=False)
    _check("hit.ratio", inp.hit_ratio, "number", lo=0, hi=1, integer=False)
    _check("hit.total", inp.hit_total, "number", lo=0, hi=100000000, integer=True)
    return _decide_budget_run(inp)


__all__ = [
    "LogicInputError",
    "HitRateOkInput",
    "hit_rate_ok",
    "BudgetRouteValues",
    "BudgetRouteInput",
    "budget_route",
    "BudgetRunInput",
    "budget_run",
]


# Unchecked evaluators: reachable only through the checked functions above.
def _check(
    fact_id: str,
    value: object,
    kind: str,
    lo: float | None = None,
    hi: float | None = None,
    integer: bool = False,
    values: tuple[str, ...] = (),
) -> None:
    if kind == "bool":
        ok = isinstance(value, bool)
    elif kind == "number":
        ok = (
            isinstance(value, (int, float))
            and not isinstance(value, bool)
            and math.isfinite(value)
        )
        number = _typing.cast("float", value)
        ok = ok and (lo is None or number >= lo) and (hi is None or number <= hi)
        if integer:
            ok = ok and float(number).is_integer() and abs(number) <= 9007199254740991
    elif kind == "enum":
        ok = isinstance(value, str) and value in values
    else:
        ok = isinstance(value, str)
    if not ok:
        raise LogicInputError(f"LOGIC_INPUT_CONTRACT: {fact_id}={value!r}")


def _decide_hit_rate_ok(s: HitRateOkInput | BudgetRouteInput | BudgetRunInput) -> bool:
    # rule: warmup (priority 10)
    if s.hit_total < 50:
        return True
    return not (s.hit_ratio < 0.4)


def _decide_budget_route(s: BudgetRouteInput | BudgetRunInput) -> str:
    # rule: disabled (priority 10)
    if s.budget_disabled is True:
        return "ok_disabled"
    # rule: lowHitRate (priority 20)
    if (_decide_hit_rate_ok(s) is not True) and (s.budget_priority != "critical"):
        return "low_hit_rate"
    # rule: dayCap (priority 30)
    if s.budget_day_cap_exceeded is True:
        return "day_cap_exceeded"
    # rule: criticalAt95 (priority 40)
    if (s.budget_session_pct >= 0.95) and (s.budget_priority == "critical"):
        return "critical_at_95"
    # rule: skipAt95 (priority 41)
    if s.budget_session_pct >= 0.95:
        return "budget_95_skipped"
    # rule: lowAt80 (priority 50)
    if (s.budget_session_pct >= 0.8) and (s.budget_priority == "low"):
        return "budget_80_low_skipped"
    # rule: lowAt50 (priority 60)
    if ((s.budget_session_pct >= 0.5) and (s.budget_priority == "low") and ((
            s.budget_feature == "watchdog") or (s.budget_feature == "summarize"))):
        return "budget_50_low_skipped"
    return "ok"


def _decide_budget_run(s: BudgetRouteInput | BudgetRunInput) -> bool:
    return ((_decide_budget_route(s) == "ok") or (_decide_budget_route(s) ==
            "ok_disabled") or (_decide_budget_route(s) == "critical_at_95"))
