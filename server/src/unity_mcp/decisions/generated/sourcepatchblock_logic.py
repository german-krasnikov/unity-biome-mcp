# Generated from dsl/decisions/sourcepatchblock.logic
# source-sha256=447b271d8c7e5d5c8b1c46efadf44e37c24965357a745302f58012840638b39d
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class SourcePatchBlockValues:
    ALLOW = "allow"
    BLOCKED = "blocked"
    DISABLE_NOT_OWNED = "disableNotOwned"


@dataclass(frozen=True, slots=True)
class SourcePatchBlockInput:
    patch_explicit_owned_disable: bool
    patch_lease_held: bool
    patch_matching_disable_receipt: bool
    patch_patches_may_be_active: bool
    patch_state: str
    patch_state_is_off: bool
    patch_state_is_unavailable: bool


def source_patch_block(inp: SourcePatchBlockInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("patch.explicitOwnedDisable", inp.patch_explicit_owned_disable, "bool")
    _check("patch.leaseHeld", inp.patch_lease_held, "bool")
    _check("patch.matchingDisableReceipt", inp.patch_matching_disable_receipt, "bool")
    _check("patch.patchesMayBeActive", inp.patch_patches_may_be_active, "bool")
    _check("patch.state", inp.patch_state, "enum", values=("Off", "Unavailable",
            "OnReady", "Busy", "Disabling", "Recovery",))
    _check("patch.stateIsOff", inp.patch_state_is_off, "bool")
    _check("patch.stateIsUnavailable", inp.patch_state_is_unavailable, "bool")
    return _decide_source_patch_block(inp)


__all__ = [
    "LogicInputError",
    "SourcePatchBlockValues",
    "SourcePatchBlockInput",
    "source_patch_block",
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


def _decide_source_patch_block(s: SourcePatchBlockInput) -> str:
    # rule: disableOwned (priority 10)
    if ((s.patch_explicit_owned_disable is True) and (s.patch_state == "Disabling") and
            (s.patch_matching_disable_receipt is True) and (s.patch_lease_held is not
            True)):
        return "allow"
    # rule: disableNotOwned (priority 11)
    if s.patch_explicit_owned_disable is True:
        return "disableNotOwned"
    # rule: patchesActive (priority 20)
    if s.patch_patches_may_be_active is True:
        return "blocked"
    # rule: leaseHeld (priority 21)
    if s.patch_lease_held is True:
        return "blocked"
    # rule: stateActive (priority 22)
    if ((s.patch_state_is_off is not True) and (s.patch_state_is_unavailable is not
            True)):
        return "blocked"
    return "allow"
