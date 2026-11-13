# Generated from dsl/decisions/askroute.logic
# source-sha256=e87b580123d982a96bbcc38610dd8b16a00ed8c4165277f89225a372d5706c11
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class AskRouteValues:
    BROKEN_REFS = "BROKEN_REFS"
    COMPILE_ERRORS = "COMPILE_ERRORS"
    EDITOR_STATE = "EDITOR_STATE"
    SCENE_HEALTH = "SCENE_HEALTH"
    SCENE_QUERY = "SCENE_QUERY"
    MUTATING = "mutating"
    OUT_OF_SCOPE = "out_of_scope"


@dataclass(frozen=True, slots=True)
class AskRouteInput:
    ask_broken_refs: bool
    ask_compile: bool
    ask_editor_state: bool
    ask_mutating: bool
    ask_scene_health: bool
    ask_unity_noun: bool


def ask_route(inp: AskRouteInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("ask.brokenRefs", inp.ask_broken_refs, "bool")
    _check("ask.compile", inp.ask_compile, "bool")
    _check("ask.editorState", inp.ask_editor_state, "bool")
    _check("ask.mutating", inp.ask_mutating, "bool")
    _check("ask.sceneHealth", inp.ask_scene_health, "bool")
    _check("ask.unityNoun", inp.ask_unity_noun, "bool")
    return _decide_ask_route(inp)


__all__ = ["LogicInputError", "AskRouteValues", "AskRouteInput", "ask_route"]


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


def _decide_ask_route(s: AskRouteInput) -> str:
    # rule: mutating (priority 10)
    if s.ask_mutating is True:
        return "mutating"
    # rule: noNoun (priority 20)
    if s.ask_unity_noun is not True:
        return "out_of_scope"
    # rule: brokenRefs (priority 30)
    if s.ask_broken_refs is True:
        return "BROKEN_REFS"
    # rule: sceneHealth (priority 40)
    if s.ask_scene_health is True:
        return "SCENE_HEALTH"
    # rule: editorState (priority 50)
    if s.ask_editor_state is True:
        return "EDITOR_STATE"
    # rule: compile (priority 60)
    if s.ask_compile is True:
        return "COMPILE_ERRORS"
    return "SCENE_QUERY"
