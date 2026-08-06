"""Tests for the update_readme command facade."""

import pathlib
import subprocess
import sys

import pytest

SCRIPTS_DIR = pathlib.Path(__file__).parent.parent
REPO_ROOT = SCRIPTS_DIR.parent
sys.path.insert(0, str(SCRIPTS_DIR))

import update_readme as ur


class TestCurrentFacade:
    def test_exposes_renderer_contract(self) -> None:
        for name in (
            "generate_changelog_summary",
            "inject_changelog_into_readme",
            "make_badge_json",
            "parse_latest_changelog",
            "render",
            "stats_summary",
            "substitute_svg_markers",
            "update_readme_stats",
        ):
            assert callable(getattr(ur, name))

    def test_lazily_exposes_current_fact_collectors(self) -> None:
        assert callable(ur.count_mcp_tools)
        assert callable(ur.count_pytest_python)
        assert callable(ur.count_pytest_stress)
        assert callable(ur.count_pytest_live)
        assert callable(ur.count_unity_tests)

    @pytest.mark.parametrize(
        "obsolete_name",
        [
            "count_nunit_tests",
            "count_live_tests",
            "count_pytest_tests",
            "update_readme_alt_text",
            "update_stats_svg",
        ],
    )
    def test_obsolete_api_is_not_resurrected(self, obsolete_name: str) -> None:
        assert not hasattr(ur, obsolete_name)


class TestFactFacade:
    def test_counts_real_tool_registrations(self) -> None:
        count = ur.count_mcp_tools(REPO_ROOT / "server" / "src" / "unity_mcp")
        assert isinstance(count, int)
        assert count >= 90

    def test_missing_tool_directory_returns_none(self, tmp_path: pathlib.Path) -> None:
        assert ur.count_mcp_tools(tmp_path / "missing") is None

    def test_reads_versions_from_package_metadata(self) -> None:
        server = ur.read_server_version(REPO_ROOT / "server" / "pyproject.toml")
        plugin = ur.read_plugin_version(REPO_ROOT / "unity-plugin" / "package.json")
        assert server == plugin
        assert server and server[0].isdigit()


class TestDrift:
    def test_reports_changed_and_missing_values(self) -> None:
        stored = {"tools": 10, "version": "1.0.0"}
        fresh = {"tools": 11, "version": "1.0.0", "tests": 20}
        assert ur._facts_drift(stored, fresh) == {
            "tools": (10, 11),
            "tests": (None, 20),
        }

    def test_equal_facts_have_no_drift(self) -> None:
        facts = {"tools": 10, "tests_semantics": "discovered"}
        assert ur._facts_drift(facts, facts) == {}


class TestCommand:
    def test_help_lists_supported_modes(self) -> None:
        result = subprocess.run(
            [sys.executable, str(SCRIPTS_DIR / "update_readme.py"), "--help"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            check=False,
        )
        assert result.returncode == 0
        for mode in ("--collect", "--render", "--all", "--check", "--check-facts"):
            assert mode in result.stdout

    @pytest.mark.skipif(sys.platform == "win32", reason="README generated on Linux; Windows test counts differ due to skips")
    def test_check_accepts_committed_generated_outputs(self) -> None:
        result = subprocess.run(
            [sys.executable, str(SCRIPTS_DIR / "update_readme.py"), "--check"],
            cwd=REPO_ROOT,
            capture_output=True,
            text=True,
            encoding="utf-8",
            check=False,
        )
        assert result.returncode == 0, result.stdout + result.stderr
        assert "up to date" in result.stdout
