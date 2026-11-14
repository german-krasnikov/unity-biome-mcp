"""Tests for suite terminal lifecycle (MCP-SUITE-022).

Guarantees:
- Play Mode always stops after suite (completion, timeout, stop_on_fail)
- Timeout produces a terminal report with per-file status
- Report always contains terminal:true and play_stopped:true/false markers
"""
import asyncio
import pytest
from unity_mcp.server import run_playtest_suite


def _pass_response(n=3):
    return f"PLAYTEST: {n}/{n} (1.0s) OK"


def _fail_response():
    return "PLAYTEST: 0/1 (1.0s)\n[1] ASSERT x==1 — FAIL"


def _editor_state(playing=False):
    p = "True" if playing else "False"
    return f"playing:{p}\npaused:False\ncompiling:False"


def _make_dispatch(files, responses_by_file=None, default_response=None):
    """Factory: list_playtest_files → files, run_playtest → per-file or default."""
    default = default_response or _pass_response()
    per_file = responses_by_file or {}

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            f = args.get("path", "")
            return {"ok": True, "data": per_file.get(f, default)}
        if cmd == "editor":
            action = args.get("action", "")
            if action == "state":
                return {"ok": True, "data": _editor_state(playing=False)}
            return {"ok": True, "data": "ok"}
        return {"ok": True, "data": "ok"}

    return dispatch


async def test_suite_always_stops_play_on_completion(mock_bridge):
    """After normal suite run, Play Mode is stopped and report carries terminal markers."""
    files = ["a.playtest", "b.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files)

    result = await run_playtest_suite("*.playtest", stop_after=True)

    stop_calls = [
        c for c in mock_bridge.send.call_args_list
        if c[0][0] == "editor" and c[0][1].get("action") == "stop"
    ]
    assert len(stop_calls) >= 1, "Play Mode must be stopped after suite completes"
    assert "terminal:true" in result
    assert "play_stopped:true" in result


async def test_suite_stops_play_on_timeout(mock_bridge):
    """Suite timeout does not leave the Editor stuck — Play Mode is still stopped."""
    files = ["a.playtest", "b.playtest"]

    async def hanging_dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            await asyncio.sleep(1000)  # simulate hang
        if cmd == "editor":
            action = args.get("action", "")
            if action == "state":
                return {"ok": True, "data": _editor_state(playing=False)}
            return {"ok": True, "data": "ok"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = hanging_dispatch

    result = await run_playtest_suite("*.playtest", stop_after=True, suite_timeout=0.05)

    stop_calls = [
        c for c in mock_bridge.send.call_args_list
        if c[0][0] == "editor" and c[0][1].get("action") == "stop"
    ]
    assert len(stop_calls) >= 1, "Play Mode must be stopped even on timeout"
    assert "timed_out" in result.lower()


async def test_suite_timeout_produces_terminal_report(mock_bridge):
    """Timeout generates a terminal report containing completed and timed-out file entries."""
    files = ["quick.playtest", "hang.playtest", "skip.playtest"]

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            if args.get("path") == "quick.playtest":
                return {"ok": True, "data": _pass_response()}
            await asyncio.sleep(1000)  # hang.playtest and skip.playtest hang
        if cmd == "editor":
            action = args.get("action", "")
            if action == "state":
                return {"ok": True, "data": _editor_state(playing=False)}
            return {"ok": True, "data": "ok"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch

    result = await run_playtest_suite("*.playtest", stop_after=True, suite_timeout=0.05)

    assert "quick.playtest" in result
    assert "terminal:true" in result
    assert "play_stopped:true" in result


async def test_suite_stop_on_fail_aborts_remaining(mock_bridge):
    """stop_on_fail=True: only first file runs, Play Mode is stopped, report is terminal."""
    files = ["fail.playtest", "skip1.playtest", "skip2.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(
        files,
        responses_by_file={"fail.playtest": _fail_response()},
        default_response=_pass_response(),
    )

    result = await run_playtest_suite("*.playtest", stop_on_fail=True, stop_after=True)

    run_calls = [
        c for c in mock_bridge.send.call_args_list if c[0][0] == "run_playtest"
    ]
    assert len(run_calls) == 1, "Only first file should run when stop_on_fail=True"

    stop_calls = [
        c for c in mock_bridge.send.call_args_list
        if c[0][0] == "editor" and c[0][1].get("action") == "stop"
    ]
    assert len(stop_calls) >= 1, "Play Mode must be stopped after stop_on_fail abort"
    assert "terminal:true" in result
    assert "play_stopped:true" in result
