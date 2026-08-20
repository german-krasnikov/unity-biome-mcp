"""Tests for fresh=True world readiness gate (MCP-LIFE-005).

Python intercepts fresh=True, manages lifecycle (stop→play→wait_for_ready),
then sends run_playtest WITHOUT the fresh flag to C#.
"""
from unittest.mock import AsyncMock


async def test_fresh_true_waits_for_ready_before_execution(monkeypatch):
    """fresh=True: Python enters Play Mode, waits for world_ready, then runs test.

    Verifies call ORDER: editor-play → state-poll(world_ready) → run_playtest(no fresh).
    """
    from unity_mcp.tools import runtime

    calls = []
    playing = False
    poll_count = 0

    async def fake_send(cmd, args, **kw):
        nonlocal playing, poll_count
        calls.append((cmd, args.copy()))
        if cmd == "editor":
            action = args.get("action")
            if action == "state":
                poll_count += 1
                world_ready = poll_count >= 2  # ready on 2nd state poll
                return f"playing:{playing}\nplay_epoch:1\nworld_ready:{world_ready}"
            if action == "play":
                playing = True
                return "entered"
            if action == "stop":
                playing = False
                return "ok"
        if cmd == "run_playtest":
            return "PLAYTEST: 1/1 (0.1s) OK"
        return "ok"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(
        runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None}
    )

    result = await runtime.run_playtest("ASSERT_CONSOLE_CLEAN", fresh=True)

    # Python must have issued editor play
    editor_actions = [a.get("action") for cmd, a in calls if cmd == "editor"]
    assert "play" in editor_actions, f"editor play not called; editor actions: {editor_actions}"

    # State was polled to check world_ready
    state_polls = [a for cmd, a in calls if cmd == "editor" and a.get("action") == "state"]
    assert len(state_polls) >= 1, "Expected at least one editor state poll for world_ready"

    # run_playtest sent to C# WITHOUT fresh param
    playtest_calls = [(cmd, a) for cmd, a in calls if cmd == "run_playtest"]
    assert len(playtest_calls) == 1
    assert "fresh" not in playtest_calls[0][1], (
        "fresh must NOT be forwarded to C# when Python handles fresh lifecycle"
    )

    assert "PLAYTEST" in result


async def test_fresh_true_timeout_returns_typed_error(monkeypatch):
    """fresh=True: if world_ready never arrives, return a typed error (not exception)."""
    from unity_mcp.tools import runtime

    playing = False

    async def fake_send(cmd, args, **kw):
        nonlocal playing
        if cmd == "editor":
            action = args.get("action")
            if action == "play":
                playing = True
                return "entered"
            if action == "state":
                # Always world_ready:False — never becomes ready
                return f"playing:{playing}\nplay_epoch:1\nworld_ready:False"
            if action == "stop":
                playing = False
                return "ok"
        return "PLAYTEST: 1/1 (0.1s) OK"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(
        runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None}
    )
    monkeypatch.setattr(runtime, "_FRESH_READINESS_TIMEOUT", 0.05)
    monkeypatch.setattr(runtime, "_FRESH_POLL_INTERVAL", 0.0)

    result = await runtime.run_playtest("ASSERT_CONSOLE_CLEAN", fresh=True)

    # Must return typed error string, not raise
    assert "not ready" in result.lower(), f"Expected 'not ready' in result; got: {result!r}"
    # Must NOT be a passing playtest result
    assert "PLAYTEST: 1/1" not in result


async def test_fresh_false_skips_readiness_wait(monkeypatch):
    """fresh=False: no Python lifecycle management; run_playtest sent directly to C#."""
    from unity_mcp.tools import runtime

    calls = []

    async def fake_send(cmd, args, **kw):
        calls.append((cmd, args.copy()))
        return "PLAYTEST: 1/1 (0.1s) OK"

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(
        runtime, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None}
    )

    await runtime.run_playtest("ASSERT_CONSOLE_CLEAN", fresh=False)

    editor_calls = [c for c, _ in calls if c == "editor"]
    assert not editor_calls, (
        f"fresh=False must not trigger editor lifecycle calls, got: {editor_calls}"
    )

    playtest_calls = [c for c, _ in calls if c == "run_playtest"]
    assert len(playtest_calls) == 1
