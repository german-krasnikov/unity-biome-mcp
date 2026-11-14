"""The calls of planGate, lineVerdict, fixGate and fixApply from the Python side.

The rules are dsl/decisions/planverify.logic, printed into generated/planverify_logic.py; the facts are
planverify.contract. This module is the mechanical half: it folds the do() plan text, Haiku's batch result
and its own repair plan into those facts and nothing here decides.
"""
from .generated import planverify_logic as logic


def plan_gate(*, lines: int, max_lines: int, rejected: bool) -> str:
    """The whole-plan verdict of `validate_plan`'s pre-loop checks (empty/rejected/too_long/ok)."""
    return logic.plan_gate(logic.PlanGateInput(plan_lines=lines, plan_max_lines=max_lines, plan_rejected=rejected))


def line_verdict(*, allowed: bool, missing_required: int, is_create: bool, has_path: bool, path_declared: bool) -> str:
    """The verdict of one plan line against the catalog and the declared-paths scene."""
    return logic.line_verdict(logic.LineVerdictInput(
        line_allowed=allowed,
        line_has_path=has_path,
        line_is_create=is_create,
        line_missing_required=missing_required,
        line_path_declared=path_declared,
    ))


def fix_gate(*, partial: bool, has_sampling: bool, failed_lines: int) -> str:
    """Whether one Haiku repair round is asked for (Executor.execute)."""
    return logic.fix_gate(logic.FixGateInput(
        exec_failed_lines=failed_lines,
        exec_has_sampling=has_sampling,
        exec_partial=partial,
    ))


def fix_apply(*, empty: bool, verdict: str) -> str:
    """Whether the repaired plan is sent, or the original batch result stays."""
    return logic.fix_apply(logic.FixApplyInput(fix_empty=empty, fix_verdict=verdict))
