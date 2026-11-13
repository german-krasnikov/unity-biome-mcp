# Generated from dsl/decisions/injectstate.logic
# source-sha256=e4ff9be5da011ac77ebc5bbc0943dbcf576efd55481018c9a50e4acb7a1fde76
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class InjectStateValues:
    INJECT = "inject"
    SKIP = "skip"


@dataclass(frozen=True, slots=True)
class InjectStateInput:
    state_auto_state_off: bool
    state_batch_readonly: bool
    state_cmd_is_batch: bool
    state_cmd_is_playtest_scenario: bool
    state_cmd_is_screenshot: bool
    state_cmd_is_write: bool
    state_cmd_present: bool
    state_due_by_count: bool
    state_gap_ok: bool


def inject_state(inp: InjectStateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("state.autoStateOff", inp.state_auto_state_off, "bool")
    _check("state.batchReadonly", inp.state_batch_readonly, "bool")
    _check("state.cmdIsBatch", inp.state_cmd_is_batch, "bool")
    _check("state.cmdIsPlaytestScenario", inp.state_cmd_is_playtest_scenario, "bool")
    _check("state.cmdIsScreenshot", inp.state_cmd_is_screenshot, "bool")
    _check("state.cmdIsWrite", inp.state_cmd_is_write, "bool")
    _check("state.cmdPresent", inp.state_cmd_present, "bool")
    _check("state.dueByCount", inp.state_due_by_count, "bool")
    _check("state.gapOk", inp.state_gap_ok, "bool")
    return _decide_inject_state(inp)


__all__ = ["LogicInputError", "InjectStateValues", "InjectStateInput", "inject_state"]


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


def _decide_inject_state(s: InjectStateInput) -> str:
    # rule: autoStateOff (priority 10)
    if s.state_auto_state_off is True:
        return "skip"
    # rule: screenshotCmd (priority 20)
    if s.state_cmd_is_screenshot is True:
        return "skip"
    # rule: playtestScenario (priority 30)
    if s.state_cmd_is_playtest_scenario is True:
        return "skip"
    # rule: nonWriteCmd (priority 40)
    if (s.state_cmd_present is True) and (s.state_cmd_is_write is not True):
        return "skip"
    # rule: readonlyBatch (priority 50)
    if (s.state_cmd_is_batch is True) and (s.state_batch_readonly is True):
        return "skip"
    # rule: periodicDue (priority 60)
    if (s.state_due_by_count is True) and (s.state_gap_ok is True):
        return "inject"
    return "skip"
