"""Single source of truth for tool metadata (M8).

Every MCP tool + a handful of protocol-level commands (category='_INTERNAL':
ping/get_version/export_package/import_package — NOT MCP tools, contribute a
timeout only) get exactly one ToolSpec entry here. gating.py derives its
_CORE_TOOLS/_THEMED_CATEGORIES/TIER1/_ALL_KNOWN collections from this dict at
import time instead of hand-typing 4 separately-drifting literals.
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
    """
    category: str
    core: bool = False
    tier1: bool = False
    timeout_s: float = 30.0
    mutability: Literal['read', 'write'] = 'write'
    runtime_only: bool = False
    direct_only: bool = False
    unity_transport: bool = False

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

_SPECS: dict[str, ToolSpec] = {
    'alias_status': ToolSpec(category='SYSTEM', mutability='read'),
    'animator_intent': ToolSpec(category='SYSTEM', direct_only=True),
    'apply_scene_change': ToolSpec(category='SCENE', tier1=True, timeout_s=120.0, direct_only=True),
    'apply_template': ToolSpec(category='SYSTEM', direct_only=True),
    'ask_user': ToolSpec(category='SYSTEM', timeout_s=300.0, mutability='read', direct_only=True, unity_transport=True),
    'auto_fix': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    # timeout_s live for 9 internal _send("batch",...) callers w/o explicit timeout
    # (animator_intent_tool.py:119, skills.py:61, autobatch.py:69/98/136,
    # ui_intent_tool.py:152, debug_tool.py:78, do_intent/executor.py:38/67).
    # Public batch() tool always overrides -- see tools/batch.py timeout param.
    'batch': ToolSpec(category='CORE', core=True, timeout_s=120.0),
    'budget_status': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'cancel_test_run': ToolSpec(category='TESTS', timeout_s=10.0),
    'checkpoint': ToolSpec(category='SYSTEM'),
    'checkpoint_create': ToolSpec(category='SYSTEM', direct_only=True),   # T19: Python orchestrator, no C# handler
    'checkpoint_restore': ToolSpec(category='SYSTEM', direct_only=True),  # T19: Python orchestrator, no C# handler
    'brief_build': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'configure_objects': ToolSpec(category='SCENE', direct_only=True),
    'console_mark': ToolSpec(category='RUNTIME', tier1=True, mutability='read', direct_only=True),
    'create_ui': ToolSpec(category='UGUI'),
    'debug': ToolSpec(category='RUNTIME', mutability='read', direct_only=True),
    'discover_tools': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'do': ToolSpec(category='SYSTEM', direct_only=True),
    # Conditional: default is observational; fix=True removes stale local files.
    # middleware_types.is_write() handles the argument-aware classification.
    'doctor': ToolSpec(category='SYSTEM', direct_only=True),
    'editor': ToolSpec(category='CORE', core=True),
    'execute_code': ToolSpec(category='SYSTEM', core=True, timeout_s=60.0),
    'get_capabilities': ToolSpec(category='SYSTEM', mutability='read'),
    'get_changeset': ToolSpec(category='VERIFY', tier1=True, timeout_s=5.0, mutability='read', direct_only=True),
    'get_compile_errors': ToolSpec(category='CORE', core=True, mutability='read'),
    'get_console': ToolSpec(category='CORE', core=True, timeout_s=10.0, mutability='read'),
    'get_console_since': ToolSpec(category='RUNTIME', tier1=True, mutability='read', direct_only=True),
    'get_frame_stats': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'get_memory': ToolSpec(category='RUNTIME', mutability='read'),
    'get_schema': ToolSpec(category='SYSTEM', mutability='read'),
    'get_selection': ToolSpec(category='SCENE', mutability='read'),
    'get_test_count': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'get_test_progress': ToolSpec(category='TESTS', mutability='read'),
    'get_test_results': ToolSpec(category='TESTS', mutability='read'),
    'get_test_run': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'lint_playtest': ToolSpec(category='TESTS', timeout_s=60.0, mutability='read'),
    'list_test_runs': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'list_skills': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'list_templates': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'mcp_status': ToolSpec(category='SYSTEM', core=True, mutability='read', direct_only=True),
    'menu': ToolSpec(category='SYSTEM'),
    'permission_prompt': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'ping_object': ToolSpec(category='SCENE', mutability='read'),
    'profile': ToolSpec(category='RUNTIME', runtime_only=True),
    'recompile': ToolSpec(category='SYSTEM'),
    'reconnect_unity': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'references': ToolSpec(category='COMPONENTS'),
    'release_smoke': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'resolve_test_request': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'resolve_tool_schema': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'run_tests': ToolSpec(category='TESTS', tier1=True, timeout_s=30.0, direct_only=True, unity_transport=True),
    'run_tests_wait': ToolSpec(category='TESTS', tier1=True, timeout_s=1200.0, direct_only=True),
    'save_skill': ToolSpec(category='SYSTEM', direct_only=True),
    'save_template': ToolSpec(category='SYSTEM', direct_only=True),
    'scene_change_plan': ToolSpec(category='SCENE', tier1=True, timeout_s=30.0, direct_only=True),
    'set_llm_config': ToolSpec(category='SYSTEM', direct_only=True),
    'set_properties': ToolSpec(category='SCENE', direct_only=True),
    'set_rect': ToolSpec(category='UGUI'),
    'setup_objects': ToolSpec(category='SCENE', direct_only=True),
    'shader': ToolSpec(category='ASSETS'),
    'smart_build': ToolSpec(category='SYSTEM', direct_only=True),
    'ui_intent': ToolSpec(category='UGUI', tier1=True, direct_only=True),
    'undo_last': ToolSpec(category='SYSTEM'),
    'validate_references': ToolSpec(category='VERIFY', tier1=True, mutability='read'),
    'vfx_intent': ToolSpec(category='MEDIA', tier1=True, direct_only=True),
    'lint_ugui': ToolSpec(category='UGUI', tier1=False, mutability='read', timeout_s=15.0),
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
