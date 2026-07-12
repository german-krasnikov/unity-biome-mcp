"""Single source of truth for tool metadata (M8).

Every MCP tool + a handful of protocol-level commands (category='_INTERNAL':
ping/get_version/export_package/import_package — NOT MCP tools, contribute a
timeout only) get exactly one ToolSpec entry here. gating.py derives its
_CORE_TOOLS/_THEMED_CATEGORIES/TIER1/_ALL_KNOWN collections from this dict at
import time instead of hand-typing 4 separately-drifting literals.
"""
from typing import Literal
from dataclasses import dataclass


@dataclass(frozen=True)
class ToolSpec:
    """Single-source truth per tool.
    category: themed group key, "CORE", or "_INTERNAL" (protocol-only, not MCP tool).
    core: always-visible + full-schema.
    tier1: always-visible but not core.
    timeout_s: per-command TCP timeout; DEFAULT_TIMEOUT (30s) if unset.
    mutability: 'read' = never mutates scene/state; 'write' = may mutate (default = fail-closed).
    runtime_only: True = Play Mode required; blocked before TCP when is_playing=False.
    """
    category: str
    core: bool = False
    tier1: bool = False
    timeout_s: float = 30.0
    mutability: Literal['read', 'write'] = 'write'
    runtime_only: bool = False


DEFAULT_TIMEOUT: float = 30.0

_SPECS: dict[str, ToolSpec] = {
    'alias_status': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    'analyze_lod_culling': ToolSpec(category='MEDIA', mutability='read'),
    'animation': ToolSpec(category='MEDIA'),
    'animator': ToolSpec(category='MEDIA'),
    'animator_intent': ToolSpec(category='SYSTEM'),
    'apply_scene_change': ToolSpec(category='SCENE', tier1=True, timeout_s=120.0),
    'apply_template': ToolSpec(category='SYSTEM'),
    'ask': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    'ask_user': ToolSpec(category='SYSTEM', tier1=True, timeout_s=300.0, mutability='read'),
    'asset': ToolSpec(category='ASSETS'),
    'auto_fix': ToolSpec(category='SYSTEM', mutability='read'),
    'auto_wire': ToolSpec(category='COMPONENTS'),
    'autofit_collider': ToolSpec(category='SCENE'),
    'await_compile': ToolSpec(category='VERIFY', tier1=True, mutability='read'),
    # timeout_s live for 9 internal _send("batch",...) callers w/o explicit timeout
    # (animator_intent_tool.py:119, skills.py:61, autobatch.py:69/98/136,
    # ui_intent_tool.py:152, debug_tool.py:78, do_intent/executor.py:38/67).
    # Public batch() tool always overrides -- see tools/batch.py timeout param.
    'batch': ToolSpec(category='CORE', core=True, timeout_s=120.0),
    'budget_status': ToolSpec(category='SYSTEM', mutability='read'),
    'check_colliders': ToolSpec(category='SCENE', mutability='read'),
    'checkpoint': ToolSpec(category='SYSTEM'),
    # timeout_s is a fallback ceiling only -- code_intel.py:25 always passes
    # timeout=15.0 (fast-fail by design).
    'compile_preflight': ToolSpec(category='VERIFY', tier1=True, timeout_s=60.0, mutability='read'),
    'configure_objects': ToolSpec(category='SCENE', tier1=True),
    'console_mark': ToolSpec(category='RUNTIME', tier1=True, mutability='read'),
    'create_object': ToolSpec(category='CORE', core=True),
    'create_ui': ToolSpec(category='MEDIA'),
    'debug': ToolSpec(category='RUNTIME', mutability='read'),
    'debug_animator': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'debug_physics': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'delete_object': ToolSpec(category='SCENE', tier1=True),
    'diagnose': ToolSpec(category='VERIFY', mutability='read'),
    'discover_tools': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    'do': ToolSpec(category='CORE', core=True),
    'doctor': ToolSpec(category='SYSTEM', mutability='read'),
    'editor': ToolSpec(category='CORE', core=True),
    'execute_code': ToolSpec(category='SYSTEM', tier1=True, timeout_s=60.0),
    'export_package': ToolSpec(category='_INTERNAL', timeout_s=120.0),
    'export_playtest_aliases_to_defs': ToolSpec(category='TESTS'),
    'find_objects': ToolSpec(category='SCENE', mutability='read'),
    'fingerprint': ToolSpec(category='SYSTEM', timeout_s=10.0, mutability='read'),
    'get_aliases': ToolSpec(category='_INTERNAL', mutability='read'),
    'get_capabilities': ToolSpec(category='SYSTEM', mutability='read'),
    'get_changes': ToolSpec(category='SYSTEM', mutability='read'),
    'get_compile_errors': ToolSpec(category='CORE', core=True, mutability='read'),
    'get_component': ToolSpec(category='CORE', core=True, mutability='read'),
    'get_components_list': ToolSpec(category='SCENE', mutability='read'),
    'get_console': ToolSpec(category='CORE', core=True, timeout_s=10.0, mutability='read'),
    'get_console_since': ToolSpec(category='RUNTIME', tier1=True, mutability='read'),
    'get_enabled_tools': ToolSpec(category='SYSTEM', mutability='read'),
    'get_frame_stats': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'get_hierarchy': ToolSpec(category='CORE', core=True, timeout_s=15.0, mutability='read'),
    'get_memory': ToolSpec(category='RUNTIME', mutability='read'),
    'get_metrics': ToolSpec(category='RUNTIME', mutability='read'),
    'get_object_detail': ToolSpec(category='SCENE', mutability='read'),
    'get_schema': ToolSpec(category='SYSTEM', mutability='read'),
    'get_selection': ToolSpec(category='SCENE', mutability='read'),
    'get_spatial_context': ToolSpec(category='SCENE', mutability='read'),
    'get_test_count': ToolSpec(category='TESTS', timeout_s=10.0, mutability='read'),
    'get_test_progress': ToolSpec(category='TESTS', mutability='read'),
    'get_test_results': ToolSpec(category='TESTS', tier1=True, mutability='read'),
    'get_version': ToolSpec(category='_INTERNAL', timeout_s=5.0),
    'get_watches': ToolSpec(category='RUNTIME', mutability='read'),
    'import_package': ToolSpec(category='_INTERNAL', timeout_s=120.0),
    'inspect': ToolSpec(category='CORE', core=True, mutability='read'),
    'invoke_method': ToolSpec(category='RUNTIME', runtime_only=True),
    'lint_playtest': ToolSpec(category='TESTS', tier1=True, timeout_s=60.0, mutability='read'),
    'lint_playtest_suite': ToolSpec(category='TESTS', timeout_s=120.0, mutability='read'),
    'lint_scene_refs': ToolSpec(category='VERIFY', tier1=True, mutability='read'),
    'list_connections': ToolSpec(category='SYSTEM', mutability='read'),
    'list_playtest_files': ToolSpec(category='_INTERNAL', timeout_s=10.0, mutability='read'),
    'list_skills': ToolSpec(category='SYSTEM', mutability='read'),
    'list_templates': ToolSpec(category='SYSTEM', mutability='read'),
    'load_session': ToolSpec(category='SYSTEM', mutability='read'),
    'manage_component': ToolSpec(category='CORE', core=True),
    'material': ToolSpec(category='ASSETS'),
    'material_audit': ToolSpec(category='ASSETS', mutability='read'),
    'mcp_status': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    'menu': ToolSpec(category='SYSTEM'),
    'move_to': ToolSpec(category='RUNTIME', runtime_only=True),
    'navmesh_query': ToolSpec(category='SCENE', mutability='read'),
    'object_diff': ToolSpec(category='SCENE', mutability='read'),
    'particle': ToolSpec(category='MEDIA'),
    'permission_prompt': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    'ping': ToolSpec(category='_INTERNAL', timeout_s=5.0),
    'ping_object': ToolSpec(category='SCENE', mutability='read'),
    'prefab': ToolSpec(category='ASSETS'),
    'profile': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'project_settings': ToolSpec(category='ASSETS'),
    'query_state': ToolSpec(category='RUNTIME', mutability='read', runtime_only=True),
    'recompile': ToolSpec(category='SYSTEM'),
    'reconnect_unity': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    'references': ToolSpec(category='COMPONENTS'),
    'region_clear': ToolSpec(category='SCENE'),
    'release_smoke': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    'rename_object': ToolSpec(category='SCENE'),
    'set_sibling_index': ToolSpec(category='SCENE'),
    'render_analyze': ToolSpec(category='MEDIA', mutability='read'),
    'resolve_scene_refs': ToolSpec(category='VERIFY', tier1=True, timeout_s=15.0, mutability='read'),
    'resolve_tool_schema': ToolSpec(category='SYSTEM', tier1=True, mutability='read'),
    # timeout_s is a fallback ceiling only -- tools/runtime.py always passes
    # timeout+20.0 explicitly.
    'run_playtest': ToolSpec(category='TESTS', tier1=True, timeout_s=300.0, runtime_only=True, mutability='read'),
    'run_playtest_suite': ToolSpec(category='TESTS', timeout_s=3600.0, mutability='read'),
    'run_tests': ToolSpec(category='TESTS', tier1=True, timeout_s=300.0, mutability='read'),
    'run_tests_wait': ToolSpec(category='TESTS', tier1=True, timeout_s=300.0, mutability='read'),
    'save_session': ToolSpec(category='SYSTEM'),
    'save_skill': ToolSpec(category='SYSTEM'),
    'save_template': ToolSpec(category='SYSTEM'),
    'scan_scene': ToolSpec(category='VERIFY', mutability='read'),
    'scene': ToolSpec(category='SCENE', tier1=True),
    'scene_diff': ToolSpec(category='SCENE', mutability='read'),
    'scene_environment': ToolSpec(category='SCENE'),
    'scene_change_plan': ToolSpec(category='SCENE', tier1=True, timeout_s=30.0),
    'scene_health': ToolSpec(category='VERIFY', mutability='read'),
    'screenshot': ToolSpec(category='MEDIA', tier1=True, mutability='read'),
    'screenshot_baseline': ToolSpec(category='MEDIA', mutability='read'),
    'screenshot_compare': ToolSpec(category='MEDIA', mutability='read'),
    'scriptable_object': ToolSpec(category='ASSETS'),
    'search_scene': ToolSpec(category='SCENE', tier1=True, timeout_s=15.0, mutability='read'),
    'set_active': ToolSpec(category='SCENE', tier1=True),
    'set_llm_config': ToolSpec(category='SYSTEM'),
    'set_material': ToolSpec(category='SCENE'),
    'set_parent': ToolSpec(category='SCENE', tier1=True),
    'set_properties': ToolSpec(category='SCENE'),
    'set_property': ToolSpec(category='CORE', core=True),
    'set_property_delta': ToolSpec(category='SCENE'),
    'set_rect': ToolSpec(category='MEDIA'),
    'set_runtime_property': ToolSpec(category='RUNTIME', runtime_only=True),
    'setup_objects': ToolSpec(category='SCENE', tier1=True),
    'shader': ToolSpec(category='ASSETS'),
    'smart_build': ToolSpec(category='SYSTEM'),
    'snapshot': ToolSpec(category='RUNTIME', mutability='read'),
    'spatial_query': ToolSpec(category='SCENE', mutability='read'),
    'sync_playtest_aliases_from_defs': ToolSpec(category='TESTS'),
    'sync_unity': ToolSpec(category='SYSTEM', tier1=True),
    'test_step': ToolSpec(category='TESTS', runtime_only=True, mutability='read'),
    'timeline': ToolSpec(category='MEDIA'),
    'transfer_object': ToolSpec(category='SCENE'),
    'ui_intent': ToolSpec(category='MEDIA'),
    'undo_last': ToolSpec(category='SYSTEM', tier1=True),
    'unwire_event': ToolSpec(category='COMPONENTS'),
    'use_skill': ToolSpec(category='SYSTEM'),
    'validate_layout': ToolSpec(category='MEDIA', mutability='read'),
    'validate_playtest_aliases': ToolSpec(category='TESTS', mutability='read'),
    'validate_references': ToolSpec(category='VERIFY', tier1=True, mutability='read'),
    'verify_after_change': ToolSpec(category='VERIFY', tier1=True, timeout_s=600.0, mutability='read'),
    'vfx_intent': ToolSpec(category='MEDIA'),
    'wait_until': ToolSpec(category='RUNTIME', runtime_only=True, mutability='read'),
    'watch': ToolSpec(category='RUNTIME'),
    'wire_event': ToolSpec(category='COMPONENTS'),
}
