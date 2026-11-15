# Generated from dsl/decisions/retry.logic
# source-sha256=0edb4f7ac525a8657f2b9a4d76653dfffcf1219ee96cc167e31bf0b0b1db28cd
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class RetryGateValues:
    BUSY = "busy"
    CAPACITY_BUSY = "capacity_busy"
    DEADLINE = "deadline"
    DOMAIN_RELOAD = "domain_reload"
    IDENTITY_MISMATCH = "identity_mismatch"
    MAX_RETRIES = "max_retries"
    OBSERVE = "observe"
    UNSAFE_SENT = "unsafe_sent"


@dataclass(frozen=True, slots=True)
class RetryGateInput:
    retry_attempt: float
    retry_deadline_passed: bool
    retry_delivery_sent: bool
    retry_error_kind: str
    retry_max_retries: float
    retry_reload_active: bool
    retry_retry_safe: bool


def retry_gate(inp: RetryGateInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("retry.attempt", inp.retry_attempt, "number", lo=0, hi=1000000, integer=True)
    _check("retry.deadlinePassed", inp.retry_deadline_passed, "bool")
    _check("retry.deliverySent", inp.retry_delivery_sent, "bool")
    _check("retry.errorKind", inp.retry_error_kind, "enum", values=("identityMismatch",
            "capacityBusy", "domainReload", "connectionRefused", "other",))
    _check("retry.maxRetries", inp.retry_max_retries, "number", lo=0, hi=1000000,
            integer=True)
    _check("retry.reloadActive", inp.retry_reload_active, "bool")
    _check("retry.retrySafe", inp.retry_retry_safe, "bool")
    return _decide_retry_gate(inp)


class RetryReasonValues:
    BUSY = "busy"
    CONNECTION_REFUSED = "connection_refused"
    GRACE_EXPIRED = "grace_expired"
    PROCESS_DEAD = "process_dead"
    TRANSIENT = "transient"


@dataclass(frozen=True, slots=True)
class RetryReasonInput:
    retry_attempt: float
    retry_error_kind: str
    retry_probe_busy: bool
    retry_process_dead: bool


def retry_reason(inp: RetryReasonInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("retry.attempt", inp.retry_attempt, "number", lo=0, hi=1000000, integer=True)
    _check("retry.errorKind", inp.retry_error_kind, "enum", values=("identityMismatch",
            "capacityBusy", "domainReload", "connectionRefused", "other",))
    _check("retry.probeBusy", inp.retry_probe_busy, "bool")
    _check("retry.processDead", inp.retry_process_dead, "bool")
    return _decide_retry_reason(inp)


__all__ = [
    "LogicInputError",
    "RetryGateValues",
    "RetryGateInput",
    "retry_gate",
    "RetryReasonValues",
    "RetryReasonInput",
    "retry_reason",
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


def _decide_retry_gate(s: RetryGateInput) -> str:
    # rule: unsafeSent (priority 5)
    if (s.retry_delivery_sent is True) and (s.retry_retry_safe is not True):
        return "unsafe_sent"
    # rule: maxRetries (priority 10)
    if s.retry_attempt >= s.retry_max_retries:
        return "max_retries"
    # rule: deadline (priority 20)
    if s.retry_deadline_passed is True:
        return "deadline"
    # rule: identity (priority 30)
    if s.retry_error_kind == "identityMismatch":
        return "identity_mismatch"
    # rule: capacity (priority 40)
    if s.retry_error_kind == "capacityBusy":
        return "capacity_busy"
    # rule: reload (priority 50)
    if s.retry_error_kind == "domainReload":
        return "domain_reload"
    # rule: reloadWindow (priority 60)
    if s.retry_reload_active is True:
        return "busy"
    return "observe"


def _decide_retry_reason(s: RetryReasonInput) -> str:
    # rule: compiling (priority 60)
    if s.retry_probe_busy is True:
        return "busy"
    # rule: processDead (priority 70)
    if (s.retry_error_kind == "connectionRefused") and (s.retry_process_dead is True):
        return "process_dead"
    # rule: refused (priority 80)
    if s.retry_error_kind == "connectionRefused":
        return "connection_refused"
    # rule: transient (priority 90)
    if s.retry_attempt < 1:
        return "transient"
    return "grace_expired"
