"""Lifespan invariant: no task/resource leak after teardown.

Regression: watchdog.cancel() in finally was memorial-fixed. This pins the contract.
"""
import asyncio

import pytest


def _import_lifespan():
    try:
        from unity_mcp.server import lifespan
        return lifespan
    except ImportError:
        pytest.skip("lifespan not importable")


async def test_lifespan_cancels_watchdog(monkeypatch):
    """Regression: lifespan finally MUST call watchdog.cancel().

    Memorial fix in Tier 1 added watchdog.cancel() to lifespan finally block.
    This test fails if that call is removed.
    """
    lifespan = _import_lifespan()
    monkeypatch.setenv("UNITY_MCP_WATCHDOG", "1")
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")
    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")

    from unity_mcp import server as srv

    cancel_called = []

    class FakeWatchdog:
        def __init__(self, *a, **kw):
            pass

        async def cancel(self):
            cancel_called.append(True)

        def maybe_trigger(self, *a, **kw):
            pass

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        async def connect(self, *a, **kw): return "no Unity (fake)"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", lambda **_: FakeSlot())
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)
    # ProactiveWatchdog is locally imported inside lifespan() from .watchdog
    monkeypatch.setattr("unity_mcp.watchdog.ProactiveWatchdog", FakeWatchdog)

    class FakeApp: pass
    async with lifespan(FakeApp()):
        await asyncio.sleep(0.01)

    assert cancel_called, "watchdog.cancel() was not called in lifespan finally — regression!"


async def test_lifespan_no_leak_when_features_disabled(monkeypatch):
    lifespan = _import_lifespan()
    for k in ("UNITY_MCP_WATCHDOG", "UNITY_MCP_HINTS", "UNITY_MCP_MIDDLEWARE",
              "UNITY_MCP_LESSONS", "UNITY_MCP_INFERENCE", "UNITY_MCP_SPECULATION",
              "UNITY_MCP_SCENE_BRIEF"):
        monkeypatch.delenv(k, raising=False)
    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")

    from unity_mcp import server as srv

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        async def connect(self, *a, **kw): return "no Unity (fake)"
        async def close(self): pass

    monkeypatch.setattr(srv, "ConnectionSlot", lambda **_: FakeSlot())
    # Restore server globals after test — lifespan writes them
    monkeypatch.setattr(srv, "slot", None)
    monkeypatch.setattr(srv, "manager", None)
    monkeypatch.setattr(srv, "_middleware", None)

    tasks_before = {t for t in asyncio.all_tasks() if not t.done()}

    class FakeApp: pass
    async with lifespan(FakeApp()):
        pass

    await asyncio.sleep(0.01)
    tasks_after = {t for t in asyncio.all_tasks() if not t.done()}
    leaked = tasks_after - tasks_before - {asyncio.current_task()}
    assert not leaked, f"Baseline leak: {[t.get_name() for t in leaked]}"


# ---------------------------------------------------------------------------
# X4.cross.1: UNITY_MCP_MIDDLEWARE=1 alone does not enable visual-verify;
# both UNITY_MCP_MIDDLEWARE=1 AND UNITY_MCP_VISUAL_VERIFY=1 are required.
# ---------------------------------------------------------------------------

def test_sampling_service_enabled_requires_visual_verify(monkeypatch):
    """SamplingService.enabled is False with only MIDDLEWARE=1 set."""
    monkeypatch.setenv("UNITY_MCP_MIDDLEWARE", "1")
    monkeypatch.delenv("UNITY_MCP_VISUAL_VERIFY", raising=False)
    from unity_mcp.sampling import SamplingService
    svc = SamplingService()
    assert not svc.enabled, "MIDDLEWARE alone must not activate visual-verify"


def test_sampling_service_enabled_with_both_envs(monkeypatch):
    """SamplingService.enabled is True when both MIDDLEWARE=1 and VISUAL_VERIFY=1 are set."""
    monkeypatch.setenv("UNITY_MCP_MIDDLEWARE", "1")
    monkeypatch.setenv("UNITY_MCP_VISUAL_VERIFY", "1")
    from unity_mcp.sampling import SamplingService
    svc = SamplingService()
    assert svc.enabled, "MIDDLEWARE + VISUAL_VERIFY must enable visual-verify"


# ---------------------------------------------------------------------------
# P9: init_corroboration re-called on reconnect
# ---------------------------------------------------------------------------

async def test_init_corroboration_called_on_reconnect(monkeypatch):
    """_on_reconnect callback calls init_corroboration(port=...) to re-resolve project. (P9)"""
    from unittest.mock import MagicMock, patch
    from unity_mcp import server as srv

    reconnect_callbacks = []
    init_corroboration_calls = []

    class FakeSlot:
        bridge = MagicMock()
        bridge.connected = True
        bridge.start_heartbeat = MagicMock()
        connected = False
        port = 9500

        async def connect(self, *a, **kw): return "ok"
        async def close(self): pass

        def add_reconnect_callback(self, cb):
            reconnect_callbacks.append(cb)

    with patch("unity_mcp.editor_log.init_corroboration",
               side_effect=lambda **kw: init_corroboration_calls.append(kw)) as mock_ic, \
         patch.object(srv, "ConnectionSlot", lambda **_: FakeSlot()), \
         patch.object(srv, "slot", None), \
         patch.object(srv, "manager", None), \
         patch.object(srv, "_middleware", None), \
         patch("unity_mcp.server._refresh_tools_cache", new=lambda *a: asyncio.sleep(0)), \
         patch("unity_mcp.server._push_catalog", new=lambda *a: asyncio.sleep(0)):

        monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
        monkeypatch.setenv("UNITY_MCP_HINTS", "0")
        monkeypatch.delenv("UNITY_MCP_WATCHDOG", raising=False)

        lifespan = _import_lifespan()

        class FakeApp:
            pass

        async with lifespan(FakeApp()):
            pass

    # Find the _on_reconnect callback (first registered) and invoke it
    on_reconnect_cb = next(
        (cb for cb in reconnect_callbacks
         if hasattr(cb, "__qualname__") and "_on_reconnect" in cb.__qualname__),
        None,
    )
    if on_reconnect_cb is None and reconnect_callbacks:
        on_reconnect_cb = reconnect_callbacks[0]

    assert on_reconnect_cb is not None, "no _on_reconnect callback was registered"
    try:
        on_reconnect_cb()
    except AttributeError:
        pass  # other parts of callback may fail outside lifespan context
    # init_corroboration must have been called with port kwarg
    assert any("port" in call for call in init_corroboration_calls), (
        "P9: init_corroboration must be called with port= in _on_reconnect"
    )


# ---------------------------------------------------------------------------
# Zombie cleanup: cleanup_stale_locks called during lifespan startup
# ---------------------------------------------------------------------------

async def test_lifespan_calls_cleanup_stale_locks_on_startup(monkeypatch):
    """lifespan() must call cleanup_stale_locks() before acquire_lock()."""
    from unittest.mock import MagicMock, patch
    from unity_mcp import server as srv

    cleanup_calls = []

    class FakeSlot:
        bridge = None
        connected = False
        port = 9500
        async def connect(self, *a, **kw): return "no Unity (fake)"
        async def close(self): pass

    monkeypatch.setenv("UNITY_MCP_BUDGET", "0")
    monkeypatch.setenv("UNITY_MCP_HINTS", "0")
    monkeypatch.delenv("UNITY_MCP_WATCHDOG", raising=False)

    with patch("unity_mcp.server.cleanup_stale_locks",
               side_effect=lambda port, **kw: cleanup_calls.append(port) or 0) as mock_cleanup, \
         patch.object(srv, "ConnectionSlot", lambda **_: FakeSlot()), \
         patch.object(srv, "slot", None), \
         patch.object(srv, "manager", None), \
         patch.object(srv, "_middleware", None):

        lifespan = _import_lifespan()
        class FakeApp: pass
        async with lifespan(FakeApp()):
            pass

    assert cleanup_calls, "cleanup_stale_locks() was never called during lifespan startup"
