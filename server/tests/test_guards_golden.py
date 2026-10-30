"""The oracle of the guard decisions on the Python side (step 7.2, P15): the guards answer what the hand-written ones answered.

golden/decisions/guards.txt was taken before the rules of the read-only, play-mode, direct_only and batch gates of the Python server
moved into dsl/decisions/guards.logic (site py), from those gates themselves, so the generated decision that now stands behind them is
compared with what it replaced and not with its own output. dsl/decisions/guards.examples holds the expectations that come from the
tests of the project; the golden says that nothing else changed, the texts of the errors included. Lines come from
scripts/decisions_golden.py. A difference the owner approved is an INTENDED entry with its reason, never a new golden.
"""
import ast
from pathlib import Path

from decisions_golden import guard_answer

GOLDEN = Path(__file__).parent / "golden" / "decisions" / "guards.txt"

# repr(the key of a row) -> (the answer it has now, the reason): a difference the owner approved.
# Any other difference fails, and an entry whose row no longer differs is stale.
INTENDED: dict[str, tuple[object, str]] = {}


def golden_rows() -> list[tuple]:
    """Every golden line as a tuple: the key (the site of the guard, then the facts it was given) and, last, the answer."""
    lines = GOLDEN.read_text(encoding="utf-8").splitlines()
    return [ast.literal_eval(line) for line in lines if line and not line.startswith("#")]


def differences(rows: list[tuple], answer, intended: dict[str, tuple[object, str]]) -> list[str]:
    """What differs between the golden rows and the live guards, minus what `intended` allows."""
    problems, used = [], set()
    for *key, was in rows:
        name = repr(tuple(key))
        now = answer(tuple(key))
        want = intended[name][0] if name in intended else was
        if name in intended:
            used.add(name)
            if was == want:
                problems.append(f"INTENDED {name} says the golden answer: remove it")
        if now != want:
            problems.append(f"{name} is {now!r}, expected {want!r} (golden {was!r})")
    return problems + [f"INTENDED {name} is not a row of the golden: remove it" for name in sorted(intended.keys() - used)]


def test_the_guards_answer_as_the_hand_written_ones_did():
    rows = golden_rows()
    assert len(rows) >= 400, "the golden is nearly empty: this test would pass without checking"
    assert differences(rows, guard_answer, INTENDED) == []


def test_the_golden_holds_a_block_and_a_pass_of_every_site():
    rows = golden_rows()
    for site in ("Middleware.check_read_only", "Middleware.check_play_mode_required", "server._check_read_only", "tools._common._guard_read_only",
                 "server._send_raw", "batch._preprocess_stop_mode", "batch._preprocess_continue_mode"):
        assert len({repr(row[-1]) for row in rows if row[0] == site}) >= 2, f"{site} has one kind of answer only"


def test_a_changed_answer_is_found_and_only_an_intended_one_is_let_through():
    rows = [("Middleware.check_read_only", True, "inspect", {}, None), ("server._send_raw", "x", "raises ToolError: no")]
    live = {"Middleware.check_read_only": None, "server._send_raw": "raises ToolError: yes"}

    def answer(key: tuple):
        return live[key[0]]  # a stand-in for the live guards

    assert differences(rows, answer, {}) == ["""('server._send_raw', 'x') is 'raises ToolError: yes', expected 'raises ToolError: no' (golden 'raises ToolError: no')"""]
    name = repr(("server._send_raw", "x"))
    assert differences(rows, answer, {name: ("raises ToolError: yes", "owner 2026-10-29")}) == []
    assert differences(rows, answer, {name: ("raises ToolError: no", "no change")}) == [
        f"INTENDED {name} says the golden answer: remove it",
        f"{name} is 'raises ToolError: yes', expected 'raises ToolError: no' (golden 'raises ToolError: no')"]
    assert differences(rows[:1], answer, {name: ("x", "not a row")}) == [f"INTENDED {name} is not a row of the golden: remove it"]
