"""N3: READ_CMDS audit — comprehensive tests for read/write classification."""
import pytest

from unity_mcp.middleware_types import READ_CMDS, WRITE_CMDS
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
    "get_changes",
    "get_enabled_tools",
    "get_frame_stats",
    "get_memory",
    "get_metrics",
    "get_perf",
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
    "screenshot_compare",
    "validate_layout",
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
