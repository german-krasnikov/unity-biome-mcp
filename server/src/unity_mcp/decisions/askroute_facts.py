"""The call of askRoute from the Python side.

The rule is dsl/decisions/askroute.logic, printed into generated/askroute_logic.py; the facts are
askroute.contract. This module is the mechanical half: it folds the regex matches of ask/router.py
into those facts and nothing here decides.
"""
from .generated import askroute_logic as logic


def ask_route(
    *,
    mutating: bool,
    unity_noun: bool,
    broken_refs: bool,
    scene_health: bool,
    editor_state: bool,
    compile: bool,
) -> str:
    """The canonical plan key that answers a question, or 'mutating'/'out_of_scope' for no plan."""
    return logic.ask_route(logic.AskRouteInput(
        ask_broken_refs=broken_refs,
        ask_compile=compile,
        ask_editor_state=editor_state,
        ask_mutating=mutating,
        ask_scene_health=scene_health,
        ask_unity_noun=unity_noun,
    ))
