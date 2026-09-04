"""Tests for runtime Play Mode tools."""
import pytest
from unittest.mock import AsyncMock, patch
from mcp.server.fastmcp.exceptions import ToolError
from unity_mcp.server import (
    invoke_method, wait_until, query_state, move_to,
    set_active, wire_event, unwire_event, run_playtest,
)


async def test_invoke_method_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "void"}
    result = await invoke_method("/Player", "PlayerController", "Jump", "5.0")
    mock_bridge.send.assert_called_once_with(
        "invoke_method",
        {"path": "/Player", "component": "PlayerController", "method": "Jump", "args": "5.0"},
        timeout=30.0,
    )
    assert result == "void"


async def test_invoke_method_no_args(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "void"}
    await invoke_method("/Player", "PlayerController", "Jump")
    call_args = mock_bridge.send.call_args[0][1]
    assert "args" not in call_args


async def test_wait_until_default_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "field=true after 1.2s"}
    result = await wait_until("/Player", "PlayerController", "isAlive", "true")
    call_args = mock_bridge.send.call_args
    assert call_args[0][0] == "wait_until"
    sent = call_args[0][1]
    assert sent["timeout"] == "5.0"
    # Python timeout = Unity timeout + 5
    assert call_args[1]["timeout"] == 10.0


async def test_wait_until_with_negate(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/Enemy", "EnemyAI", "isAlive", "true", negate=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["negate"] == "true"


async def test_wait_until_custom_timeout(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/Player", "PlayerController", "hp", "0", timeout=15.0)
    call_args = mock_bridge.send.call_args
    sent = call_args[0][1]
    assert sent["timeout"] == "15.0"
    # Python timeout = Unity timeout + 5
    assert call_args[1]["timeout"] == 20.0


async def test_wait_until_abort_on_fail_passes_arg(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/P", "C", "f", "v", abort_on_fail=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["abort_on_fail"] == "true"


async def test_wait_until_abort_on_fail_default_omits(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    await wait_until("/P", "C", "f", "v", abort_on_fail=False)
    sent = mock_bridge.send.call_args[0][1]
    assert "abort_on_fail" not in sent


async def test_wait_until_abort_on_fail_blocked_before_tcp_in_read_only(
    mock_bridge, monkeypatch
):
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")

    with pytest.raises(ToolError, match="READ_ONLY_BLOCKED"):
        await wait_until("/P", "C", "f", "v", abort_on_fail=True)

    mock_bridge.send.assert_not_awaited()


async def test_wait_until_observational_form_allowed_in_read_only(
    mock_bridge, monkeypatch
):
    monkeypatch.setenv("UNITY_MCP_READ_ONLY", "1")
    mock_bridge.send.return_value = {"ok": True, "data": "matched"}

    result = await wait_until("/P", "C", "f", "v", abort_on_fail=False)

    assert result == "matched"
    mock_bridge.send.assert_awaited_once()


async def test_run_playtest_abort_on_fail_passes_arg(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN", abort_on_fail=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["abort_on_fail"] == "true"


async def test_run_playtest_abort_on_fail_default_omits(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN")
    sent = mock_bridge.send.call_args[0][1]
    assert "abort_on_fail" not in sent


async def test_run_playtest_format_default_omits(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN")
    sent = mock_bridge.send.call_args[0][1]
    assert "format" not in sent


async def test_run_playtest_format_json_passes_arg_and_skips_compression(mock_bridge, monkeypatch):
    from unity_mcp.tools import runtime

    long_json = '{"steps":[' + ",".join(f'{{"index":{i}}}' for i in range(60)) + "]}"
    assert len(long_json) > 300
    mock_bridge.send.return_value = {"ok": True, "data": long_json}
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    monkeypatch.setattr(runtime._sampling, "summarize", AsyncMock(return_value="MUTATED_SUMMARY"))

    result = await run_playtest("LOG hi", format="json")

    sent = mock_bridge.send.call_args[0][1]
    assert sent["format"] == "json"
    # Neither compressed nor summarized — the exact receipt JSON round-trips.
    assert result == long_json


async def test_query_state_sends_correct_command(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "GridPlayer.Score=5\nGridPlayer.PosX=3"}
    result = await query_state("/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")
    mock_bridge.send.assert_called_once_with(
        "query_state",
        {"queries": "/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX"},
        timeout=10.0,
    )
    assert "GridPlayer.Score=5" in result



# --- P1-4: move_to bridge call shape ---

async def test_move_to_sends_correct_command(mock_bridge):
    """move_to sends cmd='move_to' with timeout as str and python timeout = timeout+5."""
    mock_bridge.send.return_value = {"ok": True, "data": "arrived"}
    await move_to("/Player", "5,0,-3")
    call_args = mock_bridge.send.call_args
    assert call_args[0][0] == "move_to"
    sent = call_args[0][1]
    assert sent["path"] == "/Player"
    assert sent["position"] == "5,0,-3"
    assert sent["timeout"] == "15.0"          # str(float(default=15.0))
    assert call_args[1]["timeout"] == 20.0    # timeout + 5.0


async def test_move_to_custom_timeout_offset(mock_bridge):
    """Python-level timeout is always unity timeout + 5.0."""
    mock_bridge.send.return_value = {"ok": True, "data": "arrived"}
    await move_to("/Enemy", "0,0,0", timeout=30.0)
    call_args = mock_bridge.send.call_args
    sent = call_args[0][1]
    assert sent["timeout"] == "30.0"
    assert call_args[1]["timeout"] == 35.0    # 30 + 5


# ─── ok=False → ToolError (write tools) ──────────────────────────────────────

@pytest.mark.parametrize("active,err_msg", [
    (True,  "Object not found"),
    (False, "Scene path invalid"),
])
async def test_set_active_error_raises_tool_error_runtime(mock_bridge, active, err_msg):
    """set_active raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": err_msg})
    with pytest.raises(ToolError, match=err_msg):
        await set_active("/Missing", active)


async def test_wire_event_error_raises_tool_error_runtime(mock_bridge):
    """wire_event raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "Event field not found"})
    with pytest.raises(ToolError, match="Event field not found"):
        await wire_event("/Btn", "Button", "onClick", "/Target", "SetActive")


async def test_unwire_event_error_raises_tool_error_runtime(mock_bridge):
    """unwire_event raises ToolError when Unity returns ok=False."""
    mock_bridge.send = AsyncMock(return_value={"ok": False, "err": "No listeners to remove"})
    with pytest.raises(ToolError, match="No listeners to remove"):
        await unwire_event("/Btn", "Button", "onClick")


# ─── Phase 1: Section/MOVE_PATH DSL extensions ───────────────────────────────

def test_compress_report_preserves_section_lines():
    from unity_mcp.tools.runtime import _compress_report
    report = (
        "PLAYTEST: 3/3 (1.0s)\n"
        "[1] ASSERT x == 1 — PASS (1)\n"
        "--- Movement Phase ---\n"
        "[2] ASSERT y == 2 — PASS (2)"
    )
    result = _compress_report(report)
    assert "--- Movement Phase ---" in result


async def test_move_path_sends_script(mock_bridge):
    """run_playtest passes MOVE_PATH script to bridge unchanged."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 3/3 (1.0s) OK"}
    script = "MOVE_PATH 1,0,0 > 5,0,0 > 10,0,3"
    await run_playtest(script)
    sent = mock_bridge.send.call_args[0][1]
    assert "MOVE_PATH" in sent["script"]


# ── P1.3 snapshot_on_failure ──────────────────────────────────────────────────

async def test_run_playtest_snapshot_on_failure_passes_true(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 0/1 (0.1s)\n[1] ASSERT $x == True — FAIL (False)\nsnapshot:\n  $x=False"}
    await run_playtest("ASSERT_CONSOLE_CLEAN", snapshot_on_failure=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent["snapshot_on_failure"] == "true"


async def test_run_playtest_snapshot_on_failure_default_omits(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN")
    sent = mock_bridge.send.call_args[0][1]
    assert "snapshot_on_failure" not in sent


# ── #14A: fresh mode ──────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_run_playtest_fresh_passes_param(monkeypatch):
    """fresh=True: Python handles lifecycle; does NOT pass fresh to C#."""
    from unity_mcp.tools import runtime

    playing = False
    calls = []

    async def fake_send(cmd, args, **kw):
        nonlocal playing
        calls.append((cmd, args.copy()))
        if cmd == "editor":
            action = args.get("action")
            if action == "play":
                playing = True
                return "entered"
            if action == "state":
                return f"playing:{playing}\nplay_epoch:1\nworld_ready:{playing}"
        if cmd == "run_playtest":
            return "PLAYTEST: 1/1 (0.1s) OK"
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    await runtime.run_playtest("ASSERT_CONSOLE_CLEAN", fresh=True)

    playtest_calls = [(cmd, a) for cmd, a in calls if cmd == "run_playtest"]
    assert len(playtest_calls) == 1
    assert "fresh" not in playtest_calls[0][1], "fresh must not be forwarded to C#"
    editor_actions = [a.get("action") for cmd, a in calls if cmd == "editor"]
    assert "play" in editor_actions


async def test_run_playtest_fresh_default_omits(mock_bridge):
    """fresh omitted by default."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN")
    sent = mock_bridge.send.call_args[0][1]
    assert "fresh" not in sent


@pytest.mark.asyncio
async def test_run_playtest_fresh_timeout_returns_structured(monkeypatch):
    """TimeoutError from _enter_fresh_play must produce a PLAYTEST: structured result."""
    from unity_mcp.tools import runtime

    async def fake_enter_fresh_play():
        raise TimeoutError("world not ready")

    monkeypatch.setattr(runtime, "_enter_fresh_play", fake_enter_fresh_play)

    result = await runtime.run_playtest("ASSERT_CONSOLE_CLEAN", fresh=True)
    assert result.startswith("PLAYTEST:"), f"expected PLAYTEST: prefix, got: {result!r}"


# ── #14B: restart_between ──────────────────────────────────────────────────────

async def test_run_playtest_suite_restart_between(monkeypatch):
    """restart_between=True issues editor stop+play between files."""
    from unity_mcp.tools import runtime
    calls = []
    playing = True

    async def fake_send(cmd, args, **kw):
        nonlocal playing
        calls.append((cmd, args))
        if cmd == "list_playtest_files":
            return "a.playtest\nb.playtest"
        if cmd == "run_playtest":
            return "PLAYTEST: 1/1 (0.1s) OK"
        if cmd == "editor":
            action = args.get("action")
            if action == "stop":
                playing = False
                return "ok"
            if action == "play":
                playing = True
                return "entered"
            return f"playing:{playing}\npaused:False\ncompiling:False"
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    await runtime.run_playtest_suite("Playtests/*.playtest", restart_between=True, stop_after=False, auto_play=False)

    editor_cmds = [(c, a.get("action")) for c, a in calls if c == "editor"]
    assert ("editor", "stop") in editor_cmds
    assert ("editor", "play") in editor_cmds


@pytest.mark.asyncio
async def test_restart_stop_exception_fails_and_does_not_run_next_file(monkeypatch):
    from unity_mcp.tools import runtime
    run_paths = []

    async def fake_send(cmd, args, **kw):
        if cmd == "list_playtest_files":
            return "a.playtest\nb.playtest\nc.playtest"
        if cmd == "run_playtest":
            run_paths.append(args["path"])
            return "PLAYTEST: 1/1 (0.1s) OK"
        if cmd == "editor" and args.get("action") == "stop":
            raise ConnectionError("stop transport failed")
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    result = await runtime.run_playtest_suite(
        "Playtests/*.playtest",
        restart_between=True,
        stop_after=False,
        auto_play=False,
    )

    assert run_paths == ["a.playtest"]
    assert "SUITE: 1/2" in result
    assert "restart failed" in result
    assert "ConnectionError" in result


@pytest.mark.asyncio
async def test_restart_play_exception_fails_and_does_not_run_next_file(monkeypatch):
    from unity_mcp.tools import runtime
    run_paths = []
    playing = True

    async def fake_send(cmd, args, **kw):
        nonlocal playing
        if cmd == "list_playtest_files":
            return "a.playtest\nb.playtest"
        if cmd == "run_playtest":
            run_paths.append(args["path"])
            return "PLAYTEST: 1/1 (0.1s) OK"
        if cmd == "editor":
            action = args.get("action")
            if action == "stop":
                playing = False
                return "ok"
            if action == "state":
                return f"playing:{playing}\npaused:False\ncompiling:False"
            if action == "play":
                raise ConnectionError("play transport failed")
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    result = await runtime.run_playtest_suite(
        "Playtests/*.playtest",
        restart_between=True,
        stop_after=False,
        auto_play=False,
    )

    assert run_paths == ["a.playtest"]
    assert "SUITE: 1/2" in result
    assert "restart failed" in result
    assert "ConnectionError" in result


@pytest.mark.asyncio
async def test_restart_stuck_in_play_mode_fails_without_next_file(monkeypatch):
    from unity_mcp.tools import runtime
    run_paths = []

    async def fake_send(cmd, args, **kw):
        if cmd == "list_playtest_files":
            return "a.playtest\nb.playtest"
        if cmd == "run_playtest":
            run_paths.append(args["path"])
            return "PLAYTEST: 1/1 (0.1s) OK"
        if cmd == "editor" and args.get("action") == "stop":
            return "ok"
        if cmd == "editor" and args.get("action") == "state":
            return "playing:True\npaused:False\ncompiling:False"
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    monkeypatch.setattr(runtime.asyncio, "sleep", AsyncMock(return_value=None))

    result = await runtime.run_playtest_suite(
        "Playtests/*.playtest",
        restart_between=True,
        stop_after=False,
        auto_play=False,
    )

    assert run_paths == ["a.playtest"]
    assert "SUITE: 1/2" in result
    assert "did not reach Edit Mode" in result


@pytest.mark.asyncio
async def test_initial_auto_play_exception_fails_without_running_file(monkeypatch):
    from unity_mcp.tools import runtime
    run_paths = []

    async def fake_send(cmd, args, **kw):
        if cmd == "editor" and args.get("action") == "state":
            return "playing:False\npaused:False\ncompiling:False"
        if cmd == "editor" and args.get("action") == "play":
            raise ConnectionError("play failed")
        if cmd == "run_playtest":
            run_paths.append(args["path"])
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    result = await runtime.run_playtest_suite(
        "a.playtest", auto_play=True, stop_after=False
    )

    assert run_paths == []
    assert "SUITE: 0/1" in result
    assert "startup failed" in result
    assert "ConnectionError" in result


@pytest.mark.asyncio
async def test_initial_auto_play_stuck_state_fails_without_running_file(monkeypatch):
    from unity_mcp.tools import runtime
    run_paths = []

    async def fake_send(cmd, args, **kw):
        if cmd == "editor" and args.get("action") == "state":
            return "playing:False\npaused:False\ncompiling:False"
        if cmd == "editor" and args.get("action") == "play":
            return "entered"
        if cmd == "run_playtest":
            run_paths.append(args["path"])
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    monkeypatch.setattr(runtime.asyncio, "sleep", AsyncMock(return_value=None))

    result = await runtime.run_playtest_suite(
        "a.playtest", auto_play=True, stop_after=False
    )

    assert run_paths == []
    assert "SUITE: 0/1" in result
    assert "did not reach Play Mode" in result


@pytest.mark.asyncio
async def test_restart_between_auto_play_resets_before_first_file(monkeypatch):
    from unity_mcp.tools import runtime
    calls = []
    playing = True

    async def fake_send(cmd, args, **kw):
        nonlocal playing
        calls.append((cmd, args.get("action"), args.get("path")))
        if cmd == "editor":
            action = args.get("action")
            if action == "state":
                return f"playing:{playing}\npaused:False\ncompiling:False"
            if action == "stop":
                playing = False
                return "ok"
            if action == "play":
                playing = True
                return "entered"
        if cmd == "run_playtest":
            return "PLAYTEST: 1/1 (0.1s) OK"
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    result = await runtime.run_playtest_suite(
        "a.playtest",
        auto_play=True,
        restart_between=True,
        stop_after=False,
    )

    assert result.startswith("SUITE: 1/1")
    actions_before_test = [action for cmd, action, _ in calls if cmd == "editor"]
    assert actions_before_test == ["state", "stop", "state", "play", "state"]


async def test_run_playtest_suite_restart_between_false_no_editor_cmds(monkeypatch):
    """restart_between=False (default) — no stop/play calls between files."""
    from unity_mcp.tools import runtime
    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        if cmd == "list_playtest_files":
            return "a.playtest\nb.playtest"
        return "PLAYTEST: 1/1 (0.1s) OK"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    await runtime.run_playtest_suite("Playtests/*.playtest", restart_between=False, stop_after=False, auto_play=False)

    editor_cmds = [c for c, _ in calls if c == "editor"]
    assert not editor_cmds


@pytest.mark.asyncio
async def test_run_playtest_suite_console_err_counts_as_failure(monkeypatch):
    """CONSOLE_ERR in raw report (no FAIL token) must mark suite entry as failed."""
    from unity_mcp.tools import runtime

    async def fake_send(cmd, args, **kw):
        if cmd == "list_playtest_files":
            return "a.playtest"
        return "PLAYTEST: 2/2 (1.0s)\n[1] CONSOLE_ERR during Set: NullRef"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    result = await runtime.run_playtest_suite("Playtests/*.playtest", stop_after=False, auto_play=False)
    assert "0/1" in result, f"Expected 0/1 passed, got:\n{result}"
    assert "FAIL" in result, f"Expected FAIL in report, got:\n{result}"


@pytest.mark.asyncio
async def test_run_playtest_suite_zero_total_is_not_a_pass(monkeypatch):
    from unity_mcp.tools import runtime

    async def fake_send(cmd, args, **kw):
        if cmd == "run_playtest":
            return "PLAYTEST: 0/0 (0.0s) OK"
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    result = await runtime.run_playtest_suite(
        "a.playtest", stop_after=False, auto_play=False
    )

    assert result.startswith("SUITE: 0/1")
    assert "FAIL" in result


# ── #17: runtime_snapshot ──────────────────────────────────────────────────────

async def test_runtime_snapshot_passes_type(monkeypatch):
    """runtime_snapshot(type=) sends type param to bridge."""
    from unity_mcp.tools import runtime
    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        return "runtime_snapshot: Rigidbody\n---\ntotal: 2"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    result = await runtime.runtime_snapshot(type="Rigidbody")
    assert calls[0][0] == "runtime_snapshot"
    assert calls[0][1]["type"] == "Rigidbody"
    assert "Rigidbody" in result


async def test_runtime_snapshot_compress_passes_true(monkeypatch):
    """compress=True sends compress=true string to bridge."""
    from unity_mcp.tools import runtime
    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        return "total: 0"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    await runtime.runtime_snapshot(type="Rigidbody", compress=True)
    assert calls[0][1].get("compress") == "true"


async def test_runtime_snapshot_name_filter_passed(monkeypatch):
    """name param is forwarded when provided."""
    from unity_mcp.tools import runtime
    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        return "total: 1"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    await runtime.runtime_snapshot(type="Rigidbody", name="Enemy")
    assert calls[0][1].get("name") == "Enemy"




# ── #9 .suite files ────────────────────────────────────────────────────────────

import pytest


@pytest.mark.asyncio
async def test_run_playtest_suite_suite_path_reads_file_and_runs(tmp_path, monkeypatch):
    """suite_path reads .suite file, runs each listed .playtest."""
    from unity_mcp.tools import runtime
    suite = tmp_path / "combat.suite"
    suite.write_text("Playtests/a.playtest\n# comment\nPlaytests/b.playtest\n", encoding="utf-8")
    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        return "PLAYTEST: 1/1 (0.1s) OK"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    result = await runtime.run_playtest_suite(suite_path=str(suite), stop_after=False, auto_play=False)
    assert "SUITE:" in result
    run_calls = [c for c in calls if c[0] == "run_playtest"]
    assert len(run_calls) == 2
    assert run_calls[0][1]["path"] == "Playtests/a.playtest"
    assert run_calls[1][1]["path"] == "Playtests/b.playtest"


@pytest.mark.asyncio
async def test_run_playtest_suite_paths_and_suite_path_raises(monkeypatch):
    """pattern and suite_path are mutually exclusive."""
    from unity_mcp.tools import runtime
    monkeypatch.setattr(runtime, "_send", AsyncMock())
    monkeypatch.setattr(runtime, "_args", dict)
    with pytest.raises(ValueError, match="mutually exclusive"):
        await runtime.run_playtest_suite(pattern="Playtests/*.playtest", suite_path="/tmp/x.suite")


@pytest.mark.asyncio
async def test_run_playtest_suite_empty_suite_file(tmp_path, monkeypatch):
    """Empty .suite file (only comments) returns early with no files message."""
    from unity_mcp.tools import runtime
    suite = tmp_path / "empty.suite"
    suite.write_text("# just a comment\n\n", encoding="utf-8")

    async def fake_send(cmd, args, **kw):
        return "PLAYTEST: 1/1 OK"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", dict)
    result = await runtime.run_playtest_suite(suite_path=str(suite), stop_after=False, auto_play=False)
    assert result.startswith("SUITE: 0/0 passed")
    assert "FAIL suite input: no files" in result


@pytest.mark.asyncio
async def test_lint_playtest_suite_suite_path_reads_file(tmp_path, monkeypatch):
    """lint_playtest_suite suite_path reads .suite file and lints each."""
    from unity_mcp.tools import runtime
    suite = tmp_path / "test.suite"
    suite.write_text("Playtests/health.playtest\n", encoding="utf-8")

    async def fake_send(cmd, args, **kw):
        return "OK  Playtests/health.playtest  no issues"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    result = await runtime.lint_playtest_suite(suite_path=str(suite))
    assert "LINT: 1/1 clean" in result


@pytest.mark.asyncio
async def test_lint_playtest_suite_pattern_keyword_form(monkeypatch):
    """lint_playtest_suite accepts pattern= as keyword argument."""
    from unity_mcp.tools import runtime

    async def fake_send(cmd, args, **kw):
        if cmd == "list_playtest_files":
            return "Playtests/a.playtest"
        return "OK  no issues"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    result = await runtime.lint_playtest_suite(pattern="*.playtest")
    assert "LINT: 1/1 clean" in result


# ── G48: run_playtest lifecycle hooks ─────────────────────────────────────────

async def test_run_playtest_before_hook_passes_arg(mock_bridge):
    """before_hook is forwarded to the C# bridge."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN", before_hook="Debug.Log('setup');")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("before_hook") == "Debug.Log('setup');"


async def test_run_playtest_after_hook_passes_arg(mock_bridge):
    """after_hook is forwarded to the C# bridge."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN", after_hook="Debug.Log('teardown');")
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("after_hook") == "Debug.Log('teardown');"


async def test_run_playtest_hooks_default_omitted(mock_bridge):
    """before_hook and after_hook absent from bridge call by default."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN")
    sent = mock_bridge.send.call_args[0][1]
    assert "before_hook" not in sent
    assert "after_hook" not in sent


# ── P-336: stop_after must run even when run_playtest raises ──────────────────

@pytest.mark.asyncio
async def test_run_playtest_suite_stops_play_mode_on_exception(monkeypatch):
    """P-336: stop_after=True must stop Play Mode even when run_playtest raises."""
    import asyncio
    from unity_mcp.tools import runtime

    stop_called = []

    async def fake_send(cmd, args, **kw):
        if cmd == "list_playtest_files":
            return "Playtests/A.playtest\nPlaytests/B.playtest"
        if cmd == "editor":
            if args.get("action") == "stop":
                stop_called.append(True)
                return "ok"
            return "playing:false\ndirty:False"
        if cmd == "run_playtest":
            raise asyncio.TimeoutError("network timeout")
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    result = await runtime.run_playtest_suite(
        pattern="Playtests/*.playtest",
        stop_after=True,
        auto_play=False,
    )

    assert stop_called, "P-336: stop must be called even after exception"
    assert "SUITE" in result or "ERROR" in result


@pytest.mark.asyncio
async def test_run_playtest_suite_stops_play_mode_on_failure(monkeypatch):
    """P-336: stop_after=True stops Play Mode when test fails (stop_on_fail=True)."""
    from unity_mcp.tools import runtime

    stop_called = []

    async def fake_send(cmd, args, **kw):
        if cmd == "list_playtest_files":
            return "Playtests/A.playtest"
        if cmd == "editor":
            if args.get("action") == "stop":
                stop_called.append(True)
                return "ok"
            return "playing:True\ndirty:False"
        if cmd == "run_playtest":
            return "PLAYTEST: 0/1 (1.0s)\n[1] ASSERT /Player|Health == 100 — FAIL"
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    await runtime.run_playtest_suite(
        pattern="Playtests/*.playtest",
        stop_on_fail=True,
        stop_after=True,
        auto_play=False,
    )

    assert stop_called, "P-336: stop must be called after suite failure"


# ── DEF-1: auto_play must not leak Play Mode on pre-loop errors ─────────────

@pytest.mark.asyncio
async def test_run_playtest_suite_auto_play_cleanup_on_file_error(tmp_path, monkeypatch):
    """DEF-1: auto_play=True + broken suite_path => finally still stops Play Mode."""
    from unity_mcp.tools import runtime
    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        if cmd == "editor":
            action = args.get("action")
            if action == "state":
                return "playing:True\npaused:False\ncompiling:False"
            if action in ("play", "stop"):
                return "ok"
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    # suite_path that doesn't exist => raises in file resolution
    with pytest.raises((FileNotFoundError, OSError)):
        await runtime.run_playtest_suite(
            suite_path="/nonexistent/path.suite",
            auto_play=True,
            stop_after=True,
        )

    # Assert: "editor stop" was called despite the error
    stop_calls = [(c, a) for c, a in calls if c == "editor" and a.get("action") == "stop"]
    assert len(stop_calls) >= 1, "DEF-1: Play Mode not stopped after auto_play + file error"


@pytest.mark.asyncio
async def test_run_playtest_suite_auto_play_cleanup_on_send_error(monkeypatch):
    """DEF-1: auto_play enters Play Mode, then state poll raises => finally stops."""
    from unity_mcp.tools import runtime
    import asyncio as _asyncio

    poll_count = 0
    stop_called = []

    async def fake_send(cmd, args, **kw):
        nonlocal poll_count
        if cmd == "editor":
            action = args.get("action")
            if action == "play":
                return "ok"
            if action == "state":
                poll_count += 1
                if poll_count >= 2:
                    raise ConnectionError("UNITY_UNAVAILABLE")
                return "ok:EditMode"  # not yet playing
            if action == "stop":
                stop_called.append(True)
                return "ok"
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    monkeypatch.setattr(_asyncio, "sleep", AsyncMock(return_value=None))

    # Should propagate ConnectionError, but finally must still run
    try:
        await runtime.run_playtest_suite(
            pattern="*.playtest",
            auto_play=True,
            stop_after=True,
        )
    except ConnectionError:
        pass

    assert stop_called, "DEF-1: Play Mode not stopped after auto_play + send error"


@pytest.mark.asyncio
async def test_run_playtest_suite_cancel_during_run_stops_play_mode(monkeypatch):
    """Runtime: cancellation should still run stop transition before propagating."""
    from unity_mcp.tools import runtime
    import asyncio as _asyncio

    calls: list[tuple[str, dict]] = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        if cmd == "list_playtest_files":
            return "a.playtest"
        if cmd == "editor" and args.get("action") == "stop":
            return "ok"
        if cmd == "run_playtest":
            await _asyncio.sleep(10)
            return "PLAYTEST: 1/1 (0.1s) OK"
        raise AssertionError(f"unexpected command: {cmd} {args}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})

    task = _asyncio.create_task(runtime.run_playtest_suite("a.playtest", stop_after=True))
    await _asyncio.sleep(0.05)
    task.cancel()

    with pytest.raises(_asyncio.CancelledError):
        await task

    assert any(cmd == "editor" and args.get("action") == "stop" for cmd, args in calls)


# ── Bug 1 regression: _is_playtest_pass redundant-or fix (SonarCloud S1871) ───

def test_is_playtest_pass_empty_string_returns_false():
    """Bug 1: _is_playtest_pass('') must return False without raising IndexError."""
    from unity_mcp.tools.runtime import _is_playtest_pass
    assert _is_playtest_pass("") is False


def test_is_playtest_pass_valid_full_pass():
    """_is_playtest_pass returns True for complete passing ratio."""
    from unity_mcp.tools.runtime import _is_playtest_pass
    assert _is_playtest_pass("PLAYTEST: 3/3 (1.0s) OK") is True


def test_is_playtest_pass_partial_fail():
    """_is_playtest_pass returns False when not all assertions pass."""
    from unity_mcp.tools.runtime import _is_playtest_pass
    assert _is_playtest_pass("PLAYTEST: 1/3 (1.0s)\n[2] FAIL") is False


def test_is_playtest_pass_console_err_token():
    """_is_playtest_pass returns False when CONSOLE_ERR appears in result."""
    from unity_mcp.tools.runtime import _is_playtest_pass
    assert _is_playtest_pass("PLAYTEST: 2/2 (1.0s)\n[1] CONSOLE_ERR msg") is False


def test_is_playtest_pass_zero_total():
    """_is_playtest_pass returns False for 0/0 (no assertions)."""
    from unity_mcp.tools.runtime import _is_playtest_pass
    assert _is_playtest_pass("PLAYTEST: 0/0 (0.0s) OK") is False


# ── B17: both verdict sites read the ledger, not a text/regex scan ────────────

def _ledger_json(step_ok: bool, teardown_ok: bool = True) -> str:
    """Canonical B16 JSON receipt shape, one step whose source_file deliberately
    contains " OK" — the legacy text substring shortcut would say "pass" on
    sight; the ledger's `ok` field must be what actually decides the verdict."""
    import json as _json
    return _json.dumps({
        "schema_version": 1,
        "run_id": "r1",
        "passed": 1 if step_ok else 0,
        "failed": 0 if step_ok else 1,
        "duration_seconds": 0.1,
        "steps": [{
            "index": 0, "type": "Assert", "ok": step_ok, "ms": 1.0,
            "source_file": "Foo OK.playtest", "source_line": 1,
            "raw_passed": step_ok, "expected_fail": False,
        }],
        "outer": {"teardown_ok": teardown_ok, "scene_clean": True},
        "text_report": "whatever",
    })


def test_is_playtest_pass_reads_ledger_when_json():
    """format="json": all steps ok + teardown_ok -> True; one step false -> False."""
    from unity_mcp.tools.runtime import _is_playtest_pass
    assert _is_playtest_pass(_ledger_json(step_ok=True), "json") is True
    assert _is_playtest_pass(_ledger_json(step_ok=False), "json") is False
