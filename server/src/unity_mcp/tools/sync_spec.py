"""N1b: pure ToolSpec-metadata + ownership data for sync_unity.

Split out of sync_module.py so tool_specs.py (imported by reload_ladder.py
for _SPECS) can read this data WITHOUT transitively importing
tools/sync.py's algorithm. That edge is forbidden by the import-linter
contract "diagnose/reload_ladder stay algorithm+evidence only (no import of
the sync facade)" (server/pyproject.toml [tool.importlinter]):
reload_ladder -> tool_specs must never reach sync.py. sync_module.py (the
SyncModule class + delegation, which DOES import tools/sync.py) re-exports
these same names -- single source of truth, two import paths.
"""

# Explicit public -> wire mapping the sync module owns (N1b checkbox 2).
SPEC_KWARGS: dict[str, dict] = {
    'sync_unity': {'category': 'SYSTEM', 'tier1': True, 'direct_only': True},
}

OWNED_WIRE_COMMANDS: dict[str, tuple[str, ...]] = {
    'sync_unity': ('sync', 'sync_status'),
}

# Wire commands this module calls but does not own (N1b checkbox 2). Ownership
# of each wire command's C# implementation and of the send() transport itself
# stays exactly where it is today -- calling a dependency here does not
# transfer its ownership to this module. force_refresh: C# Reload/recovery
# owns it, Sync only consumes it via the existing recovery ladder.
CONSUMED_DEPENDENCIES: frozenset[str] = frozenset({
    'get_compile_errors', 'compile_status', 'diagnose',
    'warm_type_cache', 'force_refresh',
})
