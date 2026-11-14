"""ask_facts() — the regex fold that feeds askRoute (ask/router.py).

One question per regex, isolated as far as English allows: catches a future regex edit that
silently changes a fact's value before it ever reaches the askRoute DECISION ladder.
"""
import pytest

QUESTION_FACTS = [
    ("delete the player", {
        "mutating": True, "unity_noun": False, "broken_refs": False,
        "scene_health": False, "editor_state": False, "compile": False,
    }),
    ("list active objects", {
        "mutating": False, "unity_noun": True, "broken_refs": False,
        "scene_health": False, "editor_state": False, "compile": False,
    }),
    ("are there broken references", {
        "mutating": False, "unity_noun": True, "broken_refs": True,
        "scene_health": False, "editor_state": False, "compile": False,
    }),
    ("any errors in the scene", {
        "mutating": False, "unity_noun": True, "broken_refs": False,
        "scene_health": True, "editor_state": False, "compile": False,
    }),
    ("is play mode active right now", {
        "mutating": False, "unity_noun": True, "broken_refs": False,
        "scene_health": False, "editor_state": True, "compile": False,
    }),
    ("is the project compiling", {
        "mutating": False, "unity_noun": True, "broken_refs": False,
        "scene_health": False, "editor_state": False, "compile": True,
    }),
]


@pytest.mark.parametrize("question,expected", QUESTION_FACTS)
def test_ask_facts_matches_exactly_one_regex_family(question, expected):
    from unity_mcp.ask.router import ask_facts
    assert ask_facts(question) == expected
