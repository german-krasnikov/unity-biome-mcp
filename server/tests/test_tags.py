"""Tests for unity_mcp.tools.tags — the native fold of the catalog's (tag, name)
pairs (dsl/tools.biome ITEM TAG) into per-tag frozensets. K-2 (EACH ... BY
group-by) is not in biome-dsl yet; this module is the fold, done once
(SV3-DESIGN.md §3.4).

Structural tests here hold regardless of how much TAG data the catalog
carries. The parity oracle (tagged() family == the hand-written set it
replaces) is added once dsl/tools.biome's TAG/BLAST lines are populated.
"""
import pytest

from unity_mcp.tools.tags import TAG_PAIRS, TAGS, tagged


def test_tagged_returns_frozenset():
    assert isinstance(tagged("reflect_silent"), frozenset)


def test_tagged_unknown_tag_returns_empty_frozenset():
    """Fail-open, same posture as ACTION_READS.get(cmd, frozenset())."""
    assert tagged("__no_such_tag__") == frozenset()


def test_tagged_consistent_with_pairs():
    """Every (tag, name) pair in the generated catalog must show up in tagged(tag)."""
    for tag, name in TAG_PAIRS:
        assert name in tagged(tag)


def test_tags_constant_is_union_of_pair_tags():
    assert frozenset(tag for tag, _ in TAG_PAIRS) == TAGS


# Parity oracle: the hand-written sets of the 15 consumers this slice rewires
# (SV3-DESIGN.md §0/§3.5), frozen at the moment they were read, byte-for-byte.
# sync_status (retry_safe_internal) and set_runtime_property (no family here —
# BLAST only) have no catalog block and stay native exceptions, not members.
_ORACLE: dict[str, frozenset[str]] = {
    "reflect_silent": frozenset({
        "do", "use_skill", "apply_template", "smart_build",
        "animator_intent", "ui_intent", "uitk_intent", "vfx_intent",
        "configure_objects", "setup_objects", "set_properties",
        "run_playtest", "run_playtest_suite", "start_playtest", "test_step", "move_to",
        "wait_until", "invoke_method", "execute_code",
        "run_tests", "run_tests_wait", "cancel_test_run",
        "build", "package", "export_package", "import_package",
        "save_session", "save_skill", "save_template",
        "screenshot_baseline", "screenshot_compare",
        "scene_change_plan",
        "doctor", "get_changes", "get_metrics",
    }),
    "atomic_scene": frozenset({
        "attach_uitk", "auto_wire", "autofit_collider", "create_object", "create_ui",
        "delete_object", "manage_component", "rename_object", "set_active", "set_parent",
        "set_property", "set_property_delta", "set_rect", "set_sibling_index",
        "unwire_event", "wire_event",
    }),
    "scene_state_neutral": frozenset({
        "screenshot", "run_playtest", "run_playtest_suite", "start_playtest",
    }),
    "playtest_scenario": frozenset({
        "start_playtest", "run_playtest", "run_playtest_suite", "get_playtest_run",
    }),
    "read_cacheable": frozenset({
        "get_component", "get_hierarchy", "get_components_list", "inspect",
    }),
    "strip_defaults": frozenset({"get_component", "inspect", "get_object_detail"}),
    "distill_skip": frozenset({
        "set_property", "ping", "batch", "wire_event", "set_active",
        "create_object", "delete_object", "manage_component", "recompile", "execute_code",
    }),
    "meta_cmd": frozenset({
        "list_connections", "reconnect_unity", "discover_tools", "get_enabled_tools", "ping",
    }),
    "async_full": frozenset({"get_hierarchy", "get_component", "inspect", "get_object_detail"}),
    "schema_keep_full": frozenset({"timeline", "animation", "animator", "checkpoint"}),
    "retry_safe_internal": frozenset({"get_status", "compile_status"}),
    "do_allowed": frozenset({
        "create_object", "set_property", "manage_component", "set_active", "set_material", "wire_event",
    }),
    "do_forbidden": frozenset({"delete_object", "execute_code", "recompile"}),
    "reflect_noerror": frozenset({
        "animation", "animator", "apply_scene_change", "asset", "bake", "checkpoint",
        "export_playtest_aliases_to_defs", "material", "menu", "navmesh_query", "particle",
        "prefab", "project_settings", "references", "scene", "scene_environment",
        "scriptable_object", "set_rect", "shader", "sync_playtest_aliases_from_defs",
        "timeline", "uitk_element", "uitk_file", "watch",
    }),
}


@pytest.mark.parametrize("tag", sorted(_ORACLE))
def test_tagged_matches_oracle(tag):
    """Parity oracle: every TAG family in the catalog equals the hand-written set it
    replaced, restricted to names the catalog actually has a block for (sync_status/
    set_runtime_property are documented exceptions, not failures — SV3-DESIGN.md §0)."""
    assert tagged(tag) == _ORACLE[tag]
