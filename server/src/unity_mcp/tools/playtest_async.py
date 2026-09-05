"""Async start/poll pair for run_playtest(timeout > _RUN_PLAYTEST_SYNC_CEILING_S).

E04: routes a long-running playtest through Unity's non-blocking start_playtest
(E02) + get_playtest_run (E03) commands instead of one blocking run_playtest TCP
call. Unity's own per-command dispatch ceiling (MCPServer.cs's
RunPlaytestTimeoutSeconds, currently 130s) would otherwise cancel a single
blocking call before a long script finishes. This module owns the dispatch and
the bounded poll loop — runtime.py only branches into it (R-04: no polling
state machine in runtime.py).
"""
import asyncio

# Must stay < MCPServer.RunPlaytestTimeoutSeconds (unity-plugin/Editor/MCPServer.cs:65);
# the margin absorbs transport + dispatch. Cross-checked by
# tests/test_playtest_async.py's cross-language contract test.
_RUN_PLAYTEST_SYNC_CEILING_S = 120.0

# Tight polling burns tokens per get_playtest_run round trip; loose polling delays
# the caller's response — 1s balances both.
_PLAYTEST_POLL_INTERVAL_S = 1.0

_RUNNING_PHASE_PREFIX = "phase=running"
_RUN_ID_PREFIX = "run_id="


def _extract_run_id(start_response: str) -> str:
    """start_playtest's success response is exactly 'run_id=<id>' (E02)."""
    text = (start_response or "").strip()
    if not text.startswith(_RUN_ID_PREFIX):
        raise RuntimeError(f"start_playtest did not return a run_id: {text!r}")
    return text[len(_RUN_ID_PREFIX):]


async def run_via_start_poll(send, args: dict, timeout: float, tcp_buffer: float) -> str:
    """Dispatch start_playtest, then poll get_playtest_run until terminal.

    send: the caller's _send(cmd, args, timeout=...) — passed in rather than a
    bound module global so this module stays independently testable.
    args: the same run_playtest wire args runtime.py already builds (script/path/
    timeout/abort_on_fail/...) — start_playtest and run_playtest share the exact
    same gate/parse logic server-side (CommandRouter.TryBuildPlaytestRunRequest).
    tcp_buffer: reused from runtime.py's _TCP_PLAYTEST_BUFFER (single source) —
    both start_playtest and each get_playtest_run poll return near-instantly
    (Unity never blocks either on the playtest itself finishing), so the buffer
    alone is a sufficient per-call TCP timeout.
    Raises TimeoutError if the run does not reach a terminal state within the
    bounded poll budget derived from `timeout`.
    """
    start_raw = await send("start_playtest", args, timeout=tcp_buffer)
    run_id = _extract_run_id(start_raw)

    max_polls = int(timeout / _PLAYTEST_POLL_INTERVAL_S) + 1
    for _ in range(max_polls):
        await asyncio.sleep(_PLAYTEST_POLL_INTERVAL_S)
        poll_raw = await send("get_playtest_run", {"run_id": run_id}, timeout=tcp_buffer)
        if not (poll_raw or "").strip().startswith(_RUNNING_PHASE_PREFIX):
            return poll_raw
    raise TimeoutError(
        f"run_playtest(timeout={timeout}) did not reach a terminal state after "
        f"{max_polls} polls (run_id={run_id})"
    )
