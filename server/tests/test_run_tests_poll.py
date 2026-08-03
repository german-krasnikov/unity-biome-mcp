"""Tests for immediate, exactly-once test dispatch."""

import asyncio
import json
from unittest.mock import AsyncMock, patch

import pytest

import unity_mcp.tools.testing as testing


@pytest.mark.asyncio
@pytest.mark.parametrize("request_id", ["", "bad|id", "../bad", "line\nbreak"])
async def test_run_tests_rejects_unsafe_request_identity_without_io(request_id):
    send = AsyncMock()
    with patch.object(testing, "_send", send):
        result = await testing.run_tests(request_id=request_id)

    assert result.startswith("BLOCKED: request_id")
    send.assert_not_awaited()


def _make_send(responses: list):
    iterator = iter(responses)

    async def _send(cmd, args, **kwargs):
        value = next(iterator)
        if isinstance(value, Exception):
            raise value
        return value

    return _send


async def test_connection_error_is_unknown_not_started(monkeypatch):
    monkeypatch.setattr(
        testing, "_send", _make_send(["none", ConnectionError("closed")])
    )

    result = await testing.run_tests(request_id="req-lost")

    assert result == "START-UNKNOWN|request_id=req-lost|reason=ConnectionError"
    assert "tests-started" not in result


async def test_timeout_is_unknown_not_started(monkeypatch):
    monkeypatch.setattr(
        testing, "_send", _make_send(["none", asyncio.TimeoutError()])
    )

    result = await testing.run_tests(request_id="req-timeout")

    assert result == "START-UNKNOWN|request_id=req-timeout|reason=TimeoutError"


async def test_valid_ack_is_returned_immediately(monkeypatch):
    ack = (
        "tests-started|request_id=req-1|run_id=run-1|utf_guid=utf-1|state=dispatched"
    )
    calls = []

    async def send(cmd, args, **kwargs):
        calls.append((cmd, args, kwargs))
        if cmd == "resolve_test_request":
            return "none"
        return ack

    monkeypatch.setattr(testing, "_send", send)
    result = await testing.run_tests(request_id="req-1")

    assert result == ack
    assert calls == [
        ("resolve_test_request", {"request_id": "req-1"}, {}),
        (
            "run_tests",
            {"mode": "EditMode", "request_id": "req-1"},
            {"timeout": 8.0},
        ),
    ]


async def test_legacy_full_result_is_not_misreported_as_new_ack(monkeypatch):
    monkeypatch.setattr(
        testing, "_send", _make_send(["none", "tests: 100 passed, 0 failed"])
    )

    result = await testing.run_tests(request_id="req-old")

    assert result == "START-UNKNOWN|request_id=req-old|reason=invalid-ack"


async def test_pending_or_none_are_not_fabricated_as_started(monkeypatch):
    for response in ("pending", "none"):
        monkeypatch.setattr(testing, "_send", _make_send(["none", response]))
        result = await testing.run_tests(request_id=f"req-{response}")
        assert result == (
            f"START-UNKNOWN|request_id=req-{response}|reason=invalid-ack"
        )


async def test_filter_and_stable_request_id_are_forwarded(monkeypatch):
    captured = {}
    ack = (
        "tests-started|request_id=req-filter|run_id=run-filter|utf_guid=utf-filter"
        "|state=dispatched"
    )

    async def send(cmd, args, **kwargs):
        if cmd == "resolve_test_request":
            return "none"
        captured[cmd] = args
        return ack

    monkeypatch.setattr(testing, "_send", send)
    await testing.run_tests(
        mode="EditMode", filter="MyTest|OtherTest", request_id="req-filter"
    )

    assert captured["run_tests"] == {
        "mode": "EditMode",
        "filter": "MyTest|OtherTest",
        "request_id": "req-filter",
    }


async def test_mismatched_request_id_is_rejected(monkeypatch):
    ack = (
        "tests-started|request_id=other|run_id=run-1|utf_guid=utf-1|state=dispatched"
    )
    monkeypatch.setattr(testing, "_send", _make_send(["none", ack]))

    result = await testing.run_tests(request_id="expected")

    assert result == "START-UNKNOWN|request_id=expected|reason=invalid-ack"


async def test_correlated_terminal_dispatch_failure_is_not_lost(monkeypatch):
    status = (
        "test-request|request_id=req-failed|run_id=run-failed"
        "|state=terminal|outcome=dispatch_failed"
    )
    monkeypatch.setattr(testing, "_send", _make_send(["none", status]))

    result = await testing.run_tests(request_id="req-failed")

    assert result == status


async def test_correlated_finalizing_status_is_not_lost(monkeypatch):
    status = (
        "test-request|request_id=req-final|run_id=run-final"
        "|state=finalizing|outcome=dispatch_failed"
    )
    monkeypatch.setattr(testing, "_send", _make_send(["none", status]))

    result = await testing.run_tests(request_id="req-final")

    assert result == status


async def test_invalid_correlated_status_is_rejected(monkeypatch):
    status = (
        "test-request|request_id=req-bad|run_id=run-bad"
        "|state=terminal|outcome=made_up"
    )
    monkeypatch.setattr(testing, "_send", _make_send(["none", status]))

    result = await testing.run_tests(request_id="req-bad")

    assert result == "START-UNKNOWN|request_id=req-bad|reason=invalid-ack"


async def test_existing_request_resolves_before_preflight_or_redispatch(monkeypatch):
    ack = (
        "tests-started|request_id=req-existing|run_id=run-existing"
        "|utf_guid=utf-existing|state=dispatched"
    )
    snapshot = json.dumps({
        "request_id": "req-existing",
        "run_id": "run-existing",
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
    })
    send = _make_send([ack, snapshot])
    monkeypatch.setattr(testing, "_send", send)

    async def blocked_preflight():
        raise AssertionError("preflight must not run for an existing request")

    monkeypatch.setattr(testing, "_preflight", blocked_preflight)

    assert await testing.run_tests(request_id="req-existing") == ack


async def test_existing_request_with_changed_intent_is_rejected(monkeypatch):
    ack = (
        "tests-started|request_id=req-conflict|run_id=run-conflict"
        "|utf_guid=utf-conflict|state=dispatched"
    )
    snapshot = json.dumps({
        "request_id": "req-conflict",
        "run_id": "run-conflict",
        "source": "mcp",
        "mode": "EditMode",
        "filter": "Original.Filter",
    })
    send = AsyncMock(side_effect=[ack, snapshot])
    monkeypatch.setattr(testing, "_send", send)
    monkeypatch.setattr(
        testing, "_preflight",
        AsyncMock(side_effect=AssertionError("conflict must not reach preflight")),
    )

    result = await testing.run_tests(
        mode="EditMode", filter="Different.Filter", request_id="req-conflict"
    )

    assert result.startswith("BLOCKED: request_id is already bound")
    assert [call.args[0] for call in send.await_args_list] == [
        "resolve_test_request", "get_test_run"
    ]


async def test_prepared_request_intent_is_resumed_with_same_identity(monkeypatch):
    prepared = (
        "test-request|request_id=req-prepared|run_id=run-prepared"
        "|state=prepared|outcome="
    )
    ack = (
        "tests-started|request_id=req-prepared|run_id=run-prepared"
        "|utf_guid=utf-prepared|state=dispatched"
    )
    send = AsyncMock(side_effect=[prepared, ack])
    monkeypatch.setattr(testing, "_send", send)
    monkeypatch.setattr(testing, "_preflight", AsyncMock(return_value=None))

    result = await testing.run_tests(
        mode="PlayMode", filter="retry-must-not-replace-intent",
        request_id="req-prepared"
    )

    assert result == ack
    assert send.await_args_list[0].args == (
        "resolve_test_request", {"request_id": "req-prepared"}
    )
    assert send.await_args_list[1].args[0] == "run_tests"
    assert send.await_args_list[1].args[1]["request_id"] == "req-prepared"


async def test_unreadable_request_resolution_never_risks_redispatch(monkeypatch):
    monkeypatch.setattr(
        testing, "_send", _make_send([ConnectionError("reload")])
    )

    result = await testing.run_tests(request_id="req-unknown")

    assert result == (
        "START-UNKNOWN|request_id=req-unknown|reason=resolve-ConnectionError"
    )
