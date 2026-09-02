"""Shared test helpers."""
from unittest.mock import AsyncMock, MagicMock, Mock


def make_mock_bridge(connected: bool = True):
    """Create a minimal UnityBridge mock with all required async methods."""
    b = MagicMock()
    b.connect = AsyncMock()
    b.close = AsyncMock()
    b.send = AsyncMock(return_value={"ok": True})
    b.connected = connected
    b.stop_heartbeat = MagicMock()
    return b


def make_writer():
    """Create a fresh writer mock with all required async/sync methods."""
    writer = AsyncMock()
    writer.write = Mock()
    writer.close = Mock()
    writer.wait_closed = AsyncMock()
    writer.drain = AsyncMock()
    writer.is_closing = Mock(return_value=False)
    writer.get_extra_info = Mock(return_value=None)  # no real socket in tests
    return writer


def make_idle_probe():
    """CompileStateProbe mock that reports idle (not busy). Required fields
    for _should_give_up: has_strong_busy_signal=False, has_project=True."""
    from unity_mcp.compile_state import CompileStateProbe
    p = MagicMock(spec=CompileStateProbe)
    p.is_unity_busy.return_value = False
    p.has_strong_busy_signal.return_value = False
    p.is_process_dead.return_value = False
    p.estimated_remaining_s.return_value = 5.0
    p.has_project = True
    p.mark_recompile_issued = MagicMock()
    return p


def ping_response():
    """Returns (header, payload) for a ping/pong response — needed by _reconnect."""
    import json, struct
    r = {"id": "ping", "ok": True, "data": "pong"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def version_response(proto: int = 3):
    """Returns (header, payload) for a get_version response — needed by _reconnect."""
    import json, struct
    r = {"id": "ver", "ok": True, "data": f"proto:{proto}|plugin:test|stamp:test"}
    p = json.dumps(r).encode()
    return struct.pack("!I", len(p)), p


def reconnect_preamble(proto: int = 3):
    """Returns flat list of bytes chunks for _reconnect: ping_hdr, ping_pay, ver_hdr, ver_pay."""
    ph, pp = ping_response()
    vh, vp = version_response(proto)
    return [ph, pp, vh, vp]


def csharp_created(path: str) -> str:
    """Returns 'Created {path}' (no-parent form).

    Production also emits 'Created {path}\\n--- parent ---\\n{subtree}' when
    created with a parent — not covered by this helper. Use raw string for that case.
    """
    return f"Created {path}"


def csharp_schema(name: str, fields: dict) -> str:
    body = "\n".join(f"  {k}: {v}" for k, v in fields.items())
    return f"Schema: {name}\n{body}\n"


def csharp_runtime_field(k: str, v) -> str:
    return f"{k}={v}"


# ── run_tests_wait polling sentinels + snapshot builder ─────────────────────
# Shared by test_run_tests_wait.py, test_run_tests_wait_disk_fallback.py, and
# test_run_tests_wait_cancel.py, which each carried a verbatim (or stale,
# health-less) copy of this dict-construction body.
REQUEST_ID = "req-1"
RUN_ID = "run-1"


def make_snapshot(
    request_id: str,
    run_id: str,
    state: str,
    outcome: str = "",
    health: str = "",
    *,
    utf_guid: str = "utf-1",
) -> str:
    """Build a wire-shaped get_test_run snapshot JSON string."""
    import json as _json
    terminal = state == "terminal"
    expected = 6964
    failed = 1 if terminal and outcome == "failed" else 0
    skipped = 1 if terminal else 0
    passed = expected - failed - skipped if terminal else 4
    # "incomplete" models an abandoned run: no real RunFinished evidence.
    run_finished_observed = terminal and outcome != "incomplete"
    data = {
        "request_id": request_id,
        "run_id": run_id,
        "utf_guid": utf_guid,
        "state": state,
        "lifecycle": state,
        "outcome": outcome,
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
        "is_terminal": terminal,
        "execution_finished": terminal,
        "cleanup_complete": terminal,
        "run_started_observed": True,
        "manifest_complete": True,
        "run_finished_observed": run_finished_observed,
        "build_coherent": True,
        "utf_xml_scope": "complete" if terminal else "none",
        "expected_count": expected,
        "declared_expected_count": expected,
        "readable_manifest_count": expected,
        "completed_expected_count": expected if terminal else 4,
        "unique_terminal_count": expected if terminal else 4,
        "unmaterialized_expected_count": 0,
        "missing_count": 0,
        "unexpected_count": 0,
        "conflict_count": 0,
        "passed": passed,
        "failed": failed,
        "skipped": skipped,
        "inconclusive": 0,
        "cancelled": 0,
        "invalid": 0,
        "issues": [],
        "counts": {
            "expected": expected,
            "finished": expected if terminal else 4,
        },
    }
    if health:
        data["health"] = health
    return _json.dumps(data)


def make_bridge_disconnected(busy: bool = False):
    """Return a disconnected UnityBridge with a mocked probe.

    Shared by test_bridge_heartbeat.py and test_bridge_heartbeat_port_sweep.py,
    which previously each defined an identical `_make_bridge_disconnected`.
    """
    from unity_mcp.bridge import UnityBridge
    from unity_mcp.compile_state import CompileStateProbe
    probe = MagicMock(spec=CompileStateProbe)
    probe.has_strong_busy_signal.return_value = busy
    probe.is_process_dead.return_value = False
    probe.has_project = True
    probe.mark_recompile_issued = MagicMock()
    # Leave _reader/_writer = None so connected == False
    return UnityBridge("127.0.0.1", 9999, probe=probe)


# ── Shared "existing config carries a custom env var" fixture ───────────────
# Used by test_merger_deep_merge.py, test_mcp_config_writer.py (both preserve
# a user's CUSTOM_VAR across a re-merge/re-write that omits it).
KEEPME_ENV = {"UNITY_MCP_PORT": "9500", "CUSTOM_VAR": "keepme"}
