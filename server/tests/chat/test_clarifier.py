"""Tests for Disambiguator — history-first path resolution."""
from collections import deque
from unity_mcp.clarifier import Disambiguator, _levenshtein


def test_levenshtein_identity_substitution_and_insert_distances():
    assert _levenshtein("abc", "abc") == 0
    assert _levenshtein("abc", "abd") == 1
    assert _levenshtein("", "abc") == 3


def test_unique_candidate_no_marker():
    d = Disambiguator(recent_paths=[], clean_paths=set(), mutation_log=deque())
    res = d.decide("Player", ["/Game/Player"])
    assert res == ("/Game/Player", "")


def test_recency_wins():
    d = Disambiguator(
        recent_paths=["/Game/Player"],
        clean_paths=set(),
        mutation_log=deque(),
    )
    res = d.decide("Player", ["/Game/Player", "/UI/Player"])
    assert res is not None
    assert res[0] == "/Game/Player"
    assert "recency" in res[1]


def test_margin_not_met_blocks():
    d = Disambiguator(
        recent_paths=["/Game/Player", "/UI/Player"],
        clean_paths=set(),
        mutation_log=deque(),
    )
    # Both score 3 (recency only) — margin not met
    res = d.decide("Player", ["/Game/Player", "/UI/Player"])
    assert res is None


def test_taint_combines_recency():
    d = Disambiguator(
        recent_paths=["/Game/Player"],
        clean_paths={"/Game/Player"},
        mutation_log=deque(),
    )
    res = d.decide("Player", ["/Game/Player", "/UI/Player"])
    # /Game/Player: recency(3) + taint(2) = 5
    # /UI/Player: 0
    assert res is not None
    assert res[0] == "/Game/Player"


def test_format_block_three_candidates():
    d = Disambiguator(
        recent_paths=["/Game/Player"],
        clean_paths=set(),
        mutation_log=deque(),
    )
    out = d.format_block("Player", ["/Game/Player", "/UI/Player", "/X/Player"])
    assert "[AMBIGUOUS:" in out
    assert "[BYPASS:" in out
    assert out.count("/Player") >= 3


def test_lev_leaf_match():
    d = Disambiguator(recent_paths=[], clean_paths=set(), mutation_log=deque())
    # "Plyer" → "/Game/Player" (lev=1 to leaf "Player")
    ranked = d.rank("Plyer", ["/Game/Player", "/Wholly/Different"])
    assert ranked[0].path == "/Game/Player"
