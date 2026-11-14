"""The verbs of the playtest grammar against the verbs that the hand-written parser still keeps (step 7.5 D1).

`PlaytestParser._DSL_KEYWORDS` is a hand list, one of the 16 copies of the verbs (EVAL2 E3): a VAL value may not start with a command. The grammar
(dsl/constructs/playtest.construct) prints its verbs into `PlaytestGrammar.Keywords`. This test pins in what the two lists differ, so that a verb added to one
and not to the other fails here instead of passing unnoticed. Step 7.5 H4 replaces the hand list by `Keywords`; this file goes with it.
"""
import re
from pathlib import Path

_CORE = Path(__file__).resolve().parents[3] / "unity-plugin" / "Runtime" / "Playtest" / "Core"
# text to text phases of the parser, which run before a line is read: the grammar has no construct for them (design class E)
_ONLY_IN_THE_HAND_LIST = {"CALL", "COMMENT", "END_COMMENT", "END_FOR", "END_MACRO", "FOR", "MACRO"}
# PT-1/Q23 closed the gap: EXPECT_FAIL and MCP are now in the hand list too, so a VAL
# value can no longer start with either (was the injection-guard hole EVAL2 E3 found).
_ONLY_IN_THE_GRAMMAR: set[str] = set()


def _words(file: str, initializer: str) -> set[str]:
    """The quoted words of the C# collection initializer `initializer ... { "A", "B" };` in `file` of the playtest core."""
    text = (_CORE / file).read_text(encoding="utf-8")
    found = re.search(initializer + r"[^{]*\{(.*?)\};", text, re.DOTALL)
    assert found, f"{file} has no {initializer}"
    return set(re.findall(r'"([A-Z_]+)"', found.group(1)))


def _lists() -> tuple[set[str], set[str]]:
    hand = _words("PlaytestParser.cs", "_DSL_KEYWORDS")
    grammar = _words("Generated/PlaytestReader.g.cs", "Keywords = new")
    assert len(hand) > 50 and len(grammar) > 50, "a list was not read: this test would pass without checking"
    return hand, grammar


def test_the_hand_list_holds_nothing_the_grammar_lacks_but_the_text_to_text_phases():
    hand, grammar = _lists()
    assert hand - grammar == _ONLY_IN_THE_HAND_LIST


def test_the_grammar_holds_the_verbs_the_hand_list_forgot_and_no_others():
    hand, grammar = _lists()
    assert grammar - hand == _ONLY_IN_THE_GRAMMAR
