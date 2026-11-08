"""Single source of truth for tool metadata (M8).

Every MCP tool + a handful of protocol-level commands (category='_INTERNAL':
ping/get_version/export_package/import_package — NOT MCP tools, contribute a
timeout only) get exactly one ToolSpec entry in _SPECS. gating.py derives its
_CORE_TOOLS/_THEMED_CATEGORIES/TIER1/_ALL_KNOWN collections from this dict at
import time instead of hand-typing 4 separately-drifting literals.

Where an entry comes from: the tool catalog (dsl/tools.biome, dsl/README.md) generates
tools/generated/catalog_specs.py, merged at the end of this module; watch, get_watches and
sync_unity are owned by their modules (watch.py, sync_spec.py); six tools are still written in the
literal below, because their MCP annotation and their ToolSpec disagree and one catalog effect
cannot say both (step 7.1, a decision for the owner).
"""
from dataclasses import dataclass
from typing import Literal


@dataclass(frozen=True)
class ToolSpec:
    """Single-source truth per tool.
    category: themed group key, "CORE", or "_INTERNAL" (protocol-only, not MCP tool).
    core: always-visible + full-schema.
    tier1: always-visible but not core.
    timeout_s: per-command TCP timeout; DEFAULT_TIMEOUT (30s) if unset.
    mutability: 'read' = never mutates scene/state; 'write' = may mutate (default = fail-closed).
    For category='_INTERNAL' entries this field is now load-bearing, not just
    documentation: middleware_types.WRITE_CMDS derives from it with no
    category filter, so an unmarked _INTERNAL write silently bypasses the
    UNITY_MCP_READ_ONLY gate (PR-01B/F8).
    runtime_only: True = Play Mode required; blocked before TCP when is_playing=False.
    direct_only: True = callable through its typed MCP wrapper, but not inside batch.
    unity_transport: a direct-only wrapper delegates the same command name to Unity.
    session: True = the side effect is outside the Unity project (selection, connection,
    visible tools, server config) — never a project mutation; `mutability` still follows
    `effect` alone (D2, 2026-11-08).
    """
    category: str
    core: bool = False
    tier1: bool = False
    timeout_s: float = 30.0
    mutability: Literal['read', 'write'] = 'write'
    runtime_only: bool = False
    direct_only: bool = False
    unity_transport: bool = False
    session: bool = False

    @property
    def plane(self) -> str:
        """Execution plane: 'python' if handled in MCP server, 'unity' if dispatched to Unity TCP.

        Derived from existing fields — direct_only=True with unity_transport=False means
        the MCP wrapper handles the call entirely in Python without touching the TCP bridge.
        """
        if self.direct_only and not self.unity_transport:
            return "python"
        return "unity"


DEFAULT_TIMEOUT: float = 30.0

# Written by hand until the owner decides: resolve_tool_schema and set_llm_config register without an MCP annotation;
# ping_object, lint_playtest and reconnect_unity register as writes (RW, RW_IDEM, RW_IDEM) although their ToolSpec says read.
_SPECS: dict[str, ToolSpec] = {
    'lint_playtest': ToolSpec(category='TESTS', timeout_s=60.0, mutability='read'),
    'ping_object': ToolSpec(category='SCENE', mutability='read'),
    'reconnect_unity': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'resolve_tool_schema': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'set_llm_config': ToolSpec(category='SYSTEM', direct_only=True),
}

# PR-04 pilot: watch/get_watches ToolSpec entries are owned by tools/watch.py
# (mirrors the C# WatchCommandHandler.RegisterAll physical pilot). One-way
# import only (tool_specs -> watch, never the reverse) -- see Plans/PR-04.md
# "Ordering trace" for why. watch.py exports plain kwargs, not ToolSpec
# instances, so it never needs to import ToolSpec back from this module.
from .watch import SPEC_KWARGS as _WATCH_SPEC_KWARGS  # noqa: E402

_SPEC_OWNERS: dict[str, str] = {}
for _name, _kwargs in _WATCH_SPEC_KWARGS.items():
    _SPECS[_name] = ToolSpec(**_kwargs)
    _SPEC_OWNERS[_name] = "watch"

# N1b pilot #2: sync_unity ToolSpec kwargs are owned by the sync module
# (tools/sync_module.py: SyncModule + this same SPEC_KWARGS re-exported), but
# imported here from tools/sync_spec.py specifically -- tool_specs.py must
# not transitively import tools/sync.py (reload_ladder.py imports tool_specs
# for _SPECS, and reload_ladder -> sync is a forbidden import-linter
# contract; see sync_spec.py's docstring).
from .sync_spec import SPEC_KWARGS as _SYNC_SPEC_KWARGS  # noqa: E402

for _name, _kwargs in _SYNC_SPEC_KWARGS.items():
    _SPECS[_name] = ToolSpec(**_kwargs)
    _SPEC_OWNERS[_name] = "sync"

# Tool catalog (dsl/tools.biome): the ToolSpec kwargs of its tools are generated, imported one way like the owners above. 'effect' is
# the catalog's RO|RW|RW_IDEM|DEL; ToolSpec keeps only whether the tool writes (the MCP annotation follows from effect elsewhere).
# 'max_chars' is the response limit of the Unity registration, checked by test_catalog_vs_csharp.py; ToolSpec has no such field.
from .generated.catalog_specs import SPEC_KWARGS as _CATALOG_SPEC_KWARGS  # noqa: E402

for _name, _kwargs in _CATALOG_SPEC_KWARGS.items():
    if _name in _SPECS:
        raise ValueError(f"{_name} is declared in the catalog (dsl/tools.biome) and in tool_specs.py")
    _fields = {key: value for key, value in _kwargs.items() if key not in ("effect", "max_chars")}
    _SPECS[_name] = ToolSpec(**_fields, mutability="read" if _kwargs["effect"] == "RO" else "write")
    _SPEC_OWNERS[_name] = "catalog"
