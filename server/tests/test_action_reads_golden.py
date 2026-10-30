"""The oracle of ACTION_READS (step 7.2, P15): the read actions of the action-parameterised commands are the ones the literal stated.

golden/decisions/action_reads.txt was taken when middleware_types.ACTION_READS was a literal of that module, before the tool catalog
(READ lines of dsl/tools.biome) became its source. isMutating asks it whether an action is a read (call.actionRead), on the Python and,
through the generated ActionReads.g.cs, on the C# side; a read action that appeared or vanished would change what a read-only worker
may do. A difference the owner approved is an ADDED or REMOVED entry with its reason, never a new golden. Lines come from
scripts/decisions_golden.py.
"""
import ast
from pathlib import Path

from unity_mcp.middleware_types import ACTION_READS

GOLDEN = Path(__file__).parent / "golden" / "decisions" / "action_reads.txt"

# (command, action) -> the reason: a read action the owner added to / took from the commands since the golden. Anything else fails.
ADDED: dict[tuple[str, str], str] = {}
REMOVED: dict[tuple[str, str], str] = {}


def golden_pairs() -> set[tuple[str, str]]:
    lines = GOLDEN.read_text(encoding="utf-8").splitlines()
    return {ast.literal_eval(line) for line in lines if line and not line.startswith("#")}


def live_pairs() -> set[tuple[str, str]]:
    return {(cmd, action) for cmd, actions in ACTION_READS.items() for action in actions}


def test_the_read_actions_are_the_ones_of_the_literal():
    golden, live = golden_pairs(), live_pairs()
    assert len(golden) >= 40, "the golden is nearly empty: this test would pass without checking"
    assert live - golden == set(ADDED), f"read since the golden, not in ADDED: {sorted(live - golden - set(ADDED))}; in ADDED, not read: {sorted(set(ADDED) - (live - golden))}"
    assert golden - live == set(REMOVED), f"read in the golden, gone, not in REMOVED: {sorted(golden - live - set(REMOVED))}; in REMOVED, still read: {sorted(set(REMOVED) - (golden - live))}"

