"""TDD for _tool_binding.py — pure DI-global discovery/snapshot/restore.

See Plans/DSL/SV7-DESIGN.md. Tests 1-5,7 use a bare types.ModuleType fake
(no unity_mcp import needed). Tests 6,8,9 intentionally exercise the real
unity_mcp.tools package/runtime module -- mocking that away would test
nothing, since the thing under test IS "does this work against the real
package shape".
"""
import types

import pytest
from _tool_binding import (
    bindable_tool_modules,
    di_globals,
    reset_scope,
    restore_bindings,
    snapshot_bindings,
)


def _fake_module(register_fn, **attrs):
    mod = types.ModuleType("fake")
    mod.register = register_fn
    for name, value in attrs.items():
        setattr(mod, name, value)
    return mod


def test_di_globals_simple_register_yields_send_and_args():
    def register(mcp, send, args):
        pass
    mod = _fake_module(register, _send=None, _args=None)
    assert di_globals(mod) == ["_send", "_args"]


def test_di_globals_includes_keyword_only_extras():
    def register(mcp, send, args, *, get_slot=None, stdio_alive=None):
        pass
    mod = _fake_module(register, _send=None, _args=None, _get_slot=None, _stdio_alive=None)
    assert di_globals(mod) == ["_send", "_args", "_get_slot", "_stdio_alive"]


def test_di_globals_skips_names_mod_does_not_actually_have():
    """Mirrors connection.py: register() takes send/args but the module never
    binds _send/_args as globals -- only _get_slot."""
    def register(mcp, send, args, *, get_slot):
        pass
    mod = _fake_module(register, _get_slot=None)
    assert di_globals(mod) == ["_get_slot"]


def test_di_globals_skips_var_positional_and_var_keyword():
    def register(mcp, send, args, *a, **kw):
        pass
    mod = _fake_module(register, _send=None, _args=None)
    assert di_globals(mod) == ["_send", "_args"]


def test_di_globals_no_register_attr_returns_empty():
    mod = types.ModuleType("fake")
    assert di_globals(mod) == []


def test_bindable_tool_modules_includes_real_modules():
    from unity_mcp.tools import runtime
    mods = bindable_tool_modules()
    assert runtime in mods
    assert all(isinstance(m, types.ModuleType) for m in mods)


def test_snapshot_and_restore_roundtrip():
    def register(mcp, send, args):
        pass
    mod = _fake_module(register, _send="orig_send", _args="orig_args")
    snap = snapshot_bindings([mod])
    mod._send = "mutated_send"
    mod._args = "mutated_args"
    restore_bindings(snap)
    assert mod._send == "orig_send"
    assert mod._args == "orig_args"


def test_reset_scope_restores_after_block_mutates():
    from unity_mcp.tools import runtime as rt
    original = rt._send
    with reset_scope():
        rt._send = "mutated-in-scope"
    assert rt._send is original


def test_reset_scope_restores_even_on_exception():
    from unity_mcp.tools import runtime as rt
    original = rt._send
    with pytest.raises(ValueError), reset_scope():
        rt._send = "mutated-before-raise"
        raise ValueError("boom")
    assert rt._send is original
