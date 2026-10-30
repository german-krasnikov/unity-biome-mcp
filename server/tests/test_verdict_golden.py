"""The oracle of the verdict decisions (step 7.2, P15): the playtest verdict answers what the hand-written one answered.

golden/decisions/verdict.txt was taken before the rules of _is_playtest_pass, _classify_outcome, suite_verdict._classify_state and
format_layered_verdict moved into dsl/decisions/verdict.logic, from those functions themselves, so the generated decisions that now
stand behind them are compared with what they replaced and not with their own output. dsl/decisions/verdict.examples holds the
expectations that come from the tests of the project; the golden says that nothing else changed. Lines come from
scripts/decisions_golden.py. A difference the owner approved is an INTENDED entry with its reason, never a new golden.
"""
import ast
from pathlib import Path

from decisions_golden import answer

from unity_mcp.suite_verdict import _classify_state, _parse_counts, compute_suite_verdict, format_layered_verdict
from unity_mcp.tools.runtime import _classify_outcome, _is_playtest_pass

GOLDEN = Path(__file__).parent / "golden" / "decisions" / "verdict.txt"
COLUMNS = ("passes", "outcome", "counts", "state")

# (raw, format, column) of a report row -> (the answer it has now, the reason): a difference the owner approved.
# Any other difference fails, and an entry whose row no longer differs is stale.
INTENDED: dict[tuple[str | None, str | None, str], tuple[object, str]] = {}


def golden_rows() -> list[tuple]:
    """Every golden line as a tuple: ("report", raw, format, passes, outcome, counts, state) or ("suite", rows, layered)."""
    lines = GOLDEN.read_text(encoding="utf-8").splitlines()
    return [ast.literal_eval(line) for line in lines if line and not line.startswith("#")]


def live_report(raw: str | None, fmt: str | None) -> tuple:
    """(passes, outcome, counts, state) of the live functions for one report, a raised exception as "raises <name>"."""
    counts = answer(_parse_counts, raw)
    state = answer(_classify_state, *counts, raw) if isinstance(counts, tuple) else counts
    return answer(_is_playtest_pass, raw, fmt), answer(_classify_outcome, raw, fmt), counts, state


def live_suite(rows: list[tuple[str, str, bool]]) -> str:
    return format_layered_verdict(compute_suite_verdict([(path, raw, 0.0, ok) for path, raw, ok in rows]))


def differences(rows: list[tuple], intended: dict[tuple[str | None, str | None, str], tuple[object, str]]) -> list[str]:
    """What differs between the golden rows and the live functions, minus what `intended` allows."""
    problems, used = [], set()
    for row in rows:
        if row[0] == "suite":
            _, suite, was = row
            if (now := live_suite(suite)) != was:
                problems.append(f"suite {suite!r} is {now!r}, the golden has {was!r}")
            continue
        _, raw, fmt, *golden = row
        for column, was, now in zip(COLUMNS, golden, live_report(raw, fmt), strict=True):
            key = (raw, fmt, column)
            want = intended[key][0] if key in intended else was
            if key in intended:
                used.add(key)
                if was == want:
                    problems.append(f"INTENDED {key} says the golden answer: remove it")
            if now != want:
                problems.append(f"{column} of {raw!r} (format {fmt!r}) is {now!r}, expected {want!r} (golden {was!r})")
    problems += [f"INTENDED {key} is not a row of the golden: remove it" for key in sorted(intended.keys() - used, key=repr)]
    return problems


def test_the_verdict_answers_as_the_hand_written_functions_did():
    rows = golden_rows()
    assert sum(1 for row in rows if row[0] == "report") >= 500 and sum(1 for row in rows if row[0] == "suite") >= 300, \
        "the golden is nearly empty: this test would pass without checking"
    assert differences(rows, INTENDED) == []


def test_the_golden_holds_every_kind_of_answer():
    rows = golden_rows()
    reports = [row for row in rows if row[0] == "report"]
    assert {row[3] for row in reports} >= {True, False}
    assert {row[4] for row in reports} >= {"pass", "fail", "error"}
    assert {row[6] for row in reports} >= {"pass", "fail", "error", "no_verdict"}
    assert {row[2] for row in reports} == {"json", "text", None}
    labels = {tuple(row[2].split("|")[1:3]) for row in rows if row[0] == "suite"}
    assert labels >= {(inner, outer) for inner in ("INNER_PASS", "INNER_FAIL", "NO_VERDICT") for outer in ("OUTER_PASS", "OUTER_FAIL")}


def test_a_changed_answer_is_found_and_only_an_intended_one_is_let_through():
    row = ("report", "PLAYTEST: 1/1", "text", True, "pass", (1, 1), "pass")
    live = live_report("PLAYTEST: 1/1", "text")
    assert live == (True, "pass", (1, 1), "pass")
    assert differences([row], {}) == []
    wrong = ("report", "PLAYTEST: 1/1", "text", False, "pass", (1, 1), "pass")
    assert differences([wrong], {}) == ["passes of 'PLAYTEST: 1/1' (format 'text') is True, expected False (golden False)"]
    assert differences([wrong], {("PLAYTEST: 1/1", "text", "passes"): (True, "owner 2026-10-29")}) == []
    assert differences([row], {("PLAYTEST: 1/1", "text", "passes"): (True, "no change")}) == [
        "INTENDED ('PLAYTEST: 1/1', 'text', 'passes') says the golden answer: remove it"]
    assert differences([row], {("not a row", "text", "passes"): (True, "x")}) == [
        "INTENDED ('not a row', 'text', 'passes') is not a row of the golden: remove it"]
