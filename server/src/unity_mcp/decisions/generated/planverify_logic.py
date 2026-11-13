# Generated from dsl/decisions/planverify.logic
# source-sha256=87e985eb2ecb636aa45938ec2ec16c5cffc47bb8cd245b1f6d413e087d701efe
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class PlanGateValues:
    EMPTY = "empty"
    OK = "ok"
    REJECTED = "rejected"
    TOO_LONG = "too_long"


@dataclass(frozen=True, slots=True)
class PlanGateInput:
    plan_lines: float
    plan_max_lines: float
    plan_rejected: bool


def plan_gate(inp: PlanGateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("plan.lines", inp.plan_lines, "number", lo=0, hi=1000000, integer=True)
    _check("plan.maxLines", inp.plan_max_lines, "number", lo=1, hi=1000000,
            integer=True)
    _check("plan.rejected", inp.plan_rejected, "bool")
    return _decide_plan_gate(inp)


class LineVerdictValues:
    FORBIDDEN = "forbidden"
    MISSING_KEY = "missing_key"
    OK = "ok"
    UNKNOWN_PATH = "unknown_path"


@dataclass(frozen=True, slots=True)
class LineVerdictInput:
    line_allowed: bool
    line_has_path: bool
    line_is_create: bool
    line_missing_required: float
    line_path_declared: bool


def line_verdict(inp: LineVerdictInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("line.allowed", inp.line_allowed, "bool")
    _check("line.hasPath", inp.line_has_path, "bool")
    _check("line.isCreate", inp.line_is_create, "bool")
    _check("line.missingRequired", inp.line_missing_required, "number", lo=0, hi=100,
            integer=True)
    _check("line.pathDeclared", inp.line_path_declared, "bool")
    return _decide_line_verdict(inp)


class FixGateValues:
    ASK_FIX = "ask_fix"
    DONE = "done"


@dataclass(frozen=True, slots=True)
class FixGateInput:
    exec_failed_lines: float
    exec_has_sampling: bool
    exec_partial: bool


def fix_gate(inp: FixGateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("exec.failedLines", inp.exec_failed_lines, "number", lo=0, hi=1000000,
            integer=True)
    _check("exec.hasSampling", inp.exec_has_sampling, "bool")
    _check("exec.partial", inp.exec_partial, "bool")
    return _decide_fix_gate(inp)


class FixApplyValues:
    KEEP_ORIGINAL = "keep_original"
    SEND_FIX = "send_fix"


@dataclass(frozen=True, slots=True)
class FixApplyInput:
    fix_empty: bool
    fix_verdict: str


def fix_apply(inp: FixApplyInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("fix.empty", inp.fix_empty, "bool")
    _check("fix.verdict", inp.fix_verdict, "enum", values=("ok", "empty", "rejected",
            "too_long", "forbidden", "missing_key", "unknown_path",))
    return _decide_fix_apply(inp)


__all__ = [
    "LogicInputError",
    "PlanGateValues",
    "PlanGateInput",
    "plan_gate",
    "LineVerdictValues",
    "LineVerdictInput",
    "line_verdict",
    "FixGateValues",
    "FixGateInput",
    "fix_gate",
    "FixApplyValues",
    "FixApplyInput",
    "fix_apply",
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


def _decide_plan_gate(s: PlanGateInput) -> str:
    # rule: empty (priority 10)
    if s.plan_lines == 0:
        return "empty"
    # rule: rejected (priority 20)
    if s.plan_rejected is True:
        return "rejected"
    # rule: tooLong (priority 30)
    if s.plan_lines > s.plan_max_lines:
        return "too_long"
    return "ok"


def _decide_line_verdict(s: LineVerdictInput) -> str:
    # rule: forbidden (priority 10)
    if s.line_allowed is not True:
        return "forbidden"
    # rule: missingKey (priority 20)
    if s.line_missing_required > 0:
        return "missing_key"
    # rule: unknownPath (priority 30)
    if ((s.line_is_create is not True) and (s.line_has_path is True) and (
            s.line_path_declared is not True)):
        return "unknown_path"
    return "ok"


def _decide_fix_gate(s: FixGateInput) -> str:
    # rule: complete (priority 10)
    if s.exec_partial is not True:
        return "done"
    # rule: noSampling (priority 20)
    if s.exec_has_sampling is not True:
        return "done"
    # rule: noFailedLines (priority 30)
    if s.exec_failed_lines == 0:
        return "done"
    # rule: tooMany (priority 40)
    if s.exec_failed_lines > 5:
        return "done"
    return "ask_fix"


def _decide_fix_apply(s: FixApplyInput) -> str:
    # rule: nothing (priority 10)
    if s.fix_empty is True:
        return "keep_original"
    # rule: invalid (priority 20)
    if s.fix_verdict != "ok":
        return "keep_original"
    return "send_fix"
