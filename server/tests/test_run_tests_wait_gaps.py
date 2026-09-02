"""Boundary cases for durable test-run polling."""

import json
from unittest.mock import AsyncMock, patch

import unity_mcp.tools.testing as testing

REQ = "req-gap"
RUN = "run-gap"
ACK = (
    f"tests-started|request_id={REQ}|run_id={RUN}"
    "|utf_guid=utf-gap|state=dispatched"
)


async def _started(mode, filter=None, request_id=None):
    return ACK


async def test_none_snapshot_is_not_terminal():
    terminal = json.dumps({
        "request_id": REQ,
        "run_id": RUN,
        "utf_guid": "utf-gap",
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
        "utf_xml_scope": "complete",
        "expected_count": 6964,
        "declared_expected_count": 6964,
        "readable_manifest_count": 6964,
        "completed_expected_count": 6964,
        "unique_terminal_count": 6964,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": 6963,
        "failed": 0,
        "skipped": 1,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "issues": [],
    })
    with patch.object(testing, "run_tests", _started), \
         patch.object(
             testing, "_fetch_test_run_json", AsyncMock(side_effect=["none", terminal])
         ), patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=2.0, poll_interval=1.0
        )

    assert json.loads(result)["outcome"] == "passed"


async def test_all_none_timeout_preserves_exact_run_id():
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(return_value="none")), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=0.001, poll_interval=1.0
        )

    assert result == f"TIMEOUT|request_id={REQ}|run_id={RUN}|snapshot=none"


async def test_multiple_reload_failures_do_not_replace_last_snapshot():
    running = json.dumps({
        "request_id": REQ,
        "run_id": RUN,
        "state": "running",
        "counts": {"finished": 27},
    })
    poll = AsyncMock(side_effect=[
        running,
        ConnectionError("reload 1"),
        ConnectionError("reload 2"),
    ])
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", poll), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=3.0, poll_interval=1.0
        )

    assert result.startswith(f"TIMEOUT|request_id={REQ}|run_id={RUN}|snapshot=")
    assert '"finished":27' in result


def _running_with_health(health: str) -> str:
    return json.dumps({
        "request_id": REQ,
        "run_id": RUN,
        "state": "running",
        "health": health,
    })


async def test_timeout_includes_health_streak_after_threshold():
    """ARC-1 P2: 3+ consecutive suspected_stall polls annotate the TIMEOUT."""
    stall = _running_with_health("suspected_stall")
    poll = AsyncMock(side_effect=[stall, stall, stall])
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", poll), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=2.0, poll_interval=1.0
        )

    assert result.startswith(f"TIMEOUT|request_id={REQ}|run_id={RUN}|snapshot=")
    prefix, _, count = result.rpartition("|health_streak=")
    assert prefix, result
    assert int(count) == 3


async def test_timeout_omits_health_streak_below_threshold():
    """A single stall poll must not trigger the diagnostic suffix."""
    stall = _running_with_health("suspected_stall")
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", AsyncMock(return_value=stall)), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=0.001, poll_interval=1.0
        )

    assert result.startswith(f"TIMEOUT|request_id={REQ}|run_id={RUN}|snapshot=")
    assert "health_streak=" not in result


async def test_health_streak_resets_on_healthy_poll():
    """A healthy poll between stalls proves the streak resets, not accumulates."""
    stall = _running_with_health("suspected_stall")
    healthy = _running_with_health("healthy")
    poll = AsyncMock(side_effect=[stall, stall, healthy, stall])
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", poll), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=3.0, poll_interval=1.0
        )

    assert "health_streak=" not in result


async def test_health_streak_accumulates_on_swallowed_poll_exception():
    """Transport errors swallowed by `except Exception: current = ""` still
    count toward the diagnostic streak -- Unity may be alive but polls keep
    failing, which is exactly the case this diagnostic exists to surface.
    """
    poll = AsyncMock(side_effect=[
        ConnectionError("reload 1"),
        ConnectionError("reload 2"),
        ConnectionError("reload 3"),
    ])
    with patch.object(testing, "run_tests", _started), \
         patch.object(testing, "_fetch_test_run_json", poll), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(
            request_id=REQ, timeout=2.0, poll_interval=1.0
        )

    assert result.startswith(f"TIMEOUT|request_id={REQ}|run_id={RUN}|snapshot=none")
    assert result.endswith("|health_streak=3")


async def test_generated_request_id_is_reused_for_dispatch_and_resolution(monkeypatch):
    seen = []

    async def send(cmd, args, **kwargs):
        seen.append((cmd, dict(args)))
        if cmd == "resolve_test_request":
            return "none"
        if cmd == "run_tests":
            raise ConnectionError("ACK lost")
        raise AssertionError(cmd)

    monkeypatch.setattr(testing, "_send", send)
    monkeypatch.setattr(testing, "_new_request_id", lambda: REQ)
    with patch("unity_mcp.tools.diagnose.diagnose", AsyncMock(return_value="CLEAN")), \
         patch("asyncio.sleep", AsyncMock()):
        result = await testing.run_tests_wait(timeout=0.001, poll_interval=1.0)

    assert result == f"TIMEOUT|request_id={REQ}|run_id=unknown|snapshot=none"
    assert seen[0] == ("resolve_test_request", {"request_id": REQ})
    assert seen[1][0] == "run_tests"
    assert seen[1][1]["request_id"] == REQ
    assert seen[2] == ("resolve_test_request", {"request_id": REQ})
