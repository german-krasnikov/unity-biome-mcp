# Generated from dsl/decisions/writepace.logic
# source-sha256=5dbdf6da398f0ff49ceb9bf165c075ac250076001c51fffa96c21efd27946b80
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class WritePaceValues:
    NONE = "none"
    NUDGE = "nudge"


@dataclass(frozen=True, slots=True)
class WritePaceInput:
    pace_changes_scene_state: bool
    pace_consecutive_writes: float


def write_pace(inp: WritePaceInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("pace.changesSceneState", inp.pace_changes_scene_state, "bool")
    _check("pace.consecutiveWrites", inp.pace_consecutive_writes, "number", lo=0,
            hi=1000000, integer=True)
    return _decide_write_pace(inp)


__all__ = ["LogicInputError", "WritePaceValues", "WritePaceInput", "write_pace"]


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


def _decide_write_pace(s: WritePaceInput) -> str:
    # rule: notMutation (priority 10)
    if s.pace_changes_scene_state is not True:
        return "none"
    # rule: nudge (priority 20)
    if s.pace_consecutive_writes >= 3:
        return "nudge"
    return "none"
