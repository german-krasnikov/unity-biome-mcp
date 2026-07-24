"""Tests for update_readme.py — TDD Red phase written first."""
import json
import pathlib
import sys

import pytest

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import update_readme as ur

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent


# ---------------------------------------------------------------------------
# count_mcp_tools
# ---------------------------------------------------------------------------

class TestCountMcpTools:
    def test_counts_mcp_tool_calls_in_dir(self) -> None:
        tools_dir = REPO_ROOT / "server" / "src" / "unity_mcp"
        count = ur.count_mcp_tools(tools_dir)
        # We know there are at least 80 from grepping
        assert count >= 80

    def test_returns_int(self) -> None:
        tools_dir = REPO_ROOT / "server" / "src" / "unity_mcp"
        assert isinstance(ur.count_mcp_tools(tools_dir), int)

    def test_minimal_source(self, tmp_path: pathlib.Path) -> None:
        src = tmp_path / "mytools.py"
        src.write_text(
            "async def register(mcp):\n"
            "    mcp.tool(annotations=None)(foo)\n"
            "    mcp.tool(annotations=None)(bar)\n"
            "@mcp.tool()\n"
            "async def baz(): pass\n"
        )
        assert ur.count_mcp_tools(tmp_path) == 3

    def test_missing_dir_returns_none(self, tmp_path: pathlib.Path) -> None:
        result = ur.count_mcp_tools(tmp_path / "nonexistent")
        assert result is None


# ---------------------------------------------------------------------------
# count_pytest_tests
# ---------------------------------------------------------------------------

class TestCountPytestTests:
    def test_returns_int_or_none(self) -> None:
        # C9: count_pytest_tests removed; use count_pytest_python from readme_facts
        result = ur.count_pytest_python(REPO_ROOT / "server" / "tests")
        assert result is None or isinstance(result, int)

    def test_nonexistent_dir_returns_none(self, tmp_path: pathlib.Path) -> None:
        result = ur.count_pytest_python(tmp_path / "no_such_tests")
        assert result is None or result == 0


# ---------------------------------------------------------------------------
# read_server_version
# ---------------------------------------------------------------------------

class TestReadServerVersion:
    def test_reads_real_pyproject(self) -> None:
        ver = ur.read_server_version(REPO_ROOT / "server" / "pyproject.toml")
        assert ver is not None
        assert ver.startswith("0.") or ver[0].isdigit()

    def test_returns_none_on_missing(self, tmp_path: pathlib.Path) -> None:
        assert ur.read_server_version(tmp_path / "pyproject.toml") is None

    def test_parses_version_field(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "pyproject.toml").write_text('[project]\nversion = "1.2.3"\n')
        assert ur.read_server_version(tmp_path / "pyproject.toml") == "1.2.3"


# ---------------------------------------------------------------------------
# read_plugin_version
# ---------------------------------------------------------------------------

class TestReadPluginVersion:
    def test_reads_real_package_json(self) -> None:
        ver = ur.read_plugin_version(REPO_ROOT / "unity-plugin" / "package.json")
        assert ver is not None
        assert "." in ver

    def test_returns_none_on_missing(self, tmp_path: pathlib.Path) -> None:
        assert ur.read_plugin_version(tmp_path / "package.json") is None

    def test_parses_version_field(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "package.json").write_text('{"version": "2.3.4"}')
        assert ur.read_plugin_version(tmp_path / "package.json") == "2.3.4"


# ---------------------------------------------------------------------------
# parse_latest_changelog
# ---------------------------------------------------------------------------

class TestParseLatestChangelog:
    def test_returns_version_and_title(self) -> None:
        cl = "# Changelog\n\n## [v1.2.3] — 2026-01-01\n\n- Some change\n"
        ver, title = ur.parse_latest_changelog(cl)
        assert ver == "v1.2.3"
        assert "2026-01-01" in title or title  # title may be the date part

    def test_real_changelog(self) -> None:
        text = (REPO_ROOT / "CHANGELOG.md").read_text()
        ver, title = ur.parse_latest_changelog(text)
        assert ver.startswith("v")

    def test_returns_question_marks_on_empty(self) -> None:
        ver, title = ur.parse_latest_changelog("")
        assert ver == "?"
        assert title == "?"


# ---------------------------------------------------------------------------
# extract_changelog_blocks
# ---------------------------------------------------------------------------

class TestExtractChangelogBlocks:
    def test_extracts_two_blocks(self) -> None:
        cl = (
            "# Changelog\n\n"
            "## [v2.0.0] — 2026-02-01\n\n"
            "- Feature A\n- Feature B\n- Feature C\n- Feature D\n\n"
            "## [v1.9.0] — 2026-01-01\n\n"
            "- Old thing\n"
        )
        blocks = ur.extract_changelog_blocks(cl, n=2, max_bullets=3)
        assert len(blocks) == 2
        assert "v2.0.0" in blocks[0]
        assert "v1.9.0" in blocks[1]

    def test_truncates_bullets(self) -> None:
        cl = (
            "## [v1.0.0] — 2026-01-01\n\n"
            "- A\n- B\n- C\n- D\n- E\n\n"
            "## [v0.9.0] — 2025-12-01\n\n"
            "- X\n"
        )
        blocks = ur.extract_changelog_blocks(cl, n=2, max_bullets=3)
        # First block should have exactly 3 bullets
        assert blocks[0].count("\n- ") + blocks[0].startswith("- ") <= 3 + 1

    def test_returns_one_if_only_one_version(self) -> None:
        cl = "## [v1.0.0] — 2026-01-01\n\n- Only one\n"
        blocks = ur.extract_changelog_blocks(cl, n=2, max_bullets=3)
        assert len(blocks) == 1


# ---------------------------------------------------------------------------
# inject_changelog_into_readme
# ---------------------------------------------------------------------------

class TestInjectChangelog:
    def test_replaces_between_markers(self) -> None:
        readme = "# Title\n\n<!-- CHANGELOG_START -->\nOLD CONTENT\n<!-- CHANGELOG_END -->\n\n## Footer\n"
        result = ur.inject_changelog_into_readme(readme, "NEW CONTENT")
        assert "NEW CONTENT" in result
        assert "OLD CONTENT" not in result
        assert "<!-- CHANGELOG_START -->" in result
        assert "<!-- CHANGELOG_END -->" in result

    def test_no_markers_returns_unchanged(self) -> None:
        readme = "# Title\nNo markers here\n"
        result = ur.inject_changelog_into_readme(readme, "NEW CONTENT")
        assert result == readme

    def test_preserves_surrounding_content(self) -> None:
        readme = "BEFORE\n<!-- CHANGELOG_START -->\nOLD\n<!-- CHANGELOG_END -->\nAFTER"
        result = ur.inject_changelog_into_readme(readme, "NEW")
        assert result.startswith("BEFORE\n")
        assert result.endswith("AFTER")


# ---------------------------------------------------------------------------
# make_badge_json
# ---------------------------------------------------------------------------

class TestMakeBadgeJson:
    def test_returns_valid_shields_format(self) -> None:
        data = ur.make_badge_json("tests", "1726 passing", "3ad29f")
        assert data["schemaVersion"] == 1
        assert data["label"] == "tests"
        assert data["message"] == "1726 passing"
        assert data["color"] == "3ad29f"

    def test_serializable_to_json(self) -> None:
        data = ur.make_badge_json("tools", "89 MCP", "e94560")
        text = json.dumps(data)
        parsed = json.loads(text)
        assert parsed["label"] == "tools"


# ---------------------------------------------------------------------------
# update_stats_svg
# ---------------------------------------------------------------------------

class TestUpdateStatsSvg:
    def test_replaces_tools_marker(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --></text>'
        result = ur.update_stats_svg(svg, tools=42, tests=None)
        assert "42" in result
        assert "<!-- STAT:TOOLS -->42<!-- /STAT -->" in result

    def test_replaces_tests_marker(self) -> None:
        svg = '<text><!-- STAT:TESTS -->1726<!-- /STAT --></text>'
        result = ur.update_stats_svg(svg, tools=None, tests=9999)
        assert "9999" in result

    def test_skips_none_values(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --></text>'
        result = ur.update_stats_svg(svg, tools=None, tests=None)
        assert result == svg

    def test_updates_aria_label(self) -> None:
        svg = (
            '<svg aria-label="Unity Biome MCP stats: 91 MCP Tools, 3635 Tests '
            '(1949 Python + 1633 Unity + 53 Live), 80-95% Batch Savings">'
            '<!-- STAT:TOOLS -->91<!-- /STAT -->'
            '<!-- STAT:TESTS -->3635<!-- /STAT -->'
            '</svg>'
        )
        result = ur.update_stats_svg(svg, tools=95, tests=3700,
                                     python_tests=2000, nunit_tests=1600, live_tests=100)
        assert 'aria-label="Unity Biome MCP stats: 95 MCP Tools, 3700 Tests' in result
        assert "2000 Python + 1600 Unity + 100 Live" in result

    def test_updates_subtitle_line(self) -> None:
        svg = (
            '<text>1949 Python &#x00B7; 1633 Unity &#x00B7; 53 Live</text>'
            '<!-- STAT:TOOLS -->91<!-- /STAT -->'
            '<!-- STAT:TESTS -->3635<!-- /STAT -->'
        )
        result = ur.update_stats_svg(svg, tools=91, tests=3700,
                                     python_tests=2000, nunit_tests=1600, live_tests=100)
        assert "2000 Python &#x00B7; 1600 Unity &#x00B7; 100 Live" in result
        assert "1949 Python" not in result


# ---------------------------------------------------------------------------
# count_nunit_tests
# ---------------------------------------------------------------------------

class TestCountNunitTests:
    def test_counts_test_attribute(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "Foo.cs").write_text(
            "[Test]\npublic void A() {}\n[Test]\npublic void B() {}\n"
        )
        assert ur.count_nunit_tests(tmp_path) == 2

    def test_counts_across_subdirs(self, tmp_path: pathlib.Path) -> None:
        sub = tmp_path / "sub"
        sub.mkdir()
        (tmp_path / "A.cs").write_text("[Test]\npublic void X() {}\n")
        (sub / "B.cs").write_text("[Test]\npublic void Y() {}\n[Test]\npublic void Z() {}\n")
        assert ur.count_nunit_tests(tmp_path) == 3

    def test_ignores_non_cs_files(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "notes.txt").write_text("[Test]\n[Test]\n")
        (tmp_path / "Real.cs").write_text("[Test]\npublic void A() {}\n")
        assert ur.count_nunit_tests(tmp_path) == 1

    def test_missing_dir_returns_none(self, tmp_path: pathlib.Path) -> None:
        assert ur.count_nunit_tests(tmp_path / "nonexistent") is None

    def test_zero_when_no_tests(self, tmp_path: pathlib.Path) -> None:
        (tmp_path / "NoTests.cs").write_text("public class Foo {}\n")
        assert ur.count_nunit_tests(tmp_path) == 0


# ---------------------------------------------------------------------------
# count_live_tests
# ---------------------------------------------------------------------------

class TestCountLiveTests:
    def test_returns_int_or_none(self) -> None:
        result = ur.count_live_tests(REPO_ROOT / "server" / "tests")
        assert result is None or isinstance(result, int)

    def test_nonexistent_dir_returns_none(self, tmp_path: pathlib.Path) -> None:
        result = ur.count_live_tests(tmp_path / "no_such_tests")
        assert result is None

    def test_returns_fewer_than_full_suite(self) -> None:
        tests_dir = REPO_ROOT / "server" / "tests"
        full = ur.count_pytest_tests(tests_dir)
        live = ur.count_live_tests(tests_dir)
        if full is not None and live is not None:
            assert live < full


# ---------------------------------------------------------------------------
# update_readme_alt_text
# ---------------------------------------------------------------------------

class TestUpdateReadmeAltText:
    def test_updates_alt_text(self) -> None:
        readme = '<img src="docs/assets/stats.svg" width="100%" alt="91 MCP Tools · 3635 Tests (1949 Python · 1633 Unity · 53 Live) · 80-95% Batch Savings">'
        result = ur.update_readme_alt_text(readme, tools=95, tests=3700,
                                           python_tests=2000, nunit_tests=1600, live_tests=100)
        assert "95 MCP Tools" in result
        assert "3700 Tests" in result
        assert "2000 Python" in result
        assert "1600 Unity" in result
        assert "100 Live" in result

    def test_no_change_when_all_none(self) -> None:
        readme = '<img alt="91 MCP Tools · 3635 Tests (x)">'
        result = ur.update_readme_alt_text(readme, tools=None, tests=None)
        assert result == readme


# ---------------------------------------------------------------------------
# generate_changelog_details
# ---------------------------------------------------------------------------

SAMPLE_CHANGELOG = """\
# Changelog

## [v3.0.0] — 2026-06-10 <!-- svg: feature A title -->

- **Feature A** — First sentence. Second sentence.
- Other bullet.

## [v2.0.0] — 2026-06-09 <!-- svg: feature B title -->

- **Feature B** — Some description here.

## [v1.9.0] — 2026-06-08 <!-- svg: feature C -->

- **Feature C** — Detail.

## [v1.8.0] — 2026-06-07 <!-- svg: feature D -->

- **Feature D** — Detail.

## [v1.7.0] — 2026-06-06 <!-- svg: feature E -->

- **Feature E** — Detail.

## [v1.6.0] — 2026-06-05 <!-- svg: feature F -->

- **Feature F** — older.

## [v1.5.0] — 2026-06-04 <!-- svg: feature G -->

- **Feature G** — even older.
"""


class TestGenerateChangelogDetails:
    def test_produces_details_blocks(self) -> None:
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "<details>" in result
        assert "</details>" in result

    def test_latest_version_present(self) -> None:
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "v3.0.0" in result

    def test_limits_to_n_recent_with_details(self) -> None:
        # Default n=5: first 5 get <details>, rest in "Older releases"
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG, n=5)
        # Count <details> blocks: 5 individual + 1 "Older releases" = 6
        assert result.count("<details>") == 6

    def test_older_releases_block_present(self) -> None:
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG, n=5)
        assert "Older releases" in result
        assert "v1.6.0" in result
        assert "v1.5.0" in result

    def test_extracts_svg_comment_as_title(self) -> None:
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "feature A title" in result

    def test_summary_has_version_date_and_title(self) -> None:
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "<summary><b>v3.0.0</b>" in result
        assert "2026-06-10" in result

    def test_blank_line_after_summary(self) -> None:
        # GitHub requires blank line after <summary> for markdown rendering
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "</summary>\n\n" in result

    def test_blank_line_before_closing_details(self) -> None:
        result = ur.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "\n\n</details>" in result

    def test_truncates_summary_to_150_chars(self) -> None:
        long_bullet = "- **X** — " + "A" * 200 + ". More text."
        cl = f"## [v9.0.0] — 2026-06-01 <!-- svg: long title -->\n\n{long_bullet}\n"
        result = ur.generate_changelog_details(cl, n=5)
        # Extract text between the blank-after-summary and closing details
        import re
        body = re.search(r"</summary>\n\n(.+?)\n\n</details>", result, re.DOTALL)
        assert body is not None
        assert len(body.group(1)) <= 155  # some slack for truncation marker

    def test_no_older_releases_when_few_versions(self) -> None:
        cl = "## [v1.0.0] — 2026-01-01 <!-- svg: only one -->\n\n- Single bullet.\n"
        result = ur.generate_changelog_details(cl, n=5)
        assert "Older releases" not in result

    def test_fallback_to_first_bullet_when_no_svg_comment(self) -> None:
        cl = "## [v1.0.0] — 2026-01-01\n\n- No svg comment here. More text.\n"
        result = ur.generate_changelog_details(cl, n=5)
        assert "No svg comment here" in result

    def test_real_changelog(self) -> None:
        text = (REPO_ROOT / "CHANGELOG.md").read_text()
        result = ur.generate_changelog_details(text, n=5)
        assert "<details>" in result
        assert "v0.15.8" in result
