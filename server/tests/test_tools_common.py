"""Tests for tools/_common.py — M3: shared _send/_args binding helper."""


def test_bind_sets_send_and_args_on_module_globals():
    from unity_mcp.tools._common import bind

    fake_module_globals = {}
    my_send, my_args = object(), object()
    bind(fake_module_globals, my_send, my_args)
    assert fake_module_globals["_send"] is my_send
    assert fake_module_globals["_args"] is my_args
