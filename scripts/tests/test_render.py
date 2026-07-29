"""Tests for the pure README renderer and committed presentation surfaces."""

import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ET

import pytest


sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import readme_render as rr


REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
ASSETS = REPO_ROOT / "docs" / "assets"
META_PATH = ASSETS / "_meta.json"

SAMPLE_META = {
    "tools": 142,
    "tests_total": 10872,
    "tests_python": 4252,
    "tests_stress": 511,
    "tests_unity": 5820,
    "tests_unity_source": "static_grep",
    "tests_live": 289,
}

SAMPLE_SVG = """
<desc><!-- STAT:STATS_DESC -->old<!-- /STAT --></desc>
<text><!-- STAT:TOOLS -->1<!-- /STAT --></text>
<text><!-- STAT:TESTS -->2<!-- /STAT --></text>
<text><!-- STAT:BREAKDOWN -->old<!-- /STAT --></text>
<text><!-- STAT:UNITY_SOURCE -->old<!-- /STAT --></text>
"""


class TestStatsSummary:
    def test_describes_inventory_without_claiming_execution(self) -> None:
        summary = rr.stats_summary(SAMPLE_META)
        assert "142 registered MCP tools" in summary
        assert "Test inventory: 10872 entries" in summary
        assert "511 Python stress" in summary
        assert "static source scan" in summary
        assert "passing" not in summary.lower()

    def test_unity_source_labels_are_explicit(self) -> None:
        assert "unavailable" in rr.stats_summary(
            {**SAMPLE_META, "tests_unity_source": "unavailable"}
        )

    def test_missing_metadata_fails_closed(self) -> None:
        with pytest.raises(ValueError, match="tests_stress"):
            rr.stats_summary({k: v for k, v in SAMPLE_META.items()
                              if k != "tests_stress"})


class TestSubstituteSvgMarkers:
    def test_replaces_every_stats_marker(self) -> None:
        result = rr.substitute_svg_markers(SAMPLE_SVG, SAMPLE_META)
        assert "<!-- STAT:TOOLS -->142<!-- /STAT -->" in result
        assert "<!-- STAT:TESTS -->10872<!-- /STAT -->" in result
        assert "4252 regular · 511 stress · 289 live · 5820 Unity" in result
        assert "Unity count source: static source scan" in result
        assert "<!-- STAT:UNITY_SOURCE -->static source scan<!-- /STAT -->" in result

    def test_is_idempotent(self) -> None:
        once = rr.substitute_svg_markers(SAMPLE_SVG, SAMPLE_META)
        assert rr.substitute_svg_markers(once, SAMPLE_META) == once

    def test_only_explicit_markers_change(self) -> None:
        svg = "<text>142 prose value</text>" + SAMPLE_SVG
        result = rr.substitute_svg_markers(svg, {**SAMPLE_META, "tools": 99})
        assert "<text>142 prose value</text>" in result
        assert "<!-- STAT:TOOLS -->99<!-- /STAT -->" in result

    def test_missing_marker_fails_closed(self) -> None:
        svg = SAMPLE_SVG.replace(
            "<text><!-- STAT:BREAKDOWN -->old<!-- /STAT --></text>",
            "",
        )
        with pytest.raises(ValueError, match="STAT:BREAKDOWN"):
            rr.substitute_svg_markers(svg, SAMPLE_META)

    def test_duplicate_or_incomplete_markers_fail_closed(self) -> None:
        with pytest.raises(ValueError, match="exactly one"):
            rr.substitute_svg_markers(
                "<!-- STAT:TOOLS -->1<!-- /STAT -->"
                "<!-- STAT:TOOLS -->2<!-- /STAT -->",
                SAMPLE_META,
            )
        with pytest.raises(ValueError, match="exactly one"):
            rr.substitute_svg_markers("<!-- STAT:TOOLS -->1", SAMPLE_META)


class TestReadmeStats:
    def test_image_alt_uses_canonical_summary(self) -> None:
        readme = (
            "<!-- README_STATS_START -->\n"
            '<img src="docs/assets/stats.svg" alt="stale">\n'
            "<!-- README_STATS_END -->"
        )
        result = rr.update_readme_stats(readme, SAMPLE_META)
        assert rr.stats_summary(SAMPLE_META) in result
        assert 'src="docs/assets/stats.svg"' in result
        assert "stale" not in result

    def test_missing_markers_fail_closed(self) -> None:
        readme = "# README\n"
        with pytest.raises(ValueError, match="README stats"):
            rr.update_readme_stats(readme, SAMPLE_META)


class TestChangelogSummary:
    SAMPLE = """
# Changelog

## [Unreleased]

- Work in progress.

## [v3.0.0] — 2026-06-10 <!-- svg: title -->

- First release detail.

## [v2.0.0] — 2026-06-09

- Older detail.
"""

    def test_parses_latest_dated_release(self) -> None:
        assert rr.parse_latest_changelog(self.SAMPLE) == ("v3.0.0", "2026-06-10")

    def test_renders_one_release_and_link(self) -> None:
        result = rr.generate_changelog_summary(self.SAMPLE)
        assert "v3.0.0" in result
        assert "2026-06-10" in result
        assert "CHANGELOG.md" in result
        assert "v2.0.0" not in result
        assert "<details>" not in result

    def test_missing_release_falls_back_to_link(self) -> None:
        assert rr.generate_changelog_summary("# Changelog") == (
            "[Read the full changelog.](CHANGELOG.md)"
        )

    def test_injection_preserves_markers_and_surrounding_content(self) -> None:
        readme = (
            "before\n<!-- CHANGELOG_START -->\nold\n"
            "<!-- CHANGELOG_END -->\nafter"
        )
        result = rr.inject_changelog_into_readme(readme, "new")
        assert result.startswith("before\n")
        assert result.endswith("\nafter")
        assert "new" in result and "old" not in result
        assert "<!-- CHANGELOG_START -->" in result
        assert "<!-- CHANGELOG_END -->" in result


class TestBadgeJson:
    def test_returns_shields_schema(self) -> None:
        assert rr.make_badge_json("tests", "100 discovered", "46e6a6") == {
            "schemaVersion": 1,
            "label": "tests",
            "message": "100 discovered",
            "color": "46e6a6",
        }


class TestApplyOrCheck:
    def test_check_fails_on_stale_file(self, tmp_path: pathlib.Path) -> None:
        path = tmp_path / "file.txt"
        path.write_text("old", encoding="utf-8")
        with pytest.raises(SystemExit) as exc:
            rr._apply_or_check([(path, "new")], check=True)
        assert exc.value.code == 1
        assert path.read_text(encoding="utf-8") == "old"

    def test_write_updates_file(self, tmp_path: pathlib.Path) -> None:
        path = tmp_path / "file.txt"
        path.write_text("old", encoding="utf-8")
        rr._apply_or_check([(path, "new")], check=False)
        assert path.read_text(encoding="utf-8") == "new"

    def test_check_accepts_current_file(self, tmp_path: pathlib.Path) -> None:
        path = tmp_path / "file.txt"
        path.write_text("same", encoding="utf-8")
        rr._apply_or_check([(path, "same")], check=True)


class TestCommittedAssets:
    def test_all_svgs_are_well_formed(self) -> None:
        for path in ASSETS.glob("*.svg"):
            try:
                ET.fromstring(path.read_text(encoding="utf-8"))
            except ET.ParseError as error:
                pytest.fail(f"{path.name} is not valid XML: {error}")

    @pytest.mark.parametrize(
        "name",
        [
            "hero.svg",
            "architecture.svg",
            "stats.svg",
            "divider-biome.svg",
            "comparison-hero.svg",
        ],
    )
    def test_primary_assets_have_accessible_names(self, name: str) -> None:
        svg = (ASSETS / name).read_text(encoding="utf-8")
        assert 'role="img"' in svg
        assert "<title" in svg
        assert "<desc" in svg
        assert "aria-labelledby" in svg

    @pytest.mark.parametrize(
        "name",
        [
            "hero.svg",
            "architecture.svg",
            "stats.svg",
            "divider-biome.svg",
            "comparison-hero.svg",
        ],
    )
    def test_readme_embedded_assets_use_reduced_motion(self, name: str) -> None:
        svg = (ASSETS / name).read_text(encoding="utf-8")
        assert "@keyframes" in svg
        assert "prefers-reduced-motion: reduce" in svg
        assert "animation: none" in svg
        if "<animate" in svg:
            assert ".motion-particle { display: none; }" in svg
            root = ET.fromstring(svg)
            parents = {child: parent for parent in root.iter() for child in parent}
            animated = [
                node
                for node in root.iter()
                if node.tag.rsplit("}", 1)[-1].startswith("animate")
            ]
            assert animated
            assert all(
                "motion-particle" in parents[node].attrib.get("class", "").split()
                for node in animated
            )

    @pytest.mark.parametrize(
        ("name", "max_height_ratio"),
        [
            ("hero.svg", 0.34),
            ("architecture.svg", 0.34),
            ("stats.svg", 0.22),
            ("divider-biome.svg", 0.05),
            ("comparison-hero.svg", 0.37),
        ],
    )
    def test_primary_assets_keep_compact_aspect_ratios(
        self, name: str, max_height_ratio: float
    ) -> None:
        root = ET.fromstring((ASSETS / name).read_text(encoding="utf-8"))
        _, _, width, height = map(float, root.attrib["viewBox"].split())
        assert height / width <= max_height_ratio

    @pytest.mark.parametrize(
        "name",
        ["hero.svg", "architecture.svg", "stats.svg", "comparison-hero.svg"],
    )
    def test_mobile_critical_svg_text_remains_readable(self, name: str) -> None:
        root = ET.fromstring((ASSETS / name).read_text(encoding="utf-8"))
        _, _, width, _ = map(float, root.attrib["viewBox"].split())
        critical = [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}text")
            if node.attrib.get("data-mobile-critical") == "true"
        ]
        assert critical, f"{name} has no mobile-critical text contract"
        for node in critical:
            rendered_size = float(node.attrib["font-size"]) * 340 / width
            assert rendered_size >= 9.5, (
                f"{name} renders {''.join(node.itertext()).strip()!r} at "
                f"{rendered_size:.1f}px on a 340px GitHub content width"
            )

    def test_stats_use_inventory_language(self) -> None:
        stats = (ASSETS / "stats.svg").read_text(encoding="utf-8")
        assert "TEST INVENTORY" in stats
        assert "TESTS DISCOVERED" not in stats
        assert "TESTS PASSING" not in stats
        assert "#888919" not in stats

class TestCommittedReadme:
    def test_first_success_precedes_feature_detail(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        assert readme.index("## Quick Start") < readme.index("## What You Can Do")
        assert "get_hierarchy(depth=2)" in readme

    def test_setup_and_diagnostics_are_not_conflated(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        assert "does not perform an end-to-end connection test" in readme
        assert "MCP > Status > Diagnose" in readme

    def test_remote_typing_is_removed_and_comparison_is_evidence_based(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        comparison = (REPO_ROOT / "docs" / "comparison.md").read_text(
            encoding="utf-8"
        )
        hero = (ASSETS / "comparison-hero.svg").read_text(encoding="utf-8")
        assert "readme-typing-svg" not in readme
        assert "## Unity MCP Product Comparison" in readme
        assert "(docs/comparison.md)" in readme
        assert "This July 29, 2026 snapshot" in comparison
        assert "### Unity Biome MCP" in comparison
        assert "Unity Biome MCP v1.2.0" not in comparison
        assert "Unity MCP Server / Assistant 2.16.0-pre.1" in comparison
        for snapshot in ("fc70dda", "f6db1c2", "bbfb1c0"):
            assert snapshot in comparison
        assert "not documented" in comparison.lower()
        assert "quality or coverage score" in comparison
        assert "@keyframes" in hero
        assert "prefers-reduced-motion: reduce" in hero
        assert comparison.count("| Capability | Documented support |") == 3
        assert "| Capability | Unity Biome MCP |" not in comparison

    def test_approved_biome_dividers_define_major_section_rhythm(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        divider = '<img src="docs/assets/divider-biome.svg" width="100%" alt="">'
        assert readme.count(divider) == 4
        assert "docs/assets/divider.svg" not in readme

    def test_contributor_links_are_actionable(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        assert "CONTRIBUTING.md" in readme
        assert "good+first+issue" in readme
        assert "pull request" in readme.lower()

    def test_no_machine_specific_paths(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        assert "/Users/" not in readme
        assert "/home/" not in readme
        assert not re.search(r"[A-Za-z]:\\\\Users\\\\", readme)


class TestGeneratedSurfaces:
    def test_stats_and_readme_match_meta(self) -> None:
        meta = json.loads(META_PATH.read_text(encoding="utf-8"))
        stats = (ASSETS / "stats.svg").read_text(encoding="utf-8")
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        assert f"<!-- STAT:TOOLS -->{meta['tools']}<!-- /STAT -->" in stats
        assert f"<!-- STAT:TESTS -->{meta['tests_total']}<!-- /STAT -->" in stats
        assert rr.stats_summary(meta) in stats
        assert rr.stats_summary(meta) in readme

    def test_badge_reports_inventory_not_execution(self) -> None:
        badge = json.loads(
            (REPO_ROOT / ".github" / "badges" / "tests.json").read_text(
                encoding="utf-8"
            )
        )
        assert "inventoried" in badge["message"]
        assert "passing" not in badge["message"]

    def test_readme_contains_only_current_release_summary(self) -> None:
        changelog = (REPO_ROOT / "CHANGELOG.md").read_text(encoding="utf-8")
        expected = rr.generate_changelog_summary(changelog)
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        block = re.search(
            r"<!-- CHANGELOG_START -->(.*?)<!-- CHANGELOG_END -->",
            readme,
            re.DOTALL,
        )
        assert block is not None
        assert expected in block.group(1)
        assert "Older releases" not in block.group(1)
