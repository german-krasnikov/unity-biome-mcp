# Generated from dsl/decisions/mutationpace.logic
# source-sha256=33d58beee46ebee2e01dd51aa73ac2d522881d6fcc3dfc3399a0b7151def8022
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class MutationPaceValues:
    NONE = "none"
    NUDGE = "nudge"


@dataclass(frozen=True, slots=True)
class MutationPaceInput:
    mupace_at_checkpoint: bool
    mupace_is_batch_readonly: bool
    mupace_is_neutral: bool
    mupace_is_write: bool


def mutation_pace(inp: MutationPaceInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("mupace.atCheckpoint", inp.mupace_at_checkpoint, "bool")
    _check("mupace.isBatchReadonly", inp.mupace_is_batch_readonly, "bool")
    _check("mupace.isNeutral", inp.mupace_is_neutral, "bool")
    _check("mupace.isWrite", inp.mupace_is_write, "bool")
    return _decide_mutation_pace(inp)


__all__ = [
    "LogicInputError",
    "MutationPaceValues",
    "MutationPaceInput",
    "mutation_pace",
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


def _decide_mutation_pace(s: MutationPaceInput) -> str:
    # rule: neutral (priority 10)
    if s.mupace_is_neutral is True:
        return "none"
    # rule: batchReadonly (priority 11)
    if s.mupace_is_batch_readonly is True:
        return "none"
    # rule: notWrite (priority 12)
    if s.mupace_is_write is not True:
        return "none"
    # rule: checkpoint (priority 20)
    if s.mupace_at_checkpoint is True:
        return "nudge"
    return "none"
