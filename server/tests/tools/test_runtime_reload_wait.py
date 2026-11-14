"""N0b/S8 row 4: run_playtest_suite's editor play/stop must WAIT for an
in-progress domain reload to clear before sending — never auto-resend the
unsafe write. Covers runtime._transition_play_state / _await_reload_idle.
"""
from unittest.mock import AsyncMock

from unity_mcp.bridge import DomainReloadError
from unity_mcp.errors import SessionIdentityMismatch, UncertainDeliveryError
from unity_mcp.server import run_playtest_suite
from unity_mcp.tools import runtime


def _editor_state(playing: bool) -> str:
    return f"playing:{'True' if playing else 'False'}\npaused:False\ncompiling:False"


def _status_text(playing: bool) -> str:
    """get_status's 'key=value' response shape (distinct from 'editor state'
    which uses 'key:value')."""
    return f"scene=GridTest\ndirty=False\nplaying={'True' if playing else 'False'}"


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


async def test_transition_play_state_uncertain_delivery_observed_via_state_read(monkeypatch):
    """N3: a lost ACK after 'editor play' must be RECONCILED by polling the
    retry-safe get_status probe, never auto-resent. If the probe proves Play
    Mode was reached, the transition returns normally with exactly one play
    send and one get_status read — never a second 'editor play'."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    call_log: list[tuple[str, str | None]] = []

    async def fake_send(cmd, args=None, timeout=30.0):
        args = args or {}
        if cmd == "compile_status":
            return "idle|1.0"
        if cmd == "editor":
            action = args.get("action")
            call_log.append((cmd, action))
            if action == "play":
                raise UncertainDeliveryError(cmd="editor", op_id="op-1", delivery="SENT")
            raise AssertionError(f"unexpected editor action {action!r}")
        if cmd == "get_status":
            call_log.append((cmd, None))
            return _status_text(playing=True)
        raise AssertionError(f"unexpected cmd {cmd!r}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    await runtime._transition_play_state(True)

    assert call_log == [("editor", "play"), ("get_status", None)]


async def test_transition_play_state_uncertain_delivery_mismatch_reraises(monkeypatch):
    """Negative control: when the observed status does NOT match the desired
    transition, the original UncertainDeliveryError is re-raised unchanged —
    still exactly one play send, never a resend."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    call_log: list[tuple[str, str | None]] = []

    async def fake_send(cmd, args=None, timeout=30.0):
        args = args or {}
        if cmd == "compile_status":
            return "idle|1.0"
        if cmd == "editor":
            action = args.get("action")
            call_log.append((cmd, action))
            if action == "play":
                raise UncertainDeliveryError(cmd="editor", op_id="op-1", delivery="SENT")
            raise AssertionError(f"unexpected editor action {action!r}")
        if cmd == "get_status":
            call_log.append((cmd, None))
            return _status_text(playing=False)
        raise AssertionError(f"unexpected cmd {cmd!r}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    raised = False
    try:
        # Short bound: get_status never reports playing=True in this test,
        # so the observe-poll runs to its timeout before re-raising —
        # keep that bound tiny so the test stays fast.
        await runtime._transition_play_state(True, reload_wait_timeout=0.05)
    except UncertainDeliveryError:
        raised = True
    assert raised
    play_calls = [c for c in call_log if c == ("editor", "play")]
    assert len(play_calls) == 1


async def test_transition_play_state_stop_uncertain_delivery_observed_via_state_read(monkeypatch):
    """Symmetric case for 'stop': a lost ACK is reconciled by polling
    get_status for playing=False, not by resending 'editor stop'."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    call_log: list[tuple[str, str | None]] = []

    async def fake_send(cmd, args=None, timeout=30.0):
        args = args or {}
        if cmd == "compile_status":
            return "idle|1.0"
        if cmd == "editor":
            action = args.get("action")
            call_log.append((cmd, action))
            if action == "stop":
                raise UncertainDeliveryError(cmd="editor", op_id="op-2", delivery="SENT")
            raise AssertionError(f"unexpected editor action {action!r}")
        if cmd == "get_status":
            call_log.append((cmd, None))
            return _status_text(playing=False)
        raise AssertionError(f"unexpected cmd {cmd!r}")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    await runtime._transition_play_state(False)

    assert call_log == [("editor", "stop"), ("get_status", None)]


async def test_observe_play_state_retries_transient_get_status_failure(monkeypatch):
    """_observe_play_state's poll must survive a transient connection error
    on get_status (e.g. mid-reconnect during the same reload window) and
    keep polling — get_status is retry-safe, so this is never an
    'editor'-style unsafe-resend concern."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    attempts = 0

    async def fake_send(cmd, args=None, timeout=30.0):
        nonlocal attempts
        assert cmd == "get_status"
        attempts += 1
        if attempts == 1:
            raise ConnectionError("transient disconnect")
        return _status_text(playing=True)

    monkeypatch.setattr(runtime, "_send", fake_send)

    observed = await runtime._observe_play_state(True, timeout=5.0)

    assert observed is True
    assert attempts == 2


async def test_observe_play_state_ignores_stale_reading_before_expected_value(monkeypatch):
    """EditorApplication.isPlaying flips asynchronously: the first successful
    get_status read after reconnect can still report the PRE-transition
    value for a brief window. _observe_play_state must keep polling past a
    parseable-but-wrong reading rather than settling for it — regression
    guard for the live corpus failure this was traced to."""
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    attempts = 0

    async def fake_send(cmd, args=None, timeout=30.0):
        nonlocal attempts
        assert cmd == "get_status"
        attempts += 1
        # First reading is stale (still False); only the second is correct.
        return _status_text(playing=(attempts >= 2))

    monkeypatch.setattr(runtime, "_send", fake_send)

    observed = await runtime._observe_play_state(True, timeout=5.0)

    assert observed is True
    assert attempts == 2


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


async def test_wait_for_play_state_survives_uncertain_delivery_on_confirmation_poll(monkeypatch):
    """Live-traced gap: 'editor play' can be ACKed cleanly (no exception at
    the send site at all) and Unity can still start the domain reload a
    moment later, catching _wait_for_play_state's OWN 'editor state'
    confirmation poll mid-flight. That poll is a READ inside an
    already-bounded wait loop, not a write -- an UncertainDeliveryError on
    it must be absorbed and retried next iteration, never surfaced as if
    the transition itself failed."""
    monkeypatch.setattr(runtime, "_PLAY_STATE_POLL_INTERVAL", 0.001)
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    attempts = 0

    async def fake_send(cmd, args=None, timeout=30.0):
        nonlocal attempts
        args = args or {}
        if cmd == "compile_status":
            return "idle|1.0"
        assert cmd == "editor" and args.get("action") == "state"
        attempts += 1
        if attempts == 1:
            raise UncertainDeliveryError(cmd="editor", op_id="op-3", delivery="SENT")
        return _editor_state(playing=True)

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    await runtime._wait_for_play_state(True, "play")

    assert attempts == 2


async def test_wait_for_play_state_survives_domain_reload_error_on_confirmation_poll(monkeypatch):
    """Live-traced gap (second manifestation): once the bridge's reload
    tracker is marked active by the first uncertain read, its OWN
    pre-queue guard blocks every subsequent 'editor' send (never
    retry-safe) with DomainReloadError, for as long as the tracker stays
    marked -- a bare sleep-and-retry would hit this on every remaining
    attempt (confirmed live: all 15 outer polls). The confirmation poll
    must spend its wait budget on the retry-safe compile_status probe
    instead, which drives the reconnect that actually clears the tracker,
    so the NEXT 'editor state' attempt passes the guard."""
    monkeypatch.setattr(runtime, "_PLAY_STATE_POLL_INTERVAL", 0.001)
    monkeypatch.setattr(runtime, "_RELOAD_WAIT_POLL_S", 0.001)
    editor_attempts = 0
    compile_polls = 0

    async def fake_send(cmd, args=None, timeout=30.0):
        nonlocal editor_attempts, compile_polls
        args = args or {}
        if cmd == "compile_status":
            compile_polls += 1
            # First DomainReloadError comes with the tracker already marked
            # (real behaviour: should_retry() marks it before raising) --
            # compile_status is retry-safe so it passes the guard and, once
            # it reports idle, the reconnect it drove clears the tracker.
            return "idle|1.0"
        assert cmd == "editor" and args.get("action") == "state"
        editor_attempts += 1
        if editor_attempts == 1:
            raise UncertainDeliveryError(cmd="editor", op_id="op-4", delivery="SENT")
        if editor_attempts == 2:
            raise DomainReloadError("reload in progress")
        return _editor_state(playing=True)

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    await runtime._wait_for_play_state(True, "play")

    assert editor_attempts == 3
    assert compile_polls >= 1


async def test_wait_for_play_state_session_identity_mismatch_reraises(monkeypatch):
    """Negative control: a genuine session mismatch (reconnect landed on a
    DIFFERENT Unity instance/project) must NOT be absorbed like a
    transient reload hiccup -- it's a hard stop, surfaced immediately."""
    monkeypatch.setattr(runtime, "_PLAY_STATE_POLL_INTERVAL", 0.001)
    attempts = 0

    async def fake_send(cmd, args=None, timeout=30.0):
        nonlocal attempts
        attempts += 1
        raise SessionIdentityMismatch("reconnected to a different project")

    monkeypatch.setattr(runtime, "_send", fake_send)
    monkeypatch.setattr(runtime, "_args", _args_factory)

    raised = False
    try:
        await runtime._wait_for_play_state(True, "play")
    except SessionIdentityMismatch:
        raised = True
    assert raised
    assert attempts == 1
