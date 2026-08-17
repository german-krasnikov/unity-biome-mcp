"""Live coverage metric: % of verifiable write commands with reflect rules."""
from ..middleware_types import WRITE_CMDS
from . import _RULES

# Commands with no meaningful response-parseable invariant (intentionally silent).
_SILENT_CMDS: frozenset[str] = frozenset({
    # Orchestrators
    "do", "use_skill", "apply_template", "smart_build",
    "animator_intent", "ui_intent", "uitk_intent", "vfx_intent",
    "configure_objects", "setup_objects", "set_properties",
    # Runtime / playtest (unbounded output)
    "run_playtest", "run_playtest_suite", "test_step", "move_to",
    "wait_until", "invoke_method", "execute_code",
    "run_tests", "run_tests_wait", "cancel_test_run",
    # Async / external
    "build", "package",
    # File ops without response echo
    "save_session", "save_skill", "save_template",
    "screenshot_baseline", "screenshot_compare",
    # Planning
    "scene_change_plan",
    # Conditional writes (argument-dependent)
    "doctor", "get_changes", "get_metrics",
})


def coverage() -> dict:
    """Return coverage statistics for reflect rules.

    Returns {"covered": int, "total": int, "pct": float, "missing": list[str]}.
    """
    verifiable = WRITE_CMDS - _SILENT_CMDS
    covered = set(_RULES.keys()) & verifiable
    pct = round(100 * len(covered) / max(len(verifiable), 1), 1)
    return {
        "covered": len(covered),
        "total": len(verifiable),
        "pct": pct,
        "missing": sorted(verifiable - covered),
    }
