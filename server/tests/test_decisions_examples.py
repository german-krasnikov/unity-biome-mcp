"""Tier 0 of the decisions (step 7.2): every row of the examples of a module runs through the generated Python of that module.

The rows are dsl/decisions/<module>.json, printed from <module>.examples by `biome logic examples` and never authored. The
expectation of a row was transcribed from a test of the project (the file and line stand in its provenance), not from the code under
test. Without Node: both the rows and the generated module are committed, and scripts/dsl.sh check finds either one stale. The C#
side runs the same rows in unity-plugin/Tests~/Decisions.
"""
import importlib
import json
import re
from pathlib import Path

import pytest

DECISIONS = Path(__file__).resolve().parents[2] / "dsl" / "decisions"
# The modules of step 7.2 that have examples; a module that loses its rows must fail, not vanish from the parametrization.
MODULES = {"mutability", "verdict", "guards"}


def snake(name: str) -> str:
    """The Python name of a camelCase fact or decision, as the generator prints it."""
    return re.sub(r"[A-Z]", lambda match: "_" + match.group().lower(), name)


def run_rows(generated, rows: dict) -> tuple[int, list[str]]:
    """(rows run, the failures) of the rows of every decision of `rows` through the functions of the generated module."""
    count, failures = 0, []
    for decision, items in rows.items():
        if not isinstance(items, list):  # "generated" and "provenance" are texts
            continue
        function = getattr(generated, snake(decision))
        input_type = getattr(generated, decision[0].upper() + decision[1:] + "Input")
        for row in items:
            count += 1
            got = function(input_type(**{snake(fact): value for fact, value in row["facts"].items()}))
            if got != row["expected"]:
                failures.append(f"{decision} {row['id']}: got {got!r}, expected {row['expected']!r} ({row['provenance']})")
    return count, failures


def test_the_modules_of_step_7_2_have_their_rows():
    assert {path.stem for path in DECISIONS.glob("*.json")} == MODULES


@pytest.mark.parametrize("module", sorted(MODULES))
def test_every_row_of_the_examples_gives_its_expectation(module):
    generated = importlib.import_module(f"unity_mcp.decisions.generated.{module}_logic")
    count, failures = run_rows(generated, json.loads((DECISIONS / f"{module}.json").read_text(encoding="utf-8")))
    assert count > 0, "no row was run: this test would pass without checking"
    assert failures == []


def test_a_row_that_the_decision_answers_otherwise_is_a_failure():
    class Generated:
        @staticmethod
        def is_mutating(inp) -> bool:
            return inp.flag

        class IsMutatingInput:
            def __init__(self, flag: bool) -> None:
                self.flag = flag

    rows = {"generated": "text", "isMutating": [
        {"id": "a", "facts": {"flag": True}, "expected": True, "provenance": "t.py:1"},
        {"id": "b", "facts": {"flag": True}, "expected": False, "provenance": "t.py:2"}]}
    assert run_rows(Generated, rows) == (2, ["isMutating b: got True, expected False (t.py:2)"])
