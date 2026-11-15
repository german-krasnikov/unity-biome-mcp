"""Focused unit tests for the 4 middleware DECISION modules (DSL Gap Analysis #10):
injectState, reflectGate, prefetch (prefetchServe/prefetchFire), scenarioNext
(scenarioUncertainNext/sceneInvalidateGate). The full characterization lives in
dsl/decisions/*.examples (run by test_decisions_examples.py) -- these tests pin
a handful of key branches directly against the generated module, independent
of the call-site refactor.
"""
from unity_mcp.decisions.generated.injectstate_logic import InjectStateInput, InjectStateValues, inject_state
from unity_mcp.decisions.generated.prefetch_logic import (
    PrefetchFireInput,
    PrefetchFireValues,
    PrefetchServeInput,
    PrefetchServeValues,
    prefetch_fire,
    prefetch_serve,
)
from unity_mcp.decisions.generated.reflectgate_logic import ReflectGateInput, ReflectGateValues, reflect_gate
from unity_mcp.decisions.generated.scenarionext_logic import (
    SceneInvalidateGateInput,
    SceneInvalidateGateValues,
    ScenarioUncertainNextInput,
    ScenarioUncertainNextValues,
    scene_invalidate_gate,
    scenario_uncertain_next,
)


def _inject(**overrides) -> InjectStateInput:
    base = dict(
        state_auto_state_off=False, state_cmd_is_screenshot=False, state_cmd_is_playtest_scenario=False,
        state_cmd_present=True, state_cmd_is_write=True, state_cmd_is_batch=False,
        state_batch_readonly=False, state_due_by_count=False, state_gap_ok=False,
    )
    base.update(overrides)
    return InjectStateInput(**base)


def test_inject_state_fires_when_due_and_write():
    assert inject_state(_inject(state_due_by_count=True, state_gap_ok=True)) == InjectStateValues.INJECT


def test_inject_state_skips_screenshot():
    assert inject_state(_inject(state_cmd_is_screenshot=True, state_due_by_count=True, state_gap_ok=True)) == InjectStateValues.SKIP


def test_inject_state_skips_auto_state_off():
    assert inject_state(_inject(state_auto_state_off=True, state_due_by_count=True, state_gap_ok=True)) == InjectStateValues.SKIP


def _reflect(**overrides) -> ReflectGateInput:
    base = dict(
        reflect_env_on=True, reflect_result_degraded=False, reflect_no_reflect_flag=False,
        reflect_cmd_is_write=True, reflect_cmd_is_set_property=False, reflect_has_prop_and_value=False,
    )
    base.update(overrides)
    return ReflectGateInput(**base)


def test_reflect_gate_runs_reflect_on_write():
    assert reflect_gate(_reflect()) == ReflectGateValues.RUN_REFLECT


def test_reflect_gate_skips_degraded():
    assert reflect_gate(_reflect(reflect_result_degraded=True)) == ReflectGateValues.SKIP


def test_reflect_gate_falls_back_to_verify_snapshot_when_env_off():
    decision = reflect_gate(_reflect(
        reflect_env_on=False, reflect_cmd_is_set_property=True, reflect_has_prop_and_value=True,
    ))
    assert decision == ReflectGateValues.RUN_VERIFY_SNAPSHOT


def test_prefetch_serve_eligible():
    inp = PrefetchServeInput(pf_cache_configured=True, pf_cmd_cacheable=True, pf_scenario_uncertain=False)
    assert prefetch_serve(inp) == PrefetchServeValues.SERVE


def test_prefetch_serve_skips_while_uncertain():
    inp = PrefetchServeInput(pf_cache_configured=True, pf_cmd_cacheable=True, pf_scenario_uncertain=True)
    assert prefetch_serve(inp) == PrefetchServeValues.SKIP


def test_prefetch_fire_eligible():
    inp = PrefetchFireInput(
        pf_cache_configured=True, pf_scenario_uncertain=False, pf_cmd_is_write=True, pf_cmd_scene_neutral=False,
    )
    assert prefetch_fire(inp) == PrefetchFireValues.FIRE


def test_prefetch_fire_suppressed_while_uncertain():
    inp = PrefetchFireInput(
        pf_cache_configured=True, pf_scenario_uncertain=True, pf_cmd_is_write=True, pf_cmd_scene_neutral=False,
    )
    assert prefetch_fire(inp) == PrefetchFireValues.SKIP


def test_scenario_uncertain_next_marks_on_start_playtest_ack():
    inp = ScenarioUncertainNextInput(scn_cmd_is_start_playtest=True, scn_result_is_run_id_ack=True, scn_result_is_terminal=False)
    assert scenario_uncertain_next(inp) == ScenarioUncertainNextValues.MARK_UNCERTAIN


def test_scenario_uncertain_next_clears_on_terminal():
    inp = ScenarioUncertainNextInput(scn_cmd_is_start_playtest=False, scn_result_is_run_id_ack=False, scn_result_is_terminal=True)
    assert scenario_uncertain_next(inp) == ScenarioUncertainNextValues.CLEAR_UNCERTAIN


def test_scenario_uncertain_next_keeps_on_force_invalidate_alone():
    """N0a-3: force-invalidate must not clear the guard on its own."""
    inp = ScenarioUncertainNextInput(scn_cmd_is_start_playtest=False, scn_result_is_run_id_ack=False, scn_result_is_terminal=False)
    assert scenario_uncertain_next(inp) == ScenarioUncertainNextValues.KEEP


def test_scene_invalidate_gate_invalidates_on_terminal():
    inp = SceneInvalidateGateInput(scn_result_is_terminal=True, scn_force_invalidate_flag=False)
    assert scene_invalidate_gate(inp) == SceneInvalidateGateValues.INVALIDATE


def test_scene_invalidate_gate_invalidates_on_force_flag_alone():
    inp = SceneInvalidateGateInput(scn_result_is_terminal=False, scn_force_invalidate_flag=True)
    assert scene_invalidate_gate(inp) == SceneInvalidateGateValues.INVALIDATE


def test_scene_invalidate_gate_skips_when_neither():
    inp = SceneInvalidateGateInput(scn_result_is_terminal=False, scn_force_invalidate_flag=False)
    assert scene_invalidate_gate(inp) == SceneInvalidateGateValues.SKIP
