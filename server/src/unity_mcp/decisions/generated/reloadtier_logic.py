# Generated from dsl/decisions/reloadtier.logic
# source-sha256=f4106b9673384d959d58701a4eef6140cd62192d851dd93588ec4e1c38b4f908
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class ReloadRouteValues:
    GUARD_BLOCKED = "guardBlocked"
    MANUAL_REQUIRED = "manualRequired"
    T3_AHEAD = "t3Ahead"
    T4_AHEAD = "t4Ahead"
    T5_AHEAD = "t5Ahead"
    T5_GUARD = "t5Guard"


@dataclass(frozen=True, slots=True)
class ReloadRouteInput:
    tier_consent_given: bool
    tier_guard_wedged: bool
    tier_main_dead: bool
    tier_runner_active: bool


def reload_route(inp: ReloadRouteInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("tier.consentGiven", inp.tier_consent_given, "bool")
    _check("tier.guardWedged", inp.tier_guard_wedged, "bool")
    _check("tier.mainDead", inp.tier_main_dead, "bool")
    _check("tier.runnerActive", inp.tier_runner_active, "bool")
    return _decide_reload_route(inp)


__all__ = ["LogicInputError", "ReloadRouteValues", "ReloadRouteInput", "reload_route"]


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


def _decide_reload_route(s: ReloadRouteInput) -> str:
    # rule: guardBlockedNoConsent (priority 10)
    if (s.tier_guard_wedged is True) and (s.tier_consent_given is not True):
        return "guardBlocked"
    # rule: guardWithConsent (priority 11)
    if (s.tier_guard_wedged is True) and (s.tier_consent_given is True):
        return "t5Guard"
    # rule: mainAliveT3 (priority 20)
    if s.tier_main_dead is not True:
        return "t3Ahead"
    # rule: runnerT4 (priority 30)
    if s.tier_runner_active is True:
        return "t4Ahead"
    # rule: consentT5 (priority 40)
    if s.tier_consent_given is True:
        return "t5Ahead"
    return "manualRequired"
