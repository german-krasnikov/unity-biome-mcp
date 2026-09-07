"""N1b: second self-owned Python tool module (mirrors tools/watch.py's
WatchModule pilot). SyncModule owns the public `sync_unity` ToolSpec kwargs,
the explicit public->wire mapping, and instance-bound send/args state so two
independently-configured SDK hosts never cross-contaminate each other's
_send/_args. See Plans/N1b-second-module-sync.md section 2.

Owned (public -> wire mapping):
    sync_unity (public MCP tool) -> C# wire 'sync' / 'sync_status'

NOT owned (consumed dependencies; ownership of the wire command's C#
implementation and of the send() transport itself stays exactly where it is
today -- calling a dependency here does not transfer its ownership to this
module): get_compile_errors, compile_status, diagnose, warm_type_cache,
force_refresh (C# Reload/recovery owns force_refresh; Sync only consumes it
via the existing recovery ladder), plus non-wire dependencies
editor_log.get_corroborated_errors, lockfile.read_reload_port, reload_ladder.*.

The poll/recovery/error algorithm (_sync_unity, _await_sync_completion,
_get_errors, _attempt_recovery, _warm_type_cache) is NOT duplicated here --
it stays the single tested implementation in tools/sync.py. SyncModule.
sync_unity rebinds that module's _send/_args/_bump_used to this instance's
own state immediately before delegating, so a call is dispatched with
exactly this instance's binding every time (verified in
tests/test_sync_module.py). This is a deliberate, scope-limited slice: a full
stateless refactor of the tools.sync algorithm functions is out of scope
(Plans/N1b-second-module-sync.md section 5, "module refactor" risk row).
"""
from unity_mcp import editor_log
from unity_mcp.constants import SESSION_TIMEOUT as _DEFAULT_TIMEOUT

from . import sync
from ._annotations import RW as _RW
from .sync_spec import CONSUMED_DEPENDENCIES, OWNED_WIRE_COMMANDS, SPEC_KWARGS  # noqa: F401 -- re-export

# N1b pilot #2: SyncModule owns the sync_unity spec/wire-mapping exactly like
# WatchModule owns watch/get_watches, but the data itself lives in sync_spec.py
# (see that module's docstring for why: tool_specs.py must not transitively
# reach tools/sync.py, which this file imports for algorithm delegation).


class SyncModule:
    """Owns the public sync_unity MCP tool. Instance-scoped (not a
    module-global singleton) so two independently-configured hosts never
    cross-contaminate each other's _send/_args -- same contract as
    WatchModule (tools/watch.py)."""

    def __init__(self, send, args):
        self._send = send
        self._args = args
        self._bump_used = False

    async def sync_unity(self, resolve: bool = False, bump: bool = False,
                          timeout: float = _DEFAULT_TIMEOUT) -> str:
        """Refresh Unity and await its matching compile cycle within one timeout.

        resolve=True resolves packages first; bump=True increments the plugin patch
        version once per connection and implies resolve. 'sync clean' means the
        observed cycle completed without captured errors; it is not a hash proof
        that every source file was compiled. Timeout does not cancel Unity effects.
        """
        sync._send = self._send
        sync._args = self._args
        sync._bump_used = self._bump_used
        try:
            return await sync.sync_unity(resolve, bump, timeout)
        finally:
            self._bump_used = sync._bump_used

    def register(self, mcp) -> None:
        mcp.tool(annotations=_RW)(self.sync_unity)


# Process-wide singleton pointer -- same shape as every other tools/*.py
# module today (register_all() is called exactly once, at server startup).
# "Two instances never cross-contaminate" is proven at the SyncModule class
# level directly (test_sync_module.py), not by making this compat shim
# multi-tenant.
_default: SyncModule | None = None


async def sync_unity(resolve: bool = False, bump: bool = False,
                      timeout: float = _DEFAULT_TIMEOUT) -> str:
    """Legacy compat adapter -- delegates to the process-wide _default
    instance. Actual SDK registration (register() below, called from
    tools/__init__.py:register_all) binds self.sync_unity directly; this
    free function exists only for callers still importing the pre-N1b
    module-level shape (mirrors tools/watch.py's compat shim)."""
    return await _default.sync_unity(resolve, bump, timeout)


def _reset_bump_used() -> None:
    """Reconnect callback (server.py): resets the production instance's own
    bump flag, plus the tools.sync module-global fallback used by direct
    (non-instance) callers/tests."""
    if _default is not None:
        _default._bump_used = False
    sync._reset_bump_used()


def register(mcp, send, args) -> SyncModule:
    global _default
    _default = SyncModule(send, args)
    editor_log.init_corroboration()
    _default.register(mcp)
    return _default
