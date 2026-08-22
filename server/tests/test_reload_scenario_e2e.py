"""E2E reload scenario tests (pure Python, no Unity running).

Controls bridge.send() via AsyncMock sequences; patches sleep/time so tests
run in < 1s each. Scenarios cover the sync → compile → test → results
workflow from sync.py and testing.py.
"""
import asyncio
import json
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

from unity_mcp.bridge import DomainReloadError
import unity_mcp.tools.sync as _sync
import unity_mcp.tools.testing as _testing

STAMP1 = "aaaa1111:ts1"
STAMP2 = "bbbb2222:ts2"


def _send_with(responses: list):
    """AsyncMock that pops and returns/raises items in order.

    warm_type_cache and get_status are handled transparently without consuming
    a response slot — these are ancillary calls injected by sync_unity internals.
    """
    async def _impl(*_args, **_kwargs):
        cmd = _args[0] if _args else ""
        if cmd == "warm_type_cache":
            return "ok:types=42"
        if cmd == "get_status":
            return ""  # no HR active — do not consume a response slot
        item = responses.pop(0)
        if isinstance(item, BaseException):
            raise item
        return item
    return _impl


@pytest.fixture(autouse=True)
def _fast_sleep():
    with patch("asyncio.sleep", return_value=None):
        yield


@pytest.fixture(autouse=True)
def _reset_sync():
    """Reset sync module state between tests."""
    _sync._reset_bump_used()
    orig_send = _sync._send
    orig_cache = _sync._mm_cached
    _sync._mm_cached = None  # ensure clean mutation mode check per test
    yield
    _sync._send = orig_send
    _sync._mm_cached = orig_cache


@pytest.fixture(autouse=True)
def _reset_testing():
    """Reset testing module _send between tests."""
    orig = _testing._send
    yield
    _testing._send = orig


# ---------------------------------------------------------------------------
# S1: Happy path — sync triggers compile, MVID changes, returns "sync clean"
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_happy_path_sync_compile_test_results():
    _sync._send = _send_with([
        f"epoch=0|state=idle|stamp={STAMP1}",        # pre-sync status
        "sync_ack|epoch=1|will_compile=true",          # sync trigger
        "epoch=1|state=compiling|dur=1.2",             # poll 1
        f"epoch=1|state=ready|stamp={STAMP2}",         # poll 2 — MVID changed
    ])

    with patch("unity_mcp.tools.sync.read_reload_port", return_value=None), \
         patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _sync.sync_unity()

    assert result == "sync clean"


# ---------------------------------------------------------------------------
# S2: Reload during sync — DomainReloadError + ConnectionError mid-poll → recovered
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_reload_during_sync_bridge_reconnects():
    _sync._send = _send_with([
        f"epoch=0|state=idle|stamp={STAMP1}",
        "sync_ack|epoch=1|will_compile=true",
        DomainReloadError("going_away"),               # TCP killed by domain reload
        ConnectionError("Connection refused"),          # mid-reload
        ConnectionError("Connection refused"),          # still reloading
        f"epoch=1|state=ready|stamp={STAMP2}",         # recovered
    ])

    with patch("unity_mcp.tools.sync.read_reload_port", return_value=None), \
         patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _sync.sync_unity()

    assert result == "sync clean"
    assert "REIMPORT" not in result


# ---------------------------------------------------------------------------
# S3: Reload during run_tests -- lost ACK is resolved by request identity
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_reload_during_run_tests_resolves_lost_ack():
    _testing._send = _send_with([
        "none",
        ConnectionError("0 bytes: domain reload"),
    ])

    with patch("unity_mcp.tools.diagnose.diagnose", new=AsyncMock(return_value="CLEAN-LIVE")):
        r_run = await _testing.run_tests("EditMode", request_id="req-fire")

    assert r_run == "START-UNKNOWN|request_id=req-fire|reason=ConnectionError"

    ack = (
        "tests-started|request_id=req-wait|run_id=run-wait"
        "|utf_guid=utf-wait|state=dispatched"
    )
    terminal = json.dumps({
        "request_id": "req-wait",
        "run_id": "run-wait",
        "utf_guid": "utf-wait",
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
        "passed": 6962,
        "failed": 0,
        "skipped": 2,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "counts": {"expected": 6964, "finished": 6964, "passed": 6962, "skipped": 2},
        "issues": [],
    })
    _testing._send = _send_with([
        ConnectionError("0 bytes: domain reload"),
        "none",
        ack,
        terminal,
    ])
    with patch("unity_mcp.tools.diagnose.diagnose", new=AsyncMock(return_value="CLEAN-LIVE")):
        r_wait = await _testing.run_tests_wait(
            "EditMode", timeout=3.0, poll_interval=1.0, request_id="req-wait"
        )

    decoded = json.loads(r_wait)
    assert decoded["run_id"] == "run-wait"
    assert decoded["outcome"] == "passed"
    assert decoded["counts"]["finished"] == 6964


# ---------------------------------------------------------------------------
# S4: Double reload — stale epoch responses skipped, correct epoch returned
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_double_reload_stale_epoch_skipped():
    _sync._send = _send_with([
        f"epoch=0|state=idle|stamp={STAMP1}",
        "sync_ack|epoch=2|will_compile=true",           # C# bumped epoch to 2
        "epoch=1|state=ready|stamp=mid1:ts2",           # stale: s_epoch=1 ≠ 2
        "epoch=1|state=ready|stamp=mid1:ts3",           # still stale
        "epoch=2|state=ready|stamp=bbbb2222:ts4",       # correct epoch
    ])

    with patch("unity_mcp.tools.sync.read_reload_port", return_value=None), \
         patch("unity_mcp.editor_log.get_corroborated_errors", new=AsyncMock(return_value="")):
        result = await _sync.sync_unity()

    assert result == "sync clean"


# ---------------------------------------------------------------------------
# S5: Wedge recovery — dur=0.0 past threshold → force_refresh → MVID changes
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_wedge_recovery_force_refresh_heals():
    # Sync _send: pre-status, sync ack, compiling/dur=0.0, then recovery polls sync_status
    sync_responses = [
        f"epoch=0|state=idle|stamp={STAMP1}",
        "sync_ack|epoch=1|will_compile=true",
        "epoch=1|state=compiling|dur=0.0",             # wedge detected
        f"epoch=1|state=ready|stamp={STAMP2}",         # MVID changed — healed
    ]
    _sync._send = _send_with(sync_responses)

    # time.monotonic call order in sync + _attempt_recovery (with timed_send):
    # 1: deadline=0+300, 2: timed_send pre-stamp remaining, 3: started=0.0,
    # 4: loop deadline check=1.0, 5: timed_send poll remaining, 6: focus-hint=20.0,
    # 7: recovery_deadline min(300,20+30)=50, 8: recovery while check=21.0,
    # 9: timed_send recovery remaining
    # time.monotonic call order (with _timed_send in polling + recovery):
    # 1: deadline=0+300, 2: started=0.0,
    # 3: loop check=1.0, 4: _timed_send remaining=1.0, 5: focus-hint=20.0 (20-0>15→fires),
    # 6: recovery_deadline min(300,20+30)=50, 7: recovery while=21.0 (21<50→yes),
    # 8: _timed_send remaining=21.0 (50-21=29>0)
    monotonic_values = [0.0, 0.0, 1.0, 1.0, 20.0, 20.0, 21.0, 21.0]

    with patch("unity_mcp.tools.sync.time") as mock_time, \
         patch("unity_mcp.tools.sync._send_with_fallback", new=AsyncMock(return_value=None)), \
         patch("unity_mcp.tools.sync.read_reload_port", return_value=None):
        mock_time.monotonic.side_effect = monotonic_values
        result = await _sync.sync_unity()

    assert result == "sync clean"


# ---------------------------------------------------------------------------
# S6b: run_tests blocked by STALE-DOMAIN preflight verdict
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_run_tests_blocked_by_stale_domain():
    _testing._send = AsyncMock()  # must NOT be called

    with patch("unity_mcp.tools.diagnose.diagnose",
               new=AsyncMock(return_value="STALE-DOMAIN: MVID stale after reload")):
        result = await _testing.run_tests("EditMode")

    assert result.startswith("BLOCKED:"), f"Expected BLOCKED, got {result!r}"
    assert "STALE-DOMAIN" in result
    _testing._send.assert_not_called()


# ---------------------------------------------------------------------------
# S7: Port file stale — new Unity on new port, bridge migrates
# ---------------------------------------------------------------------------

@pytest.mark.asyncio
async def test_port_file_stale_unity_restart_new_port_discovered():
    from unity_mcp.bridge import UnityBridge

    port_discoverer = MagicMock(return_value=9501)
    probe_mock = MagicMock()
    bridge = UnityBridge(port=9500, port_discoverer=port_discoverer, probe=probe_mock)
    bridge._pinned_port = 9500
    bridge._pinned_pid = 1000

    mock_reader = AsyncMock()
    mock_writer = MagicMock()
    mock_writer.is_closing.return_value = False
    mock_writer.drain = AsyncMock()
    mock_writer.close = MagicMock()
    mock_writer.wait_closed = AsyncMock()
    mock_writer.get_extra_info = MagicMock(return_value=MagicMock())

    # Responses: ping pong then version
    ping_bytes = json.dumps({"id": "rc0001", "ok": True}).encode()
    ver_bytes  = json.dumps({"id": "ver", "ok": True, "data": "proto:3|plugin:1.0.0"}).encode()

    with patch("unity_mcp.bridge.is_pid_alive", return_value=False), \
         patch("asyncio.open_connection", new=AsyncMock(return_value=(mock_reader, mock_writer))), \
         patch("unity_mcp.bridge.frame_write"), \
         patch("unity_mcp.bridge.frame_read_with_timeout",
               new=AsyncMock(side_effect=[ping_bytes, ver_bytes])), \
         patch("unity_mcp.lockfile.read_pid_from_port_file", return_value=2000), \
         patch("unity_mcp.bridge._apply_socket_options"), \
         patch.object(bridge, "close", new=AsyncMock()), \
         patch.object(bridge, "start_heartbeat"):
        await bridge._reconnect(fire_callbacks=False)

    assert bridge._port == 9501
    assert bridge._pinned_port == 9501
    assert bridge._pinned_pid == 2000
    assert bridge.connected is True
    port_discoverer.assert_called_once()
