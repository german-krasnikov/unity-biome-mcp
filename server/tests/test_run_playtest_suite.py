"""Tests for run_playtest_suite tool and _format_suite_report (P0.2)."""
import pytest
from unittest.mock import AsyncMock, call
from unity_mcp.server import run_playtest_suite
from unity_mcp.tools.runtime import _format_suite_report


# ── helpers ──────────────────────────────────────────────────────────────────

def _make_dispatch(files, playtest_response="PLAYTEST: 3/3 (1.0s) OK"):
    """side_effect factory: list_playtest_files → files, run_playtest → response."""
    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            return {"ok": True, "data": playtest_response}
        if cmd == "editor":
            return {"ok": True, "data": "stopped"}
        return {"ok": True, "data": "ok"}
    return dispatch


# ── tests ─────────────────────────────────────────────────────────────────────

async def test_glob_expands_via_list_playtest_files(mock_bridge):
    """Glob pattern triggers list_playtest_files call."""
    files = ["Playtests/a.playtest", "Playtests/b.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files)
    result = await run_playtest_suite("Playtests/*.playtest", stop_after=False)
    cmds = [c[0][0] for c in mock_bridge.send.call_args_list]
    assert cmds[0] == "list_playtest_files"
    assert "SUITE: 2/2" in result


async def test_files_run_sequentially(mock_bridge):
    """run_playtest called in order for each file."""
    files = ["Playtests/a.playtest", "Playtests/b.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files)
    await run_playtest_suite("Playtests/*.playtest", stop_after=False)
    run_calls = [c for c in mock_bridge.send.call_args_list if c[0][0] == "run_playtest"]
    assert len(run_calls) == 2
    assert run_calls[0][0][1]["path"] == "Playtests/a.playtest"
    assert run_calls[1][0][1]["path"] == "Playtests/b.playtest"


async def test_stop_on_fail_stops_early(mock_bridge):
    """stop_on_fail=True: only first file run when it fails."""
    files = ["a.playtest", "b.playtest", "c.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files, playtest_response="PLAYTEST: 0/1 (1.0s)\n[1] ASSERT x==1 — FAIL")
    await run_playtest_suite("*.playtest", stop_on_fail=True, stop_after=False)
    run_calls = [c for c in mock_bridge.send.call_args_list if c[0][0] == "run_playtest"]
    assert len(run_calls) == 1


async def test_stop_after_calls_editor_stop(mock_bridge):
    """stop_after=True sends editor stop after suite."""
    files = ["a.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files)
    await run_playtest_suite("*.playtest", stop_after=True)
    cmds = [c[0][0] for c in mock_bridge.send.call_args_list]
    assert "editor" in cmds
    editor_call = next(c for c in mock_bridge.send.call_args_list if c[0][0] == "editor")
    assert editor_call[0][1].get("action") == "stop"


async def test_no_stop_if_stop_after_false(mock_bridge):
    """stop_after=False: editor NOT called."""
    files = ["a.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files)
    await run_playtest_suite("*.playtest", stop_after=False)
    cmds = [c[0][0] for c in mock_bridge.send.call_args_list]
    assert "editor" not in cmds


async def test_matrix_format_ok_lines(mock_bridge):
    """All pass: output starts with SUITE summary and OK lines."""
    files = ["Playtests/a.playtest", "Playtests/b.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files)
    result = await run_playtest_suite("Playtests/*.playtest", stop_after=False)
    assert result.startswith("SUITE: 2/2 passed")
    assert "OK  " in result
    assert "a.playtest" in result


async def test_failure_details_preserved(mock_bridge):
    """Failure raw output included in report (not compressed out)."""
    fail_raw = "PLAYTEST: 0/1 (1.0s)\n[1] ASSERT score == 10 — FAIL got 5"
    mock_bridge.send.side_effect = _make_dispatch(["a.playtest"], playtest_response=fail_raw)
    result = await run_playtest_suite("*.playtest", stop_after=False)
    assert "FAIL got 5" in result
    assert "SUITE: 0/1" in result


async def test_comma_separated_paths(mock_bridge):
    """Comma-separated paths: no list_playtest_files call."""
    mock_bridge.send.return_value = {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
    result = await run_playtest_suite("Playtests/a.playtest,Playtests/b.playtest", stop_after=False)
    cmds = [c[0][0] for c in mock_bridge.send.call_args_list]
    assert "list_playtest_files" not in cmds
    assert cmds.count("run_playtest") == 2


async def test_err_response_counted_as_failure(mock_bridge):
    """err: response from Unity counts as failure, not pass."""
    files = ["a.playtest", "b.playtest"]
    call_count = [0]
    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            call_count[0] += 1
            if call_count[0] == 1:
                return {"ok": True, "data": "err: file not found: a.playtest"}
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        return {"ok": True, "data": "ok"}
    mock_bridge.send.side_effect = dispatch
    result = await run_playtest_suite("*.playtest", stop_after=False)
    assert "SUITE: 1/2" in result
    assert "FAIL" in result


async def test_format_suite_report_unit():
    """_format_suite_report produces correct summary line and per-file rows."""
    results = [
        ("Playtests/a.playtest", "PLAYTEST: 3/3 (1.0s)", 1.0, True),
        ("Playtests/b.playtest", "PLAYTEST: 0/1 (2.0s)\n[1] ASSERT x==1 — FAIL", 2.0, False),
    ]
    report = _format_suite_report(results, 3.0)
    assert report.startswith("SUITE: 1/2 passed (3.0s)")
    assert "OK  " in report
    assert "FAIL" in report
    assert "a.playtest" in report
    assert "b.playtest" in report
    assert "ASSERT x==1 — FAIL" in report  # failure details included


# ── Phase 7b: step_part only for failures ─────────────────────────────────────

def test_format_ok_line_has_no_step_info():
    """Phase 7b: passing tests show pure matrix line (no step counter)."""
    results = [("Playtests/a.playtest", "PLAYTEST: 3/3 (1.0s)", 1.0, True)]
    report = _format_suite_report(results, 1.0)
    ok_line = [l for l in report.splitlines() if "a.playtest" in l][0]
    assert "3/3" not in ok_line, f"Step info should not appear on OK line: {ok_line!r}"


def test_format_fail_line_includes_step_info():
    """Phase 7b: failing tests show step counter on the per-test row."""
    raw = "PLAYTEST: 1/3 (2.0s)\n[2] ASSERT score == 10 — FAIL got 5"
    results = [("Playtests/b.playtest", raw, 2.0, False)]
    report = _format_suite_report(results, 2.0)
    fail_line = [l for l in report.splitlines() if "b.playtest" in l][0]
    assert "1/3" in fail_line, f"Step info missing from FAIL line: {fail_line!r}"


# ── Phase 3a: auto_play param ─────────────────────────────────────────────────

async def test_auto_play_false_does_not_call_editor(mock_bridge):
    """auto_play=False (default): no editor calls before suite runs."""
    files = ["a.playtest"]
    mock_bridge.send.side_effect = _make_dispatch(files)
    await run_playtest_suite("a.playtest", auto_play=False, stop_after=False)
    editor_calls = [c for c in mock_bridge.send.call_args_list if c[0][0] == "editor"]
    assert not editor_calls, "auto_play=False must not call editor"


async def test_auto_play_true_enters_play_when_not_playing(mock_bridge):
    """auto_play=True: calls editor(play) when state does not contain 'playing:True'."""
    files = ["a.playtest"]
    call_log = []

    async def dispatch(cmd, args, timeout=30.0):
        call_log.append((cmd, dict(args)))
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "editor":
            action = args.get("action", "")
            if action == "state":
                # After play is called, return playing (colon format); before, return stopped
                played = any(a == "play" for _, a in [(c, d.get("action", "")) for c, d in call_log])
                state_str = "playing:True\npaused:False\ncompiling:False" if played else "playing:False\npaused:False\ncompiling:False"
                return {"ok": True, "data": state_str}
            return {"ok": True, "data": "ok"}
        if cmd == "run_playtest":
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch
    await run_playtest_suite("a.playtest", auto_play=True, stop_after=False)
    play_calls = [(c, d) for c, d in call_log if c == "editor" and d.get("action") == "play"]
    assert play_calls, "auto_play=True must call editor(action=play) when not playing"


async def test_auto_play_true_skips_play_when_already_playing(mock_bridge):
    """auto_play=True: does NOT call play when already in play mode (colon format)."""
    files = ["a.playtest"]

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "editor":
            # Correct C# EditorStateHelper.GetState() colon format
            return {"ok": True, "data": "playing:True\npaused:False\ncompiling:False"}
        if cmd == "run_playtest":
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch
    await run_playtest_suite("a.playtest", auto_play=True, stop_after=False)
    play_calls = [c for c in mock_bridge.send.call_args_list
                  if c[0][0] == "editor" and c[0][1].get("action") == "play"]
    assert not play_calls, "auto_play=True must not call play when already playing"


# ── P-336: state format fix (colon format from EditorStateHelper.GetState()) ──

async def test_auto_play_colon_format_already_playing(mock_bridge):
    """P-336: editor state 'playing:True\\n...' (C# colon format) must be recognized.
    auto_play=True: no editor(play) call when response already contains 'playing:True'."""
    files = ["a.playtest"]

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "editor":
            # C# EditorStateHelper.GetState() actual format — colon not space
            return {"ok": True, "data": "playing:True\npaused:False\ncompiling:False"}
        if cmd == "run_playtest":
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch
    await run_playtest_suite("a.playtest", auto_play=True, stop_after=False)
    play_calls = [c for c in mock_bridge.send.call_args_list
                  if c[0][0] == "editor" and c[0][1].get("action") == "play"]
    assert not play_calls, (
        "P-336: already playing (colon format) must not trigger editor(play). "
        "Fix: check 'playing:true' not 'state: playing'."
    )


async def test_auto_play_colon_format_not_playing_enters_play(mock_bridge):
    """P-336: editor state 'playing:False\\n...' must trigger play."""
    files = ["a.playtest"]
    call_log = []

    async def dispatch(cmd, args, timeout=30.0):
        call_log.append((cmd, dict(args)))
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "editor":
            action = args.get("action", "")
            if action == "state":
                played = any(a == "play" for _, a in [(c, d.get("action", "")) for c, d in call_log])
                state_str = "playing:True\npaused:False\ncompiling:False" if played else "playing:False\npaused:False\ncompiling:False"
                return {"ok": True, "data": state_str}
            return {"ok": True, "data": "ok"}
        if cmd == "run_playtest":
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch
    await run_playtest_suite("a.playtest", auto_play=True, stop_after=False)
    play_calls = [(c, d) for c, d in call_log if c == "editor" and d.get("action") == "play"]
    assert play_calls, "P-336: not playing (colon format) must call editor(play)"


# ── P-325: lifecycle FSM fail-closed ─────────────────────────────────────────

async def test_restart_between_lifecycle_error_in_report(mock_bridge):
    """P-325: stop error during restart_between must appear in suite report (not swallowed)."""
    files = ["a.playtest", "b.playtest"]
    stop_call_n = [0]

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        if cmd == "editor":
            action = args.get("action", "")
            if action == "stop":
                stop_call_n[0] += 1
                if stop_call_n[0] == 1:  # the inter-file restart stop
                    raise RuntimeError("Unity stopped responding")
            return {"ok": True, "data": "playing:True\npaused:False\ncompiling:False"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch
    result = await run_playtest_suite(
        "*.playtest", restart_between=True, stop_after=False
    )
    assert "LIFECYCLE_ERR" in result or "(restart)" in result, (
        "P-325: lifecycle error must appear in suite report, not be silently swallowed. "
        f"Got: {result!r}"
    )


async def test_restart_between_stop_on_fail_terminates_on_lifecycle_error(mock_bridge):
    """P-325: stop_on_fail=True + lifecycle error aborts suite (only 1 file result)."""
    files = ["a.playtest", "b.playtest", "c.playtest"]
    stop_call_n = [0]

    async def dispatch(cmd, args, timeout=30.0):
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "\n".join(files)}
        if cmd == "run_playtest":
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        if cmd == "editor":
            action = args.get("action", "")
            if action == "stop":
                stop_call_n[0] += 1
                if stop_call_n[0] == 1:
                    raise RuntimeError("Unity stopped responding")
            return {"ok": True, "data": "playing:True\npaused:False\ncompiling:False"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch
    result = await run_playtest_suite(
        "*.playtest", restart_between=True, stop_on_fail=True, stop_after=False
    )
    # Suite should abort: only first file result + error entry, not all 3
    run_calls = [c for c in mock_bridge.send.call_args_list if c[0][0] == "run_playtest"]
    assert len(run_calls) == 1, (
        f"P-325: stop_on_fail + lifecycle error must abort suite. ran {len(run_calls)} files"
    )
