"""Tests for durable TestRunHandle — persists run metadata across timeouts.

TDD: tests written first, implementation follows.
"""

import asyncio
import json
from unittest.mock import AsyncMock

import pytest

import unity_mcp.tools.testing as testing
from unity_mcp.tools.run_handle import TestRunHandle, TestRunRegistry


# ---------------------------------------------------------------------------
# Pure registry unit tests
# ---------------------------------------------------------------------------


def test_handle_tracks_state_transitions():
    """Handle state follows dispatched→running→completed lifecycle."""
    h = TestRunHandle(run_id="r", request_id="q")
    assert h.state == "dispatched"

    h.update("running")
    assert h.state == "running"

    h.update("completed")
    assert h.state == "completed"


def test_handle_includes_expected_count():
    """Handle records expected test count when updated."""
    h = TestRunHandle(run_id="r", request_id="q")
    assert h.expected_count is None

    h.update("completed", expected_count=42)
    assert h.expected_count == 42


def test_registry_get_unknown_id_returns_none():
    """Registry returns None for an ID that was never registered."""
    reg = TestRunRegistry()
    assert reg.get("not-registered") is None


def test_registry_register_and_retrieve():
    """Registered handle is retrievable by run_id."""
    reg = TestRunRegistry()
    handle = reg.register(run_id="run-1", request_id="req-1")
    assert handle.run_id == "run-1"
    assert handle.request_id == "req-1"
    assert handle.state == "dispatched"
    assert reg.get("run-1") is handle


def test_registry_ttl_evicts_completed_handles():
    """Completed handles past TTL are evicted; active handles survive."""
    import time
    reg = TestRunRegistry(ttl=0.0)  # instant TTL

    active = reg.register("run-active", "req-a")
    done = reg.register("run-done", "req-b")
    done.update("completed")
    # Mark completed_at in the past by using negative offset
    done._completed_at = time.monotonic() - 1.0

    assert reg.get("run-active") is active
    assert reg.get("run-done") is None  # evicted


# ---------------------------------------------------------------------------
# Integration tests with testing module
# ---------------------------------------------------------------------------


def _make_send(responses: list):
    it = iter(responses)

    async def _send(cmd, args, **kwargs):
        val = next(it)
        if isinstance(val, Exception):
            raise val
        return val

    return _send


def _valid_ack(request_id: str, run_id: str) -> str:
    return (
        f"tests-started|request_id={request_id}|run_id={run_id}"
        f"|utf_guid=utf-1|state=dispatched"
    )


@pytest.fixture(autouse=True)
def _fresh_registry(monkeypatch):
    """Each test gets an isolated registry."""
    reg = TestRunRegistry()
    monkeypatch.setattr(testing, "_registry", reg)
    monkeypatch.setattr(testing, "_preflight", AsyncMock(return_value=None))
    return reg


async def test_run_tests_returns_handle_with_run_id(monkeypatch):
    """Dispatching tests registers a handle with the correct run_id."""
    ack = _valid_ack("req-1", "run-1")
    monkeypatch.setattr(
        testing, "_send",
        _make_send(["none", ack]),  # resolve → none, run_tests → ack
    )

    await testing.run_tests(request_id="req-1")

    handle = testing._registry.get("run-1")
    assert handle is not None
    assert handle.run_id == "run-1"
    assert handle.request_id == "req-1"
    assert handle.state == "dispatched"


async def test_handle_survives_timeout(monkeypatch):
    """After run_tests_wait times out, the handle is still queryable."""
    ack = _valid_ack("req-to", "run-to")

    async def mock_send(cmd, args, **kw):
        if cmd == "resolve_test_request":
            return "none"
        if cmd == "run_tests":
            return ack
        return "pending"  # get_test_run never returns terminal

    monkeypatch.setattr(testing, "_send", mock_send)

    result = await testing.run_tests_wait(
        mode="EditMode",
        filter="",
        timeout=0.05,
        poll_interval=0.01,
        request_id="req-to",
    )

    assert result.startswith("TIMEOUT")
    handle = testing._registry.get("run-to")
    assert handle is not None
    assert handle.run_id == "run-to"
    assert handle.request_id == "req-to"
    # State is still dispatched/running (not completed) — survived timeout
    assert handle.state in ("dispatched", "running")


def _terminal_snapshot(run_id: str, request_id: str, expected_count: int = 5) -> str:
    return json.dumps({
        "run_id": run_id,
        "request_id": request_id,
        "state": "terminal",
        "lifecycle": "terminal",
        "outcome": "passed",
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
        "is_terminal": True,
        "execution_finished": True,
        "cleanup_complete": True,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": True,
        "build_coherent": True,
        "utf_guid": "utf-1",
        "utf_xml_scope": "complete",
        "expected_count": expected_count,
        "declared_expected_count": expected_count,
        "readable_manifest_count": expected_count,
        "completed_expected_count": expected_count,
        "unique_terminal_count": expected_count,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": expected_count,
        "failed": 0,
        "skipped": 0,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "issues": [],
    })


async def test_handle_includes_expected_count_integration(monkeypatch):
    """Handle records expected_count once terminal snapshot is received."""
    ack = _valid_ack("req-ec", "run-ec")
    terminal = _terminal_snapshot("run-ec", "req-ec", expected_count=7)

    async def mock_send(cmd, args, **kw):
        if cmd == "resolve_test_request":
            return "none"
        if cmd == "run_tests":
            return ack
        return terminal  # immediate terminal on first poll

    monkeypatch.setattr(testing, "_send", mock_send)

    await testing.run_tests_wait(
        mode="EditMode",
        filter="",
        timeout=5.0,
        poll_interval=0.01,
        request_id="req-ec",
    )

    handle = testing._registry.get("run-ec")
    assert handle is not None
    assert handle.expected_count == 7
    assert handle.state in ("completed", "passed")


async def test_get_test_run_unknown_id_returns_not_found(monkeypatch):
    """get_test_run returns NOT_FOUND for IDs absent from registry and Unity."""
    monkeypatch.setattr(testing, "_send", AsyncMock(return_value="none"))

    result = await testing.get_test_run("totally-unknown-run-id")

    assert result.startswith("NOT_FOUND")
    assert "totally-unknown-run-id" in result
