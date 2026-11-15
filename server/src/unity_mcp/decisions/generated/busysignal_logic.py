# Generated from dsl/decisions/busysignal.logic
# source-sha256=475f882276cb0ff6777413df62324fe970e49deac95ec47df2a4a1b9c40eaeb8
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class BusySignalValues:
    BUSY = "busy"
    NOT_BUSY = "notBusy"


@dataclass(frozen=True, slots=True)
class BusySignalInput:
    probe_has_port: bool
    probe_has_wedge: bool
    probe_lock_file_exists: bool
    probe_process_dead: bool
    probe_startup_in_progress: bool
    probe_state_busy: bool
    probe_state_present: bool
    probe_state_stale: bool


def busy_signal(inp: BusySignalInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("probe.hasPort", inp.probe_has_port, "bool")
    _check("probe.hasWedge", inp.probe_has_wedge, "bool")
    _check("probe.lockFileExists", inp.probe_lock_file_exists, "bool")
    _check("probe.processDead", inp.probe_process_dead, "bool")
    _check("probe.startupInProgress", inp.probe_startup_in_progress, "bool")
    _check("probe.stateBusy", inp.probe_state_busy, "bool")
    _check("probe.statePresent", inp.probe_state_present, "bool")
    _check("probe.stateStale", inp.probe_state_stale, "bool")
    return _decide_busy_signal(inp)


__all__ = ["LogicInputError", "BusySignalValues", "BusySignalInput", "busy_signal"]


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


def _decide_busy_signal(s: BusySignalInput) -> str:
    # rule: freshBusy (priority 10)
    if ((s.probe_has_port is True) and (s.probe_state_present is True) and (
            s.probe_state_stale is not True) and (s.probe_state_busy is True)):
        return "busy"
    # rule: staleBusyDead (priority 20)
    if ((s.probe_has_port is True) and (s.probe_state_present is True) and (
            s.probe_state_stale is True) and (s.probe_state_busy is True) and (
            s.probe_process_dead is True)):
        return "notBusy"
    # rule: staleBusyWedge (priority 21)
    if ((s.probe_has_port is True) and (s.probe_state_present is True) and (
            s.probe_state_stale is True) and (s.probe_state_busy is True) and (
            s.probe_has_wedge is True)):
        return "notBusy"
    # rule: staleBusyAlive (priority 22)
    if ((s.probe_has_port is True) and (s.probe_state_present is True) and (
            s.probe_state_stale is True) and (s.probe_state_busy is True)):
        return "busy"
    # rule: stateNotBusy (priority 30)
    if (s.probe_has_port is True) and (s.probe_state_present is True):
        return "notBusy"
    # rule: noStateDead (priority 40)
    if ((s.probe_has_port is True) and (s.probe_state_present is not True) and (
            s.probe_process_dead is True)):
        return "notBusy"
    # rule: noStateStartup (priority 41)
    if ((s.probe_has_port is True) and (s.probe_state_present is not True) and (
            s.probe_startup_in_progress is True)):
        return "busy"
    # rule: lockFile (priority 50)
    if s.probe_lock_file_exists is True:
        return "busy"
    return "notBusy"
