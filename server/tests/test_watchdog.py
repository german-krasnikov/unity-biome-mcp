"""Tests for ProactiveWatchdog."""
import asyncio
import time
from unittest.mock import AsyncMock
from unity_mcp.console_levels import PROBLEM_LEVELS
from unity_mcp.watchdog import ProactiveWatchdog


# ── budget_gate ─────────────────────────────────────────────────────────────

async def test_watchdog_skips_scan_when_budget_gate_returns_false():
    """budget_gate=False must prevent _scan from being called.

    Fails if the budget_gate branch is removed: _scan would be scheduled and
    awaited, incrementing AsyncMock call count above zero.
    """
    scan_called = []

    async def fake_scan():
        scan_called.append(True)

    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send, interval=1, budget_gate=lambda: False)
    wd._scan = fake_scan  # intercept before task creation
    wd.maybe_trigger("delete_object")
    await asyncio.sleep(0)  # give event loop a turn
    assert not scan_called, "_scan must not run when budget_gate returns False"


async def test_watchdog_runs_scan_when_budget_gate_returns_true():
    """budget_gate=True (default) must allow _scan to be scheduled and run.

    Fails if the budget_gate branch is removed entirely: this test would still
    pass — but removing the False-branch would break the other test, making
    both together a coherent pair. This test validates the positive path.
    """
    scan_called = []

    async def fake_scan():
        scan_called.append(True)

    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send, interval=1, budget_gate=lambda: True)
    wd._scan = fake_scan
    wd.maybe_trigger("delete_object")
    await asyncio.sleep(0)  # let task run
    assert scan_called, "_scan must run when budget_gate returns True"


# ── maybe_trigger() ─────────────────────────────────────────────────────────

def test_maybe_trigger_ignores_read_cmds():
    wd = ProactiveWatchdog(AsyncMock(), interval=3)
    wd.maybe_trigger("get_hierarchy")
    assert wd._counter == 0


def test_maybe_trigger_increments_counter():
    wd = ProactiveWatchdog(AsyncMock(), interval=5)
    wd.maybe_trigger("set_property")
    assert wd._counter == 1


def test_maybe_trigger_high_blast_immediate():
    """delete_object / batch / scene trigger scan immediately (threshold=1)."""
    send = AsyncMock(return_value="ok")
    wd = ProactiveWatchdog(send, interval=10)
    wd.maybe_trigger("delete_object")
    assert wd._counter == 0  # reset after triggering


def test_maybe_trigger_normal_cmd_reaches_threshold():
    send = AsyncMock(return_value="ok")
    wd = ProactiveWatchdog(send, interval=3)
    for _ in range(3):
        wd.maybe_trigger("set_property")
    assert wd._counter == 0  # reset after trigger


# ── consume_alert() ──────────────────────────────────────────────────────────

def test_consume_alert_returns_none_when_empty():
    wd = ProactiveWatchdog(AsyncMock())
    assert wd.consume_alert() is None


def test_consume_alert_returns_pending():
    wd = ProactiveWatchdog(AsyncMock())
    wd._pending_alert = "ISSUES: broken ref"
    assert wd.consume_alert() == "ISSUES: broken ref"


def test_consume_alert_clears_after_consume():
    wd = ProactiveWatchdog(AsyncMock())
    wd._pending_alert = "alert"
    wd.consume_alert()
    assert wd.consume_alert() is None


# ── _scan() ─────────────────────────────────────────────────────────────────

async def test_scan_collects_issues():
    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return "ERROR: broken ref /Player"
        return ""  # empty console
    wd = ProactiveWatchdog(send)
    await wd._scan()
    assert wd._pending_alert is not None
    assert "ISSUES" in wd._pending_alert


async def test_scan_no_issues_no_alert():
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    await wd._scan()
    assert wd._pending_alert is None


async def test_watchdog_cancel_stops_task():
    """cancel() must cancel the running task and mark it done within 2s."""
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    # Manually set a long-running task
    wd._task = asyncio.get_running_loop().create_task(asyncio.sleep(10))
    assert not wd._task.done()
    await wd.cancel()
    assert wd._task.done()


# no-assert: crash guard
async def test_watchdog_cancel_no_task_is_noop():
    """cancel() with no task should not raise."""
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    await wd.cancel()  # must not raise


async def test_watchdog_cancel_already_done_is_noop():
    """cancel() on an already-done task should be safe."""
    send = AsyncMock(return_value="")
    wd = ProactiveWatchdog(send)
    wd._task = asyncio.get_running_loop().create_task(asyncio.sleep(0))
    await asyncio.sleep(0)  # schedule
    await asyncio.sleep(0)  # run
    assert wd._task.done()
    await wd.cancel()  # must not raise


async def test_scan_console_only_error():
    """Non-empty console with clean refs must produce a 'console:' alert."""
    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return ""  # no ref errors
        return "NullRef exception in Update"
    wd = ProactiveWatchdog(send)
    await wd._scan()
    assert wd._pending_alert is not None
    assert "console:" in wd._pending_alert


async def test_scan_both_issues_in_alert():
    """Both ref ERROR and non-empty console must both appear in the alert."""
    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return "ERROR: broken ref /Player"
        return "crash in Update"
    wd = ProactiveWatchdog(send)
    await wd._scan()
    assert wd._pending_alert is not None
    parts = wd._pending_alert.split("; ")
    assert len(parts) >= 2
    assert any("console:" in p for p in parts)
    assert any("ERROR" in p for p in parts)


async def test_dedup_within_60s_returns_no_alert():
    """Same issue within 60s should not set a new alert."""
    async def send(cmd, args, **kw):
        if cmd == "validate_references":
            return "ERROR: broken ref"
        return ""
    wd = ProactiveWatchdog(send)
    await wd._scan()
    first_alert = wd.consume_alert()
    assert first_alert is not None
    # Second scan immediately — same hash, within 60s
    await wd._scan()
    assert wd._pending_alert is None


# ── F01-qw: watchdog gather ────────────────────────────────────────────────────

async def test_watchdog_scan_uses_problem_levels():
    """Issue 27: _scan's get_console call must scan Error+Exception+Assert, not just Error."""
    seen_args = {}

    async def send(cmd, args, **kw):
        if cmd == "get_console":
            seen_args["level"] = args["level"]
        return ""
    wd = ProactiveWatchdog(send)
    await wd._scan()
    assert seen_args["level"] == PROBLEM_LEVELS


async def test_watchdog_scan_dispatches_pings_concurrently():
    """F01-behavioral: _scan must dispatch its two probes concurrently (gather), not serially."""
    import asyncio
    from unity_mcp.watchdog import ProactiveWatchdog

    in_flight = [0]
    max_in_flight = [0]

    async def send_fn(cmd, args, timeout=5.0):
        in_flight[0] += 1
        max_in_flight[0] = max(max_in_flight[0], in_flight[0])
        await asyncio.sleep(0.02)
        in_flight[0] -= 1
        return ""

    wd = ProactiveWatchdog(send_fn)
    await wd._scan()
    assert max_in_flight[0] == 2, "both probes must be in-flight together (gather, not serial)"
