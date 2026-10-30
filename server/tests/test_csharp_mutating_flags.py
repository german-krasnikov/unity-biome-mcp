"""The C# registrations that step 7.2 repaired to say what Python says, and the two flags that keep the rest of their behavior.

In C# `mutating` stood for three things: authorization (read-only, ask mode, the compile guard of a batch), the Play Mode block ("changes
will be lost") and the Undo group with the change watcher. The owner decided on 2026-10-29 that Python is the authority for the first
(is_write), so each command of the census KNOWN_DIVERGENT["mutating"] of test_catalog_vs_csharp.py is registered mutating, one commit
per command, and keeps the other two by `playOk:` and `noUndo:` in its Register call. FLIPPED is that list with the flags each one
needs; a command enters it in the commit that repairs its registration, and leaves KNOWN_DIVERGENT in the same one. `batch` stays a
dispatcher in C# (per-line guards, BatchHelperReadOnlyTests) and is the one entry of KNOWN_DIVERGENT that remains.
"""
from test_catalog_vs_csharp import _hand_written

from unity_mcp.middleware_types import WRITE_CMDS

# command -> (playOk, noUndo)
FLIPPED: dict[str, tuple[bool, bool]] = {
    "editor": (True, True),  # stop, pause and step must work in Play Mode; play/stop/select change no Undo-recorded scene data
    "build": (True, False),  # async: no Undo group of its own; the build runs from the Editor whatever the mode
    "move_to": (True, False),  # async, runtime: it acts on the live Play Mode context, so the Play Mode block must not meet it
}


def test_each_repaired_command_is_registered_mutating_with_the_flags_of_its_decision():
    registered = _hand_written()
    problems = []
    for name, (play_ok, no_undo) in FLIPPED.items():
        cs = registered.get(name)
        if cs is None:
            problems.append(f"{name} has no Register call")
        elif (cs["mutating"], cs["play_ok"], cs["no_undo"]) != (True, play_ok, no_undo):
            problems.append(f"{name} is registered (mutating, playOk, noUndo) = {(cs['mutating'], cs['play_ok'], cs['no_undo'])}, "
                            f"expected {(True, play_ok, no_undo)}")
    assert problems == []


def test_each_repaired_command_is_a_write_on_the_python_side():
    assert FLIPPED.keys() <= WRITE_CMDS


def test_no_other_registration_claims_the_flags():
    """playOk and noUndo are the owner's list: a command that carries one without being on it needs a decision first."""
    claimed = {name for name, cs in _hand_written().items() if cs["play_ok"] or cs["no_undo"]}
    assert claimed <= FLIPPED.keys(), f"not on the owner's list: {sorted(claimed - FLIPPED.keys())}"
