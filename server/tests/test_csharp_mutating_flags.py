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
    "package": (True, False),  # async; list and search are reads (READ lines), the rest write; Play Mode is not a concern of the package manager
    "run_playtest": (True, False),  # async; the playtest gates Play Mode itself (B05), so the Play Mode block of a mutating command must not meet it
    "run_tests": (True, False),  # async; the test runner drives Play Mode itself
    "start_playtest": (True, False),  # async; the same gates as run_playtest (E02)
    "test_step": (True, False),  # async, runtime: it steps the running game
    "cancel_test_run": (True, True),  # sync; it cancels a run and changes no scene; always allowed and allowed while compiling stay
    "checkpoint": (True, True),  # sync; it opens its own Undo group inside the handler, so the router must not open another
    "export_playtest_aliases_to_defs": (True, True),  # sync; writes a .defs file from the PlaytestConfig asset: a file effect, no Undo-recorded scene change
    "sync_playtest_aliases_from_defs": (True, True),  # sync; loads a .defs file into the PlaytestConfig asset: an asset write, no Undo-recorded scene change
    "invoke_method": (True, True),  # sync, runtime: calls a method of the running game; its changes are no Undo-recorded scene change
    "set_runtime_property": (True, True),  # sync, runtime: sets a property of the running game; Python adds it to WRITE_CMDS by hand (no ToolSpec)
    "recompile": (True, True),  # sync; refreshes the AssetDatabase: no Undo-recorded scene change
    "scene": (True, True),  # sync; P-414: scene save must not create an Undo group; list is a read, the rest write
    "timeline": (False, False),  # sync; no flags: its write actions are Undo-recorded and may not run in Play Mode; get and get_bindings are reads (READ lines)
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
