"""C18: scripts/check_test_metadata.py -- unknown category/tag = CI failure.

3 named scenarios (plan item C18): (a) a bare [Category("...")] not routed
through TestCategories.* or the allow-listed CleanupOrderingSentinel.Category
wrapper is a violation; (b) a lane value absent from taxonomy-map.json is a
violation; (c) the current tree is a green (exit 0) baseline. Also covers
the guarded .playtest '@needs' <-> taxonomy-map.json dsl_header_value check
(job 3), since B18's scanner is real production code this lint depends on.

Runs in the standard scripts/tests lane: no Unity, no network. tmp_path
fixtures isolate the .cs-scanning tests from the real tracked tree; only
the green-baseline test (c) reads the real repo.
"""
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
SCRIPTS_DIR = REPO_ROOT / "scripts"
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))

import check_test_metadata  # noqa: E402

_DECLARED = frozenset({"Stress", "Slow", "FaultInjection"})


def _write(path: Path, text: str) -> Path:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")
    return path


# --- (a) bare [Category("...")] not routed through TestCategories.* -------

def test_bare_string_category_is_flagged(tmp_path):
    _write(tmp_path / "Nope.cs", '[Category("SomeNewString")]\npublic class NopeTests {}\n')
    violations = check_test_metadata.find_category_violations(cs_roots=(tmp_path,), declared=_DECLARED)
    assert len(violations) == 1
    assert "SomeNewString" in violations[0]


def test_testcategories_and_allowlisted_wrapper_are_not_flagged(tmp_path):
    _write(
        tmp_path / "Real.cs",
        "[Category(TestCategories.Stress)]\n"
        "[Category(CleanupOrderingSentinel.Category)]\n"
        "public class RealTests {}\n",
    )
    violations = check_test_metadata.find_category_violations(cs_roots=(tmp_path,), declared=_DECLARED)
    assert violations == []


def test_testcategories_reference_to_undeclared_const_is_flagged(tmp_path):
    # TestCategories.Bogus isn't in `declared` -- must not pass just because
    # it matches the TestCategories.* shape.
    _write(tmp_path / "Bogus.cs", "[Category(TestCategories.Bogus)]\npublic class BogusTests {}\n")
    violations = check_test_metadata.find_category_violations(cs_roots=(tmp_path,), declared=_DECLARED)
    assert len(violations) == 1
    assert "TestCategories.Bogus" in violations[0]


# --- (b) lane value absent from taxonomy-map.json --------------------------

def test_lane_referencing_unknown_dimension_is_flagged():
    lanes = {"some-lane": {"filter": {
        "modes": [], "speeds": [], "include_tags": [], "exclude_tags": [],
        "exclude_capabilities": ["totally_bogus_dimension"],
    }}}
    dimensions = {"live": {}, "monkey": {}}
    violations = check_test_metadata.check_lanes(lanes, dimensions)
    assert len(violations) == 1
    assert "totally_bogus_dimension" in violations[0]
    assert "some-lane" in violations[0]


def test_lane_with_only_known_dimension_values_is_clean():
    lanes = {"some-lane": {"filter": {
        "modes": [], "speeds": [], "include_tags": [], "exclude_tags": [],
        "exclude_capabilities": ["live", "monkey"],
    }}}
    dimensions = {"live": {}, "monkey": {}}
    assert check_test_metadata.check_lanes(lanes, dimensions) == []


# --- job 3: .playtest '@needs' values against dsl_header_value -------------

def test_playtest_needs_value_with_no_matching_dimension_is_flagged(tmp_path):
    _write(tmp_path / "orphan.playtest", "# @needs editmode\nASSERT_CONSOLE_CLEAN\n")
    dimensions = {"playmode": {"dsl_header_value": "playmode"}}  # editmode row missing
    violations = check_test_metadata.check_playtest_headers(tmp_path, dimensions)
    assert len(violations) == 1
    assert "editmode" in violations[0]


def test_playtest_header_check_no_ops_when_map_declares_no_dsl_header_rows(tmp_path):
    _write(tmp_path / "orphan.playtest", "# @needs editmode\nASSERT_CONSOLE_CLEAN\n")
    dimensions = {"live": {"dsl_header_value": None}}
    assert check_test_metadata.check_playtest_headers(tmp_path, dimensions) == []


def test_playtest_needs_editmode_on_real_corpus_matches_real_map():
    # Real B18 scanner + real taxonomy-map.json against the real .playtest
    # corpus (23 files, several declaring `# @needs editmode`) -- proves the
    # guard actually engages today (not a permanently-dormant no-op).
    dimensions = check_test_metadata.load_dimensions()
    assert any(row.get("dsl_header_value") for row in dimensions.values()), (
        "guard should be engaged on the real map, not no-opping"
    )
    violations = check_test_metadata.check_playtest_headers(
        check_test_metadata.PLAYTEST_ROOT, dimensions
    )
    assert violations == []


# --- (c) green baseline on the current tree ---------------------------------

def test_check_test_metadata_exits_zero_on_current_tree():
    assert check_test_metadata.main() == 0


def test_main_exits_nonzero_when_a_bare_category_violation_exists(tmp_path, monkeypatch):
    _write(tmp_path / "Nope.cs", '[Category("Nope")]\npublic class NopeTests {}\n')
    monkeypatch.setattr(check_test_metadata, "CS_ROOTS", (tmp_path,))
    assert check_test_metadata.main() == 1
