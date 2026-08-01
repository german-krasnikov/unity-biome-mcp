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

async def test_run_playtest_fresh_passes_param(mock_bridge):
    """fresh=True passes fresh=true to C# bridge."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN", fresh=True)
    sent = mock_bridge.send.call_args[0][1]
    assert sent.get("fresh") == "true"


async def test_run_playtest_fresh_default_omits(mock_bridge):
    """fresh omitted by default."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    await run_playtest("ASSERT_CONSOLE_CLEAN")
    sent = mock_bridge.send.call_args[0][1]
    assert "fresh" not in sent


# ── #14B: restart_between ──────────────────────────────────────────────────────

async def test_run_playtest_suite_restart_between(monkeypatch):
    """restart_between=True issues editor stop+play between files."""
    from unity_mcp.tools import runtime
    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args))
        if cmd == "list_playtest_files":
            return "a.playtest\nb.playtest"
        if cmd == "run_playtest":
            return "PLAYTEST: 1/1 (0.1s) OK"
        if cmd == "editor":
            return "state: playing"
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    await runtime.run_playtest_suite("Playtests/*.playtest", restart_between=True, stop_after=False, auto_play=False)

    editor_cmds = [(c, a.get("action")) for c, a in calls if c == "editor"]
    assert ("editor", "stop") in editor_cmds
    assert ("editor", "play") in editor_cmds


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
    assert "no files" in result


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
