"""N1a T3: commit-time ownership validation for API-v1 metadata calls.

Split out of test_plugin_atomicity.py to keep both files under the 300-line
budget. Shares fixtures/helpers with it via _plugin_fixtures.py. See
Plans/N1a-plugin-registration-owner.md section 2.3 for the design: every name
a plugin declares via register_read_cmds/write_cmds/tools/dsl_tools/features
during its register() call must be a tool it registered through
guarded.tool() in that same call, or the whole plugin is rejected and rolled
back (_atomic._restore).
"""
import importlib

import pytest
from _plugin_fixtures import (  # noqa: F401 — autouse fixtures, re-exported for pytest
    _declare_dsl_tools_typo,
    _declare_features_typo,
    _declare_register_tools_typo,
    _declare_write_cmds_typo,
    _mk_module,
    _reset_atomic_scoped_state,
    _restore_gating_state,
)
from test_plugins import _DictMcp

from unity_mcp.plugins import _atomic


def test_plugin_declaring_foreign_metadata_rejected_entirely(monkeypatch):
    """Plugin A registers its own tool AND declares read-classification for a
    tool that belongs to plugin B. A must be rejected entirely (its own tool
    rolled back too) — a plugin cannot reach into another plugin's metadata."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    from unity_mcp.middleware import READ_CMDS
    from unity_mcp.plugin_api import register_read_cmds

    mcp = _DictMcp()

    def register_a(m, send, args):
        @m.tool()
        def a_tool():
            pass
        register_read_cmds("a_tool", "tool_from_b")

    def register_b(m, send, args):
        @m.tool()
        def tool_from_b():
            pass

    ok_a = _atomic.register_plugin_module(_mk_module(register_a), "plugin_a", mcp, None, None)
    ok_b = _atomic.register_plugin_module(_mk_module(register_b), "plugin_b", mcp, None, None)

    assert ok_a is False
    assert ok_b is True
    assert "a_tool" not in mcp._tool_manager._tools
    assert "a_tool" not in _atomic._command_owner
    assert "tool_from_b" not in READ_CMDS
    assert mcp._tool_manager._tools["tool_from_b"] is not None
    assert _atomic._command_owner["tool_from_b"] == "plugin_b"
    failed = _atomic.get_failed_plugins()
    assert len(failed) == 1
    assert failed[0][0] == "plugin_a"
    assert "tool_from_b" in failed[0][1]


def test_api_v1_typo_name_not_registered_via_tool_rejects_plugin(monkeypatch):
    """A plugin declaring register_read_cmds for a name it never registers via
    guarded.tool() (a typo, or a name it forgot to define) rejects the whole
    plugin — the typo'd name must not silently land in READ_CMDS."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    from unity_mcp.middleware import READ_CMDS
    from unity_mcp.plugin_api import register_read_cmds

    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def real_tool():
            pass
        register_read_cmds("real_tool", "typo_tool")

    ok = _atomic.register_plugin_module(_mk_module(register), "typo_plugin", mcp, None, None)

    assert ok is False
    assert "real_tool" not in mcp._tool_manager._tools
    assert "typo_tool" not in READ_CMDS
    failed = _atomic.get_failed_plugins()
    assert len(failed) == 1
    assert "typo_tool" in failed[0][1]


@pytest.mark.parametrize("declare_typo, container_path, container_name", [
    (_declare_write_cmds_typo, "unity_mcp.middleware", "WRITE_CMDS"),
    (_declare_register_tools_typo, "unity_mcp.tools.gating", "_ALL_KNOWN"),
    (_declare_dsl_tools_typo, "unity_mcp.tools.batch", "_dsl_tools"),
    (_declare_features_typo, "unity_mcp.budget.registry", "FEATURES"),
], ids=["write_cmds", "register_tools", "dsl_tools", "features"])
def test_api_v1_typo_name_rejects_plugin_for_every_v1_function(
    monkeypatch, declare_typo, container_path, container_name
):
    """The commit-time ownership check covers all five API-v1 functions, not
    just register_read_cmds — a plugin cannot shadow a name (builtin or not)
    that it never registered via guarded.tool(), through ANY of them, and the
    per-container mutation (WRITE_CMDS / _ALL_KNOWN / _dsl_tools / FEATURES)
    is rolled back along with the tool."""
    monkeypatch.setattr(_atomic, "_command_owner", {})
    container = getattr(importlib.import_module(container_path), container_name)

    mcp = _DictMcp()

    def register(m, send, args):
        @m.tool()
        def real_tool():
            pass
        declare_typo()

    before = set(container)
    ok = _atomic.register_plugin_module(_mk_module(register), "typo_plugin_2", mcp, None, None)

    assert ok is False
    assert "real_tool" not in mcp._tool_manager._tools
    assert "typo_tool" not in container
    assert set(container) == before
    failed = _atomic.get_failed_plugins()
    assert len(failed) == 1
    assert "typo_tool" in failed[0][1]
