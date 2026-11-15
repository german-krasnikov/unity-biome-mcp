"""Tests for suite disconnect/uncaught-exception lifecycle (MCP-SUITE-022)."""

import pytest
from unity_mcp.server import run_playtest_suite


def _editor_state(playing=False):
    p = "True" if playing else "False"
    return f"playing:{p}\npaused:False\ncompiling:False"


def _make_disconnect_dispatch(files, disconnect_on_file_n=2):
    """Factory: raises ConnectionError on Nth run_playtest call; handles editor state polls."""
    call_count = [0]

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            call_count[0] += 1
            if call_count[0] == disconnect_on_file_n:
                raise ConnectionError("socket disconnected")
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.5s) OK"}
        if cmd == "editor":
            action = args.get("action", "")
            if action == "state":
                return {"ok": True, "data": _editor_state(playing=False)}
            return {"ok": True, "data": "ok"}
        return {"ok": True, "data": "ok"}

    return dispatch


async def test_suite_disconnect_mid_run_produces_partial_terminal_report(mock_bridge):
    """ConnectionError on file 2 of 3 produces a partial terminal report; play stops."""
    files = ["a.playtest", "b.playtest", "c.playtest"]
    mock_bridge.send.side_effect = _make_disconnect_dispatch(files, disconnect_on_file_n=2)

    result = await run_playtest_suite("*.playtest", stop_after=True)

    assert "terminal:true" in result
    assert "play_stopped:true" in result
    assert "a.playtest" in result
    assert "b.playtest" in result
    # file-2 shows a failure — ConnectionError wrapped as ToolError by the bridge layer
    assert "FAIL" in result or "ERROR" in result


async def test_suite_uncaught_exception_always_stops_play_mode(mock_bridge, monkeypatch):
    """RuntimeError inside _run_single_file: Play Mode stop happens exactly once via finally."""
    from unity_mcp.tools import runtime

    async def failing_run_single(_path, _timeout):
        raise RuntimeError("boom")

    monkeypatch.setattr(runtime, "_run_single_file", failing_run_single)

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "a.playtest"}
        if cmd == "editor":
            action = args.get("action", "")
            if action == "state":
                return {"ok": True, "data": _editor_state(playing=False)}
            return {"ok": True, "data": "ok"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch

    with pytest.raises(RuntimeError, match="boom"):
        await run_playtest_suite("*.playtest", stop_after=True)

    stop_calls = [
        c for c in mock_bridge.send.call_args_list
        if c[0][0] == "editor" and c[0][1].get("action") == "stop"
    ]
    assert len(stop_calls) == 1
