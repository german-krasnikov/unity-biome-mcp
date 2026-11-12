# Generated from dsl/decisions/versionorder.logic
# source-sha256=98759a13063a38f1172f16849cd254912daf41c78eaa38802818008f2245ff4c
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


@dataclass(frozen=True, slots=True)
class IsNewerInput:
    ver_cand_pre: bool
    ver_cur_pre: bool
    ver_parsed: bool
    ver_triple: str


def is_newer(inp: IsNewerInput) -> bool:
    """Checks every input against the contract, then decides."""
    _check("ver.candPre", inp.ver_cand_pre, "bool")
    _check("ver.curPre", inp.ver_cur_pre, "bool")
    _check("ver.parsed", inp.ver_parsed, "bool")
    _check("ver.triple", inp.ver_triple, "enum", values=("less", "equal", "greater",))
    return _decide_is_newer(inp)


__all__ = ["LogicInputError", "IsNewerInput", "is_newer"]


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


def _decide_is_newer(s: IsNewerInput) -> bool:
    # rule: unparsed (priority 5)
    if s.ver_parsed is not True:
        return False
    # rule: stableUserNeverSeesRc (priority 10)
    if (s.ver_cand_pre is True) and (s.ver_cur_pre is not True):
        return False
    # rule: higher (priority 20)
    if s.ver_triple == "greater":
        return True
    return (((s.ver_triple == "equal") and (s.ver_cur_pre is True)) and (s.ver_cand_pre
            is not True))
