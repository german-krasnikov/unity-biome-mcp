"""Retry decision policy -- extracted from UnityBridge (C8 / SOLID finding S2).

Owns transport retry timing and the Unity ``retry`` hint gate. Why a failed send retries or stops is the pair of DECISIONs retryGate
and retryReason of dsl/decisions/retry.logic: retryGate judges the facts that cost nothing (the SENT boundary, the budget, the deadline,
the kind of error, the reload window); only when it says ``observe`` does decide() read the probe (state file, pid, a TCP connect,
Editor.log) and ask retryReason. What stays here is mechanical: the kind of an exception, the probe reads, and the table from a reason
to a delay.
"""
import time
from collections.abc import Callable  # noqa: TC003
from dataclasses import dataclass

from .bridge_reload_state import DomainReloadTracker  # noqa: TC001
from .compile_state import CompileStateProbe  # noqa: TC001
from .decisions.generated.retry_logic import (
    RetryGateInput,
    RetryGateValues,
    RetryReasonInput,
    RetryReasonValues,
    retry_gate,
    retry_reason,
)

# Exponential retry backoff for domain_reload/busy/connection_refused reasons:
# BASE**(attempt+1) capped at CAP_S (2/4/8s in production). Named so
# tests/_fast_clock.py's autouse fixture can zero them out pre-construction
# instead of paying real wall-clock backoff on every retry-path test.
_RETRY_BACKOFF_BASE_S: float = 2.0
_RETRY_BACKOFF_CAP_S: float = 8.0
# Flat retry delay for the first-attempt "transient" fallback reason.
_TRANSIENT_RETRY_DELAY_S: float = 1.0
_DOMAIN = 1_000_000  # the domain of attempt and max_retries in retry.contract (UNITY_MCP_MAX_RETRIES can be anything)

# reason -> does the send go on? Every reason of retryGate and retryReason but ``observe`` has an entry (test_retry_golden.py checks it).
_RESENDS: dict[str, bool] = {
    RetryGateValues.UNSAFE_SENT: False, RetryGateValues.MAX_RETRIES: False, RetryGateValues.DEADLINE: False,
    RetryGateValues.IDENTITY_MISMATCH: False, RetryGateValues.CAPACITY_BUSY: True, RetryGateValues.DOMAIN_RELOAD: True,
    RetryGateValues.BUSY: True, RetryReasonValues.CONNECTION_REFUSED: True, RetryReasonValues.PROCESS_DEAD: False,
    RetryReasonValues.TRANSIENT: True, RetryReasonValues.GRACE_EXPIRED: False,
}


def _capped_backoff(attempt: int) -> float:
    """Exponential backoff: _RETRY_BACKOFF_BASE_S**(attempt+1), capped at _RETRY_BACKOFF_CAP_S."""
    return min(_RETRY_BACKOFF_BASE_S ** (attempt + 1), _RETRY_BACKOFF_CAP_S)


def _error_kind(error: Exception) -> str:
    """The class of failure that retry.logic tells apart (retry.errorKind), tested in the order the policy always did."""
    from .bridge_socket import DomainReloadError  # local import avoids cycle
    from .errors import CapacityBusyError, SessionIdentityMismatch  # local import avoids cycle

    if isinstance(error, SessionIdentityMismatch):
        return "identityMismatch"
    if isinstance(error, CapacityBusyError):
        return "capacityBusy"
    if isinstance(error, DomainReloadError):
        return "domainReload"
    return "connectionRefused" if isinstance(error, ConnectionRefusedError) else "other"


def _delay(reason: str, error: Exception, attempt: int) -> float:
    """Seconds to wait before the resend that ``reason`` allows (0 for a reason that stops)."""
    if reason == RetryGateValues.CAPACITY_BUSY:
        return error.retry_after_seconds  # type: ignore[attr-defined]  (CapacityBusyError says how long Unity wants)
    if reason in (RetryGateValues.DOMAIN_RELOAD, RetryGateValues.BUSY, RetryReasonValues.CONNECTION_REFUSED):
        return _capped_backoff(attempt)  # 2 / 4 / 8s
    return _TRANSIENT_RETRY_DELAY_S if reason == RetryReasonValues.TRANSIENT else 0.0


@dataclass
class RetryPolicy:
    probe: CompileStateProbe
    reload: DomainReloadTracker
    is_retry_safe: Callable[[str], bool]
    max_retries: int

    def decide(self, error: Exception, attempt: int, session_deadline: float,
               cmd: str = "", *, delivery_sent: bool = False) -> tuple[bool, float, str]:
        """Exception-path decision. Same contract/return shape as the former
        UnityBridge.should_retry() -- (should_retry, delay_s, reason).

        ``delivery_sent``: the writer accepted the frame, so the command may already have run. Then a command that is not
        retry-safe is never resent (reason ``unsafe_sent``, the first rule of retryGate), whatever the error: the bridge says
        what its delivery state is, this policy only decides. Before SENT, reconnecting is transport recovery.
        decide() has no effects: UnityBridge.should_retry() marks the reload and the probe before it asks.
        """
        kind = _error_kind(error)
        attempts, budget = min(attempt, _DOMAIN), max(0, min(self.max_retries, _DOMAIN))
        reason = retry_gate(RetryGateInput(
            retry_error_kind=kind, retry_attempt=attempts, retry_max_retries=budget,
            retry_deadline_passed=time.monotonic() >= session_deadline,
            retry_delivery_sent=delivery_sent,
            # is_retry_safe is asked only of a frame that was sent, as the bridge always did
            retry_retry_safe=not delivery_sent or bool(self.is_retry_safe(cmd)),
            retry_reload_active=bool(self.reload.is_active())))
        if reason == RetryGateValues.OBSERVE:  # nothing cheap decided: now the probes may be read
            reason = retry_reason(RetryReasonInput(
                retry_error_kind=kind, retry_attempt=attempts, retry_probe_busy=bool(self.probe_busy()),
                retry_process_dead=kind == "connectionRefused" and bool(self.probe.is_process_dead())))
        return _RESENDS[reason], _delay(reason, error, attempt), reason

    def allow_hint_retry(self, cmd: str) -> bool:
        """Hint-path decision (Unity's post-dispatch 'retry' JSON sentinel,
        ClientConnectionHandler.cs:180-187). Same is_retry_safe gate as
        decide() -- this identity is the whole point of the extraction."""
        return self.is_retry_safe(cmd)

    def probe_busy(self) -> bool:
        try:
            return self.probe.has_strong_busy_signal()
        except Exception:
            return False
