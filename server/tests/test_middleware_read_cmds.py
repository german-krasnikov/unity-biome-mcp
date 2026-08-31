"""N3: READ_CMDS audit — comprehensive tests for read/write classification."""
import pytest

from unity_mcp.middleware_types import READ_CMDS, WRITE_CMDS, is_write
from unity_mcp.middleware_guards import _is_batch_readonly


# ── Property tests ────────────────────────────────────────────────────────────

def test_no_overlap_read_write():
    assert READ_CMDS & WRITE_CMDS == set()


def test_compress_hierarchy_not_in_read_cmds():
    assert "compress_hierarchy" not in READ_CMDS


def test_rename_object_in_write_cmds():
    assert "rename_object" in WRITE_CMDS


def test_set_sibling_index_in_write_cmds():
    assert "set_sibling_index" in WRITE_CMDS


# ── _RO tools that were previously missing from READ_CMDS ────────────────────

@pytest.mark.parametrize("cmd", [
    "alias_status",
    "get_aliases",
    "get_capabilities",
    "get_enabled_tools",
    "get_frame_stats",
    "get_memory",
    "get_schema",
    "get_selection",
    "get_test_count",
    "get_test_progress",
    "get_test_results",
    "get_watches",
    "list_connections",
    "list_skills",
    "list_templates",
    "load_session",
    "material_audit",
    "object_diff",
    "scene_diff",
    "scene_health",
    "validate_triggers",
    "analyze_lod_culling",
    "check_colliders",
    "spatial_query",
    "render_analyze",
    "fingerprint",
    "debug_animator",
    "debug_physics",
    "debug",
    "budget_status",
    "diagnose",
    "permission_prompt",
    "ask",
    "ask_user",
    "compile_preflight",
    "await_compile",
    "auto_fix",
])
def test_ro_cmd_in_read_cmds(cmd):
    assert cmd in READ_CMDS, f"{cmd} is _RO-annotated but missing from READ_CMDS"


# ── editor action-aware read detection ───────────────────────────────────────

def test_editor_state_is_readonly():
    assert _is_batch_readonly("editor action=state") is True


def test_editor_project_path_is_readonly():
    assert _is_batch_readonly("editor action=project_path") is True


def test_editor_play_is_write():
    assert _is_batch_readonly("editor action=play") is False


def test_editor_stop_is_write():
    assert _is_batch_readonly("editor action=stop") is False


def test_editor_pause_is_write():
    assert _is_batch_readonly("editor action=pause") is False


def test_editor_no_action_is_write():
    """Conservative: editor with no action is NOT readonly."""
    assert _is_batch_readonly("editor") is False


# ── batch combinations ────────────────────────────────────────────────────────

def test_batch_alias_status_readonly():
    assert _is_batch_readonly("alias_status") is True


def test_batch_diagnose_readonly():
    assert _is_batch_readonly("diagnose") is True


def test_batch_mixed_reads_readonly():
    cmds = "editor action=state\nalias_status\nget_component path=/Player type=Transform"
    assert _is_batch_readonly(cmds) is True


def test_batch_read_plus_write_not_readonly():
    cmds = "get_component path=/X type=T\nset_property path=/X component=T prop=v value=1"
    assert _is_batch_readonly(cmds) is False


def test_batch_comments_and_blanks_ignored():
    cmds = "# comment\n\nalias_status\n  \n"
    assert _is_batch_readonly(cmds) is True


def test_blast_radius_skipped_for_readonly_editor_state_batch(tmp_path):
    """check_blast_radius returns None for read-only batch (no warning)."""
    from unity_mcp.middleware import Middleware

    mw = Middleware.__new__(Middleware)
    mw._consecutive_writes = 0
    mw._mutation_count = 0
    mw._response_hashes = []
    mw._retry_cache = {}
    mw._last_writes = {}
    mw._clean_paths = set()
    mw._error_dedup = {}
    mw._mutation_log = None
    mw._play_state_known = False
    mw.is_playing = False
    mw._RETRY_TTL = 30.0
    mw._RETRY_MAX = 128

    args = {"commands": "editor action=state\nalias_status"}
    result = mw.check_blast_radius("batch", args)
    assert result is None


# ── Phase 1a: ACTION_READS + is_write() ──────────────────────────────────────

from unity_mcp.middleware_types import is_write, ACTION_READS  # noqa: E402


# ── is_write: pure write cmds ─────────────────────────────────────────────────

def test_is_write_set_property():
    assert is_write("set_property", {}) is True

def test_is_write_delete_object():
    assert is_write("delete_object", {}) is True

def test_is_write_create_object():
    assert is_write("create_object", {}) is True

# ── is_write: pure read cmds ──────────────────────────────────────────────────

def test_is_write_get_hierarchy():
    assert is_write("get_hierarchy", {}) is False

def test_is_write_get_component():
    assert is_write("get_component", {"path": "/X", "type": "T"}) is False

def test_is_write_unknown_cmd_is_not_write():
    assert is_write("nonexistent_cmd", {}) is False


def test_is_write_uitk_file_read_is_read():
    assert is_write("uitk_file", {"action": "read"}) is False


def test_is_write_uitk_file_mutations_and_unknown_actions_fail_closed():
    assert is_write("uitk_file", {"action": "write"}) is True
    assert is_write("uitk_file", {"action": "future_action"}) is True
    assert is_write("uitk_file", {}) is True


@pytest.mark.parametrize("cmd", ["navmesh", "navmesh_query"])
@pytest.mark.parametrize(
    "action", ["sample", "path", "raycast", "status", "get_settings"]
)
def test_is_write_navmesh_read_actions(cmd, action):
    assert is_write(cmd, {"action": action}) is False


@pytest.mark.parametrize("cmd", ["navmesh", "navmesh_query"])
@pytest.mark.parametrize(
    "args",
    [
        {},
        {"action": "bake"},
        {"action": "clear"},
        {"action": "set_settings"},
        {"action": "future_action"},
        {"action": "STATUS"},
    ],
)
def test_is_write_navmesh_writes_and_unknowns_fail_closed(cmd, args):
    assert is_write(cmd, args) is True


@pytest.mark.parametrize(
    "args, expected",
    [
        ({}, False),
        ({"abort_on_fail": False}, False),
        ({"abort_on_fail": "false"}, False),
        ({"abort_on_fail": True}, True),
        ({"abort_on_fail": "true"}, True),
        ({"abort_on_fail": "future"}, True),
    ],
)
def test_is_write_wait_until_depends_on_abort_on_fail(args, expected):
    assert is_write("wait_until", args) is expected


@pytest.mark.parametrize(
    "cmd, read_args, write_args",
    [
        ("get_metrics", {"reset": False}, {"reset": True}),
        ("get_changes", {"clear": False}, {"clear": True}),
    ],
)
def test_consuming_reads_are_argument_aware(cmd, read_args, write_args):
    assert is_write(cmd, read_args) is False
    assert is_write(cmd, write_args) is True


def test_get_changes_default_consumes_and_unknowns_fail_closed():
    assert is_write("get_changes", {}) is True
    assert is_write("get_changes", {"clear": "future"}) is True


def test_asset_export_package_is_a_write():
    assert is_write("asset", {"action": "export_package"}) is True


@pytest.mark.parametrize("action", ["status", "analyze", "compare", "list_sessions"])
def test_profile_observational_actions_are_reads(action):
    assert is_write("profile", {"action": action}) is False


@pytest.mark.parametrize("args", [{}, {"action": "start"}, {"action": "stop"}, {"action": "future"}])
def test_profile_stateful_and_unknown_actions_are_writes(args):
    assert is_write("profile", args) is True


def test_is_write_doctor_is_argument_aware():
    assert is_write("doctor", {}) is False
    assert is_write("doctor", {"fix": False}) is False
    assert is_write("doctor", {"fix": True}) is True


@pytest.mark.parametrize("cmd", [
    "test_step", "run_playtest", "run_playtest_suite", "screenshot_baseline",
    "verify_after_change",
])
def test_playtest_and_baseline_side_effect_tools_are_writes(cmd):
    assert is_write(cmd, {}) is True

# ── is_write: animation ───────────────────────────────────────────────────────

def test_is_write_animation_get_is_read():
    assert is_write("animation", {"action": "get"}) is False

def test_is_write_animation_get_events_is_read():
    assert is_write("animation", {"action": "get_events"}) is False

def test_is_write_animation_get_clip_path_is_read():
    assert is_write("animation", {"action": "get_clip_path"}) is False

def test_is_write_animation_create_is_write():
    assert is_write("animation", {"action": "create"}) is True

def test_is_write_animation_edit_is_write():
    assert is_write("animation", {"action": "edit"}) is True

def test_is_write_animation_no_action_is_write():
    assert is_write("animation", {}) is True

def test_is_write_animation_none_args_is_write():
    assert is_write("animation", None) is True

# ── is_write: timeline ────────────────────────────────────────────────────────

def test_is_write_timeline_get_is_read():
    assert is_write("timeline", {"action": "get"}) is False

def test_is_write_timeline_get_bindings_is_read():
    assert is_write("timeline", {"action": "get_bindings"}) is False

def test_is_write_timeline_add_track_is_write():
    assert is_write("timeline", {"action": "add_track"}) is True

# ── is_write: animator ────────────────────────────────────────────────────────

def test_is_write_animator_get_is_read():
    assert is_write("animator", {"action": "get"}) is False

def test_is_write_animator_get_blend_tree_is_read():
    assert is_write("animator", {"action": "get_blend_tree"}) is False

def test_is_write_animator_add_state_is_write():
    assert is_write("animator", {"action": "add_state"}) is True

# ── is_write: particle ────────────────────────────────────────────────────────

def test_is_write_particle_get_is_read():
    assert is_write("particle", {"action": "get"}) is False

def test_is_write_particle_play_is_write():
    assert is_write("particle", {"action": "play"}) is True

# ── is_write: asset ───────────────────────────────────────────────────────────

def test_is_write_asset_find_is_read():
    assert is_write("asset", {"action": "find"}) is False

def test_is_write_asset_validate_move_is_read():
    assert is_write("asset", {"action": "validate_move"}) is False

def test_is_write_asset_get_dependencies_is_read():
    assert is_write("asset", {"action": "get_dependencies"}) is False

def test_is_write_asset_find_dependents_is_read():
    assert is_write("asset", {"action": "find_dependents"}) is False

def test_is_write_asset_export_package_is_not_a_read():
    assert is_write("asset", {"action": "export_package"}) is True

def test_is_write_asset_delete_is_write():
    assert is_write("asset", {"action": "delete"}) is True

def test_is_write_asset_move_is_write():
    assert is_write("asset", {"action": "move"}) is True

# ── is_write: scene, project_settings, material, shader, prefab, scriptable_object, menu ──

def test_is_write_scene_list_is_read():
    assert is_write("scene", {"action": "list"}) is False

def test_is_write_scene_open_is_write():
    assert is_write("scene", {"action": "open"}) is True

def test_is_write_project_settings_get_is_read():
    assert is_write("project_settings", {"action": "get"}) is False

def test_is_write_project_settings_set_is_write():
    assert is_write("project_settings", {"action": "set"}) is True

def test_is_write_material_list_shaders_is_read():
    assert is_write("material", {"action": "list_shaders"}) is False

def test_is_write_material_set_is_write():
    assert is_write("material", {"action": "set"}) is True

def test_is_write_shader_get_is_read():
    assert is_write("shader", {"action": "get"}) is False

def test_is_write_shader_graph_get_is_read():
    assert is_write("shader", {"action": "graph_get"}) is False

def test_is_write_shader_create_is_write():
    assert is_write("shader", {"action": "create"}) is True

def test_is_write_prefab_get_overrides_is_read():
    assert is_write("prefab", {"action": "get_overrides"}) is False

def test_is_write_prefab_save_is_write():
    assert is_write("prefab", {"action": "save"}) is True

def test_is_write_scriptable_object_find_is_read():
    assert is_write("scriptable_object", {"action": "find"}) is False

def test_is_write_scriptable_object_list_types_is_read():
    assert is_write("scriptable_object", {"action": "list_types"}) is False

def test_is_write_scriptable_object_create_is_write():
    assert is_write("scriptable_object", {"action": "create"}) is True

def test_is_write_menu_list_is_read():
    assert is_write("menu", {"action": "list"}) is False

def test_is_write_menu_execute_is_write():
    assert is_write("menu", {"action": "execute"}) is True

# ── _is_batch_readonly: action-based tools ────────────────────────────────────

def test_batch_animation_get_is_readonly():
    assert _is_batch_readonly("animation action=get path=/Player") is True

def test_batch_animation_create_not_readonly():
    assert _is_batch_readonly("animation action=create path=/Player clip=Walk") is False

def test_batch_animation_no_action_not_readonly():
    assert _is_batch_readonly("animation path=/Player") is False

def test_batch_scene_list_is_readonly():
    assert _is_batch_readonly("scene action=list") is True

def test_batch_scene_open_not_readonly():
    assert _is_batch_readonly("scene action=open path=Assets/Scenes/Main.unity") is False

def test_batch_material_list_shaders_is_readonly():
    assert _is_batch_readonly("material action=list_shaders") is True

def test_batch_asset_find_is_readonly():
    assert _is_batch_readonly("asset action=find type=Texture2D") is True

def test_batch_asset_delete_not_readonly():
    assert _is_batch_readonly("asset action=delete path=Assets/Old.mat") is False

def test_batch_mixed_animation_get_and_hierarchy_is_readonly():
    cmds = "get_hierarchy depth=2\nanimation action=get path=/Player"
    assert _is_batch_readonly(cmds) is True

def test_batch_mixed_animation_get_and_set_not_readonly():
    cmds = "animation action=get path=/Player\nset_property path=/P component=T prop=x value=1"
    assert _is_batch_readonly(cmds) is False

# ── integration: check_verification_needed respects is_write ─────────────────

def _make_mw():
    from unity_mcp.middleware import Middleware
    mw = Middleware.__new__(Middleware)
    mw._consecutive_writes = 0
    mw._mutation_count = 0
    mw._response_hashes = []
    mw._retry_cache = {}
    mw._last_writes = {}
    mw._clean_paths = {}
    mw._error_dedup = {}
    mw._mutation_log = None
    mw._play_state_known = False
    mw.is_playing = False
    mw._RETRY_TTL = 30.0
    mw._RETRY_MAX = 128
    return mw


def test_check_verification_not_triggered_for_animation_get():
    mw = _make_mw()
    result = mw.check_verification_needed("animation", {"action": "get"})
    assert result is None
    assert mw._mutation_count == 0


def test_transition_resets_on_animation_get():
    mw = _make_mw()
    mw._consecutive_writes = 3
    result = mw.transition("animation", {"action": "get"})
    assert result is None
    assert mw._consecutive_writes == 0


def test_check_retry_skipped_for_scene_list():
    from collections import OrderedDict
    mw = _make_mw()
    mw._retry_cache = OrderedDict()
    mw.check_retry("scene", {"action": "list"})
    result = mw.check_retry("scene", {"action": "list"})
    assert result is None


# ── editor mutation_mode argument-aware classification (P0-70) ───────────────

def test_is_write_mutation_mode_query_is_read():
    assert is_write("editor", {"action": "mutation_mode"}) is False


def test_is_write_mutation_mode_enable_true_is_write():
    assert is_write("editor", {"action": "mutation_mode", "enable": True}) is True


def test_is_write_mutation_mode_enable_false_is_still_write():
    """An explicit False is a real intent-set, not a query."""
    assert is_write("editor", {"action": "mutation_mode", "enable": False}) is True
