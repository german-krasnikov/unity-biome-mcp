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
    'analyze_lod_culling': ToolSpec(category='MEDIA', mutability='read'),
    'animation': ToolSpec(category='MEDIA'),
    'animator': ToolSpec(category='MEDIA'),
    'animator_intent': ToolSpec(category='SYSTEM', direct_only=True),
    'apply_scene_change': ToolSpec(category='SCENE', tier1=True, timeout_s=120.0, direct_only=True),
    'apply_template': ToolSpec(category='SYSTEM', direct_only=True),
    'ask': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'ask_user': ToolSpec(category='SYSTEM', timeout_s=300.0, mutability='read', direct_only=True, unity_transport=True),
    'asset': ToolSpec(category='ASSETS'),
    'auto_fix': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'auto_wire': ToolSpec(category='COMPONENTS'),
    'autofit_collider': ToolSpec(category='SCENE'),
    'bake': ToolSpec(category='ASSETS'),
    'await_compile': ToolSpec(category='VERIFY', tier1=True, mutability='read', direct_only=True),
    # timeout_s live for 9 internal _send("batch",...) callers w/o explicit timeout
    # (animator_intent_tool.py:119, skills.py:61, autobatch.py:69/98/136,
    # ui_intent_tool.py:152, debug_tool.py:78, do_intent/executor.py:38/67).
    # Public batch() tool always overrides -- see tools/batch.py timeout param.
    'batch': ToolSpec(category='CORE', core=True, timeout_s=120.0),
    'budget_status': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'build': ToolSpec(category='SYSTEM', timeout_s=300.0, direct_only=True, unity_transport=True),
    'package': ToolSpec(category='ASSETS', timeout_s=60.0, direct_only=True, unity_transport=True),
    'cancel_test_run': ToolSpec(category='TESTS', timeout_s=10.0),
    'check_colliders': ToolSpec(category='SCENE', mutability='read'),
    'checkpoint': ToolSpec(category='SYSTEM'),
    'checkpoint_create': ToolSpec(category='SYSTEM', direct_only=True),   # T19: Python orchestrator, no C# handler
    'checkpoint_restore': ToolSpec(category='SYSTEM', direct_only=True),  # T19: Python orchestrator, no C# handler
    'brief_build': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    # timeout_s is a fallback ceiling only -- code_intel.py:25 always passes
    # timeout=15.0 (fast-fail by design).
    'compile_preflight': ToolSpec(category='VERIFY', core=True, timeout_s=60.0, mutability='read'),
    'configure_objects': ToolSpec(category='SCENE', direct_only=True),
    'console_mark': ToolSpec(category='RUNTIME', tier1=True, mutability='read', direct_only=True),
    'create_object': ToolSpec(category='CORE', core=True),
    'create_ui': ToolSpec(category='UGUI'),
    'debug': ToolSpec(category='RUNTIME', mutability='read', direct_only=True),
    'debug_animator': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'debug_physics': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'delete_object': ToolSpec(category='SCENE', tier1=True),
    'diagnose': ToolSpec(category='VERIFY', mutability='read'),
    'discover_tools': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'do': ToolSpec(category='SYSTEM', direct_only=True),
    # Conditional: default is observational; fix=True removes stale local files.
    # middleware_types.is_write() handles the argument-aware classification.
    'doctor': ToolSpec(category='SYSTEM', direct_only=True),
    'editor': ToolSpec(category='CORE', core=True),
    'clear_held_types': ToolSpec(category='SYSTEM', core=False, timeout_s=5.0, mutability='read'),
    'execute_code': ToolSpec(category='SYSTEM', core=True, timeout_s=60.0),
    'end_write_session':   ToolSpec(category='ASSETS', tier1=True),
    'export_package': ToolSpec(category='_INTERNAL', timeout_s=120.0),
    'export_playtest_aliases_to_defs': ToolSpec(category='TESTS'),
    'find_objects': ToolSpec(category='SCENE', mutability='read'),
    'fingerprint': ToolSpec(category='SYSTEM', timeout_s=10.0, mutability='read'),
    'get_aliases': ToolSpec(category='_INTERNAL', mutability='read'),
    'get_capabilities': ToolSpec(category='SYSTEM', mutability='read'),
    'get_changeset': ToolSpec(category='VERIFY', tier1=True, timeout_s=5.0, mutability='read', direct_only=True),
    'get_changes': ToolSpec(category='SYSTEM'),
    'get_compile_errors': ToolSpec(category='CORE', core=True, mutability='read'),
    'get_component': ToolSpec(category='CORE', core=True, mutability='read'),
    'get_components_list': ToolSpec(category='SCENE', mutability='read'),
    'get_console': ToolSpec(category='CORE', core=True, timeout_s=10.0, mutability='read'),
    'get_console_since': ToolSpec(category='RUNTIME', tier1=True, mutability='read', direct_only=True),
    'get_enabled_tools': ToolSpec(category='SYSTEM', mutability='read'),
    'get_frame_stats': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'get_hierarchy': ToolSpec(category='CORE', core=True, timeout_s=15.0, mutability='read'),
    'get_memory': ToolSpec(category='RUNTIME', mutability='read'),
    'get_metrics': ToolSpec(category='RUNTIME', direct_only=True),
    'get_object_detail': ToolSpec(category='SCENE', mutability='read'),
    'get_schema': ToolSpec(category='SYSTEM', mutability='read'),
    'get_selection': ToolSpec(category='SCENE', mutability='read'),
    'get_spatial_context': ToolSpec(category='SCENE', mutability='read'),
    'get_test_count': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'get_test_progress': ToolSpec(category='TESTS', mutability='read'),
    'get_test_results': ToolSpec(category='TESTS', mutability='read'),
    'get_test_run': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'get_unity_events': ToolSpec(category='SCENE', mutability='read'),
    'list_events': ToolSpec(category='COMPONENTS', mutability='read'),
    'get_version': ToolSpec(category='_INTERNAL', timeout_s=5.0),
    'get_watches': ToolSpec(category='RUNTIME', mutability='read'),
    'import_package': ToolSpec(category='_INTERNAL', timeout_s=120.0),
    'inspect': ToolSpec(category='CORE', core=True, mutability='read'),
    'invoke_method': ToolSpec(category='RUNTIME', runtime_only=True),
    'lint_playtest': ToolSpec(category='TESTS', timeout_s=60.0, mutability='read'),
    'lint_playtest_suite': ToolSpec(category='TESTS', timeout_s=120.0, mutability='read', direct_only=True),
    'lint_scene_refs': ToolSpec(category='VERIFY', tier1=True, mutability='read'),
    'list_connections': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'list_test_runs': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'list_playtest_files': ToolSpec(category='_INTERNAL', timeout_s=10.0, mutability='read'),
    'list_skills': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'list_templates': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'load_session': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'manage_component': ToolSpec(category='CORE', core=True),
    'material': ToolSpec(category='ASSETS'),
    'material_audit': ToolSpec(category='ASSETS', mutability='read'),
    'mcp_status': ToolSpec(category='SYSTEM', core=True, mutability='read', direct_only=True),
    'menu': ToolSpec(category='SYSTEM'),
    'move_to': ToolSpec(category='RUNTIME', runtime_only=True, direct_only=True, unity_transport=True),
    'navmesh_query': ToolSpec(category='SCENE', mutability='write', direct_only=True),
    'object_diff': ToolSpec(category='SCENE', mutability='read'),
    'particle': ToolSpec(category='MEDIA'),
    'permission_prompt': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'ping': ToolSpec(category='_INTERNAL', timeout_s=5.0),
    'ping_object': ToolSpec(category='SCENE', mutability='read'),
    'prefab': ToolSpec(category='ASSETS'),
    'profile': ToolSpec(category='RUNTIME', runtime_only=True),
    'project_settings': ToolSpec(category='ASSETS'),
    'query_state': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'recompile': ToolSpec(category='SYSTEM'),
    'reconnect_unity': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    'references': ToolSpec(category='COMPONENTS'),
    'region_clear': ToolSpec(category='SCENE'),
    'release_smoke': ToolSpec(category='SYSTEM', mutability='read', direct_only=True),
    'rename_object': ToolSpec(category='SCENE'),
    'set_sibling_index': ToolSpec(category='SCENE'),
    'render_analyze': ToolSpec(category='MEDIA', mutability='read'),
    'resolve_scene_refs': ToolSpec(category='VERIFY', timeout_s=15.0, mutability='read'),
    'resolve_test_request': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'resolve_tool_schema': ToolSpec(category='SYSTEM', tier1=True, mutability='read', direct_only=True),
    # timeout_s is a fallback ceiling only -- tools/runtime.py always passes
    # timeout+20.0 explicitly.
    'run_playtest': ToolSpec(category='TESTS', tier1=True, timeout_s=300.0, runtime_only=True, mutability='write', direct_only=True, unity_transport=True),
    'run_playtest_suite': ToolSpec(category='TESTS', tier1=True, timeout_s=3600.0, mutability='write', direct_only=True),
    'runtime_snapshot': ToolSpec(category='RUNTIME', mutability='read'),
    'run_tests': ToolSpec(category='TESTS', tier1=True, timeout_s=30.0, direct_only=True, unity_transport=True),
    'run_tests_wait': ToolSpec(category='TESTS', tier1=True, timeout_s=1200.0, direct_only=True),
    'save_session': ToolSpec(category='SYSTEM', direct_only=True),
    'save_skill': ToolSpec(category='SYSTEM', direct_only=True),
    'save_template': ToolSpec(category='SYSTEM', direct_only=True),
    'scan_scene': ToolSpec(category='VERIFY', mutability='read'),
    'scene': ToolSpec(category='SCENE', tier1=True),
    'scene_diff': ToolSpec(category='SCENE', mutability='read'),
    'scene_environment': ToolSpec(category='SCENE'),
    'scene_change_plan': ToolSpec(category='SCENE', tier1=True, timeout_s=30.0, direct_only=True),
    'scene_health': ToolSpec(category='VERIFY', mutability='read'),
    'screenshot': ToolSpec(category='MEDIA', tier1=True, direct_only=True, unity_transport=True),
    'screenshot_baseline': ToolSpec(category='MEDIA', mutability='write', direct_only=True),
    'screenshot_compare': ToolSpec(category='MEDIA', direct_only=True),
    'scriptable_object': ToolSpec(category='ASSETS'),
    'search_scene': ToolSpec(category='SCENE', tier1=True, timeout_s=15.0, mutability='read'),
    'serialized_field_rename_audit': ToolSpec(category='VERIFY', mutability='read'),
    'set_active': ToolSpec(category='SCENE', tier1=True),
    'set_llm_config': ToolSpec(category='SYSTEM', direct_only=True),
    'set_material': ToolSpec(category='SCENE'),
    'set_parent': ToolSpec(category='SCENE', tier1=True),
    'set_properties': ToolSpec(category='SCENE', direct_only=True),
    'set_property': ToolSpec(category='CORE', core=True),
    'set_property_delta': ToolSpec(category='SCENE'),
    'set_rect': ToolSpec(category='UGUI'),
    'setup_objects': ToolSpec(category='SCENE', direct_only=True),
    'start_write_session': ToolSpec(category='ASSETS', mutability='write'),
    'shader': ToolSpec(category='ASSETS'),
    'smart_build': ToolSpec(category='SYSTEM', direct_only=True),
    'snapshot': ToolSpec(category='RUNTIME', mutability='read', direct_only=True),
    'spatial_query': ToolSpec(category='SCENE', mutability='read'),
    'sync_playtest_aliases_from_defs': ToolSpec(category='TESTS'),
    'sync_unity': ToolSpec(category='SYSTEM', tier1=True, direct_only=True),
    'test_step': ToolSpec(category='TESTS', runtime_only=True, mutability='write', direct_only=True, unity_transport=True),
    'timeline': ToolSpec(category='MEDIA'),
    'transfer_object': ToolSpec(category='SCENE'),
    'ui_intent': ToolSpec(category='UGUI', tier1=True, direct_only=True),
    'undo_last': ToolSpec(category='SYSTEM'),
    'unwire_event': ToolSpec(category='COMPONENTS'),
    'use_skill': ToolSpec(category='SYSTEM', direct_only=True),
    'validate_triggers': ToolSpec(category='SCENE', mutability='read'),
    'validate_playtest_aliases': ToolSpec(category='TESTS', mutability='read'),
    'validate_references': ToolSpec(category='VERIFY', tier1=True, mutability='read'),
    # May run Unity tests/playtests and stop or restart Play Mode.
    'verify_after_change': ToolSpec(category='VERIFY', tier1=True, timeout_s=600.0, direct_only=True),
    'vfx_intent': ToolSpec(category='MEDIA', tier1=True, direct_only=True),
    'inspect_uitk': ToolSpec(category='UITOOLKIT', tier1=False, mutability='read', timeout_s=15.0),
    'lint_uitk': ToolSpec(category='UITOOLKIT', tier1=False, mutability='read', timeout_s=15.0),
    'uitk_element': ToolSpec(category='UITOOLKIT', tier1=False, mutability='write', timeout_s=15.0),
    'attach_uitk': ToolSpec(category='UITOOLKIT', tier1=False, mutability='write', timeout_s=30.0),
    'uitk_file': ToolSpec(category='UITOOLKIT', tier1=False, mutability='write', timeout_s=30.0, direct_only=True, unity_transport=True),
    'uitk_intent': ToolSpec(category='UITOOLKIT', tier1=True, direct_only=True, timeout_s=60.0),
    'lint_ugui': ToolSpec(category='UGUI', tier1=False, mutability='read', timeout_s=15.0),
    'wait_until': ToolSpec(category='RUNTIME', runtime_only=True, direct_only=True, unity_transport=True),
    'watch': ToolSpec(category='RUNTIME', direct_only=True),
    'wire_event': ToolSpec(category='COMPONENTS'),
}
