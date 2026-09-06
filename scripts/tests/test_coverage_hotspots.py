"""Tests for scripts/coverage_hotspots.py — TDD Red phase first.

Mock boundary: never shell out to real git. git_changed_lines is tested by
monkeypatching subprocess.run to return a canned unified-diff string, per
.claude/skills/testing-tdd.md ("Module State — MUST use monkeypatch").
"""
import importlib.util
import pathlib

_SCRIPT = pathlib.Path(__file__).parent.parent / "coverage_hotspots.py"
FIXTURES = pathlib.Path(__file__).parent.parent / "fixtures"


def _load():
    spec = importlib.util.spec_from_file_location("coverage_hotspots", _SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


ch = _load()


class _FakeCompletedProcess:
    def __init__(self, stdout: str):
        self.stdout = stdout


def _method(
    method_name="M",
    class_name="C",
    file="a.py",
    cc=1,
    seq_covered=0,
    seq_total=1,
    line_start=1,
    line_end=1,
):
    return ch.MethodRecord(
        file=file,
        class_name=class_name,
        method_name=method_name,
        cyclomatic_complexity=cc,
        seq_covered=seq_covered,
        seq_total=seq_total,
        branch_covered=0,
        branch_total=0,
        line_start=line_start,
        line_end=line_end,
    )


# ---------------------------------------------------------------------------
# git_changed_lines
# ---------------------------------------------------------------------------


def test_git_changed_lines_parses_multi_hunk_diff(monkeypatch):
    diff = (
        "diff --git a/foo.py b/foo.py\n"
        "index abc..def 100644\n"
        "--- a/foo.py\n"
        "+++ b/foo.py\n"
        "@@ -10,0 +11,2 @@ def foo():\n"
        "+line1\n"
        "+line2\n"
        "@@ -20,0 +23,1 @@ def bar():\n"
        "+line3\n"
    )
    monkeypatch.setattr(ch.subprocess, "run", lambda *a, **k: _FakeCompletedProcess(diff))

    result = ch.git_changed_lines("base", "head")

    assert result["foo.py"] == {11, 12, 23}


def test_git_changed_lines_handles_deletion_only_hunk(monkeypatch):
    diff = (
        "diff --git a/bar.py b/bar.py\n"
        "index abc..def 100644\n"
        "--- a/bar.py\n"
        "+++ b/bar.py\n"
        "@@ -5,3 +5,0 @@ def baz():\n"
        "-old1\n"
        "-old2\n"
        "-old3\n"
    )
    monkeypatch.setattr(ch.subprocess, "run", lambda *a, **k: _FakeCompletedProcess(diff))

    result = ch.git_changed_lines("base", "head")

    assert result["bar.py"] == set()


# ---------------------------------------------------------------------------
# rank_methods
# ---------------------------------------------------------------------------


def test_rank_methods_changed_always_before_unchanged():
    changed_low_score = _method(method_name="Changed", file="a.py", cc=1, seq_covered=9, seq_total=10, line_start=5, line_end=5)
    unchanged_high_score = _method(method_name="Unchanged", file="b.py", cc=50, seq_covered=0, seq_total=10, line_start=5, line_end=5)
    changed_lines = {"a.py": {5}}

    rows = ch.rank_methods([changed_low_score, unchanged_high_score], changed_lines, {})

    assert rows[0].method.method_name == "Changed"
    assert rows[0].changed is True
    assert rows[1].changed is False


def test_rank_methods_unknown_coverage_treated_as_worst_not_best():
    unknown = _method(method_name="Unknown", file="a.py", cc=5, seq_covered=0, seq_total=0)
    covered_90pct = _method(method_name="Covered", file="a.py", cc=5, seq_covered=9, seq_total=10)

    rows = ch.rank_methods([covered_90pct, unknown], {}, {})

    assert rows[0].method.method_name == "Unknown"
    assert rows[0].coverage_fraction is None


def test_rank_methods_caps_at_limit():
    methods = [_method(method_name=f"M{i}", file="a.py", cc=i + 1) for i in range(25)]

    rows = ch.rank_methods(methods, {}, {}, limit=20)

    assert len(rows) == 20


# ---------------------------------------------------------------------------
# render_markdown
# ---------------------------------------------------------------------------


def _meta(stale=False, source_sha="aaa1111", generated_for_sha="aaa1111"):
    return ch.ReportMeta(
        source_sha=source_sha,
        lane="Linux",
        unity_version="6000.0.65f1",
        generated_for_sha=generated_for_sha,
        stale=stale,
    )


def test_render_markdown_shows_literal_unknown_for_unmapped_coverage():
    unknown = _method(method_name="Unknown", seq_covered=0, seq_total=0)
    rows = ch.rank_methods([unknown], {}, {})

    md = ch.render_markdown(rows, _meta())

    row_line = next(line for line in md.splitlines() if "Unknown" in line)
    assert "unknown" in row_line
    assert "%" not in row_line


def test_render_markdown_marks_stale_report():
    rows = ch.rank_methods([_method()], {}, {})

    stale_md = ch.render_markdown(rows, _meta(stale=True, source_sha="aaa1111", generated_for_sha="bbb2222"))
    fresh_md = ch.render_markdown(rows, _meta(stale=False))

    assert "historical" in stale_md.lower()
    assert "historical" not in fresh_md.lower()


# ---------------------------------------------------------------------------
# scenario map
# ---------------------------------------------------------------------------


def test_scenario_map_resolves_known_class_unknown_for_rest():
    scenario_map = ch.load_scenario_map(FIXTURES / "coverage_scenario_map.txt")
    assert scenario_map["UnityMCP.Editor.CommandRegistry.Register"] == "A04"

    known = _method(class_name="UnityMCP.Editor.CommandRegistry", method_name="Register")
    unmapped = _method(class_name="Some.Other.Class", method_name="Method")

    rows = ch.rank_methods([known, unmapped], {}, scenario_map)

    by_name = {r.method.method_name: r for r in rows}
    assert by_name["Register"].scenario_ref == "A04"
    assert by_name["Method"].scenario_ref == "unknown"
