"""Tests for scripts/quality_delta.py — TDD Red phase first."""
import contextlib
import importlib.util
import json
import pathlib
import sys

# ---------------------------------------------------------------------------
# Import helper — load quality_delta without installing it as a package
# ---------------------------------------------------------------------------
_SCRIPT = pathlib.Path(__file__).parent.parent / "quality_delta.py"


def _load():
    spec = importlib.util.spec_from_file_location("quality_delta", _SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


qd = _load()


# ---------------------------------------------------------------------------
# Fixtures
# ---------------------------------------------------------------------------
FIXTURES = pathlib.Path(__file__).parent / "fixtures"


def _toolsmith_file() -> pathlib.Path:
    return FIXTURES / "toolsmith_sample.json"


def _current(te=5, tw=10, ta=80.5):
    return {
        "date": "2026-08-05",
        "version": "1.0.0",
        "commit": "abc1234",
        "toolsmith_errors": te,
        "toolsmith_warnings": tw,
        "toolsmith_avg_score": ta,
    }


# ---------------------------------------------------------------------------
# parse_toolsmith
# ---------------------------------------------------------------------------


def test_parse_toolsmith_normal():
    r = qd.parse_toolsmith(_toolsmith_file())
    assert r == {"errors": 5, "warnings": 10, "avg_score": 80.5}


def test_parse_toolsmith_missing_summary(tmp_path):
    f = tmp_path / "t.json"
    f.write_text("{}", encoding="utf-8")
    r = qd.parse_toolsmith(f)
    assert r == {"errors": 0, "warnings": 0, "avg_score": 0.0}


def test_parse_toolsmith_toplevel_fallback(tmp_path):
    f = tmp_path / "t.json"
    f.write_text('{"errors": 3}', encoding="utf-8")
    r = qd.parse_toolsmith(f)
    assert r["errors"] == 3


# ---------------------------------------------------------------------------
# compute_delta
# ---------------------------------------------------------------------------


def test_compute_delta_no_baseline():
    d = qd.compute_delta(_current(), None)
    assert d["has_baseline"] is False
    assert d["toolsmith_errors"] == 0


def test_compute_delta_improvement():
    cur = _current(te=3)
    base = _current(te=5)
    d = qd.compute_delta(cur, base)
    assert d["toolsmith_errors"] == -2
    assert d["has_baseline"] is True


def test_compute_delta_regression():
    cur = _current(te=7)
    base = _current(te=5)
    d = qd.compute_delta(cur, base)
    assert d["toolsmith_errors"] == 2


# ---------------------------------------------------------------------------
# is_regression
# ---------------------------------------------------------------------------


def test_is_regression_true_toolsmith():
    assert qd.is_regression(_current(te=10), _current(te=5)) is True


def test_is_regression_false_no_baseline():
    assert qd.is_regression(_current(), None) is False


def test_is_regression_false_same():
    c = _current()
    assert qd.is_regression(c, c) is False


# ---------------------------------------------------------------------------
# render_pr_comment
# ---------------------------------------------------------------------------


def test_render_pr_comment_has_table():
    d = qd.compute_delta(_current(), _current())
    out = qd.render_pr_comment(_current(), _current(), d)
    assert "|" in out


def test_render_pr_comment_no_delta_col():
    d = qd.compute_delta(_current(), None)
    out = qd.render_pr_comment(_current(), None, d)
    assert "Baseline" not in out


def test_render_pr_comment_regression_block():
    cur = _current(te=10)
    base = _current(te=5)
    d = qd.compute_delta(cur, base)
    out = qd.render_pr_comment(cur, base, d)
    assert "REGRESSION" in out.upper()


def test_render_pr_comment_marker():
    d = qd.compute_delta(_current(), None)
    out = qd.render_pr_comment(_current(), None, d)
    assert "<!-- tool-quality-report -->" in out


# ---------------------------------------------------------------------------
# make_badge
# ---------------------------------------------------------------------------


def test_make_badge_green():
    b = qd.make_badge(_current(te=0))
    assert b["color"] == "green"


def test_make_badge_yellow():
    b = qd.make_badge(_current(te=5))
    assert b["color"] == "yellow"


def test_make_badge_red():
    b = qd.make_badge(_current(te=11))
    assert b["color"] == "red"


def test_make_badge_message_format():
    cur = _current(te=35, ta=72.1)
    b = qd.make_badge(cur)
    assert b["message"] == "72.1/100 · 35 errors"


# ---------------------------------------------------------------------------
# load_history / append_history
# ---------------------------------------------------------------------------


def test_load_history_missing(tmp_path):
    assert qd.load_history(tmp_path / "nope.json") == []


def test_load_history_empty_array(tmp_path):
    f = tmp_path / "h.json"
    f.write_text("[]", encoding="utf-8")
    assert qd.load_history(f) == []


def test_load_history_existing(tmp_path):
    f = tmp_path / "h.json"
    entries = [_current() for _ in range(3)]
    f.write_text(json.dumps(entries), encoding="utf-8")
    assert len(qd.load_history(f)) == 3


def test_append_history_adds_entry():
    hist = [_current(), _current()]
    result = qd.append_history(hist, _current())
    assert len(result) == 3


def test_append_history_trims():
    hist = [_current() for _ in range(91)]
    result = qd.append_history(hist, _current(), max_entries=90)
    assert len(result) == 90


# ---------------------------------------------------------------------------
# main mode A & B (integration)
# ---------------------------------------------------------------------------


def test_main_mode_a_writes_all_files(tmp_path, monkeypatch):
    toolsmith = tmp_path / "toolsmith.json"
    toolsmith.write_text(
        json.dumps({"summary": {"issues_by_severity": {"error": 5, "warning": 10}, "score": 80.5}}),
        encoding="utf-8",
    )
    history = tmp_path / "history.json"
    latest = tmp_path / "latest.json"
    comment = tmp_path / "pr-comment.md"
    badge = tmp_path / "quality.json"

    # Stub git so it doesn't fail in CI
    import subprocess

    monkeypatch.setattr(
        subprocess,
        "run",
        lambda *a, **kw: type("R", (), {"stdout": "abc1234\n", "returncode": 0})(),
    )

    sys.argv = [
        "quality_delta.py",
        "--toolsmith", str(toolsmith),
        "--history", str(history),
        "--out-latest", str(latest),
        "--out-comment", str(comment),
        "--out-badge", str(badge),
    ]
    with contextlib.suppress(SystemExit):
        qd.main()

    assert latest.exists()
    assert comment.exists()
    assert badge.exists()


# ---------------------------------------------------------------------------
# render_report
# ---------------------------------------------------------------------------


def test_render_report_basic():
    out = qd.render_report(_current(), _toolsmith_file())
    assert "# Quality Report" in out
    assert "## Project Overview" in out
    assert "## Tool Quality" in out
    assert "mcp-tool-card-linter" in out


def test_render_report_with_test_results(tmp_path):
    tr = tmp_path / "tests.json"
    tr.write_text(json.dumps({"suites": [
        {"name": "Python Server", "passed": 4886, "failed": 0, "skipped": 3, "total": 4889},
        {"name": "C# EditMode (Linux)", "passed": 3286, "failed": 4, "skipped": 0, "total": 3290},
    ]}), encoding="utf-8")
    out = qd.render_report(_current(), _toolsmith_file(), test_results_paths=[tr])
    assert "## Test Results" in out
    assert "Python Server" in out
    assert "4886" in out
    assert "C# EditMode (Linux)" in out


def test_render_report_with_coverage(tmp_path):
    cov = tmp_path / "coverage.json"
    cov.write_text(json.dumps({
        "modules": [{"name": "unity_mcp", "statements": 1000, "covered": 930, "missed": 70, "coverage": 93.0}],
        "total": {"statements": 1000, "covered": 930, "missed": 70, "coverage": 93.0},
    }), encoding="utf-8")
    out = qd.render_report(_current(), _toolsmith_file(), coverage_path=cov)
    assert "## Code Coverage" in out
    assert "93.0%" in out


def test_render_report_without_optional_sections():
    out = qd.render_report(_current(), _toolsmith_file())
    assert "## Test Results" not in out
    assert "## Code Coverage" not in out


def test_main_mode_a_with_report(tmp_path, monkeypatch):
    toolsmith = tmp_path / "toolsmith.json"
    toolsmith.write_text(
        json.dumps({"summary": {"issues_by_severity": {"error": 5, "warning": 10}, "score": 80.5}}),
        encoding="utf-8",
    )
    history = tmp_path / "history.json"
    latest = tmp_path / "latest.json"
    comment = tmp_path / "pr-comment.md"
    badge = tmp_path / "quality.json"
    report = tmp_path / "REPORT.md"

    import subprocess
    monkeypatch.setattr(
        subprocess,
        "run",
        lambda *a, **kw: type("R", (), {"stdout": "abc1234\n", "returncode": 0})(),
    )

    sys.argv = [
        "quality_delta.py",
        "--toolsmith", str(toolsmith),
        "--history", str(history),
        "--out-latest", str(latest),
        "--out-comment", str(comment),
        "--out-badge", str(badge),
        "--out-report", str(report),
    ]
    with contextlib.suppress(SystemExit):
        qd.main()

    assert report.exists()
    text = report.read_text(encoding="utf-8")
    assert "# Quality Report" in text


def test_main_mode_b_appends(tmp_path, monkeypatch):
    latest = tmp_path / "latest.json"
    latest.write_text(json.dumps(_current()), encoding="utf-8")
    history = tmp_path / "history.json"
    history.write_text(json.dumps([_current()]), encoding="utf-8")

    sys.argv = [
        "quality_delta.py",
        "--append",
        "--latest", str(latest),
        "--history", str(history),
    ]
    with contextlib.suppress(SystemExit):
        qd.main()

    result = json.loads(history.read_text(encoding="utf-8"))
    assert len(result) == 2
