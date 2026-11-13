# Generated from dsl/decisions/prefetch.logic
# source-sha256=6854ee350feda6bd0f19ac7b25b64a16f8dc76b8eef3b10004986b3c61d80434
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class PrefetchServeValues:
    SERVE = "serve"
    SKIP = "skip"


@dataclass(frozen=True, slots=True)
class PrefetchServeInput:
    pf_cache_configured: bool
    pf_cmd_cacheable: bool
    pf_scenario_uncertain: bool


def prefetch_serve(inp: PrefetchServeInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("pf.cacheConfigured", inp.pf_cache_configured, "bool")
    _check("pf.cmdCacheable", inp.pf_cmd_cacheable, "bool")
    _check("pf.scenarioUncertain", inp.pf_scenario_uncertain, "bool")
    return _decide_prefetch_serve(inp)


class PrefetchFireValues:
    FIRE = "fire"
    SKIP = "skip"


@dataclass(frozen=True, slots=True)
class PrefetchFireInput:
    pf_cache_configured: bool
    pf_cmd_is_write: bool
    pf_cmd_scene_neutral: bool
    pf_scenario_uncertain: bool


def prefetch_fire(inp: PrefetchFireInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("pf.cacheConfigured", inp.pf_cache_configured, "bool")
    _check("pf.cmdIsWrite", inp.pf_cmd_is_write, "bool")
    _check("pf.cmdSceneNeutral", inp.pf_cmd_scene_neutral, "bool")
    _check("pf.scenarioUncertain", inp.pf_scenario_uncertain, "bool")
    return _decide_prefetch_fire(inp)


__all__ = [
    "LogicInputError",
    "PrefetchServeValues",
    "PrefetchServeInput",
    "prefetch_serve",
    "PrefetchFireValues",
    "PrefetchFireInput",
    "prefetch_fire",
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


def _decide_prefetch_serve(s: PrefetchServeInput) -> str:
    # rule: notConfigured (priority 10)
    if s.pf_cache_configured is not True:
        return "skip"
    # rule: notCacheable (priority 20)
    if s.pf_cmd_cacheable is not True:
        return "skip"
    # rule: uncertain (priority 30)
    if s.pf_scenario_uncertain is True:
        return "skip"
    # rule: eligible (priority 40)
    if ((s.pf_cache_configured is True) and (s.pf_cmd_cacheable is True) and (
            s.pf_scenario_uncertain is not True)):
        return "serve"
    return "skip"


def _decide_prefetch_fire(s: PrefetchFireInput) -> str:
    # rule: uncertain (priority 10)
    if s.pf_scenario_uncertain is True:
        return "skip"
    # rule: notWrite (priority 20)
    if s.pf_cmd_is_write is not True:
        return "skip"
    # rule: sceneNeutral (priority 21)
    if s.pf_cmd_scene_neutral is True:
        return "skip"
    # rule: notConfigured (priority 30)
    if s.pf_cache_configured is not True:
        return "skip"
    # rule: eligible (priority 40)
    if ((s.pf_scenario_uncertain is not True) and (s.pf_cmd_is_write is True) and (
            s.pf_cmd_scene_neutral is not True) and (s.pf_cache_configured is True)):
        return "fire"
    return "skip"
