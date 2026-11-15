"""Pure DI-global discovery/snapshot/restore for tools/*.py register() seams.

No pytest dependency, no I/O -- operates on real module objects via plain
introspection. See Plans/DSL/SV7-DESIGN.md.

Every tools/*.py module exposes module-level globals (_send, _args, and for a
few modules extra DI params like _get_slot) that production register() sets
once via _common.bind() or a direct `global` assignment. di_globals() derives
the exact set of such globals straight from each module's own register()
signature, so there is nothing to hand-maintain when a module is added,
split, or gains a new DI param.
"""
import contextlib
import inspect
import types

from unity_mcp import tools as _tools_pkg

Snapshot = list[tuple[types.ModuleType, str, object]]


def bindable_tool_modules() -> list[types.ModuleType]:
    """Every unity_mcp.tools submodule that defines its own register()."""
    return [
        v for v in vars(_tools_pkg).values()
        if isinstance(v, types.ModuleType) and callable(getattr(v, "register", None))
    ]


def di_globals(mod: types.ModuleType) -> list[str]:
    """Names of module globals register() binds, e.g. ['_send', '_args', '_get_slot'].

    Walks register()'s own signature: skips 'mcp' and any VAR_POSITIONAL/
    VAR_KEYWORD catch-all (e.g. connection.py's **_kw), maps each remaining
    param name `x` to candidate global `_x`, and keeps it only if the module
    actually has that attribute -- this is what correctly excludes _send/_args
    for connection.py (which never binds them) while still including its
    _get_slot/_stdio_alive/_refresh_tools_cache/_push_catalog.
    """
    register = getattr(mod, "register", None)
    if register is None:
        return []
    names = []
    for name, param in inspect.signature(register).parameters.items():
        if name == "mcp":
            continue
        if param.kind in (inspect.Parameter.VAR_POSITIONAL, inspect.Parameter.VAR_KEYWORD):
            continue
        candidate = f"_{name}"
        if hasattr(mod, candidate):
            names.append(candidate)
    return names


def snapshot_bindings(modules: list[types.ModuleType]) -> Snapshot:
    """Capture current values of every DI global on every given module."""
    return [
        (mod, name, getattr(mod, name))
        for mod in modules
        for name in di_globals(mod)
    ]


def restore_bindings(snapshot: Snapshot) -> None:
    """Write every captured value back onto its module global."""
    for mod, name, value in snapshot:
        setattr(mod, name, value)


def clear_bindings(modules: list[types.ModuleType]) -> None:
    """Reset every DI global to None — safe default before a test runs.

    The old _reset_diagnose_send forced diagnose._send = None so tests that
    call diagnose indirectly (via testing.run_tests → diagnose.diagnose) hit
    the skip-path instead of reaching the real bridge.  This generalises it
    to every bindable module."""
    for mod in modules:
        for name in di_globals(mod):
            setattr(mod, name, None)


@contextlib.contextmanager
def reset_scope():
    """Snapshot every bindable module's DI globals, yield, always restore."""
    snapshot = snapshot_bindings(bindable_tool_modules())
    try:
        yield
    finally:
        restore_bindings(snapshot)
