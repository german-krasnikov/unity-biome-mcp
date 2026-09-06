"""Gap 2b (PR-07): a named CI lane's filter matching zero tests must fail
closed when the lane declares `allow_empty: false` -- TestRunReconciler's
ZERO_TEST_MATCH handling stays lenient for ad hoc developer filters (must not
change), this is the one-level-up, named-lane guard the plan calls for.

Runs in the standard scripts/tests lane: no Unity, no network, reads the
tracked Tests/biome-test-lanes.json only.
"""
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPTS_DIR = REPO_ROOT / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import assert_lane_non_empty as alne  # noqa: E402


def test_check_fails_closed_on_zero_count_when_allow_empty_false():
    lanes = {"x": {"filter": {"allow_empty": False}}}
    assert alne.check("x", 0, lanes) is not None


def test_check_passes_on_nonzero_count():
    lanes = {"x": {"filter": {"allow_empty": False}}}
    assert alne.check("x", 5, lanes) is None


def test_check_allows_zero_when_allow_empty_true():
    lanes = {"x": {"filter": {"allow_empty": True}}}
    assert alne.check("x", 0, lanes) is None


def test_check_unknown_lane_returns_error():
    assert alne.check("nope", 5, {}) is not None


def test_main_exits_nonzero_on_zero_selection(capsys, monkeypatch):
    monkeypatch.setattr(alne, "load_lanes", lambda: {"x": {"filter": {"allow_empty": False}}})
    assert alne.main(["x", "0"]) == 1
    assert "zero tests" in capsys.readouterr().err


def test_main_exits_zero_on_nonzero_selection(monkeypatch):
    monkeypatch.setattr(alne, "load_lanes", lambda: {"x": {"filter": {"allow_empty": False}}})
    assert alne.main(["x", "3"]) == 0


def test_main_rejects_non_integer_count(capsys):
    assert alne.main(["x", "not-a-number"]) == 2
    assert "integer" in capsys.readouterr().err


def test_main_wrong_arg_count_exits_two(capsys):
    assert alne.main(["only-one-arg"]) == 2
    assert capsys.readouterr().err


def test_real_lanes_all_fail_closed_on_zero_selection():
    # Every real lane today declares allow_empty=false (Tests/biome-test-lanes.json) --
    # a zero-selection must fail closed for each one, proven against the real file
    # rather than a synthetic dict.
    lanes = alne.load_lanes()
    assert lanes, "expected at least one real lane"
    for name in lanes:
        assert alne.check(name, 0, lanes) is not None, f"{name} should fail closed on zero"
