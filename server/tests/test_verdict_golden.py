"""The oracle of the verdict decisions (step 7.2, P15): the playtest verdict answers what the hand-written one answered.

golden/decisions/verdict.txt was taken before the rules of _is_playtest_pass, _classify_outcome, suite_verdict._classify_state and
format_layered_verdict moved into dsl/decisions/verdict.logic, from those functions themselves, so the generated decisions that now
stand behind them are compared with what they replaced and not with their own output. dsl/decisions/verdict.examples holds the
expectations that come from the tests of the project; the golden says that nothing else changed. Lines come from
scripts/decisions_golden.py. A difference is allowed only by an Intended rule below, with its reason and the exact answer it
gives; the golden is never regenerated to make a difference disappear.
"""
import ast
import json
from collections.abc import Callable  # noqa: TC003  (read by dataclass)
from dataclasses import dataclass
from pathlib import Path

from decisions_golden import answer

from unity_mcp.suite_verdict import _classify_state, _parse_counts, compute_suite_verdict, format_layered_verdict
from unity_mcp.tools.runtime import _classify_outcome, _is_playtest_pass

GOLDEN = Path(__file__).parent / "golden" / "decisions" / "verdict.txt"
CORPUS = Path(__file__).parent / "golden" / "decisions" / "verdict_reports.txt"
COLUMNS = ("passes", "outcome", "counts", "state")


@dataclass(frozen=True)
class Intended:
    """A difference the owner's design approved: the cells it applies to (raw, format, column, golden answer) and the answer they have now."""
    reason: str
    applies: Callable[[str | None, str | None, str, object], bool]
    now: Callable[[str | None, str | None, str], object]


def _looks_like_json(raw: str | None) -> bool:
    return (raw or "").lstrip().startswith("{")


INTENDED = [
    Intended(
        "A receipt that is no ledger (a list, null, an outer that is a number, steps that are a string, a number or hold a number) made the "
        "hand-written function raise AttributeError or TypeError. The extractor reads it as unreadable (ledger.readable is false): not a pass, "
        "outcome error (Plans/STEP_7_2_LOGIC_DECISIONS_DESIGN.md of biome-dsl, section 5; EVAL2 E2.2).",
        lambda raw, fmt, column, was: column in ("passes", "outcome") and isinstance(was, str) and was.startswith("raises "),
        lambda raw, fmt, column: False if column == "passes" else "error"),
    Intended(
        "No format given and a report that looks like JSON: the hand-written code sniffed the format for passes but made the receipt checks of "
        "outcome (unreadable, no steps, a failed that disagrees with the steps) only for an explicit json. Both now follow the sniffed format, "
        "so outcome is what the caller gets by saying json. No caller passes no format (runtime.py passes text or json, always).",
        lambda raw, fmt, column, was: column == "outcome" and fmt is None and _looks_like_json(raw),
        lambda raw, fmt, column: answer(_classify_outcome, raw, "json")),
]


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


def differences(rows: list[tuple], intended: list[Intended]) -> list[str]:
    """What differs between the golden rows and the live functions, minus what an `intended` rule gives; a rule that changes nothing is stale."""
    problems, changed = [], set()
    for row in rows:
        if row[0] == "suite":
            _, suite, was = row
            if (now := live_suite(suite)) != was:
                problems.append(f"suite {suite!r} is {now!r}, the golden has {was!r}")
            continue
        _, raw, fmt, *golden = row
        for column, was, now in zip(COLUMNS, golden, live_report(raw, fmt), strict=True):
            rule = next((rule for rule in intended if rule.applies(raw, fmt, column, was)), None)
            want = rule.now(raw, fmt, column) if rule else was
            if rule and want != was:
                changed.add(rule.reason)
            if now != want:
                problems.append(f"{column} of {raw!r} (format {fmt!r}) is {now!r}, expected {want!r} (golden {was!r})")
    return problems + [f"INTENDED rule changes no answer: remove it ({rule.reason[:60]}...)" for rule in intended if rule.reason not in changed]


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
    assert live_report("PLAYTEST: 1/1", "text") == (True, "pass", (1, 1), "pass")
    assert differences([row], []) == []
    wrong = ("report", "PLAYTEST: 1/1", "text", False, "pass", (1, 1), "pass")
    assert differences([wrong], []) == ["passes of 'PLAYTEST: 1/1' (format 'text') is True, expected False (golden False)"]
    allows = Intended("the owner said so", lambda raw, fmt, column, was: column == "passes", lambda raw, fmt, column: True)
    assert differences([wrong], [allows]) == []
    assert differences([row], [allows]) == ["INTENDED rule changes no answer: remove it (the owner said so...)"]
    names_other_answer = Intended("wrong answer", lambda raw, fmt, column, was: column == "passes", lambda raw, fmt, column: False)
    assert differences([row], [names_other_answer]) == [
        "passes of 'PLAYTEST: 1/1' (format 'text') is True, expected False (golden True)"]


def test_the_editor_plane_corpus_is_the_reports_of_the_golden():
    """verdict_reports.txt feeds the C# oracle test (Tests~/Decisions/PlaytestOracleTests.cs): it must list exactly the reports of verdict.txt, in order."""
    lines = [line.split("\t") for line in CORPUS.read_text(encoding="utf-8").splitlines() if line and not line.startswith("#")]
    corpus = [(None if fmt == "-" else fmt, json.loads(raw)) for fmt, raw in lines]
    assert len(corpus) >= 500, "the corpus is nearly empty: the C# test would pass without checking"
    assert corpus == [(fmt, raw) for kind, raw, fmt, *_ in golden_rows() if kind == "report"]
