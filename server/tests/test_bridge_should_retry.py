"""TDD: should_retry() decision function for UnityBridge.

RED phase — written before implementation exists.
"""
import time
from unittest.mock import patch, MagicMock
import pytest
from unity_mcp.bridge import UnityBridge, MAX_RETRIES, SESSION_TIMEOUT
from unity_mcp.bridge_socket import DomainReloadError
from helpers import make_idle_probe

# A03: this whole module asserts the real backoff formula's numeric values
# (2.0/4.0/8.0) via bridge.should_retry() -- opt out of the fast-clock fixture.
pytestmark = pytest.mark.real_clock


def _make_bridge() -> UnityBridge:
    probe = make_idle_probe()
    probe.has_strong_busy_signal.return_value = False
    return UnityBridge(probe=probe)


def _far_deadline() -> float:
    return time.monotonic() + SESSION_TIMEOUT


def _expired_deadline() -> float:
    return time.monotonic() - 1.0


# ── Test 1: DomainReloadError → retry=True ───────────────────────────────────

def test_should_retry_domain_reload_returns_true():
    """DomainReloadError: should_retry returns True and reason='domain_reload'."""
    bridge = _make_bridge()
    err = DomainReloadError("going away")

    do_retry, delay, reason = bridge.should_retry(err, attempt=0, session_deadline=_far_deadline())

    assert do_retry is True
    assert reason == "domain_reload"
    assert delay > 0


# ── Test 2: busy probe → retry=True ──────────────────────────────────────────

def test_should_retry_busy_probe_returns_true():
    """Busy probe (compile in progress): should_retry returns True with reason='busy'."""
    bridge = _make_bridge()
    bridge._probe.has_strong_busy_signal.return_value = True
    err = ConnectionRefusedError("refused")

    do_retry, delay, reason = bridge.should_retry(err, attempt=0, session_deadline=_far_deadline())

    assert do_retry is True
    assert reason == "busy"
    assert delay > 0


# ── Test 3: ConnectionRefused at attempt=0 → backoff retry (#05) ─────────────

def test_should_retry_connection_refused_attempt0():
    """#05: ConnectionRefused at attempt=0 → retry with 2s backoff (domain reload)."""
    bridge = _make_bridge()
    err = ConnectionRefusedError("refused")

    do_retry, delay, reason = bridge.should_retry(err, attempt=0, session_deadline=_far_deadline())

    assert do_retry is True
    assert delay == 2.0
    assert reason == "connection_refused"


# ── Test 4: ConnectionRefused at attempt=1 → exponential backoff ─────────────

def test_should_retry_connection_refused_attempt1():
    """#05: ConnectionRefused at attempt=1 → 4s backoff."""
    bridge = _make_bridge()
    err = ConnectionRefusedError("refused")

    do_retry, delay, reason = bridge.should_retry(err, attempt=1, session_deadline=_far_deadline())

    assert do_retry is True
    assert delay == 4.0
    assert reason == "connection_refused"


# ── Test 5: attempt >= MAX_RETRIES → stop regardless ─────────────────────────

def test_should_retry_max_retries_exceeded():
    """attempt >= MAX_RETRIES: always False, reason='max_retries'."""
    bridge = _make_bridge()
    bridge._probe.has_strong_busy_signal.return_value = True  # busy, but still stop
    err = DomainReloadError("going away")

    do_retry, delay, reason = bridge.should_retry(err, attempt=MAX_RETRIES, session_deadline=_far_deadline())

    assert do_retry is False
    assert reason == "max_retries"


# ── Test 6: deadline exceeded → stop regardless ──────────────────────────────

def test_should_retry_deadline_exceeded():
    """Expired session deadline: always False, reason='deadline'."""
    bridge = _make_bridge()
    bridge._probe.has_strong_busy_signal.return_value = True  # busy, but still stop
    err = ConnectionRefusedError("refused")

    do_retry, delay, reason = bridge.should_retry(err, attempt=0, session_deadline=_expired_deadline())

    assert do_retry is False
    assert reason == "deadline"


# ── Test 7: DomainReloadError marks _reload and sets state ───────────────────

def test_should_retry_domain_reload_marks_reload_tracker():
    """DomainReloadError side-effect: marks _reload and sets DOMAIN_RELOADING state."""
    from unity_mcp.bridge import BridgeState
    bridge = _make_bridge()
    err = DomainReloadError("going away")

    assert bridge._reload.is_active() is False
    bridge.should_retry(err, attempt=0, session_deadline=_far_deadline())

    assert bridge._reload.is_active() is True
    assert bridge._state == BridgeState.DOMAIN_RELOADING


# ── Test 8: busy delay uses exponential backoff ───────────────────────────────

def test_should_retry_busy_delay_exponential():
    """Busy retry: delay = min(2**(attempt+1), 8.0) → 2s, 4s, 8s."""
    bridge = _make_bridge()
    bridge._probe.has_strong_busy_signal.return_value = True
    err = ConnectionRefusedError("refused")

    _, d0, _ = bridge.should_retry(err, attempt=0, session_deadline=_far_deadline())
    _, d1, _ = bridge.should_retry(err, attempt=1, session_deadline=_far_deadline())
    _, d2, _ = bridge.should_retry(err, attempt=2, session_deadline=_far_deadline())

    assert d0 == 2.0   # min(2**1, 8) = 2
    assert d1 == 4.0   # min(2**2, 8) = 4
    assert d2 == 8.0   # min(2**3, 8) = 8
