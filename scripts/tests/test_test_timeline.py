"""TDD tests for scripts/test_timeline.py (A08: top-N fixture duration reporter)."""

import importlib.util
import pathlib
import sys

# ---------------------------------------------------------------------------
# Import helper — load without installing as a package (matches
# scripts/tests/test_check_skills_freshness.py's convention).
# ---------------------------------------------------------------------------
_SCRIPT = pathlib.Path(__file__).parent.parent / "test_timeline.py"


def _load():
    spec = importlib.util.spec_from_file_location("biome_test_timeline", _SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    sys.modules["biome_test_timeline"] = mod
    spec.loader.exec_module(mod)
    return mod


tt = _load()

CaseDuration = tt.CaseDuration
main = tt.main
median_base_setup_ms = tt.median_base_setup_ms
parse_nunit_case_durations = tt.parse_nunit_case_durations
parse_nunit_durations = tt.parse_nunit_durations
top_n = tt.top_n


def _synthetic_xml(cases: list[tuple[str, str, float]]) -> str:
    """Minimal NUnit3-shaped XML from (fixture, name, duration_s) triples."""
    case_elems = "\n".join(
        f'<test-case classname="{fixture}" fullname="{fixture}.{name}" '
        f'name="{name}" duration="{duration}" />'
        for fixture, name, duration in cases
    )
    return f"<test-run><test-suite type=\"TestSuite\">{case_elems}</test-suite></test-run>"


def test_top_n_slowest_fixtures_from_nunit_xml():
    """5 synthetic single-case fixtures -> sorted desc, truncated to N.

    Double-red: wrong sort order breaks the exact-order assert; missing
    truncation breaks both the length assert and the order assert (comparing
    against a 3-item list while 5 rows would remain).
    """
    xml_text = _synthetic_xml([
        ("FixtureA", "T1", 1.0),
        ("FixtureB", "T1", 5.0),
        ("FixtureC", "T1", 3.0),
        ("FixtureD", "T1", 0.5),
        ("FixtureE", "T1", 2.0),
    ])
    rows = parse_nunit_durations(xml_text)
    ranked = top_n(rows, 3)
    assert [name for name, _ in ranked] == ["FixtureB", "FixtureC", "FixtureE"]
    assert len(ranked) == 3


def test_parse_nunit_durations_sums_two_cases_of_same_fixture():
    """Fixture-level aggregation must sum multiple <test-case> durations under
    one classname. Double-red: red if aggregation is dropped (2 separate rows
    for SharedFixture instead of 1 summed row), red if summed into the wrong
    key (e.g. keyed by test name/fullname instead of fixture/classname).
    """
    xml_text = _synthetic_xml([
        ("SharedFixture", "TestOne", 1.5),
        ("SharedFixture", "TestTwo", 2.5),
        ("OtherFixture", "TestThree", 9.0),
    ])
    rows = dict(parse_nunit_durations(xml_text))
    assert rows == {"SharedFixture": 4.0, "OtherFixture": 9.0}


def test_parse_nunit_case_durations_is_the_single_xml_walker():
    """Case-level primitive (reused by A09 per the plan) returns one
    CaseDuration per <test-case>, unaggregated."""
    xml_text = _synthetic_xml([("FixtureA", "T1", 1.25)])
    cases = parse_nunit_case_durations(xml_text)
    assert cases == [CaseDuration(fixture="FixtureA", name="FixtureA.T1", duration_s=1.25)]


def test_median_base_setup_ms():
    """Values chosen so median != mean, discriminating a mean-instead-of-median bug."""
    assert median_base_setup_ms([1.0, 2.0, 100.0]) == 2.0
    assert median_base_setup_ms([1.0, 2.0, 3.0, 100.0]) == 2.5


def test_cli_prints_top_n_table_and_returns_zero(tmp_path, capsys):
    xml_path = tmp_path / "utf-results.xml"
    xml_path.write_text(
        _synthetic_xml([("FixtureA", "T1", 1.0), ("FixtureB", "T1", 5.0)]),
        encoding="utf-8",
    )
    exit_code = main(["--nunit-xml", str(xml_path), "--top", "5"])
    captured = capsys.readouterr()
    assert exit_code == 0
    assert "FixtureB" in captured.out
    assert "FixtureA" in captured.out
