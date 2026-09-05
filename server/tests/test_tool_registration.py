"""Tests for register() in tool modules — Pattern B audit.

Guards against silent argument-order breakage. Each test:
  1. Calls register(mcp, send, args)
  2. Confirms _send and _args module globals are set
  3. Confirms mcp.tool was called (tools actually wired)
"""
import pytest
from unittest.mock import AsyncMock, MagicMock
import importlib


@pytest.fixture(autouse=True)
def _restore_tool_globals():
    """Restore module-level _send/_args after each test to avoid cross-test pollution.

    B2: console/screenshot/testing/editor_control added (split out of scene.py).
    """
    import unity_mcp.tools.objects as obj_mod
    import unity_mcp.tools.runtime as rt_mod
    import unity_mcp.tools.scene as sc_mod
    import unity_mcp.tools.console as con_mod
    import unity_mcp.tools.screenshot as shot_mod
    import unity_mcp.tools.testing as test_mod
    import unity_mcp.tools.editor_control as ec_mod
    saved = {
        "obj": (obj_mod._send, obj_mod._args),
        "rt": (rt_mod._send, rt_mod._args),
        "sc": (sc_mod._send, sc_mod._args),
        "con": (con_mod._send, con_mod._args),
        "shot": (shot_mod._send, shot_mod._args),
        "test": (test_mod._send, test_mod._args),
        "ec": (ec_mod._send, ec_mod._args),
    }
    yield
    obj_mod._send, obj_mod._args = saved["obj"]
    rt_mod._send, rt_mod._args = saved["rt"]
    sc_mod._send, sc_mod._args = saved["sc"]
    con_mod._send, con_mod._args = saved["con"]
    shot_mod._send, shot_mod._args = saved["shot"]
    test_mod._send, test_mod._args = saved["test"]
    ec_mod._send, ec_mod._args = saved["ec"]


def _make_mcp():
    """Minimal mcp stub: mcp.tool(annotations=X)(fn) must not raise."""
    mcp = MagicMock()
    # mcp.tool(annotations=...) returns a decorator that accepts any callable
    mcp.tool.return_value = lambda fn: fn
    return mcp


# ── Part 1: objects.py ────────────────────────────────────────────────────────

def test_objects_register_sets_send():
    import unity_mcp.tools.objects as mod
    # Reset state so test is isolated
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_objects_register_wires_tools():
    import unity_mcp.tools.objects as mod
    send = AsyncMock()
    mcp = _make_mcp()

    mod.register(mcp, send, MagicMock())

    # At least get_component and set_property must be registered
    assert mcp.tool.call_count >= 8


# ── Part 2: scene.py (B2: slimmed — get_hierarchy/scene/search_scene/fingerprint/
#    scene_diff/scene_environment + scene.py delegation only) ──────────────────

def test_scene_register_sets_send():
    import unity_mcp.tools.scene as mod
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_scene_register_wires_tools():
    import unity_mcp.tools.scene as mod
    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # get_hierarchy, scene, search_scene, fingerprint, scene_diff, scene_environment (6)
    # + scene.py delegation: save_session, load_session, screenshot_baseline,
    # screenshot_compare, get_changes (5) = 11
    assert mcp.tool.call_count >= 11


# ── Part 3: runtime.py ────────────────────────────────────────────────────────

def test_runtime_register_sets_send():
    import unity_mcp.tools.runtime as mod
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_runtime_register_wires_tools():
    import unity_mcp.tools.runtime as mod
    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # invoke_method, wait_until, move_to, query_state, test_step, run_playtest = 6 tools minimum
    assert mcp.tool.call_count >= 6


# ── Part 4: console.py / screenshot.py / testing.py / editor_control.py
#    (B2: split out of scene.py) ──────────────────────────────────────────────

def test_console_register_sets_send(monkeypatch):
    import unity_mcp.tools.console as mod
    # register() calls editor_log.init_corroboration — stub it (moved from scene.py's
    # register(), same idempotent side effect, same reason to stub in unit tests)
    monkeypatch.setattr("unity_mcp.editor_log.init_corroboration", lambda: None, raising=False)
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_console_register_wires_tools(monkeypatch):
    import unity_mcp.tools.console as mod
    monkeypatch.setattr("unity_mcp.editor_log.init_corroboration", lambda: None, raising=False)
    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # get_console, get_compile_errors, recompile = 3 tools
    assert mcp.tool.call_count >= 3


def test_screenshot_register_sets_send():
    import unity_mcp.tools.screenshot as mod
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_screenshot_register_wires_tools():
    import unity_mcp.tools.screenshot as mod
    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # screenshot = 1 tool
    assert mcp.tool.call_count == 1


def test_testing_register_sets_send():
    import unity_mcp.tools.testing as mod
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_testing_register_wires_tools():
    import unity_mcp.tools.testing as mod
    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # run_tests, get_test_results, get_test_count, get_test_progress = 4 tools
    assert mcp.tool.call_count >= 3


def test_editor_control_register_sets_send():
    import unity_mcp.tools.editor_control as mod
    mod._send = None
    mod._args = None

    send = AsyncMock()
    args = MagicMock()
    mcp = _make_mcp()

    mod.register(mcp, send, args)

    assert mod._send is send
    assert mod._args is args


def test_editor_control_register_wires_tools():
    import unity_mcp.tools.editor_control as mod
    mcp = _make_mcp()
    mod.register(mcp, AsyncMock(), MagicMock())

    # editor, ping_object, get_selection, checkpoint, undo_last, get_capabilities = 6 tools
    assert mcp.tool.call_count >= 6


def test_b2_split_modules_wired_into_register_all():
    """B2: scene.py split into console/screenshot/testing/editor_control — each
    new module must actually be wired into register_all()'s module iteration list,
    not just importable in isolation."""
    import inspect
    import unity_mcp.tools as tools_pkg
    src = inspect.getsource(tools_pkg.register_all)
    for name in ("console", "screenshot", "testing", "editor_control"):
        assert name in src, f"{name} missing from register_all() module wiring"


def test_register_all_wires_uitk_module_exactly_once(monkeypatch):
    """Schema export wires UITK once without rebinding every tool module."""
    import unity_mcp.tools as tools_pkg

    register_uitk = MagicMock()
    # register_all() binds module-global transports. This call-count test does
    # not need any real registration, so mock every other imported registrar
    # instead of leaking a fake _send/_args/get_slot into later tests.
    for candidate in vars(tools_pkg).values():
        register = getattr(candidate, "register", None)
        if candidate is tools_pkg.uitk or not callable(register):
            continue
        monkeypatch.setattr(candidate, "register", MagicMock())
    monkeypatch.setattr(tools_pkg.uitk, "register", register_uitk)
    monkeypatch.setattr(tools_pkg, "register_metrics", MagicMock())

    tools_pkg.register_all(
        _make_mcp(),
        AsyncMock(),
        MagicMock(),
        get_slot=lambda: None,
    )

    register_uitk.assert_called_once()


def test_b2_split_total_tool_count_unchanged(monkeypatch):
    """Split modules include the durable test-run protocol tools."""
    monkeypatch.setattr("unity_mcp.editor_log.init_corroboration", lambda: None, raising=False)
    import unity_mcp.tools.scene as scene_mod
    import unity_mcp.tools.console as console_mod
    import unity_mcp.tools.screenshot as screenshot_mod
    import unity_mcp.tools.testing as testing_mod
    import unity_mcp.tools.editor_control as ec_mod

    def _own_count(mod):
        mcp = _make_mcp()
        mod.register(mcp, AsyncMock(), MagicMock())
        return mcp.tool.call_count

    scene_total = _own_count(scene_mod)  # includes scene.py's 5 delegated tools
    scene_own = scene_total - 5
    total = (scene_own + _own_count(console_mod) + _own_count(screenshot_mod)
             + _own_count(testing_mod) + _own_count(ec_mod))
    assert total == 27


# ── PR-02 Track A: register_all() threads stdio_alive through to connection ────

def test_register_all_forwards_stdio_alive_to_connection(monkeypatch):
    """register_all(..., stdio_alive=X) must forward X to connection.register()
    unchanged — this is the composition-root -> tools wiring for the callback
    that replaced connection.py's reverse import into unity_mcp.server."""
    import unity_mcp.tools as tools_pkg
    import unity_mcp.tools.connection as connection

    register_mock = MagicMock()
    # register_all() binds module-global transports for every tool module in
    # its list. Mock every OTHER registrar (same technique as
    # test_register_all_wires_uitk_module_exactly_once) so this call doesn't
    # leak a throwaway _send/_args into spatial/watch/uitk/etc. for the rest
    # of the pytest session.
    for candidate in vars(tools_pkg).values():
        register = getattr(candidate, "register", None)
        if candidate is tools_pkg.connection or not callable(register):
            continue
        monkeypatch.setattr(candidate, "register", MagicMock())
    monkeypatch.setattr(connection, "register", register_mock)
    monkeypatch.setattr(tools_pkg, "register_metrics", MagicMock())
    sentinel = object()

    tools_pkg.register_all(
        _make_mcp(),
        AsyncMock(),
        MagicMock(),
        get_slot=lambda: None,
        stdio_alive=sentinel,
    )

    assert register_mock.call_args.kwargs["stdio_alive"] is sentinel


def test_server_wires_real_stdio_alive_into_register_all():
    """server.py (the composition root) must pass its own _stdio_alive function
    into register_all() at the actual call site — not a stub, not omitted."""
    import inspect

    import unity_mcp.server as srv
    src = inspect.getsource(srv)

    assert "stdio_alive=_stdio_alive" in src
