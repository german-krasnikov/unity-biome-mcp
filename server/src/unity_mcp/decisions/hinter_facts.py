"""The calls of hintPattern, hintGate and hintAdoption from the Python side.

The rules are dsl/decisions/hinter.logic, printed into generated/hinter_logic.py; the facts are
hinter.contract. This module is the mechanical half: it folds ToolHinter's own recent-calls window and
counters into those facts and nothing here decides.
"""
from .generated import hinter_logic as logic


def hint_pattern(
    *,
    cmd: str,
    reflect_on: bool,
    get_component8: int,
    same_target_writes6: int,
    find_objects4: int,
    screenshots_since_write8: int,
    recompile6: int,
    get_console6: int,
    same_target_set_property3: int,
    inspect_loop_open: bool,
    batch_writes_open: bool,
    find_then_read_open: bool,
    screenshot_spam_open: bool,
    console_poll_open: bool,
    redundant_verify_open: bool,
) -> str:
    """Which hint pattern (or 'none') the current call earns, one hint per call, first match wins."""
    return logic.hint_pattern(logic.HintPatternInput(
        call_cmd=cmd,
        env_reflect_on=reflect_on,
        open_batch_writes=batch_writes_open,
        open_console_poll=console_poll_open,
        open_find_then_read=find_then_read_open,
        open_inspect_loop=inspect_loop_open,
        open_redundant_verify=redundant_verify_open,
        open_screenshot_spam=screenshot_spam_open,
        win_find_objects4=find_objects4,
        win_get_component8=get_component8,
        win_get_console6=get_console6,
        win_recompile6=recompile6,
        win_same_target_set_property3=same_target_set_property3,
        win_same_target_writes6=same_target_writes6,
        win_screenshots_since_write8=screenshots_since_write8,
    ))


def hint_gate(*, enabled: bool, suppressed: bool, calls_since_emit: int) -> str:
    """Whether a pattern may speak now ('open') or is skipped ('closed') — disabled/suppressed/cooldown."""
    return logic.hint_gate(logic.HintGateInput(
        hint_calls_since_emit=calls_since_emit,
        hint_enabled=enabled,
        hint_suppressed=suppressed,
    ))


def hint_adoption(*, is_suggested: bool, is_trigger: bool, ignored_before: int) -> str:
    """What the call right after a hint means: adopted, ignored, suppress (2nd ignore), or pending."""
    return logic.hint_adoption(logic.HintAdoptionInput(
        adopt_ignored_before=ignored_before,
        adopt_is_suggested=is_suggested,
        adopt_is_trigger=is_trigger,
    ))
