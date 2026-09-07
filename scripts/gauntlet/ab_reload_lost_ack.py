"""N3 T3 slice 3: lost-ACK against a non-idempotent owned counter. Pure
phase logic over injected call/send/proxy seams -- no Unity lifecycle, no
TCP proxy lifecycle (that lives in scripts/run_ab_reload_identity.py and
gauntlet.ab_reload_proxy.CounterProxy). See
Plans/N3-T3-T4-live-reload-identity.md Part 1.
"""
from __future__ import annotations

from collections.abc import Awaitable, Callable  # noqa: TC003
from dataclasses import dataclass
from typing import Protocol

from gauntlet.ab_reload_identity import ABReloadIdentityError, CallFn, read_counter
from unity_mcp.errors import UncertainDeliveryError, recovery_barrier


class ProxyEvidence(Protocol):
    forwarded: int


@dataclass
class LostAckConfig:
    port_a: int  # direct wire, bypasses the proxy entirely -- for before/after reads


@dataclass
class LostAckSeams:
    call: CallFn  # fresh direct wire to A, never through the proxy
    send_increment_via_proxy: Callable[[], Awaitable[None]]  # the one faulted send
    proxy: ProxyEvidence  # delivery trace: .forwarded, from the same CounterProxy instance


def check_effect_applied_exactly_once(counter_before: int, counter_after: int, forwarded: int) -> None:
    """Lost-ACK oracle for a non-idempotent owned counter. A repeated `SET
    x=1` could never distinguish 'lost' from 'duplicated'; a counter delta
    can: forwarded != 1 means the proxy itself forwarded more than the one
    request under test (a duplicate forward); delta == 0 means the fault
    landed before the real effect, not just the ACK (lost, not merely
    uncertain); delta > 1 means an auto-resend actually duplicated the
    effect Unity applied."""
    if forwarded != 1:
        raise ABReloadIdentityError(
            f"proxy forwarded the increment {forwarded} time(s), expected exactly 1 (duplicate forward)"
        )
    delta = counter_after - counter_before
    if delta == 0:
        raise ABReloadIdentityError(
            "effect not applied: counter did not advance after the forwarded increment"
        )
    if delta != 1:
        raise ABReloadIdentityError(
            f"duplicate increment: counter advanced by {delta}, expected exactly 1"
        )


async def run_lost_ack(config: LostAckConfig, seams: LostAckSeams) -> dict[str, object]:
    """The fault is injected AFTER the real increment executes on A and
    BEFORE its ACK reaches the caller (CounterProxy drops the reply). The
    caller must see UncertainDeliveryError -- and no auto-resend may
    follow. Oracle: the delivery trace (proxy.forwarded) AND the actual
    multiplicity (the counter's own before/after delta) both prove exactly
    one effect.
    """
    counter_before = await read_counter(seams.call, config.port_a)

    try:
        await seams.send_increment_via_proxy()
    except Exception as exc:  # noqa: BLE001 -- classified by recovery_barrier below
        barrier = recovery_barrier(exc)
        if not isinstance(barrier, UncertainDeliveryError):
            raise ABReloadIdentityError(
                f"lost-ACK increment failed with {type(exc).__name__}, not UncertainDeliveryError: {exc}"
            ) from exc
    else:
        raise ABReloadIdentityError(
            "lost-ACK increment did not raise UncertainDeliveryError -- the ACK was not actually dropped"
        )

    counter_after = await read_counter(seams.call, config.port_a)
    check_effect_applied_exactly_once(counter_before, counter_after, seams.proxy.forwarded)

    return {
        "counter_before": counter_before,
        "counter_after": counter_after,
        "forwarded": seams.proxy.forwarded,
        "uncertain_delivery_raised": True,
    }
