"""Tests for readme_render.py — render logic, C1 regression, C3 regression."""
import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

import pytest

sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import readme_render as rr
import update_readme as ur

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
_META = REPO_ROOT / "docs" / "assets" / "_meta.json"
_ASSETS = REPO_ROOT / "docs" / "assets"


# ---------------------------------------------------------------------------
# substitute_svg_markers
# ---------------------------------------------------------------------------

class TestSubstituteSvgMarkers:
    def test_replaces_tools_marker(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --> Tools</text>'
        assert "<!-- STAT:TOOLS -->92<!-- /STAT -->" in rr.substitute_svg_markers(svg, {"tools": 92})

    def test_replaces_tests_marker(self) -> None:
        svg = '<text><!-- STAT:TESTS -->3432<!-- /STAT --></text>'
        assert "<!-- STAT:TESTS -->3500<!-- /STAT -->" in rr.substitute_svg_markers(svg, {"tests_total": 3500})

    def test_idempotent_tools(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->92<!-- /STAT --> Tools</text>'
        r1 = rr.substitute_svg_markers(svg, {"tools": 92})
        assert r1 == rr.substitute_svg_markers(r1, {"tools": 92})

    def test_idempotent_tests(self) -> None:
        svg = '<text><!-- STAT:TESTS -->3500<!-- /STAT --></text>'
        r1 = rr.substitute_svg_markers(svg, {"tests_total": 3500})
        assert r1 == rr.substitute_svg_markers(r1, {"tests_total": 3500})

    def test_aria_label_mcp_tools_updated(self) -> None:
        svg = '<svg aria-label="Unity Biome MCP stats: 91 MCP Tools">'
        assert "92 MCP tools" in rr.substitute_svg_markers(svg, {"tools": 92})

    def test_none_tools_skips(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --></text>'
        assert "89" in rr.substitute_svg_markers(svg, {"tools": None})

    def test_none_tests_skips(self) -> None:
        svg = '<text><!-- STAT:TESTS -->3432<!-- /STAT --></text>'
        assert "3432" in rr.substitute_svg_markers(svg, {"tests_total": None})


# ---------------------------------------------------------------------------
# C1 regression: render propagates tool count to all 3 README spots
# ---------------------------------------------------------------------------

class TestReadmeToolCountWired:
    def test_hero_alt_updated(self) -> None:
        readme = '<img alt="banner with 50 token-minimized MCP tools connecting">'
        result = rr._substitute_readme_tool_counts(readme, {"tools": 777})
        assert "777 token-minimized MCP tools" in result
        assert "50 token-minimized MCP tools" not in result

    def test_prose_full_access_updated(self) -> None:
        readme = "Full access to 50 MCP tools with 80–95% token compression."
        result = rr._substitute_readme_tool_counts(readme, {"tools": 777})
        assert "Full access to 777 MCP tools" in result
        assert "Full access to 50 MCP tools" not in result

    def test_arch_alt_updated(self) -> None:
        readme = '<img alt="Architecture: Claude Code ... MCP Server with 50 tools, which connects">'
        result = rr._substitute_readme_tool_counts(readme, {"tools": 777})
        assert "MCP Server with 777 tools" in result
        assert "MCP Server with 50 tools" not in result

    def test_unrelated_numbers_untouched(self) -> None:
        readme = "port 9500 free. savings 80–95%. version 0.21.5."
        result = rr._substitute_readme_tool_counts(readme, {"tools": 777})
        assert "9500" in result
        assert "80–95%" in result
        assert "0.21.5" in result

    def test_all_three_spots_set_to_777(self) -> None:
        readme = (
            '<img alt="banner with 92 token-minimized MCP tools">\n'
            "Full access to 92 MCP tools with 80–95%.\n"
            '<img alt="MCP Server with 92 tools, which">'
        )
        result = rr._substitute_readme_tool_counts(readme, {"tools": 777})
        assert result.count("777") == 3


# ---------------------------------------------------------------------------
# C3 regression: [Unreleased] not rendered as first entry
# ---------------------------------------------------------------------------

class TestChangelogUnreleased:
    def test_unreleased_skipped(self) -> None:
        cl = (
            "## [Unreleased]\n\n- some WIP\n\n"
            "## [v1.2.3] — 2026-01-01\n\n- Real feature.\n"
        )
        result = rr.generate_changelog_details(cl, n=5)
        first_summary = re.search(r"<summary><b>([^<]+)</b>", result)
        assert first_summary is not None
        assert "?" not in first_summary.group(1)
        assert "v1.2.3" in first_summary.group(1)

    def test_real_changelog_first_entry_is_dated(self) -> None:
        text = (REPO_ROOT / "CHANGELOG.md").read_text()
        result = rr.generate_changelog_details(text, n=5)
        first_summary = re.search(r"<summary><b>([^<]+)</b>", result)
        assert first_summary is not None
        assert "?" not in first_summary.group(1)
        assert re.search(r"v\d+\.\d+", first_summary.group(1))


# ---------------------------------------------------------------------------
# C7: dynamic top version (no hardcoded "v0.20.7")
# ---------------------------------------------------------------------------

def _top_dated_version() -> str:
    text = (REPO_ROOT / "CHANGELOG.md").read_text()
    ver, _ = rr.parse_latest_changelog(text)
    return ver


class TestChangelogBlock:
    def test_readme_changelog_block_has_current_version(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text()
        ver = _top_dated_version()
        assert ver in readme, f"README CHANGELOG block is stale (missing {ver})"

    def test_readme_first_entry_not_v0_17_25(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text()
        m = re.search(r"<!-- CHANGELOG_START -->(.*?)<!-- CHANGELOG_END -->", readme, re.DOTALL)
        if m:
            first = re.search(r"<summary><b>([^<]+)</b>", m.group(1))
            if first:
                assert "v0.17.25" not in first.group(1)


# ---------------------------------------------------------------------------
# render drift checks on real files
# ---------------------------------------------------------------------------

class TestRenderDrift:
    def test_all_svgs_have_tools_marker(self) -> None:
        for name in ("stats.svg", "hero.svg", "architecture.svg"):
            svg = (_ASSETS / name).read_text()
            assert "<!-- STAT:TOOLS -->" in svg, f"{name} missing STAT:TOOLS marker"

    def test_all_svgs_well_formed_xml(self) -> None:
        for svg_path in _ASSETS.glob("*.svg"):
            try:
                ET.fromstring(svg_path.read_text())
            except ET.ParseError as e:
                pytest.fail(f"{svg_path.name} is not well-formed XML: {e}")

    def test_all_surfaces_agree_on_tool_count(self) -> None:
        if not _META.exists():
            pytest.skip("_meta.json not yet written")
        meta = json.loads(_META.read_text())
        n = str(meta["tools"])
        hero = (_ASSETS / "hero.svg").read_text()
        arch = (_ASSETS / "architecture.svg").read_text()
        stats = (_ASSETS / "stats.svg").read_text()
        badges_tools = json.loads((REPO_ROOT / ".github" / "badges" / "tools.json").read_text())
        readme = (REPO_ROOT / "README.md").read_text()
        assert f"<!-- STAT:TOOLS -->{n}<!-- /STAT -->" in hero, "hero.svg mismatch"
        assert f"<!-- STAT:TOOLS -->{n}<!-- /STAT -->" in arch, "architecture.svg mismatch"
        assert f"<!-- STAT:TOOLS -->{n}<!-- /STAT -->" in stats, "stats.svg mismatch"
        assert n in badges_tools["message"], "badges/tools.json mismatch"
        assert n in readme, "tool count not found anywhere in README"


# ---------------------------------------------------------------------------
# _apply_or_check
# ---------------------------------------------------------------------------

class TestApplyOrCheck:
    def test_check_exits_1_on_stale(self, tmp_path: pathlib.Path) -> None:
        p = tmp_path / "file.txt"
        p.write_text("old")
        with pytest.raises(SystemExit) as exc:
            rr._apply_or_check([(p, "new")], check=True)
        assert exc.value.code == 1

    def test_check_exits_0_when_current(self, tmp_path: pathlib.Path) -> None:
        p = tmp_path / "file.txt"
        p.write_text("same")
        rr._apply_or_check([(p, "same")], check=True)

    def test_normal_writes_file(self, tmp_path: pathlib.Path) -> None:
        p = tmp_path / "file.txt"
        p.write_text("old")
        rr._apply_or_check([(p, "new")], check=False)
        assert p.read_text() == "new"

    def test_check_stale_lists_all_paths(self, tmp_path: pathlib.Path, capsys) -> None:
        p1, p2 = tmp_path / "a.txt", tmp_path / "b.txt"
        p1.write_text("old1")
        p2.write_text("old2")
        with pytest.raises(SystemExit):
            rr._apply_or_check([(p1, "new1"), (p2, "new2")], check=True)
        out = capsys.readouterr().out
        assert "a.txt" in out or "b.txt" in out


# ---------------------------------------------------------------------------
# inject_changelog / make_badge_json / extract_changelog_blocks
# ---------------------------------------------------------------------------

class TestInjectChangelog:
    def test_replaces_between_markers(self) -> None:
        readme = "# T\n\n<!-- CHANGELOG_START -->\nOLD\n<!-- CHANGELOG_END -->\n\n## F\n"
        result = rr.inject_changelog_into_readme(readme, "NEW")
        assert "NEW" in result and "OLD" not in result

    def test_no_markers_returns_unchanged(self) -> None:
        readme = "# Title\nNo markers here\n"
        assert rr.inject_changelog_into_readme(readme, "NEW") == readme


class TestMakeBadgeJson:
    def test_returns_valid_shields_format(self) -> None:
        d = rr.make_badge_json("tests", "1726 passing", "3ad29f")
        assert d == {"schemaVersion": 1, "label": "tests", "message": "1726 passing", "color": "3ad29f"}


class TestExtractChangelogBlocks:
    def test_extracts_two_blocks(self) -> None:
        cl = "# C\n\n## [v2.0.0] — 2026-02-01\n\n- A\n\n## [v1.9.0] — 2026-01-01\n\n- X\n"
        blocks = rr.extract_changelog_blocks(cl, n=2)
        assert len(blocks) == 2
        assert "v2.0.0" in blocks[0]

    def test_returns_one_if_only_one_version(self) -> None:
        cl = "## [v1.0.0] — 2026-01-01\n\n- Only one\n"
        assert len(rr.extract_changelog_blocks(cl, n=2)) == 1


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
        r = rr.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "<details>" in r and "</details>" in r

    def test_latest_version_present(self) -> None:
        assert "v3.0.0" in rr.generate_changelog_details(SAMPLE_CHANGELOG)

    def test_limits_to_n_recent(self) -> None:
        r = rr.generate_changelog_details(SAMPLE_CHANGELOG, n=5)
        assert r.count("<details>") == 6  # 5 individual + 1 "Older releases"

    def test_older_releases_block_present(self) -> None:
        r = rr.generate_changelog_details(SAMPLE_CHANGELOG, n=5)
        assert "Older releases" in r and "v1.6.0" in r

    def test_extracts_svg_comment_as_title(self) -> None:
        assert "feature A title" in rr.generate_changelog_details(SAMPLE_CHANGELOG)

    def test_summary_has_version_date_title(self) -> None:
        r = rr.generate_changelog_details(SAMPLE_CHANGELOG)
        assert "<summary><b>v3.0.0</b>" in r and "2026-06-10" in r

    def test_blank_line_after_summary(self) -> None:
        assert "</summary>\n\n" in rr.generate_changelog_details(SAMPLE_CHANGELOG)

    def test_blank_line_before_closing_details(self) -> None:
        assert "\n\n</details>" in rr.generate_changelog_details(SAMPLE_CHANGELOG)

    def test_no_older_when_few_versions(self) -> None:
        cl = "## [v1.0.0] — 2026-01-01 <!-- svg: only one -->\n\n- Single bullet.\n"
        assert "Older releases" not in rr.generate_changelog_details(cl, n=5)

    def test_real_changelog(self) -> None:
        text = (REPO_ROOT / "CHANGELOG.md").read_text()
        r = rr.generate_changelog_details(text, n=5)
        assert "<details>" in r


# ---------------------------------------------------------------------------
# Backward compat: update_stats_svg still works via update_readme re-export
# ---------------------------------------------------------------------------

class TestUpdateStatsSvgBackwardCompat:
    def test_replaces_tools_marker(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --></text>'
        assert "42" in ur.update_stats_svg(svg, tools=42, tests=None)

    def test_replaces_tests_marker(self) -> None:
        svg = '<text><!-- STAT:TESTS -->100<!-- /STAT --></text>'
        assert "999" in ur.update_stats_svg(svg, tools=None, tests=999)

    def test_skips_none(self) -> None:
        svg = '<text><!-- STAT:TOOLS -->89<!-- /STAT --></text>'
        assert ur.update_stats_svg(svg, tools=None, tests=None) == svg


# ---------------------------------------------------------------------------
# divider-dataflow.svg and a11y (moved from test_single_source)
# ---------------------------------------------------------------------------

class TestDividerDataflow:
    def test_no_bare_href_mpath(self) -> None:
        svg = (_ASSETS / "divider-dataflow.svg").read_text()
        assert len(re.findall(r"<mpath\s+href=", svg)) == 0

    def test_has_xlink_namespace(self) -> None:
        assert 'xmlns:xlink="http://www.w3.org/1999/xlink"' in (_ASSETS / "divider-dataflow.svg").read_text()

    def test_well_formed_xml_after_fix(self) -> None:
        ET.fromstring((_ASSETS / "divider-dataflow.svg").read_text())


class TestStatsColor:
    def test_no_color_typo_888919(self) -> None:
        assert "#888919" not in (_ASSETS / "stats.svg").read_text()

    def test_correct_color_888899(self) -> None:
        assert "#888899" in (_ASSETS / "stats.svg").read_text()


class TestA11y:
    def test_hero_has_title_and_desc(self) -> None:
        hero = (_ASSETS / "hero.svg").read_text()
        assert "<title" in hero and "<desc" in hero

    def test_architecture_svg_has_title_and_desc(self) -> None:
        arch = (_ASSETS / "architecture.svg").read_text()
        assert "<title" in arch and "<desc" in arch

    def test_stats_svg_has_title_and_desc(self) -> None:
        stats = (_ASSETS / "stats.svg").read_text()
        assert "<title" in stats and "<desc" in stats

    def test_svgs_have_role_img(self) -> None:
        for name in ("hero.svg", "architecture.svg", "stats.svg"):
            assert 'role="img"' in (_ASSETS / name).read_text(), f"{name} missing role=img"


class TestContributing:
    def test_good_first_issue_link(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text()
        assert "good%20first%20issue" in readme or "good first issue" in readme.lower()

    def test_contributing_has_open_pr_guidance(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text()
        assert "master" in readme.lower() or "pull request" in readme.lower()


# ---------------------------------------------------------------------------
# Step D: architecture.svg STAT:TOOLS gate — render-controlled, not hardcoded
# ---------------------------------------------------------------------------

class TestArchitectureSvgGate:
    """Prove architecture.svg tool count is render-controlled and gate bites."""

    def test_architecture_svg_has_stat_tools_marker(self) -> None:
        """STAT:TOOLS marker must exist so render() can rewrite it."""
        arch = (_ASSETS / "architecture.svg").read_text()
        assert "<!-- STAT:TOOLS -->" in arch, "architecture.svg missing STAT:TOOLS marker"
        assert "<!-- /STAT -->" in arch

    def test_stale_architecture_svg_fails_check(self, tmp_path: pathlib.Path) -> None:
        """A stale tool count in architecture.svg causes --check to exit non-zero."""
        arch = (_ASSETS / "architecture.svg").read_text()
        meta = json.loads(_META.read_text())
        stale = re.sub(
            r"<!-- STAT:TOOLS -->[^<]*<!-- /STAT -->",
            "<!-- STAT:TOOLS -->42<!-- /STAT -->",
            arch,
        )
        rendered = rr.substitute_svg_markers(stale, meta)
        assert rendered != stale, "substitute_svg_markers must change stale value"
        p = tmp_path / "architecture.svg"
        p.write_text(stale, encoding="utf-8")
        with pytest.raises(SystemExit) as exc:
            rr._apply_or_check([(p, rendered)], check=True)
        assert exc.value.code == 1

    def test_render_corrects_stale_architecture_svg(self) -> None:
        """After --render, architecture.svg STAT:TOOLS equals _meta.json tools."""
        meta = json.loads(_META.read_text())
        expected = f"<!-- STAT:TOOLS -->{meta['tools']}<!-- /STAT -->"
        arch = (_ASSETS / "architecture.svg").read_text()
        assert expected in arch, f"architecture.svg stale: expected {expected}"


# ---------------------------------------------------------------------------
# Gate-hardening: STAT:BREAKDOWN marker + aria-label substitution
# ---------------------------------------------------------------------------

class TestBreakdownGate:
    """Prove the freshness gate is no longer hollow for breakdown stats."""

    def test_stats_svg_has_breakdown_marker(self) -> None:
        """STAT:BREAKDOWN marker must be present so render() can rewrite it."""
        assert "<!-- STAT:BREAKDOWN -->" in (_ASSETS / "stats.svg").read_text()

    def test_substitute_svg_markers_rewrites_breakdown_marker(self) -> None:
        svg = (
            '<text fill="#888899"><!-- STAT:BREAKDOWN -->'
            '1904 Python &#x00B7; 1475 Unity &#x00B7; 53 Live'
            '<!-- /STAT --></text>'
        )
        result = rr.substitute_svg_markers(svg, {"tests_python": 1933, "tests_unity": 1613, "tests_live": 59})
        assert "1933 Python &#x00B7; 1613 Unity &#x00B7; 59 Live" in result
        assert "1904" not in result

    def test_substitute_svg_markers_rewrites_aria_label_breakdown(self) -> None:
        svg = 'aria-label="Unity Biome MCP stats: 92 MCP tools, 3605 Tests (1904 Python + 1475 Unity + 53 Live), 80-95% Batch Savings"'
        result = rr.substitute_svg_markers(svg, {"tests_python": 1933, "tests_unity": 1613, "tests_live": 59})
        assert "(1933 Python + 1613 Unity + 59 Live)" in result
        assert "1904" not in result

    def test_render_writes_correct_breakdown_into_stats_svg(self, tmp_path: pathlib.Path) -> None:
        """render() must produce canonical breakdown values in stats.svg."""
        meta = json.loads(_META.read_text())
        # Seed a stale stats.svg copy in tmp repo structure
        assets_tmp = tmp_path / "docs" / "assets"
        assets_tmp.mkdir(parents=True)
        stale_svg = (_ASSETS / "stats.svg").read_text().replace(
            "<!-- STAT:BREAKDOWN -->", "<!-- STAT:BREAKDOWN -->"
        )
        # Replace breakdown content with stale values
        stale_svg = re.sub(
            r"<!-- STAT:BREAKDOWN -->.*?<!-- /STAT -->",
            "<!-- STAT:BREAKDOWN -->1904 Python &#x00B7; 1475 Unity &#x00B7; 53 Live<!-- /STAT -->",
            stale_svg,
        )
        (assets_tmp / "stats.svg").write_text(stale_svg, encoding="utf-8")
        # Copy _meta.json
        (assets_tmp / "_meta.json").write_text(_META.read_text(), encoding="utf-8")

        updated = rr.substitute_svg_markers(stale_svg, meta)
        assert "1933 Python &#x00B7; 1613 Unity &#x00B7; 59 Live" in updated
        assert "1904" not in updated

    def test_check_mode_catches_stale_breakdown(self, tmp_path: pathlib.Path) -> None:
        """--check must exit non-zero when breakdown is stale (gate is not hollow)."""
        meta = json.loads(_META.read_text())
        stale_svg = re.sub(
            r"<!-- STAT:BREAKDOWN -->.*?<!-- /STAT -->",
            "<!-- STAT:BREAKDOWN -->1904 Python &#x00B7; 1475 Unity &#x00B7; 53 Live<!-- /STAT -->",
            (_ASSETS / "stats.svg").read_text(),
        )
        p = tmp_path / "stats.svg"
        p.write_text(stale_svg, encoding="utf-8")
        rendered = rr.substitute_svg_markers(stale_svg, meta)
        # rendered != stale → check must flag stale
        with pytest.raises(SystemExit) as exc:
            rr._apply_or_check([(p, rendered)], check=True)
        assert exc.value.code == 1

    def test_real_stats_svg_breakdown_matches_meta(self) -> None:
        """The real stats.svg breakdown must equal _meta.json values."""
        meta = json.loads(_META.read_text())
        p, u, lv = meta["tests_python"], meta["tests_unity"], meta["tests_live"]
        stats = (_ASSETS / "stats.svg").read_text()
        expected_card = f"{p} Python &#x00B7; {u} Unity &#x00B7; {lv} Live"
        expected_aria = f"({p} Python + {u} Unity + {lv} Live)"
        assert expected_card in stats, f"Card breakdown stale. Expected: {expected_card}"
        assert expected_aria in stats, f"aria-label breakdown stale. Expected: {expected_aria}"
