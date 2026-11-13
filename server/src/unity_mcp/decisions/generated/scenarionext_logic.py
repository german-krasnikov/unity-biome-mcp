# Generated from dsl/decisions/scenarionext.logic
# source-sha256=ddd1a4513e28de9208ea09ce17ff7bb7303a60fa1e0cda6e24ec83563fed856c
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class ScenarioUncertainNextValues:
    CLEAR_UNCERTAIN = "clearUncertain"
    KEEP = "keep"
    MARK_UNCERTAIN = "markUncertain"


@dataclass(frozen=True, slots=True)
class ScenarioUncertainNextInput:
    scn_cmd_is_start_playtest: bool
    scn_result_is_run_id_ack: bool
    scn_result_is_terminal: bool


def scenario_uncertain_next(inp: ScenarioUncertainNextInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("scn.cmdIsStartPlaytest", inp.scn_cmd_is_start_playtest, "bool")
    _check("scn.resultIsRunIdAck", inp.scn_result_is_run_id_ack, "bool")
    _check("scn.resultIsTerminal", inp.scn_result_is_terminal, "bool")
    return _decide_scenario_uncertain_next(inp)


class SceneInvalidateGateValues:
    INVALIDATE = "invalidate"
    SKIP = "skip"


@dataclass(frozen=True, slots=True)
class SceneInvalidateGateInput:
    scn_force_invalidate_flag: bool
    scn_result_is_terminal: bool


def scene_invalidate_gate(inp: SceneInvalidateGateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("scn.forceInvalidateFlag", inp.scn_force_invalidate_flag, "bool")
    _check("scn.resultIsTerminal", inp.scn_result_is_terminal, "bool")
    return _decide_scene_invalidate_gate(inp)


__all__ = [
    "LogicInputError",
    "ScenarioUncertainNextValues",
    "ScenarioUncertainNextInput",
    "scenario_uncertain_next",
    "SceneInvalidateGateValues",
    "SceneInvalidateGateInput",
    "scene_invalidate_gate",
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


def _decide_scenario_uncertain_next(s: ScenarioUncertainNextInput) -> str:
    # rule: ackStart (priority 10)
    if (s.scn_cmd_is_start_playtest is True) and (s.scn_result_is_run_id_ack is True):
        return "markUncertain"
    # rule: terminalDone (priority 20)
    if s.scn_result_is_terminal is True:
        return "clearUncertain"
    return "keep"


def _decide_scene_invalidate_gate(s: SceneInvalidateGateInput) -> str:
    # rule: terminalInvalidate (priority 10)
    if s.scn_result_is_terminal is True:
        return "invalidate"
    # rule: forceInvalidate (priority 20)
    if s.scn_force_invalidate_flag is True:
        return "invalidate"
    return "skip"
