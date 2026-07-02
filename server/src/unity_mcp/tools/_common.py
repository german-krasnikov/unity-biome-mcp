"""Shared registration helper for tools/*.py modules."""


def bind(module_globals: dict, send, args) -> None:
    """Bind the standard _send/_args module globals shared by every tools/*.py
    register(mcp, send, args) implementation. Always binds both names, even in
    modules that don't currently call _args() — uniformity here eliminates the
    3-variant drift class (full-bind / send-only / neither) this helper replaces.
    """
    module_globals["_send"] = send
    module_globals["_args"] = args
