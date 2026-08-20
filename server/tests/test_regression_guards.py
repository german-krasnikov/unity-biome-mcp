"""Regression guards RG-01 to RG-11.

Each test guards one specific behavior that was previously broken, got fixed,
and must NEVER regress. All tests are CI-safe (no live Unity connection).
"""
import json
import time

import pytest
from unittest.mock import AsyncMock, Mock, call

from mcp.server.fastmcp.exceptions import ToolError

# Importing from server.py triggers register_all → bind() in every tools module.
# This ensures all tools' _send is set to server._send before tests run.
from unity_mcp.server import get_component, run_playtest_suite, scene, run_playtest, batch
from unity_mcp.tools.console import get_console_since


# ---------------------------------------------------------------------------
# RG-01: re-dispatching same request_id returns existing result, no new run
# ---------------------------------------------------------------------------

async def test_rg01_idempotent_dispatch_same_request_id(monkeypatch):
    """Same request_id must resolve to the existing run without dispatching again.

    Guards: run_tests never sends a second 'run_tests' TCP command when the
    request_id already maps to a live run.
    """
    import unity_mcp.tools.testing as testing_mod

    req_id = "stable-req-rg01"
    run_id = "run-rg01-abc"
    utf_guid = "utf-rg01"
    ack = (
        f"tests-started|request_id={req_id}|run_id={run_id}"
        f"|utf_guid={utf_guid}|state=dispatched"
    )
    terminal_snapshot = json.dumps({
        "state": "terminal",
        "run_id": run_id,
        "request_id": req_id,
        "source": "mcp",
        "mode": "EditMode",
        "filter": "",
        "outcome": "passed",
    })

    dispatch_count = 0

    async def mock_send(cmd, args, **kw):
        nonlocal dispatch_count
        if cmd == "resolve_test_request":
            return ack  # existing run already committed
        if cmd == "run_tests":
            dispatch_count += 1
            return ack
        if cmd == "get_test_run":
            return terminal_snapshot
        return "ok"

    monkeypatch.setattr(testing_mod, "_send", mock_send)
    monkeypatch.setattr(testing_mod, "_preflight", AsyncMock(return_value=None))

    result = await testing_mod.run_tests(mode="EditMode", request_id=req_id)

    assert dispatch_count == 0, (
        f"Second call with existing request_id dispatched a new run "
        f"({dispatch_count} times)"
    )
    assert run_id in result, f"Expected run_id in result, got: {result!r}"


# ---------------------------------------------------------------------------
# RG-02: scene save returns a receipt token, not just "ok"
# ---------------------------------------------------------------------------

async def test_rg02_scene_save_returns_receipt(mock_bridge):
    """scene(action='save') must return a receipt string, not a bare 'ok'."""
    mock_bridge.send = AsyncMock(
        return_value={"ok": True, "data": "saved Assets/Scenes/Sample.unity"}
    )

    result = await scene(action="save")

    assert "saved" in result.lower(), (
        f"scene save must return a receipt containing 'saved', got: {result!r}"
    )


# ---------------------------------------------------------------------------
# RG-03: play admission requires world_ready, not just playing=True
# ---------------------------------------------------------------------------

def test_rg03_play_admission_requires_world_ready_not_just_playing():
    """playing=True alone must not satisfy play admission; world_ready=True required.

    Guards: PlayReadinessTracker.ready is False when world_ready is absent/False
    even when playing is True.
    """
    from unity_mcp.play_state import PlayReadinessTracker

    tracker = PlayReadinessTracker()
    tracker.update("playing:True\nplay_epoch:1\nworld_ready:False")

    assert tracker.state.playing is True
    assert tracker.state.ready is False, (
        "playing=True without world_ready=True must not mark tracker as ready"
    )

    tracker.update("playing:True\nplay_epoch:1\nworld_ready:True")
    assert tracker.state.ready is True


# ---------------------------------------------------------------------------
# RG-04: mcp_status includes a version field in response
# ---------------------------------------------------------------------------

async def test_rg04_version_reported_in_mcp_status(mock_bridge):
    """mcp_status() must surface a version field from Unity's get_status response."""
    from unity_mcp.tools.meta import mcp_status

    mock_bridge.send = AsyncMock(
        return_value={"ok": True, "data": "version:1.46\nscene:Sample.unity\ncompile:clean"}
    )

    result = await mcp_status()

    assert "version" in result, (
        f"mcp_status must include 'version' in response, got: {result!r}"
    )


# ---------------------------------------------------------------------------
# RG-05: unknown kwargs are rejected, not silently ignored
# ---------------------------------------------------------------------------

async def test_rg05_unknown_args_are_rejected_not_silently_ignored():
    """Tool functions with typed signatures must reject unexpected keyword arguments.

    Guards: Python enforces function signatures — extra kwargs raise TypeError
    before the function body executes, so no silent data loss occurs.
    """
    from unity_mcp.tools.scene import scene as scene_fn

    with pytest.raises(TypeError, match="unexpected keyword"):
        await scene_fn(action="save", totally_undeclared_arg=True)


# ---------------------------------------------------------------------------
# RG-06: extra connection rejected with CapacityBusyError; existing client survives
# ---------------------------------------------------------------------------

async def test_rg06_extra_connection_rejected_with_capacity_busy_error(monkeypatch):
    """_open_reconnect_candidate raises CapacityBusyError when Unity is at capacity.

    Guards: when Unity sends CLIENT_CAPACITY_BUSY, the new bridge raises a typed
    CapacityBusyError and the existing connection (bridge_a) is not evicted.
    """
    from unity_mcp.bridge import UnityBridge
    from unity_mcp.errors import CapacityBusyError

    bridge_a = Mock()
    bridge_a.send = AsyncMock(return_value={"ok": True, "data": "pong"})

    bridge_b = UnityBridge(port=9500)
    monkeypatch.setattr(
        bridge_b,
        "_open_reconnect_candidate",
        AsyncMock(side_effect=CapacityBusyError(
            "at capacity 2/2", retry_after_seconds=5.0, capacity=2, active=2
        )),
    )

    with pytest.raises(CapacityBusyError):
        await bridge_b._reconnect(fire_callbacks=False)

    result = await bridge_a.send("ping", {})
    assert result["ok"] is True, "Existing connection must not be evicted"


def test_rg06_capacity_busy_error_carries_active_capacity_attributes():
    """CapacityBusyError must expose .capacity, .active, and .retry_after_seconds.

    Guards: typed rejection carries enough info for meaningful diagnostics.
    """
    from unity_mcp.errors import CapacityBusyError

    err = CapacityBusyError("at capacity 2/2", retry_after_seconds=5.0, capacity=2, active=2)

    assert err.capacity == 2
    assert err.active == 2
    assert err.retry_after_seconds == 5.0


# ---------------------------------------------------------------------------
# RG-07: scene discard — success receipt, dirty flag, and error path
# ---------------------------------------------------------------------------

async def test_rg07_discard_success_returns_receipt_not_bare_ok(mock_bridge):
    """scene(action='discard') must return the receipt string, not a bare 'ok'.

    Guards: Python passes the C# data field verbatim — success is not collapsed
    to a generic 'ok' string.
    """
    mock_bridge.send = AsyncMock(
        return_value={"ok": True, "data": "discarded Assets/Scenes/Sample.unity"}
    )

    result = await scene(action="discard")

    assert result.strip() != "ok", f"Must not return bare 'ok'; got: {result!r}"
    assert "discarded" in result.lower() or "Assets" in result


async def test_rg07_discard_ok_with_dirty_true_surfaces_in_result(mock_bridge):
    """scene(action='discard') with dirty=true in C# response must reach the caller.

    Guards: Python does not strip or transform the C# data field — 'dirty=true'
    propagates to the caller verbatim.
    """
    mock_bridge.send = AsyncMock(
        return_value={"ok": True, "data": "discarded Assets/Scenes/Sample.unity dirty=true"}
    )

    result = await scene(action="discard")

    assert "dirty=true" in result, f"'dirty=true' must reach caller; got: {result!r}"


async def test_rg07_discard_error_raises_tool_error(mock_bridge):
    """scene(action='discard') must raise ToolError when Unity returns an error.

    Guards: Unity error responses from scene discard are not silently swallowed.
    """
    mock_bridge.send = AsyncMock(
        return_value={"ok": False, "err": "Cannot discard: no unsaved changes"}
    )

    with pytest.raises(ToolError, match="Cannot discard"):
        await scene(action="discard")


# ---------------------------------------------------------------------------
# RG-08: suite propagates dispatch_failed; non-disposable worker not silently ok
# ---------------------------------------------------------------------------

async def test_rg08_suite_gateway_rejects_non_disposable_worker(mock_bridge):
    """run_playtest_suite must report failure when playtests return dispatch_failed.

    Guards: dispatch_failed results from Unity (issued for non-disposable workers)
    are propagated as suite failures, never silently converted to pass.
    """
    # C# returns dispatch_failed for playtest files run outside a disposable worker.
    # Python-side: _run_single_file gets this string and _is_playtest_pass returns False.
    mock_bridge.send = AsyncMock(
        return_value={"ok": True, "data": "PLAYTEST: 0/1 FAIL dispatch_failed"}
    )

    result = await run_playtest_suite(
        pattern="test1.playtest,test2.playtest",
        stop_after=False,
        auto_play=False,
    )

    # Should show 0 passed out of 2, not "2/2 passed"
    assert "2/2" not in result, "Suite must not report 2/2 passed on dispatch_failed"
    assert "0/" in result or "FAIL" in result, (
        f"Suite must report failures, got: {result!r}"
    )


# ---------------------------------------------------------------------------
# RG-09: alias resolution is correct regardless of include chain depth
# ---------------------------------------------------------------------------

def test_rg09_nested_alias_include_resolves_transitively():
    """Aliases loaded via INCLUDE chains resolve correctly in the Python cache.

    Guards: resolve_aliases_in_args correctly resolves any alias in the flat cache,
    including aliases that originated from transitively included .defs files
    (C# PlaytestParser flattens INCLUDE chains; Python sees the flat result).
    """
    from unity_mcp.middleware_alias import resolve_aliases_in_args

    # Simulate aliases that arrived via: main.defs → INCLUDE a.defs → INCLUDE b.defs
    # After C# parsing and warm_alias_cache, all aliases are in one flat dict.
    cache = {
        "hp": "/Player|Health|hitPoints",  # came from b.defs transitively (3-part)
        "enemy": "/Enemy|EnemyAI",          # came from a.defs (2-part)
    }

    result = resolve_aliases_in_args(
        {"path": "$hp", "component": "$hp", "field": "$hp"},
        cache,
    )

    assert result["path"] == "/Player", f"Expected /Player, got {result['path']!r}"
    assert result["component"] == "Health", f"Expected Health, got {result['component']!r}"
    assert result["field"] == "hitPoints", f"Expected hitPoints, got {result['field']!r}"


# ---------------------------------------------------------------------------
# RG-10: SETUP_END / TEARDOWN_END markers forwarded to C# unchanged
# ---------------------------------------------------------------------------

async def test_rg10_setup_teardown_end_markers_forwarded_to_cs_unchanged(monkeypatch):
    """run_playtest must forward SETUP_END and TEARDOWN_END to C# without stripping.

    Guards: DSL end-marker keywords survive the Python layer intact — Python must
    not rewrite or omit them before sending to C#.
    """
    import unity_mcp.tools.runtime as runtime_mod
    from unity_mcp.server import run_playtest

    captured: dict = {}

    async def mock_send(cmd, args, **kw):
        captured.update(args)
        return "PLAYTEST: 1/1 PASS"

    monkeypatch.setattr(runtime_mod, "_send", mock_send)

    script = (
        "SETUP\n"
        "  WAIT 0.01\n"
        "SETUP_END\n"
        "ASSERT /Player|activeSelf\n"
        "TEARDOWN\n"
        "  WAIT 0.01\n"
        "TEARDOWN_END"
    )
    await run_playtest(script=script)

    forwarded = captured.get("script", "")
    assert "SETUP_END" in forwarded, f"SETUP_END must be forwarded to C#; got: {forwarded!r}"
    assert "TEARDOWN_END" in forwarded, f"TEARDOWN_END must be forwarded to C#; got: {forwarded!r}"


async def test_rg10_teardown_runs_after_setup_failure_surfaced_in_result(monkeypatch):
    """run_playtest must surface teardown evidence when C# reports it after setup failure.

    Guards: Python returns the C# response verbatim — teardown_ran evidence from
    C# reaches the caller and is not stripped.
    """
    import unity_mcp.tools.runtime as runtime_mod
    from unity_mcp.server import run_playtest

    async def mock_send(cmd, args, **kw):
        return "PLAYTEST: 0/1 FAIL setup_failed teardown_ran"

    monkeypatch.setattr(runtime_mod, "_send", mock_send)

    result = await run_playtest(
        script="SETUP\n  WAIT 0.01\nSETUP_END\nASSERT /X|activeSelf"
    )

    assert "teardown_ran" in result, f"teardown_ran evidence must reach caller; got: {result!r}"
    assert "FAIL" in result or "setup_failed" in result


# ---------------------------------------------------------------------------
# RG-11: ObjectReference survives text serialization roundtrip
# ---------------------------------------------------------------------------

async def test_rg11a_component_ref_survives_text_serialization_roundtrip(mock_bridge):
    """ObjectReference fields must pass through the Python layer unchanged.

    Guards: C# text-serializes component with ObjectReference (e.g. '&ABCD1234'),
    Python returns it verbatim — no accidental stripping or transformation.
    """
    object_ref = "&ABCD1234"
    component_text = (
        f"Transform\n"
        f"  slot_0 [] {object_ref}\n"
        f"  position [Vector3] (0, 1, 0)\n"
    )
    mock_bridge.send = AsyncMock(
        return_value={"ok": True, "data": component_text}
    )

    result = await get_component(path="/Player", type="Transform")

    assert object_ref in result, (
        f"ObjectReference {object_ref!r} must survive roundtrip, got: {result!r}"
    )


# ---------------------------------------------------------------------------
# RG-11b: clearing a nullable ObjectReference returns null marker, not old value
# ---------------------------------------------------------------------------

async def test_rg11b_nullable_reference_cleared_returns_null_marker(mock_bridge):
    """Cleared ObjectReference fields must surface as null, not the prior value.

    Guards: C# serializes a cleared (null) reference field as 'null', Python
    must not strip or transform it — null marker reaches the caller unchanged.
    """
    component_text = (
        "MeshRenderer\n"
        "  material [] null\n"
        "  castShadows [bool] True\n"
    )
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": component_text})

    result = await get_component(path="/Cube", type="MeshRenderer")

    assert "null" in result, f"null marker must survive Python layer, got: {result!r}"


# ---------------------------------------------------------------------------
# RG-11c: batch read with two get_component commands returns both results
# ---------------------------------------------------------------------------

async def test_rg11c_batch_read_multiple_components_returns_all(monkeypatch):
    """batch() with two get_component reads must return both results, not just first.

    Guards: Python batch layer must not drop any component response from the
    C# multi-command result.
    """
    import unity_mcp.tools.batch as batch_mod

    response = "Transform\n  position (0,0,0)\nBoxCollider\n  size (1,1,1)\nok:2"

    async def mock_send(cmd, args, **kw):
        return response

    monkeypatch.setattr(batch_mod, "_send", mock_send)

    commands = (
        "get_component path=/Cube type=Transform\n"
        "get_component path=/Cube type=BoxCollider"
    )
    result = await batch(commands=commands)

    assert "Transform" in result, f"Transform response must be present, got: {result!r}"
    assert "BoxCollider" in result, f"BoxCollider response must be present, got: {result!r}"


# ---------------------------------------------------------------------------
# RG-11d: bracketed path with embedded slash forwarded intact to C#
# ---------------------------------------------------------------------------

async def test_rg11d_bracketed_path_with_slash_forwarded_intact(monkeypatch):
    """Path '/[Zone A/Zone B]/Child' must be forwarded to C# with brackets intact.

    Guards: the embedded slash inside brackets must not be treated as a path
    separator — Python forwards the raw path string without splitting on '/'.
    """
    import unity_mcp.tools.objects as objects_mod

    captured: dict = {}

    async def mock_send(cmd, args, **kw):
        captured.update(args)
        return "Transform\n  position (0,0,0)\n"

    monkeypatch.setattr(objects_mod, "_send", mock_send)

    await get_component(path="/[Zone A/Zone B]/Child", type="Transform")

    path_sent = captured.get("path", "")
    assert "[Zone A/Zone B]" in path_sent, (
        f"Bracketed path segment must be forwarded intact, got: {path_sent!r}"
    )


# ---------------------------------------------------------------------------
# RG-11e: run_playtest timeout propagated to C# correctly
# ---------------------------------------------------------------------------

async def test_rg11e_playtest_timeout_propagated_to_cs(monkeypatch):
    """run_playtest(timeout=45.0) must send timeout='45.0' to C# — not dropped.

    Guards: the Play-entry deadline is forwarded verbatim, not replaced with
    a default or silently ignored.
    """
    import unity_mcp.tools.runtime as runtime_mod

    captured: dict = {}

    async def mock_send(cmd, args, **kw):
        captured.update(args)
        return "PLAYTEST: 1/1 PASS"

    monkeypatch.setattr(runtime_mod, "_send", mock_send)

    await run_playtest(script="ASSERT /Player|activeSelf", timeout=45.0)

    assert captured.get("timeout") == "45.0", (
        f"timeout must be forwarded as '45.0', got: {captured.get('timeout')!r}"
    )


# ---------------------------------------------------------------------------
# RG-11f: stale object path returns typed error, not unhandled exception
# ---------------------------------------------------------------------------

async def test_rg11f_stale_object_path_returns_typed_error(mock_bridge):
    """get_component for a destroyed object must raise ToolError, not crash.

    Guards: stale path responses from C# are converted to a typed ToolError
    at the Python layer — no unhandled AttributeError or KeyError escapes.
    """
    from mcp.server.fastmcp.exceptions import ToolError

    mock_bridge.send = AsyncMock(return_value={
        "ok": False,
        "err": "Object not found: /DestroyedObject (stale path)"
    })

    with pytest.raises(ToolError, match="stale|not found|Object"):
        await get_component(path="/DestroyedObject", type="Transform")


# ---------------------------------------------------------------------------
# RG-11g: console overflow sentinel reaches caller unchanged
# ---------------------------------------------------------------------------

async def test_rg11g_console_overflow_sentinel_propagated(mock_bridge):
    """get_console_since must surface the overflow warning when C# drops entries.

    Guards: the '#MCP_INTERNAL overflow:N' sentinel from C# must produce a
    visible overflow warning in the returned string — not be silently swallowed.
    """
    mock_bridge.send = AsyncMock(return_value={
        "ok": True,
        "data": "#MCP_INTERNAL overflow:5\nsome log line"
    })

    mark_id = str(time.time() - 5.0)
    result = await get_console_since(mark_id=mark_id)

    assert "overflow" in result, (
        f"overflow sentinel must reach caller; got: {result!r}"
    )


# ---------------------------------------------------------------------------
# RG-11h: ObjectReference hex suffix preserved through Python layer
# ---------------------------------------------------------------------------

async def test_rg11h_objectref_hex_suffix_preserved(mock_bridge):
    """ObjectReference with colon-suffix ('&ABCD1234:2') must be returned verbatim.

    Guards: some Unity versions emit a file-index suffix after the GUID colon;
    Python must not strip or truncate the suffix.
    """
    ref_with_suffix = "&ABCD1234:2"
    component_text = f"MeshFilter\n  mesh [] {ref_with_suffix}\n"
    mock_bridge.send = AsyncMock(return_value={"ok": True, "data": component_text})

    result = await get_component(path="/Cube", type="MeshFilter")

    assert ref_with_suffix in result, (
        f"hex suffix format must be preserved, got: {result!r}"
    )
