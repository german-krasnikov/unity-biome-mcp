# Generated from dsl/decisions/connstatus.logic
# source-sha256=abed4114a1c50a1660815d4c58ff817a22e13e58519660b8a7ee3edc05ace2ed
# Edit the source, then run biome generate.

from __future__ import annotations

import math
import typing as _typing
from dataclasses import dataclass


class LogicInputError(ValueError):
    """An input outside the Logic contract (LOGIC_INPUT_CONTRACT)."""


class ConnStatusValues:
    CONNECTED = "connected"
    DISCONNECTED = "disconnected"
    DOMAIN_RELOADING = "domainReloading"
    DORMANT = "dormant"
    RECONNECTING = "reconnecting"
    WAKING = "waking"


@dataclass(frozen=True, slots=True)
class ConnStatusInput:
    conn_state: str
    conn_writer_live: bool


def conn_status(inp: ConnStatusInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("conn.state", inp.conn_state, "enum", values=("disconnected", "connected",
            "domainReloading", "failed", "dormant", "waking",))
    _check("conn.writerLive", inp.conn_writer_live, "bool")
    return _decide_conn_status(inp)


class TransportStatusValues:
    CONNECTED = "connected"
    DORMANT = "dormant"
    FAILED = "failed"
    RECONNECTING = "reconnecting"
    WAKING = "waking"


@dataclass(frozen=True, slots=True)
class TransportStatusInput:
    conn_state: str
    conn_writer_live: bool


def transport_status(inp: TransportStatusInput) -> str:
    """Checks every input against the contract, then decides."""
    _check("conn.state", inp.conn_state, "enum", values=("disconnected", "connected",
            "domainReloading", "failed", "dormant", "waking",))
    _check("conn.writerLive", inp.conn_writer_live, "bool")
    return _decide_transport_status(inp)


__all__ = [
    "LogicInputError",
    "ConnStatusValues",
    "ConnStatusInput",
    "conn_status",
    "TransportStatusValues",
    "TransportStatusInput",
    "transport_status",
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


def _decide_conn_status(s: ConnStatusInput | TransportStatusInput) -> str:
    # rule: dormant (priority 10)
    if s.conn_state == "dormant":
        return "dormant"
    # rule: waking (priority 20)
    if s.conn_state == "waking":
        return "waking"
    # rule: live (priority 30)
    if s.conn_writer_live is True:
        return "connected"
    # rule: failed (priority 40)
    if s.conn_state == "failed":
        return "disconnected"
    # rule: reload (priority 50)
    if s.conn_state == "domainReloading":
        return "domainReloading"
    return "reconnecting"


def _decide_transport_status(s: ConnStatusInput | TransportStatusInput) -> str:
    # rule: dormant (priority 10)
    if s.conn_state == "dormant":
        return "dormant"
    # rule: waking (priority 20)
    if s.conn_state == "waking":
        return "waking"
    # rule: live (priority 30)
    if s.conn_writer_live is True:
        return "connected"
    # rule: failed (priority 40)
    if s.conn_state == "failed":
        return "failed"
    return "reconnecting"
