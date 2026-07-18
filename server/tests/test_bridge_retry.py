"""TDD: bridge_retry.py — #05 ConnectionRefused exponential backoff."""
import time
from unittest.mock import MagicMock
from unity_mcp.bridge_retry import RetryPolicy


def _policy(max_retries=3):
    probe = MagicMock()
    probe.has_strong_busy_signal.return_value = False
    probe.is_process_dead.return_value = False
    reload_ = MagicMock()
    reload_.is_active.return_value = False
    return RetryPolicy(
        probe=probe,
        reload=reload_,
        is_retry_safe=lambda cmd: True,
        max_retries=max_retries,
    )


_FAR_FUTURE = time.monotonic() + 99999


def test_connection_refused_attempt0_retries():
    """#05: ConnectionRefused on attempt 0 → retry with 2s backoff."""
    p = _policy()
    should, delay, reason = p.decide(ConnectionRefusedError(), attempt=0, session_deadline=_FAR_FUTURE)
    assert should is True
    assert delay == 2.0
    assert reason == "connection_refused"


def test_connection_refused_attempt1_retries():
    """#05: attempt 1 → 4s backoff (exponential)."""
    p = _policy()
    should, delay, reason = p.decide(ConnectionRefusedError(), attempt=1, session_deadline=_FAR_FUTURE)
    assert should is True
    assert delay == 4.0
    assert reason == "connection_refused"


def test_connection_refused_attempt2_retries():
    """#05: attempt 2 → 8s (capped at max)."""
    p = _policy()
    should, delay, reason = p.decide(ConnectionRefusedError(), attempt=2, session_deadline=_FAR_FUTURE)
    assert should is True
    assert delay == 8.0
    assert reason == "connection_refused"


def test_connection_refused_at_max_retries_gives_up():
    """#05: at max_retries boundary → stop retrying."""
    p = _policy(max_retries=3)
    should, _, _ = p.decide(ConnectionRefusedError(), attempt=3, session_deadline=_FAR_FUTURE)
    assert should is False


def test_connection_refused_past_deadline_gives_up():
    """#05: ConnectionRefused after session deadline → stop."""
    p = _policy()
    past = time.monotonic() - 1
    should, _, _ = p.decide(ConnectionRefusedError(), attempt=0, session_deadline=past)
    assert should is False


def test_connection_refused_process_dead_gives_up():
    """#05: ConnectionRefused + is_process_dead() → bail immediately, no 14s wait."""
    p = _policy()
    p.probe.is_process_dead.return_value = True
    should, delay, reason = p.decide(ConnectionRefusedError(), attempt=0, session_deadline=_FAR_FUTURE)
    assert should is False
    assert delay == 0.0
    assert reason == "process_dead"


def test_other_error_unaffected():
    """#05: OSError (not ConnectionRefused) falls through to existing transient path."""
    p = _policy()
    should, delay, reason = p.decide(OSError("other"), attempt=0, session_deadline=_FAR_FUTURE)
    assert should is True
    assert reason == "transient"
