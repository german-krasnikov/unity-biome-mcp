"""N0b/S8 row 4: run_playtest_suite's editor play/stop must WAIT for an
in-progress domain reload to clear before sending — never auto-resend the
unsafe write. Covers runtime._transition_play_state / _await_reload_idle.
"""
from unittest.mock import AsyncMock

from unity_mcp.bridge import DomainReloadError
from unity_mcp.errors import UncertainDeliveryError
from unity_mcp.server import run_playtest_suite
from unity_mcp.tools import runtime


def _editor_state(playing: bool) -> str:
    return f"playing:{'True' if playing else 'False'}\npaused:False\ncompiling:False"


def _args_factory(**kwargs) -> dict:
    """Mirrors server._args — dict factory dropping None values."""
    return {k: v for k, v in kwargs.items() if v is not None}


async def test_transition_play_state_waits_for_reload_idle_before_editor_send(monkeypatch):
    """compile_status reports compiling for 2 polls then idle; editor play must
    only be sent once, after idle is observed — never while reload is active."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    call_log: list[tuple[str, str | None]] = []
    compile_polls = 0

    async def fake_send(cmd, args=None, timeout=30.0):
        nonlocal compile_polls
        args = args or {}
        if cmd == "compile_status":
            call_log.append((cmd, None))
            compile_polls += 1
            return "compiling|0.5" if compile_polls <= 2 else "idle|1.0"
        if cmd == "editor":
            action = args.get("action")
            call_log.append((cmd, action))
            if action == "play":
                # Proves enforcement: sending before idle would still explode,
                # exactly like the real bridge's pre-queue DomainReloadError guard.
                if compile_polls <= 2:
                    raise DomainReloadError("reload in progress")
                return "ok"
            if action == "state":
                return _editor_state(playing=True)
        raise AssertionError(f"unexpected cmd {cmd!r}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    await runtime._transition_play_state(True)

    play_calls = [c for c in call_log if c == ("editor", "play")]
    assert len(play_calls) == 1
    # editor play must come after at least the 2 "compiling" polls settled.
    play_index = call_log.index(("editor", "play"))
    compile_calls_before = [c for c in call_log[:play_index] if c[0] == "compile_status"]
    assert len(compile_calls_before) >= 2


async def test_await_reload_idle_bounded_timeout_raises_not_hangs(monkeypatch):
    """compile_status never reports idle → bounded timeout raises instead of
    hanging forever (existing timeout-verdict path stays reachable)."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)

    async def always_compiling(cmd, args=None, timeout=30.0):
        assert cmd == "compile_status"
        return "compiling|9.0"

    monkeypatch.setattr(runtime, "_send", always_compiling)

    try:
        await runtime._await_reload_idle(timeout=0.02)
        raised = False
    except TimeoutError:
        raised = True
    assert raised


async def test_transition_play_state_uncertain_delivery_not_retried(monkeypatch):
    """A genuine UncertainDeliveryError from the editor send itself must
    propagate untouched — the wait step must never cause a resend."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    call_log: list[tuple[str, str | None]] = []

    async def fake_send(cmd, args=None, timeout=30.0):
        args = args or {}
        if cmd == "compile_status":
            call_log.append((cmd, None))
            return "idle|1.0"
        if cmd == "editor":
            action = args.get("action")
            call_log.append((cmd, action))
            if action == "stop":
                raise UncertainDeliveryError(cmd="editor", op_id="op-1", delivery="SENT")
        raise AssertionError(f"unexpected cmd {cmd!r}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    raised = False
    try:
        await runtime._transition_play_state(False)
    except UncertainDeliveryError:
        raised = True
    assert raised
    stop_calls = [c for c in call_log if c == ("editor", "stop")]
    assert len(stop_calls) == 1


async def test_run_playtest_suite_cleanup_uses_bounded_reload_wait(mock_bridge, monkeypatch):
    """run_playtest_suite's finally-block stop_after cleanup must wait for
    reload idle with the smaller _CLEANUP_RELOAD_WAIT_S bound, not the full
    _RELOAD_WAIT_TIMEOUT_S (90s) used before Play Mode entry -- a stuck
    cleanup must not block a caller for up to 90s."""
    async def dispatch(cmd, args=None, timeout=30.0):
        args = args or {}
        if cmd == "list_playtest_files":
            return {"ok": True, "data": "a.playtest"}
        if cmd == "run_playtest":
            return {"ok": True, "data": "PLAYTEST: 1/1 (0.1s) OK"}
        return {"ok": True, "data": "ok"}

    mock_bridge.send.side_effect = dispatch
    wait_mock = AsyncMock()
    monkeypatch.setattr(runtime, "_await_reload_idle", wait_mock)

    await run_playtest_suite("*.playtest", auto_play=False, stop_after=True)

    assert wait_mock.await_args_list == [
        ((), {"timeout": runtime._CLEANUP_RELOAD_WAIT_S}),
    ]
