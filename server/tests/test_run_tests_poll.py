"""TDD: run_tests fire-and-return-immediately (no inline polling)."""
import asyncio
import unity_mcp.tools.testing as scene_mod


def _make_send(responses: list):
    """Build an async _send that pops from responses list sequentially."""
    it = iter(responses)

    async def _send(cmd, args, **kw):
        val = next(it)
        if isinstance(val, Exception):
            raise val
        return val

    return _send


# ─── 1. TCP dies → returns "tests-started" immediately ──────────────────────

async def test_run_tests_returns_started_on_connection_error(monkeypatch):
    """TCP dies on run_tests → returns tests-started message (no polling)."""
    monkeypatch.setattr(scene_mod, "_send", _make_send([
        ConnectionError("TCP closed"),
    ]))
    result = await scene_mod.run_tests(mode="EditMode")
    assert "tests-started" in result
    assert "EditMode" in result


async def test_run_tests_returns_started_on_timeout_error(monkeypatch):
    """asyncio.TimeoutError on run_tests → returns tests-started."""
    monkeypatch.setattr(scene_mod, "_send", _make_send([
        asyncio.TimeoutError(),
    ]))
    result = await scene_mod.run_tests(mode="EditMode")
    assert "tests-started" in result


# ─── 2. Immediate return when result is full ─────────────────────────────────

async def test_run_tests_returns_immediately_on_full_result(monkeypatch):
    """_send returns full result synchronously → no polling, direct return."""
    call_log = []

    async def _send(cmd, args, **kw):
        call_log.append(cmd)
        return "tests: 100 passed, 0 failed"

    monkeypatch.setattr(scene_mod, "_send", _send)
    result = await scene_mod.run_tests(mode="EditMode")
    assert "100 passed" in result
    assert call_log == ["run_tests"]


# ─── 3. "pending" / "none" treated as no-result → returns started ───────────

async def test_run_tests_pending_returns_started(monkeypatch):
    """Unity returns 'pending' → treated as no result, returns started."""
    monkeypatch.setattr(scene_mod, "_send", _make_send(["pending"]))
    result = await scene_mod.run_tests(mode="EditMode")
    assert "tests-started" in result


async def test_run_tests_none_returns_started(monkeypatch):
    """Unity returns 'none' → treated as no result, returns started."""
    monkeypatch.setattr(scene_mod, "_send", _make_send(["none"]))
    result = await scene_mod.run_tests(mode="PlayMode")
    assert "tests-started" in result
    assert "PlayMode" in result


# ─── 4. filter param is forwarded ────────────────────────────────────────────

async def test_run_tests_filter_forwarded(monkeypatch):
    """filter arg is included in the run_tests command args."""
    captured = {}

    async def _send(cmd, args, **kw):
        captured[cmd] = args
        return "tests: 3 passed"

    monkeypatch.setattr(scene_mod, "_send", _send)
    await scene_mod.run_tests(mode="EditMode", filter="MyTest|OtherTest")
    assert captured["run_tests"]["filter"] == "MyTest|OtherTest"


# ─── 5. PlayMode returns started on disconnect ──────────────────────────────

async def test_run_tests_playmode_returns_started(monkeypatch):
    """PlayMode TCP dies → returns tests-started with PlayMode in message."""
    monkeypatch.setattr(scene_mod, "_send", _make_send([
        ConnectionError("TCP closed"),
    ]))
    result = await scene_mod.run_tests(mode="PlayMode")
    assert "tests-started" in result
    assert "PlayMode" in result
