"""Binder-gotcha guard (T5 pilot slice 0): biome-dsl's py/cs printers recover a
case-driven arg's field name by matching VALUE SEQUENCES across TESTCASE rows, not by
the source's actual `case.<field>` reference
(implementations/typescript/packages/languages/py/src/plan-pytest.ts:66-74, `args()`).
Two TESTCASE columns with the same per-case values can silently print a reference to
the wrong -- but value-equal -- field. This guard fails a plan file that has that
hazard before biome ever prints it wrong. A minimal repro and the resulting mis-bound
generated line live in Plans/Reviews/pilot-scratch/binder-repro/ for the toolchain
owner; this guard does not fix the toolchain, it just keeps our own plans honest.
"""
import shlex
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TEST_PLANS = REPO_ROOT / "dsl" / "test-plans"
BINDER_REPRO = REPO_ROOT / "Plans" / "Reviews" / "pilot-scratch" / "binder-repro" / "repro.editmode"


def case_columns(text: str) -> dict[str, list[str]]:
    """{key: [value per TESTCASE row, in row order]} for every TESTCASE line of one plan file."""
    columns: dict[str, list[str]] = {}
    for line in text.splitlines():
        stripped = line.strip()
        if not stripped.startswith("TESTCASE"):
            continue  # skip prose/comment lines before shlex -- an apostrophe there is not a quote
        tokens = shlex.split(stripped)
        for token in tokens[2:]:  # tokens[1] is the case id, unnamed
            key, _, value = token.partition("=")
            if _:
                columns.setdefault(key, []).append(value)
    return columns


def colliding_columns(columns: dict[str, list[str]]) -> list[tuple[str, str]]:
    """Pairs of distinct keys whose value sequence is identical -- the printer's
    field-recovery heuristic cannot tell them apart."""
    keys = list(columns)
    return [(keys[i], keys[j]) for i in range(len(keys)) for j in range(i + 1, len(keys))
            if columns[keys[i]] == columns[keys[j]]]


def test_animation_plan_files_have_no_colliding_case_columns():
    for plan in sorted(TEST_PLANS.glob("*.editmode")):
        collisions = colliding_columns(case_columns(plan.read_text(encoding="utf-8")))
        assert not collisions, f"{plan.name}: columns with identical per-case values: {collisions}"


def test_colliding_columns_catches_the_known_repro():
    collisions = colliding_columns(case_columns(BINDER_REPRO.read_text(encoding="utf-8")))
    assert ("clip", "clip_name") in collisions
