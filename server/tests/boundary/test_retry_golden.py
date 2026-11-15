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

from unity_mcp import bridge_retry
from unity_mcp.decisions.generated import retry_logic

GOLDEN = Path(__file__).parent.parent / "golden" / "decisions" / "retry.txt"
_KEY = 7  # the first seven fields of a row are the call, the rest is the answer

# repr(the key of a row) -> (the answer it has now, the reason): a difference the owner approved.
# Any other difference fails, and an entry whose row no longer differs is stale.
# retryReason takes the two probe facts together, so a refused connection reads the pid of Unity also when the probe already says busy
# (6 rows: refused, attempts 0..2, the process dead or alive). The answer is the same, the pid file is read once more; the hand-written
# method returned before it asked. The expensive probe (has_strong_busy_signal) is read exactly as often as before, in every row.
INTENDED: dict[str, tuple[tuple, str]] = {
    repr(("refused", attempt, 3, False, False, True, dead)): (
        (True, min(2.0 ** (attempt + 1), 8.0), "busy", 1, 1), "retryReason reads processDead with probeBusy: the pid is asked once more")
    for attempt in range(3) for dead in (False, True)
}

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


@pytest.mark.parametrize(("delivery_sent", "retry_safe"), [(False, False), (False, True), (True, False), (True, True)])
def test_the_sent_boundary_is_the_first_rule_and_costs_no_probe(delivery_sent, retry_safe):
    """A frame the writer accepted is never resent for a command that is not retry-safe, whatever the error; every other row is the golden's."""
    stopped = (False, 0.0, "unsafe_sent", 0, 0)
    problems = []
    for row in golden_rows():
        key, was = row[:_KEY], row[_KEY:]
        want = stopped if delivery_sent and not retry_safe else INTENDED.get(repr(key), (was,))[0]
        if (now := retry_answer(key, delivery_sent=delivery_sent, retry_safe=retry_safe)) != want:
            problems.append(f"{key} is {now}, expected {want}")
    assert problems == []


def test_retry_safety_is_asked_only_of_a_frame_that_was_sent():
    asked = []
    policy = bridge_retry.RetryPolicy(probe=None, reload=bridge_retry.DomainReloadTracker(), max_retries=0,
                                      is_retry_safe=lambda cmd: asked.append(cmd) or True)
    for sent in (False, True):
        policy.decide(OSError(), 0, 0.0, cmd="x", delivery_sent=sent)
    assert asked == ["x"]


def test_every_reason_of_the_decisions_is_in_the_table_of_resends():
    """A reason that retry.logic gains and the table lacks would be a KeyError at the first failure that meets it."""
    reasons = {value for values in (retry_logic.RetryGateValues, retry_logic.RetryReasonValues)
               for name, value in vars(values).items() if name.isupper()} - {retry_logic.RetryGateValues.OBSERVE}
    assert set(bridge_retry._RESENDS) == reasons
