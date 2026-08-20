"""Tests for middleware post-call hook registry."""
import asyncio
import pytest
from unity_mcp.middleware_hooks import POST_HOOKS, register_post, run_post_hooks


@pytest.fixture(autouse=True)
def clean_hooks():
    """Isolate POST_HOOKS between tests."""
    saved = {k: v[:] for k, v in POST_HOOKS.items()}
    POST_HOOKS.clear()
    yield
    POST_HOOKS.clear()
    POST_HOOKS.update(saved)


def test_register_post_appends_in_order():
    calls: list[int] = []

    @register_post("mycmd")
    def h1(cmd, args, result, mw):
        calls.append(1)
        return result

    @register_post("mycmd")
    def h2(cmd, args, result, mw):
        calls.append(2)
        return result

    asyncio.run(run_post_hooks("mycmd", {}, "r", None))
    assert calls == [1, 2]


def test_run_post_hooks_unknown_cmd():
    result = asyncio.run(run_post_hooks("noop", {}, "original", None))
    assert result == "original"


def test_run_post_hooks_async_hook():
    @register_post("asynccmd")
    async def h(cmd, args, result, mw):
        return result + "-async"

    result = asyncio.run(run_post_hooks("asynccmd", {}, "base", None))
    assert result == "base-async"


def test_run_post_hooks_failing_hook_no_propagation():
    """A failing hook must not abort subsequent hooks (isolation)."""
    calls: list[str] = []

    @register_post("testcmd")
    def h1(cmd, args, result, mw):
        raise ValueError("hook failed")

    @register_post("testcmd")
    def h2(cmd, args, result, mw):
        calls.append("h2")
        return result + "-h2"

    result = asyncio.run(run_post_hooks("testcmd", {}, "base", None))
    assert "h2" in calls, "second hook must still run after first raises"
    assert result == "base-h2"
