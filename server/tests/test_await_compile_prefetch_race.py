"""Closes the await_compile <-> middleware prefetch/cache coverage gap.

test_await_compile.py monkeypatches code_intel._send with a bespoke router
(_make_send), never through wrap_send/Middleware — so the race between
recompile's (now-removed) GATE_PRIORS entry and get_compile_errors' (now-removed)
_READ_CACHEABLE membership had zero coverage. This file wires code_intel._send
to a REAL wrap_send(raw_send, Middleware()), mirroring the production wiring at
server.py:558 (_wrapped_send = wrap_send(_send_raw, _middleware)) and
code_intel.py:257-258 (bind(globals(), send, args)).

Scenario: `recompile` acks immediately (async write); a background prefetch
task may fire in its wake (pre-fix behavior) and race Unity's actual compile.
compile_status goes compiling -> idle; sync_status is unavailable, forcing the
same compile_status fallback path test_await_compile.py already exercises.
get_compile_errors returns the clean sentinel while Unity is still compiling
and the real error text once compile_status has reported idle — this lets the
test tell "served fresh, post-compile" apart from "served stale, pre-compile"
without any wall-clock dependency (asyncio.sleep is patched to a no-op).
"""
import asyncio

import pytest

import unity_mcp.tools.code_intel as _ci
from unity_mcp import editor_log
from unity_mcp.middleware import Middleware, wrap_send

REAL_ERROR_TEXT = "Assets/Broken.cs(3,5): error CS0246: type not found"


@pytest.fixture(autouse=True)
def _patch_sleep(monkeypatch):
    async def _no_sleep(_secs):
        return None

    monkeypatch.setattr(asyncio, "sleep", _no_sleep)


@pytest.fixture(autouse=True)
def _reset_ci_send():
    original = _ci._send
    yield
    _ci._send = original


@pytest.fixture(autouse=True)
def _reset_editor_log_globals(monkeypatch):
    # No log path -> corroborate() takes its "no log path" pass-through branch
    # (editor_log.py corroborate_compile_status: `if log_path is None: return
    # csharp_response`) instead of touching disk.
    monkeypatch.setattr(editor_log, "_cor_log_path", None)
    monkeypatch.setattr(editor_log, "_cor_project_path", None)


def _make_raw_send(call_log: list):
    """Scripted bridge send. `call_log` records, for each get_compile_errors
    call, whether Unity had already reported idle at that instant — the
    "post-compile" flag. A background prefetch fired right after recompile's
    ack always observes flag=False (Unity hasn't compiled yet); await_compile's
    own read, once the fix lands, always observes flag=True.
    """
    compile_status_seq = iter(["compiling|1.0", "idle|3.1"])
    state = {"last_status": "idle|3.1", "compiled": False}

    async def raw_send(cmd, args=None, timeout=30.0):
        if cmd == "recompile":
            return "ok"
        if cmd == "sync_status":
            raise ConnectionError("Command not registered: sync_status")
        if cmd == "compile_status":
            try:
                val = next(compile_status_seq)
                state["last_status"] = val
            except StopIteration:
                val = state["last_status"]
            if val.startswith("idle"):
                state["compiled"] = True
            return val
        if cmd == "get_compile_errors":
            call_log.append(state["compiled"])
            return REAL_ERROR_TEXT if state["compiled"] else "No compilation errors"
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return raw_send


async def test_await_compile_does_not_serve_precompile_cache_as_postcompile_result():
    """RED today: recompile's background prefetch (GATE_PRIORS) primes a
    get_compile_errors cache entry (_READ_CACHEABLE) before Unity has compiled
    anything. await_compile's own post-compile read must reach Unity fresh,
    never the stale pre-compile entry.

    Two independent effect signals (no substring scan):
      1. exact final string == the real post-compile error text.
      2. exact call log == [True] — the ONE wire call that determined the
         final output happened after compile_status reported idle, and no
         earlier wire call for this command happened at all.
    """
    call_log: list = []
    raw_send = _make_raw_send(call_log)
    mw = Middleware()
    wrapped = wrap_send(raw_send, mw)
    _ci._send = wrapped

    await wrapped("recompile", {})
    if mw._bg_tasks:
        await asyncio.gather(*mw._bg_tasks)

    result = await _ci.await_compile(timeout=60.0)

    assert result == REAL_ERROR_TEXT, (
        f"await_compile must return the real post-compile error, not a stale "
        f"pre-compile cache entry. Got: {result!r}"
    )
    assert call_log == [True], (
        f"get_compile_errors must reach Unity exactly once, after compile_status "
        f"reported idle — no earlier (pre-compile) wire call should occur at "
        f"all. Got call_log={call_log!r}"
    )
