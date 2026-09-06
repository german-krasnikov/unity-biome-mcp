"""F3 (PR-03): plugin registration is all-or-nothing.

A plugin's register() call must never leave a partial registration behind —
neither its own tools nor a duplicate-name collision with another plugin.
See Plans/PR-03.md for the full architecture.
"""
import types

import pytest
from test_plugins import _DictMcp

from unity_mcp.plugins import _atomic


@pytest.fixture(autouse=True)
def _restore_gating_state():
    """A successfully-registered plugin tool gets auto-gated into gating's shared
    module state (_ALL_KNOWN / _THEMED_CATEGORIES / CATEGORIES) — restore it exactly,
    the same wholesale snapshot/restore shape _atomic.py itself uses, so these tests
    don't leak fake tool names into test_schema_parity.py or other test files."""
    from unity_mcp.tools import gating
    all_known = set(gating._ALL_KNOWN)
    themed = {k: list(v) for k, v in gating._THEMED_CATEGORIES.items()}
    categories = gating.CATEGORIES
    yield
    gating._ALL_KNOWN.clear()
    gating._ALL_KNOWN.update(all_known)
    gating._THEMED_CATEGORIES.clear()
    gating._THEMED_CATEGORIES.update(themed)
    gating.CATEGORIES = categories


def _mk_module(register_fn):
    mod = types.ModuleType("fake_plugin_module")
    mod.register = register_fn
    return mod


def test_register_plugin_module_second_tool_raises_rolls_back_first(monkeypatch):
    """RED — first tool registered, second raises → nothing survives (exact F3 repro)."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def leftover():
            pass
        raise RuntimeError("boom")

    ok = _atomic.register_plugin_module(_mk_module(register), "bad", mcp, None, None)

    assert ok is False
    assert "leftover" not in mcp._tool_manager._tools
    failed = _atomic.get_failed_plugins()
    assert len(failed) == 1
    assert failed[0][0] == "bad"
    assert "boom" in failed[0][1]


def test_register_plugin_module_failure_does_not_affect_host_or_sibling_plugin(monkeypatch):
    """Host tool (registered before any plugin loaded) and an independent, successfully
    loaded sibling plugin both keep working after a failing plugin rolls back."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    mcp = _DictMcp()

    def host_tool():
        pass
    mcp._tool_manager._tools["host_tool"] = host_tool

    def bad_register(m, send, args):
        @m.tool()
        def leftover():
            pass
        raise RuntimeError("boom")

    def good_register(m, send, args):
        @m.tool()
        def good_tool():
            pass

    ok_bad = _atomic.register_plugin_module(_mk_module(bad_register), "bad", mcp, None, None)
    ok_good = _atomic.register_plugin_module(_mk_module(good_register), "good", mcp, None, None)

    assert ok_bad is False
    assert ok_good is True
    assert "host_tool" in mcp._tool_manager._tools
    assert "good_tool" in mcp._tool_manager._tools
    assert "leftover" not in mcp._tool_manager._tools


def test_register_plugin_module_duplicate_tool_name_second_plugin_rejected(monkeypatch):
    """Plugin A claims 'shared_tool' first and succeeds. Plugin B also tries to claim
    'shared_tool' — B must fail with a diagnosable message, A's registration survives."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    mcp = _DictMcp()

    def register_a(m, send, args):
        @m.tool()
        def shared_tool():
            return "A"

    def register_b(m, send, args):
        @m.tool()
        def shared_tool():
            return "B"

    ok_a = _atomic.register_plugin_module(_mk_module(register_a), "plugin_a", mcp, None, None)
    ok_b = _atomic.register_plugin_module(_mk_module(register_b), "plugin_b", mcp, None, None)

    assert ok_a is True
    assert ok_b is False
    failed = _atomic.get_failed_plugins()
    assert any("shared_tool" in msg for _, msg in failed)
    assert mcp._tool_manager._tools["shared_tool"]() == "A"


def test_register_plugin_module_same_identity_reload_no_duplicate_no_failure(monkeypatch):
    """Calling register_plugin_module twice with the SAME identity (domain-reload-style
    re-run) must not fail and must not duplicate the tool."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def stable_tool():
            pass

    ok1 = _atomic.register_plugin_module(_mk_module(register), "same_id", mcp, None, None)
    count_after_first = len(mcp._tool_manager._tools)
    ok2 = _atomic.register_plugin_module(_mk_module(register), "same_id", mcp, None, None)
    count_after_second = len(mcp._tool_manager._tools)

    assert ok1 is True
    assert ok2 is True
    assert count_after_second == count_after_first


def test_register_write_cmds_rejects_core_command_name():
    """A plugin must never reclassify an existing core command's read/write direction."""
    from unity_mcp.middleware import WRITE_CMDS
    from unity_mcp.plugin_api import register_write_cmds

    register_write_cmds("get_hierarchy")
    assert "get_hierarchy" not in WRITE_CMDS

    register_write_cmds("my_new_plugin_tool")
    try:
        assert "my_new_plugin_tool" in WRITE_CMDS
    finally:
        WRITE_CMDS.discard("my_new_plugin_tool")
