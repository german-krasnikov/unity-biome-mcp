"""The oracle of the retry decisions (step 7.2, P15): RetryPolicy.decide answers what the hand-written one answered.

golden/decisions/retry.txt was taken before the order of rules of RetryPolicy.decide moved into dsl/decisions/retry.logic, from the
hand-written method itself: for every error class it tells apart (and seven it does not), every attempt of three retries, and every
combination of the deadline, the reload window and what the probe says, the answer (retry or not, the delay, the reason) and how
often the probe was asked. The generated decisions that now stand behind decide are compared with what they replaced and not with
their own output; dsl/decisions/retry.examples holds the intent. Lines come from scripts/decisions_golden.py. A difference the owner
approved is an INTENDED entry with its reason, never a new golden. The delays are those of production, so the test opts out of the
fast clock of tests/_fast_clock.py.
"""
import ast
from pathlib import Path

import pytest
from decisions_golden import retry_answer

GOLDEN = Path(__file__).parent / "golden" / "decisions" / "retry.txt"
_KEY = 7  # the first seven fields of a row are the call, the rest is the answer

# repr(the key of a row) -> (the answer it has now, the reason): a difference the owner approved.
# Any other difference fails, and an entry whose row no longer differs is stale.
INTENDED: dict[str, tuple[tuple, str]] = {}

pytestmark = pytest.mark.real_clock


def golden_rows() -> list[tuple]:
    lines = GOLDEN.read_text(encoding="utf-8").splitlines()
    return [ast.literal_eval(line) for line in lines if line and not line.startswith("#")]


def differences(rows: list[tuple], answer, intended: dict[str, tuple[tuple, str]]) -> list[str]:
    """What differs between the golden rows and the live RetryPolicy.decide, minus what `intended` allows."""
    problems, used = [], set()
    for row in rows:
        key, was = row[:_KEY], row[_KEY:]
        name = repr(key)
        now = tuple(answer(key))
        want = intended[name][0] if name in intended else was
        if name in intended:
            used.add(name)
            if was == want:
                problems.append(f"INTENDED {name} says the golden answer: remove it")
        if now != want:
            problems.append(f"{name} is {now!r}, expected {want!r} (golden {was!r})")
    return problems + [f"INTENDED {name} is not a row of the golden: remove it" for name in sorted(intended.keys() - used)]


def test_decide_answers_as_the_hand_written_method_did():
    rows = golden_rows()
    assert len(rows) >= 1000, "the golden is nearly empty: this test would pass without checking"
    assert differences(rows, retry_answer, INTENDED) == []


def test_the_golden_holds_every_reason_and_every_delay():
    rows = golden_rows()
    assert {row[9] for row in rows} == {"max_retries", "deadline", "identity_mismatch", "capacity_busy", "domain_reload", "busy",
                                        "connection_refused", "process_dead", "transient", "grace_expired"}
    assert {row[8] for row in rows} == {0.0, 1.0, 2.0, 4.0, 5.0, 7.5, 8.0}
    assert {row[0] for row in rows} >= {"identity", "capacity", "reload", "refused", "OSError", "TimeoutError", "UnicodeDecodeError"}


def test_a_changed_answer_is_found_and_only_an_intended_one_is_let_through():
    rows = [("OSError", 0, 3, False, False, False, False, True, 1.0, "transient", 1, 0)]
    key = rows[0][:_KEY]
    name = repr(key)

    def same(_key):
        return (True, 1.0, "transient", 1, 0)

    def slower(_key):
        return (True, 2.0, "transient", 1, 0)

    assert differences(rows, same, {}) == []
    assert differences(rows, slower, {}) == [f"{name} is (True, 2.0, 'transient', 1, 0), expected (True, 1.0, 'transient', 1, 0) (golden (True, 1.0, 'transient', 1, 0))"]
    assert differences(rows, slower, {name: ((True, 2.0, "transient", 1, 0), "owner 2026-10-29")}) == []
    assert differences(rows, same, {name: ((True, 1.0, "transient", 1, 0), "no change")}) == [f"INTENDED {name} says the golden answer: remove it"]
    assert differences(rows, same, {"('x',)": ((), "not a row")}) == ["INTENDED ('x',) is not a row of the golden: remove it"]
