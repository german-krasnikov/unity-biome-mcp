"""Tests for scripts/cochange_report.py — TDD Red phase first.

Mock boundary: subprocess.run / shutil.which for the external `cochange`
binary — never assume it's installed on the test machine (per PR-08
grounding: cochange 0.5.0, installed via Homebrew or `gradlew installDist`,
confirmed not to be present in this CI/dev environment).
"""
import importlib.util
import pathlib

import pytest

_SCRIPT = pathlib.Path(__file__).parent.parent / "cochange_report.py"


def _load():
    spec = importlib.util.spec_from_file_location("cochange_report", _SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


cr = _load()


def test_run_cochange_raises_when_binary_missing(monkeypatch):
    monkeypatch.setattr(cr.shutil, "which", lambda name: None)

    with pytest.raises(cr.CochangeUnavailable):
        cr.run_cochange(paths=["server/src"], since="1 year ago")


def test_render_report_includes_denominator_and_window():
    data = {
        "options": {"changeUnit": "merge", "since": "2025-01-01T00:00:00Z"},
        "commit": "abc1234",
        "warnings": [],
    }

    report = cr.render_report(data, denominator_note="rarer-file P(other|rarer)")

    assert "merge" in report
    assert "2025-01-01T00:00:00Z" in report
    assert "abc1234" in report
    assert "rarer-file P(other|rarer)" in report
