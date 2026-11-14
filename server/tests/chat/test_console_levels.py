"""Issue 27: PROBLEM_LEVELS is the single source of truth for "is there a problem?"
console-level filtering — must include Exception (unhandled C# exceptions), not just Error.
"""
from unity_mcp.console_levels import PROBLEM_LEVELS


def test_problem_levels_constant_includes_exception():
    levels = PROBLEM_LEVELS.split(",")
    assert "Error" in levels
    assert "Exception" in levels
    assert "Assert" in levels


def test_ask_plans_scene_health_uses_problem_levels():
    """M1: SCENE_HEALTH's get_console step had a hardcoded level='Error' that
    silently missed Exception/Assert entries — must use PROBLEM_LEVELS."""
    from unity_mcp.ask.plans import CANONICAL_PLANS

    plan = CANONICAL_PLANS["SCENE_HEALTH"]
    console_steps = [args for name, args in plan.steps if name == "get_console"]
    assert console_steps, "SCENE_HEALTH plan must include a get_console step"
    assert console_steps[0]["level"] == PROBLEM_LEVELS
