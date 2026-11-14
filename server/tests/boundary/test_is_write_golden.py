"""The oracle of the isMutating decision (step 7.2, P15): is_write answers what the hand-written function answered.

golden/decisions/is_write.txt was taken before the rule of is_write moved into dsl/decisions/mutability.logic, from the hand-written
function itself, so the generated decision that now stands behind is_write is compared with what it replaced and not with its own
output. It is the other half of the evidence next to dsl/decisions/mutability.examples, whose expectations come from the tests of
the project: the examples say what is right, the golden says nothing changed that nobody decided. Lines come from
scripts/decisions_golden.py. A difference the owner approved is an INTENDED entry with its reason, never a new golden.
"""
import ast
from pathlib import Path
from typing import TYPE_CHECKING

from unity_mcp.middleware_types import is_write

if TYPE_CHECKING:
    from collections.abc import Callable

GOLDEN = Path(__file__).parent.parent / "golden" / "decisions" / "is_write.txt"

# (cmd, repr(args), unknown_is_write) of a golden row -> (the answer it has now, the reason): a difference the owner approved.
# Any other difference fails, and an entry whose row no longer differs is stale.
INTENDED: dict[tuple[str, str, bool], tuple[bool, str]] = {}


def golden_rows() -> list[tuple[str, dict, bool, bool]]:
    """(cmd, args, unknown_is_write, answer) of every golden line."""
    lines = GOLDEN.read_text(encoding="utf-8").splitlines()
    return [ast.literal_eval(line) for line in lines if line and not line.startswith("#")]


def differences(rows: list[tuple[str, dict, bool, bool]], answer: Callable[..., bool],
                intended: dict[tuple[str, str, bool], tuple[bool, str]]) -> list[str]:
    """What differs between the golden rows and the live `answer`, minus what `intended` allows."""
    problems, used = [], set()
    for cmd, args, unknown, was in rows:
        key = (cmd, repr(args), unknown)
        now = answer(cmd, args, unknown_is_write=unknown)
        want = intended[key][0] if key in intended else was
        if key in intended:
            used.add(key)
            if was == want:
                problems.append(f"INTENDED {key} says the golden answer: remove it")
        if now != want:
            problems.append(f"is_write({cmd!r}, {args!r}, unknown_is_write={unknown}) is {now}, expected {want} (golden {was})")
    problems += [f"INTENDED {key} is not a row of the golden: remove it" for key in sorted(intended.keys() - used)]
    return problems


def test_is_write_answers_as_the_hand_written_function_did():
    rows = golden_rows()
    assert len(rows) >= 600, "the golden is nearly empty: this test would pass without checking"
    assert differences(rows, is_write, INTENDED) == []


def test_the_golden_holds_both_answers_and_both_modes_of_every_kind_of_call():
    rows = golden_rows()
    assert {answer for *_, answer in rows} == {True, False}
    assert {unknown for _, _, unknown, _ in rows} == {True, False}
    for cmd in ("wait_until", "get_changes", "doctor", "get_metrics", "editor", "navmesh", "uitk_file", "totally_unregistered_xyz"):
        assert any(row[0] == cmd for row in rows), cmd
    switches = {(cmd, tuple(sorted(args))) for cmd, args, *_ in rows}
    for key in (("wait_until", ("abort_on_fail",)), ("get_changes", ("clear",)), ("doctor", ("fix",)), ("get_metrics", ("reset",)),
                ("editor", ("action", "enable"))):
        assert key in switches, key


def _answering(table: dict[str, bool]) -> Callable[..., bool]:
    """A stand-in for is_write that answers from `table` by command."""
    def answer(cmd: str, args: dict, unknown_is_write: bool = False) -> bool:
        return table[cmd]
    return answer


_ROWS = [("inspect", {}, False, False), ("set_property", {"path": "/a"}, True, True)]


def test_a_changed_answer_is_found():
    problems = differences(_ROWS, _answering({"inspect": True, "set_property": True}), {})
    assert problems == ["is_write('inspect', {}, unknown_is_write=False) is True, expected False (golden False)"]


def test_an_intended_change_is_let_through_and_a_stale_or_foreign_entry_is_found():
    flipped = _answering({"inspect": True, "set_property": True})
    intended = {("inspect", "{}", False): (True, "owner 2026-10-29")}
    assert differences(_ROWS, flipped, intended) == []
    unchanged = _answering({"inspect": False, "set_property": True})
    assert differences(_ROWS, unchanged, intended) == ["is_write('inspect', {}, unknown_is_write=False) is False, expected True (golden False)"]
    says_golden = {("inspect", "{}", False): (False, "no change")}
    assert differences(_ROWS, unchanged, says_golden) == [f"INTENDED {next(iter(says_golden))} says the golden answer: remove it"]
    foreign = {("delete_object", "{}", False): (True, "not a row")}
    assert differences(_ROWS, unchanged, foreign) == [f"INTENDED {next(iter(foreign))} is not a row of the golden: remove it"]
