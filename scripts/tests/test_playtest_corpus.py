from __future__ import annotations

from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PLAYTEST_ROOT = REPO_ROOT / "unity-test-project" / "Playtests"


def test_unity_test_project_has_checked_in_playtest_corpus() -> None:
    files = sorted(PLAYTEST_ROOT.glob("*.playtest"))

    assert [path.name for path in files] == ["ci_smoke.playtest"]


def test_ci_smoke_playtest_has_console_clean_acceptance() -> None:
    text = (PLAYTEST_ROOT / "ci_smoke.playtest").read_text(encoding="utf-8")

    assert "ASSERT_CONSOLE_CLEAN" in text
    assert "WAIT " in text
    assert "ALIAS " not in text
