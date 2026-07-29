"""Tests for readme_facts.py — load_meta and check-facts guard."""
import json
import pathlib
import sys

import pytest

# scripts/ is at repo_root/scripts, not on sys.path by default
REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
SCRIPTS_DIR = REPO_ROOT / "scripts"

sys.path.insert(0, str(SCRIPTS_DIR))

import readme_facts  # noqa: E402
import update_readme  # noqa: E402
from readme_facts import load_meta  # noqa: E402


# ---------------------------------------------------------------------------
# load_meta
# ---------------------------------------------------------------------------

def test_load_meta_returns_dict(tmp_path):
    meta = {"tools": 10, "tests_total": 100}
    (tmp_path / "docs" / "assets").mkdir(parents=True)
    (tmp_path / "docs" / "assets" / "_meta.json").write_text(json.dumps(meta), encoding="utf-8")
    assert load_meta(tmp_path) == meta


def test_load_meta_missing_returns_empty(tmp_path):
    assert load_meta(tmp_path) == {}


def test_load_meta_returns_all_keys(tmp_path):
    meta = {"tools": 98, "tests_total": 5139, "tests_python": 2410,
            "tests_stress": 500, "tests_unity": 2149,
            "tests_unity_source": "static_grep", "tests_live": 80}
    (tmp_path / "docs" / "assets").mkdir(parents=True)
    (tmp_path / "docs" / "assets" / "_meta.json").write_text(json.dumps(meta), encoding="utf-8")
    result = load_meta(tmp_path)
    assert result["tests_unity"] == 2149
    assert result["tests_stress"] == 500


# ---------------------------------------------------------------------------
# --check-facts CLI mode
# ---------------------------------------------------------------------------


def test_check_facts_cli_exits_1_on_drift(tmp_path, monkeypatch):
    """--check-facts exits 1 when stored _meta.json has wrong unity count."""
    (tmp_path / "docs" / "assets").mkdir(parents=True)
    stale = {"tools": 0, "tests_total": 0, "tests_python": 0,
             "tests_stress": 0, "tests_unity": 0,
             "tests_unity_source": "static_grep", "tests_live": 0}
    (tmp_path / "docs" / "assets" / "_meta.json").write_text(json.dumps(stale), encoding="utf-8")
    monkeypatch.setattr(update_readme, "REPO_ROOT", tmp_path)
    monkeypatch.setattr(
        readme_facts,
        "collect_facts",
        lambda root: {**stale, "tools": 99, "tests_unity": 999},
    )
    monkeypatch.setattr(sys, "argv", ["update_readme.py", "--check-facts"])

    with pytest.raises(SystemExit) as error:
        update_readme.main()
    assert error.value.code == 1
