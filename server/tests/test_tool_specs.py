"""Tests for tools/tool_specs.py — M8: single ToolSpec source of truth."""
import pytest


def test_tool_spec_is_frozen_dataclass_with_defaults():
    from unity_mcp.tools.tool_specs import ToolSpec
    spec = ToolSpec(category="SCENE_EDIT")
    assert spec.category == "SCENE_EDIT"
    assert spec.core is False
    assert spec.tier1 is False
    assert spec.timeout_s == 30.0
    assert spec.direct_only is False
    assert spec.unity_transport is False
    with pytest.raises(Exception):  # frozen dataclass rejects mutation
        spec.category = "OTHER"


def test_specs_is_nonempty_dict_of_toolspec():
    from unity_mcp.tools.tool_specs import _SPECS, ToolSpec
    assert len(_SPECS) > 100
    assert all(isinstance(v, ToolSpec) for v in _SPECS.values())


def test_internal_commands_present_and_excluded_from_core_tier1():
    """ping/get_version/export_package/import_package are protocol commands, not MCP
    tools — category='_INTERNAL', never core/tier1 (would leak into gating collections)."""
    from unity_mcp.tools.tool_specs import _SPECS
    for name in ("ping", "get_version", "export_package", "import_package"):
        spec = _SPECS[name]
        assert spec.category == "_INTERNAL"
        assert spec.core is False
        assert spec.tier1 is False


def test_core_tools_have_core_true_and_core_category():
    from unity_mcp.tools.tool_specs import _SPECS
    # 'do' demoted from CORE to SYSTEM direct_only (Wave 2)
    for name in ("get_hierarchy", "batch", "set_property"):
        spec = _SPECS[name]
        assert spec.core is True
        assert spec.category == "CORE"


def test_do_demoted_to_system_direct_only():
    from unity_mcp.tools.tool_specs import _SPECS
    spec = _SPECS["do"]
    assert spec.core is False
    assert spec.category == "SYSTEM"
    assert spec.direct_only is True


def test_tier1_residual_tool_has_tier1_true_and_themed_category():
    """A tool promoted to always-visible but not core keeps its real themed category
    (still reachable via discover_tools()) plus tier1=True."""
    from unity_mcp.tools.tool_specs import _SPECS
    spec = _SPECS["screenshot"]
    assert spec.tier1 is True
    assert spec.core is False
    assert spec.category == "MEDIA"  # Phase 2: SCREENSHOTS → MEDIA


def test_themed_tool_has_no_core_or_tier1():
    from unity_mcp.tools.tool_specs import _SPECS
    spec = _SPECS["animation"]
    assert spec.category == "MEDIA"  # Phase 2: ANIMATION → MEDIA
    assert spec.core is False
    assert spec.tier1 is False
    assert spec.timeout_s == 30.0


def test_custom_timeout_preserved():
    from unity_mcp.tools.tool_specs import _SPECS
    assert _SPECS["run_tests"].timeout_s == 30.0
    assert _SPECS["run_tests_wait"].timeout_s == 1200.0
    assert _SPECS["get_test_run"].timeout_s == 10.0
    assert _SPECS["ping"].timeout_s == 5.0
    assert _SPECS["get_console"].timeout_s == 10.0


def test_core_count_is_13():
    """P-12440 Phase 1: CORE shrunk from 15 to 13 (compile_preflight+mcp_status in, 4 scene/verify out)."""
    from unity_mcp.tools.tool_specs import _SPECS
    assert sum(1 for s in _SPECS.values() if s.core) == 13


def test_tier1_count_in_bounds():
    """Tier1 (non-core) count: P-12440 Phase 1 target is 20; guard [23, 27].
    +3 intent tools (ui_intent, vfx_intent, uitk_intent) promoted in Step 1."""
    from unity_mcp.tools.tool_specs import _SPECS
    count = sum(1 for s in _SPECS.values() if s.tier1 and not s.core)
    assert 23 <= count <= 27, (
        f"Tier1 non-core count is {count} — outside [23, 27] bounds. "
        "Update this range intentionally if the tier1 set changed."
    )


# P-301 / G12: run_playtest_suite must be tier1 so filter_by_tier keeps it visible
def test_run_playtest_suite_is_tier1():
    """G12: run_playtest_suite must be tier1=True so it survives filter_by_tier.
    Without tier1, it lives in _ALL_KNOWN but not TIER1 — filter_by_tier drops it,
    making it invisible to clients even though mcp.tool() registered it."""
    from unity_mcp.tools.tool_specs import _SPECS
    from unity_mcp.tools.gating import TIER1
    spec = _SPECS["run_playtest_suite"]
    assert spec.tier1 is True, "run_playtest_suite must be tier1=True to appear in gateway"
    assert "run_playtest_suite" in TIER1


def test_side_effecting_playtest_and_baseline_tools_are_writes():
    from unity_mcp.tools.tool_specs import _SPECS

    for name in (
        "test_step", "run_playtest", "run_playtest_suite", "screenshot_baseline",
    ):
        assert _SPECS[name].mutability == "write", name


def test_navmesh_query_is_mixed_but_fail_closed_as_write():
    from unity_mcp.tools.tool_specs import _SPECS

    assert _SPECS["navmesh_query"].mutability == "write"


def test_non_batchable_unity_wrappers_are_direct_transport_tools():
    from unity_mcp.tools.tool_specs import _SPECS

    for name in (
        "ask_user", "run_tests", "wait_until", "move_to", "test_step",
        "run_playtest", "build", "package", "screenshot", "uitk_file",
    ):
        assert _SPECS[name].direct_only, name
        assert _SPECS[name].unity_transport, name


def test_conditional_and_file_side_effect_tools_fail_closed_as_write():
    from unity_mcp.tools.tool_specs import _SPECS

    for name in (
        "wait_until", "get_metrics", "get_changes", "screenshot",
        "screenshot_compare", "profile",
    ):
        assert _SPECS[name].mutability == "write", name


def test_runtime_only_single_source():
    """R-05: run_playtest's Play-mode gate has exactly one authority — C#'s
    CommandRouter.Registration.cs registration (runtime: false since B05 moved the gate
    past parsing into AsyncRunPlaytest's own header check). tool_specs.py's static
    runtime_only flag must NOT also flag it True: that would reintroduce a stale second
    authority Python cannot keep in sync with C#'s per-header decision — exactly the
    failure mode described in the plan's "Why merged" note for B05/B10.

    Double-red: red if C#'s registration reverts to runtime: true (assertion 1), red if
    tool_specs.py's static flag is reintroduced (assertions 2-3), red if the live C#
    export mechanism that back-fills _RUNTIME_ONLY_CMDS for every OTHER runtime-only
    command stops populating anything at all (assertion 4 — a regression there would
    silently open every Play-mode gate, not just run_playtest's).
    """
    from pathlib import Path
    from unity_mcp.tools.tool_specs import _SPECS
    from unity_mcp.middleware_types import _RUNTIME_ONLY_CMDS

    project_root = Path(__file__).parents[2]
    cs_path = project_root / "unity-plugin/Editor/CommandRouter.Registration.cs"
    assert cs_path.exists(), f"C# source not found: {cs_path}"
    cs_source = cs_path.read_text(encoding="utf-8")

    assert 'CommandRegistry.RegisterAsync("run_playtest", AsyncRunPlaytest, runtime: false,' in cs_source, (
        "C# registration must keep runtime: false — the gate moved past parsing into "
        "AsyncRunPlaytest's header check (B05); reverting here re-blocks every "
        "Edit-mode run_playtest before its header is ever read"
    )
    assert _SPECS["run_playtest"].runtime_only is False, (
        "tool_specs.py must not flag run_playtest runtime_only=True — that would "
        "reintroduce a second, Python-only authority the C# header gate cannot "
        "override, pre-blocking Edit-mode playtests again"
    )
    assert "run_playtest" not in _RUNTIME_ONLY_CMDS

    # The static baseline (from _SPECS) remains the fail-safe authority for every OTHER
    # runtime-only command; a regression that stopped the derivation from populating
    # ANYTHING would silently open every one of those gates instead of failing loudly.
    assert len(_RUNTIME_ONLY_CMDS) > 0, (
        "_RUNTIME_ONLY_CMDS derived empty — this would silently open every Play-mode "
        "gate (invoke_method, wait_until, move_to, ...) instead of failing loudly"
    )


def test_python_only_tools_without_c_handler_are_direct_only():
    """B1/B2: checkpoint_create, checkpoint_restore, brief_build, get_changeset have no
    C# handler — must be direct_only=True so batch rejects them before forwarding to C#."""
    from unity_mcp.tools.tool_specs import _SPECS
    for name in ("checkpoint_create", "checkpoint_restore", "brief_build", "get_changeset"):
        assert _SPECS[name].direct_only, (
            f"{name} has no C# handler but direct_only=False — "
            "batch will forward it to C# and get 'Unknown command'"
        )
