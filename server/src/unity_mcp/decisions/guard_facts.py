"""The call of the DECISION guardVerdict from the Python side.

The rules are dsl/decisions/guards.logic, printed into generated/guards_logic.py; the facts are guards.contract. A guard of the Python
server asks about one site (`py`, or `batch` for the line gate of a batch) and gives the facts it owns: the read-only gate, whether the
call is a write; the play-mode gate, the play state and whether the command is runtime-only; the direct_only gate, whether the tool is
Python-only; the batch gate, whether the line can be part of a batch. The facts it does not own stay neutral, so that none of the other
rules of the site can answer. The texts of the errors stay with the guard that raises them.
"""
from .generated import guards_logic as logic

Verdict = logic.GuardVerdictValues

# ready, batchable, always allowed and enabled are true, so that nothing is stopped; every other fact is false
_NEUTRAL = {"ready": True, "batchable": True, "python_only": False, "auth_denied": False, "compiling": False, "allowed_during_compile": False,
            "play_mode": False, "play_known": False, "mutating": False, "allowed_in_play": False, "runtime": False, "read_only": False,
            "always_allowed": True, "tool_enabled": True}


def guard_verdict(site: str, **facts: bool) -> str:
    """The verdict (a Verdict value) of one guard at `site`, given the facts it owns by name (read_only, mutating, play_known, ...)."""
    if facts.keys() - _NEUTRAL.keys():
        raise TypeError(f"not a fact of guards.contract: {sorted(facts.keys() - _NEUTRAL.keys())}")
    return logic.guard_verdict(logic.GuardVerdictInput(guard_site=site, **{f"guard_{name}": value for name, value in {**_NEUTRAL, **facts}.items()}))
