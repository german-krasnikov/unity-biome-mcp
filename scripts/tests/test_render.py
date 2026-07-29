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
    "server_version": "3.2.1",
}

SAMPLE_SVG = """
<svg xmlns="http://www.w3.org/2000/svg">
<desc><!-- BIOME:STATS_DESC -->old<!-- /BIOME:STATS_DESC --></desc>
<text><!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS --></text>
<text><!-- BIOME:TESTS -->2<!-- /BIOME:TESTS --></text>
<text><!-- BIOME:VERSION -->old<!-- /BIOME:VERSION --></text>
</svg>
"""


class TestStatsSummary:
    def test_describes_inventory_without_claiming_execution(self) -> None:
        summary = rr.stats_summary(SAMPLE_META)
        assert "142 registered MCP tools" in summary
        assert "Test inventory: 10872 entries" in summary
        assert "511 Python stress" in summary
        assert "static source scan" in summary
        assert "Server package version: v3.2.1" in summary
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
        assert "<!-- BIOME:TOOLS -->142<!-- /BIOME:TOOLS -->" in result
        assert "<!-- BIOME:TESTS -->10872<!-- /BIOME:TESTS -->" in result
        assert "Unity count source: static source scan" in result
        assert "<!-- BIOME:VERSION -->v3.2.1<!-- /BIOME:VERSION -->" in result

    def test_is_idempotent(self) -> None:
        once = rr.substitute_svg_markers(SAMPLE_SVG, SAMPLE_META)
        assert rr.substitute_svg_markers(once, SAMPLE_META) == once

    def test_only_marker_payload_bytes_change(self) -> None:
        svg = SAMPLE_SVG.replace(
            '<svg xmlns="http://www.w3.org/2000/svg">',
            (
                '<svg xmlns="http://www.w3.org/2000/svg">'
                "<text>142 prose value</text>"
            ),
        )
        result = rr.substitute_svg_markers(svg, {**SAMPLE_META, "tools": 99})
        assert "<text>142 prose value</text>" in result
        assert "<!-- BIOME:TOOLS -->99<!-- /BIOME:TOOLS -->" in result

        expected = svg.replace(
            "<!-- BIOME:STATS_DESC -->old<!-- /BIOME:STATS_DESC -->",
            (
                "<!-- BIOME:STATS_DESC -->"
                f"{rr.stats_summary({**SAMPLE_META, 'tools': 99})}"
                "<!-- /BIOME:STATS_DESC -->"
            ),
        )
        expected = expected.replace(
            "<!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS -->",
            "<!-- BIOME:TOOLS -->99<!-- /BIOME:TOOLS -->",
        )
        expected = expected.replace(
            "<!-- BIOME:TESTS -->2<!-- /BIOME:TESTS -->",
            "<!-- BIOME:TESTS -->10872<!-- /BIOME:TESTS -->",
        )
        expected = expected.replace(
            "<!-- BIOME:VERSION -->old<!-- /BIOME:VERSION -->",
            "<!-- BIOME:VERSION -->v3.2.1<!-- /BIOME:VERSION -->",
        )
        assert result == expected

    def test_missing_marker_fails_closed(self) -> None:
        svg = SAMPLE_SVG.replace(
            "<text><!-- BIOME:TESTS -->2<!-- /BIOME:TESTS --></text>",
            "",
        )
        with pytest.raises(ValueError, match="Missing BIOME marker.*TESTS"):
            rr.substitute_svg_markers(svg, SAMPLE_META)

    def test_duplicate_marker_fails_closed(self) -> None:
        duplicate = SAMPLE_SVG.replace(
            "</svg>",
            "<!-- BIOME:TOOLS -->2<!-- /BIOME:TOOLS --></svg>",
        )
        with pytest.raises(ValueError, match="exactly one BIOME:TOOLS"):
            rr.substitute_svg_markers(duplicate, SAMPLE_META)

    def test_incomplete_or_mismatched_markers_fail_closed(self) -> None:
        incomplete = SAMPLE_SVG.replace("<!-- /BIOME:TOOLS -->", "")
        with pytest.raises(ValueError, match="exactly one BIOME:TOOLS"):
            rr.substitute_svg_markers(incomplete, SAMPLE_META)

        mismatched = SAMPLE_SVG.replace(
            "<!-- /BIOME:TOOLS -->",
            "<!-- /BIOME:TESTS -->",
        )
        with pytest.raises(ValueError, match="exactly one BIOME:TESTS"):
            rr.substitute_svg_markers(mismatched, SAMPLE_META)

    def test_unknown_or_nested_markers_fail_closed(self) -> None:
        unknown = SAMPLE_SVG.replace(
            "</svg>",
            "<!-- BIOME:OTHER -->x<!-- /BIOME:OTHER --></svg>",
        )
        with pytest.raises(ValueError, match="Unexpected BIOME marker.*OTHER"):
            rr.substitute_svg_markers(unknown, SAMPLE_META)

        nested = (
            "<!-- BIOME:OUTER -->"
            "<!-- BIOME:INNER -->x<!-- /BIOME:INNER -->"
            "<!-- /BIOME:OUTER -->"
        )
        with pytest.raises(ValueError, match="must not be nested"):
            rr._validate_svg_marker_contract(
                nested,
                frozenset({"OUTER", "INNER"}),
            )

    @pytest.mark.parametrize(
        "marker",
        [
            "<!-- BIOME:lower -->x<!-- /BIOME:lower -->",
            "<!-- BIOME:BAD-NAME -->x<!-- /BIOME:BAD-NAME -->",
            "<!-- biome:TOOLS -->x<!-- /biome:TOOLS -->",
        ],
    )
    def test_malformed_marker_like_comments_fail_closed(self, marker: str) -> None:
        malformed = SAMPLE_SVG.replace("</svg>", f"{marker}</svg>")
        with pytest.raises(ValueError, match="Malformed BIOME marker"):
            rr.substitute_svg_markers(malformed, SAMPLE_META)

    def test_marker_payload_cannot_contain_svg_markup(self) -> None:
        destructive = SAMPLE_SVG.replace(
            "<!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS -->",
            (
                "<!-- BIOME:TOOLS -->"
                '<rect x="1" y="1" width="1" height="1"/>'
                "<!-- /BIOME:TOOLS -->"
            ),
        )
        with pytest.raises(ValueError, match="payload must be scalar text"):
            rr.substitute_svg_markers(destructive, SAMPLE_META)

    def test_malformed_nested_marker_cannot_hide_inside_payload(self) -> None:
        malformed = SAMPLE_SVG.replace(
            "<!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS -->",
            (
                "<!-- BIOME:TOOLS -->"
                "<!-- BIOME:BAD-NAME -->x<!-- /BIOME:BAD-NAME -->"
                "<!-- /BIOME:TOOLS -->"
            ),
        )
        with pytest.raises(ValueError, match="Malformed BIOME marker"):
            rr.substitute_svg_markers(malformed, SAMPLE_META)

    def test_invalid_xml_fails_before_replacement(self) -> None:
        with pytest.raises(ValueError, match="not valid XML"):
            rr.substitute_svg_markers(SAMPLE_SVG.replace("</svg>", ""), SAMPLE_META)

    def test_only_stats_svg_is_generator_owned(self) -> None:
        assert rr.SVG_MARKER_ALLOWLIST == {
            "stats.svg": rr.STATS_SVG_MARKERS,
        }


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

    def test_exact_io_preserves_crlf_bytes(self, tmp_path: pathlib.Path) -> None:
        path = tmp_path / "file.txt"
        content = "first\r\nsecond\r\n"
        rr._write_text_exact(path, content)
        assert path.read_bytes() == content.encode("utf-8")
        assert rr._read_text_exact(path) == content


class TestRenderOwnership:
    def test_render_preserves_curated_svgs_and_stats_line_endings(
        self,
        tmp_path: pathlib.Path,
    ) -> None:
        assets = tmp_path / "docs" / "assets"
        assets.mkdir(parents=True)
        (tmp_path / ".github" / "badges").mkdir(parents=True)

        readme = (
            "before\r\n"
            "<!-- README_STATS_START -->\r\nold\r\n"
            "<!-- README_STATS_END -->\r\n"
            "<!-- CHANGELOG_START -->\r\nold\r\n"
            "<!-- CHANGELOG_END -->\r\n"
        )
        rr._write_text_exact(tmp_path / "README.md", readme)
        rr._write_text_exact(
            tmp_path / "CHANGELOG.md",
            "# Changelog\r\n\r\n## [v3.2.1] - 2026-07-29\r\n",
        )
        rr._write_text_exact(
            assets / "stats.svg",
            SAMPLE_SVG.replace("\n", "\r\n"),
        )

        curated = {}
        for name in ("hero.svg", "architecture.svg", "comparison-hero.svg"):
            payload = f"<svg>\r\n<!-- curated {name} -->\r\n</svg>\r\n".encode()
            (assets / name).write_bytes(payload)
            curated[name] = payload

        rr.render(tmp_path, SAMPLE_META)

        for name, expected in curated.items():
            assert (assets / name).read_bytes() == expected
        stats_bytes = (assets / "stats.svg").read_bytes()
        assert b"\r\n" in stats_bytes
        assert b"\n" not in stats_bytes.replace(b"\r\n", b"")


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
        ("name", "expected_viewbox"),
        [
            ("hero.svg", (0.0, 0.0, 1200.0, 360.0)),
            ("architecture.svg", (0.0, 0.0, 960.0, 340.0)),
            ("stats.svg", (0.0, 0.0, 960.0, 170.0)),
            ("comparison-hero.svg", (0.0, 0.0, 960.0, 280.0)),
        ],
    )
    def test_primary_assets_keep_approved_full_compositions(
        self, name: str, expected_viewbox: tuple[float, float, float, float]
    ) -> None:
        root = ET.fromstring((ASSETS / name).read_text(encoding="utf-8"))
        assert tuple(map(float, root.attrib["viewBox"].split())) == expected_viewbox

    @pytest.mark.parametrize(
        "name",
        ["hero.svg", "architecture.svg", "stats.svg", "comparison-hero.svg"],
    )
    def test_all_svg_text_remains_readable_on_mobile(self, name: str) -> None:
        root = ET.fromstring((ASSETS / name).read_text(encoding="utf-8"))
        _, _, width, _ = map(float, root.attrib["viewBox"].split())
        text_nodes = list(root.iter("{http://www.w3.org/2000/svg}text"))
        assert text_nodes, f"{name} has no text"
        for node in text_nodes:
            assert node.attrib.get("data-mobile-critical") == "true"
            rendered_size = float(node.attrib["font-size"]) * 340 / width
            assert rendered_size >= 9.8, (
                f"{name} renders {''.join(node.itertext()).strip()!r} at "
                f"{rendered_size:.1f}px on a 340px GitHub content width"
            )

    def test_stats_use_inventory_language(self) -> None:
        stats = (ASSETS / "stats.svg").read_text(encoding="utf-8")
        assert "TEST INVENTORY" in stats
        assert "TESTS DISCOVERED" not in stats
        assert "TESTS PASSING" not in stats
        assert "#888919" not in stats

    def test_stats_cards_use_source_backed_values(self) -> None:
        stats = (ASSETS / "stats.svg").read_text(encoding="utf-8")
        for label in ("MCP TOOLS", "TEST INVENTORY", "SERVER VERSION"):
            assert label in stats
        assert "BATCH SAVINGS" not in stats

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

    def test_readme_does_not_use_decorative_dividers(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        assert "divider-biome.svg" not in readme
        assert not (ASSETS / "divider-biome.svg").exists()

    def test_primary_badges_are_large_horizontal_and_compact(self) -> None:
        readme = (REPO_ROOT / "README.md").read_text(encoding="utf-8")
        block = re.search(
            r'<p align="center">(?P<badges>.*?img\.shields\.io.*?)</p>',
            readme,
        )
        assert block is not None
        badges = block.group("badges")
        assert badges.count("<img ") == 4
        assert badges.count('height="28"') == 4
        assert badges.count("style=for-the-badge") == 4
        assert "<br" not in badges
        assert "&nbsp;" not in badges

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
        assert (
            f"<!-- BIOME:TOOLS -->{meta['tools']}<!-- /BIOME:TOOLS -->"
            in stats
        )
        assert (
            f"<!-- BIOME:TESTS -->{meta['tests_total']}<!-- /BIOME:TESTS -->"
            in stats
        )
        assert (
            f"<!-- BIOME:VERSION -->v{meta['server_version']}"
            f"<!-- /BIOME:VERSION -->"
            in stats
        )
        assert rr.stats_summary(meta) in stats
        assert rr.stats_summary(meta) in readme

    def test_badge_reports_inventory_not_execution(self) -> None:
        badge = json.loads(
            (REPO_ROOT / ".github" / "badges" / "tests.json").read_text(
                encoding="utf-8"
            )
        )
        assert "inventory" in badge["message"]
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
