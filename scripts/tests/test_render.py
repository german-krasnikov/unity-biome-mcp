"""Tests for the pure README renderer and committed presentation surfaces."""

import json
import pathlib
import re
import sys
import xml.etree.ElementTree as ET
from collections import Counter

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
<desc data-biome-marker="STATS_DESC"><!-- BIOME:STATS_DESC -->old<!-- /BIOME:STATS_DESC --></desc>
<text data-biome-marker="TOOLS"><!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS --></text>
<text data-biome-marker="TESTS"><!-- BIOME:TESTS -->2<!-- /BIOME:TESTS --></text>
<text data-biome-marker="BREAKDOWN_PRIMARY"><!-- BIOME:BREAKDOWN_PRIMARY -->old<!-- /BIOME:BREAKDOWN_PRIMARY --></text>
<text data-biome-marker="BREAKDOWN_SECONDARY"><!-- BIOME:BREAKDOWN_SECONDARY -->old<!-- /BIOME:BREAKDOWN_SECONDARY --></text>
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
        assert "4252 regular / 511 stress" in result
        assert "289 live / 5820 Unity" in result
        assert "Unity count source: static source scan" in result

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
            (
                "<!-- BIOME:BREAKDOWN_PRIMARY -->old"
                "<!-- /BIOME:BREAKDOWN_PRIMARY -->"
            ),
            (
                "<!-- BIOME:BREAKDOWN_PRIMARY -->4252 regular / 511 stress"
                "<!-- /BIOME:BREAKDOWN_PRIMARY -->"
            ),
        )
        expected = expected.replace(
            (
                "<!-- BIOME:BREAKDOWN_SECONDARY -->old"
                "<!-- /BIOME:BREAKDOWN_SECONDARY -->"
            ),
            (
                "<!-- BIOME:BREAKDOWN_SECONDARY -->289 live / 5820 Unity"
                "<!-- /BIOME:BREAKDOWN_SECONDARY -->"
            ),
        )
        assert result == expected

    def test_missing_marker_fails_closed(self) -> None:
        svg = SAMPLE_SVG.replace(
            (
                '<text data-biome-marker="TESTS">'
                "<!-- BIOME:TESTS -->2<!-- /BIOME:TESTS --></text>"
            ),
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

    def test_marker_must_own_visible_text_or_description(self) -> None:
        hidden = SAMPLE_SVG.replace(
            (
                '<text data-biome-marker="TOOLS">'
                "<!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS --></text>"
            ),
            (
                "<text>999</text>"
                '<metadata data-biome-marker="TOOLS">'
                "<!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS --></metadata>"
            ),
        )
        with pytest.raises(ValueError, match="must be a direct child payload"):
            rr.substitute_svg_markers(hidden, SAMPLE_META)

    def test_marker_owner_rejects_stale_text_outside_payload(self) -> None:
        stale = SAMPLE_SVG.replace(
            "<!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS -->",
            "999<!-- BIOME:TOOLS -->1<!-- /BIOME:TOOLS -->",
        )
        with pytest.raises(ValueError, match="text outside its marker payload"):
            rr.substitute_svg_markers(stale, SAMPLE_META)

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

        rr._write_text_exact(
            tmp_path / "mkdocs.yml",
            "site_description: MCP server for Unity Editor — 142 tools registered\n",
        )
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
            except ET.ParseError as error:  # noqa: PERF203
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
            assert re.search(
                r"[^{}]*\.motion-particle[^{}]*\{[^{}]*display:\s*none",
                svg,
            )
            root = ET.fromstring(svg)
            parents = {child: parent for parent in root.iter() for child in parent}
            animated = [
                node
                for node in root.iter()
                if node.tag.rsplit("}", 1)[-1].startswith("animate")
            ]
            assert animated

            def has_motion_particle_ancestor(node: ET.Element) -> bool:
                parent = parents.get(node)
                while parent is not None:
                    if "motion-particle" in parent.attrib.get("class", "").split():
                        return True
                    parent = parents.get(parent)
                return False

            assert all(
                has_motion_particle_ancestor(node)
                for node in animated
            )

    @pytest.mark.parametrize(
        ("name", "expected_viewbox"),
        [
            ("hero.svg", (0.0, 0.0, 640.0, 270.0)),
            ("architecture.svg", (0.0, 0.0, 640.0, 458.0)),
            ("stats.svg", (0.0, 0.0, 560.0, 380.0)),
            ("comparison-hero.svg", (0.0, 0.0, 640.0, 280.0)),
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
        critical = [n for n in text_nodes if n.attrib.get("data-mobile-critical") == "true"]
        assert critical, f"{name} has no mobile-critical text nodes"
        for node in critical:
            rendered_size = float(node.attrib["font-size"]) * 340 / width
            assert rendered_size >= 9.8, (
                f"{name} renders {''.join(node.itertext()).strip()!r} at "
                f"{rendered_size:.1f}px on a 340px GitHub content width"
            )

    @pytest.mark.parametrize(
        "name",
        ["hero.svg", "architecture.svg", "stats.svg", "comparison-hero.svg"],
    )
    def test_primary_svg_motion_is_self_contained_and_bounded(
        self,
        name: str,
    ) -> None:
        smil_budgets = {
            "hero.svg": 4,
            "architecture.svg": 8,
            "stats.svg": 8,
            "comparison-hero.svg": 3,
        }
        css_animation_budgets = {
            "hero.svg": 18,
            "architecture.svg": 6,
            "stats.svg": 8,
            "comparison-hero.svg": 12,
        }
        css_timeline_budgets = {
            "hero.svg": 70,
            "architecture.svg": 25,
            "stats.svg": 24,
            "comparison-hero.svg": 24,
        }
        filtered_motion_budgets = {
            "hero.svg": 4,
            "architecture.svg": 8,
            "stats.svg": 0,
            "comparison-hero.svg": 3,
        }
        svg = (ASSETS / name).read_text(encoding="utf-8")
        lower = svg.lower()
        assert "<script" not in lower
        assert "javascript:" not in lower
        assert "random" not in lower
        assert not re.search(r'(?:href|src)\s*=\s*["\']https?://', svg)
        assert not re.search(r"letter-spacing\s*:\s*-", svg)
        assert not re.search(
            r'<animate[^>]+attributeName=["\']stdDeviation["\']',
            svg,
        )

        root = ET.fromstring(svg)
        css_animated_nodes: set[ET.Element] = set()
        for rule in re.finditer(r"([^{}]+)\{([^{}]*)\}", svg):
            if not re.search(r"(?:^|;)\s*animation\s*:", rule.group(2)):
                continue
            for selector in rule.group(1).split(","):
                classes = set(re.findall(r"\.([A-Za-z_][\w-]*)", selector))
                if not classes:
                    continue
                for node in root.iter():
                    node_classes = set(node.attrib.get("class", "").split())
                    if classes.issubset(node_classes):
                        css_animated_nodes.add(node)
        assert len(css_animated_nodes) <= css_timeline_budgets[name]

        filters = list(root.iter("{http://www.w3.org/2000/svg}filter"))
        assert len(filters) <= 3
        for filter_node in filters:
            for attribute in ("x", "y", "width", "height"):
                assert attribute in filter_node.attrib

        smil = [
            node
            for node in root.iter()
            if node.tag.rsplit("}", 1)[-1].startswith("animate")
        ]
        assert len(smil) <= smil_budgets[name]
        assert svg.count("animation:") <= css_animation_budgets[name]
        for node in smil:
            assert node.attrib.get("dur")
            assert node.attrib.get("repeatCount") == "indefinite"

        parents = {
            child: parent
            for parent in root.iter()
            for child in parent
        }

        def has_motion_particle_ancestor(node: ET.Element) -> bool:
            current: ET.Element | None = node
            while current is not None:
                if "motion-particle" in current.attrib.get("class", "").split():
                    return True
                current = parents.get(current)
            return False

        particle_nodes = [
            node
            for node in root.iter()
            if "motion-particle" in node.attrib.get("class", "").split()
        ]
        assert len(particle_nodes) <= 20

        filtered_motion_children = []
        for node in root.iter():
            if "filter" not in node.attrib:
                continue
            if has_motion_particle_ancestor(node):
                filtered_motion_children.append(node)
                assert node.tag.rsplit("}", 1)[-1] == "circle"
                assert float(node.attrib.get("r", "0")) <= 4
            if node.tag.rsplit("}", 1)[-1] == "rect":
                assert float(node.attrib.get("width", "0")) < 200
        assert len(filtered_motion_children) <= filtered_motion_budgets[name]

    def test_hero_embeds_the_unity_mark_without_external_assets(self) -> None:
        root = ET.fromstring((ASSETS / "hero.svg").read_text(encoding="utf-8"))
        unity_mark = next(
            node
            for node in root.iter()
            if node.attrib.get("data-brand") == "unity"
        )
        paths = list(unity_mark.iter("{http://www.w3.org/2000/svg}path"))
        assert len(paths) == 3
        assert not list(root.iter("{http://www.w3.org/2000/svg}image"))

    def test_hero_unity_node_creates_a_gameobject_on_firefly_arrival(self) -> None:
        svg = (ASSETS / "hero.svg").read_text(encoding="utf-8")
        root = ET.fromstring(svg)
        created = [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}g")
            if node.attrib.get("data-created") == "gameobject"
        ]
        assert len(created) == 1
        assert len(list(created[0].iter("{http://www.w3.org/2000/svg}path"))) == 4
        assert "hero-unity-create 7s" in svg
        assert "hero-unity-build-led 7s" in svg

        fireflies = list(
            root.iter("{http://www.w3.org/2000/svg}animateMotion")
        )
        assert [
            (node.attrib["dur"], node.attrib["begin"])
            for node in fireflies
        ] == [
            ("7s", "-2.1s"),
            ("7s", "-4s"),
            ("7s", "-5.9s"),
            ("7s", "-0.8s"),
        ]

    def test_hero_generated_object_pool_is_bounded_varied_and_fifo(self) -> None:
        svg = (ASSETS / "hero.svg").read_text(encoding="utf-8")
        root = ET.fromstring(svg)
        pool = next(
            node
            for node in root.iter("{http://www.w3.org/2000/svg}g")
            if node.attrib.get("id") == "hero-generated-pool"
        )
        flights = list(pool)
        assert len(flights) == 50
        assert [int(node.attrib["data-seq"]) for node in flights] == list(range(50))
        assert pool.attrib["mask"] == "url(#hero-flight-background-mask)"
        assert pool.attrib["data-cycle-seconds"] == "28"
        assert pool.attrib["data-command-cycle-seconds"] == "7"
        assert pool.attrib["data-spawn-delay-seconds"] == "0.18"
        flight_mask = next(
            node
            for node in root.iter("{http://www.w3.org/2000/svg}mask")
            if node.attrib.get("id") == "hero-flight-background-mask"
        )
        assert flight_mask.attrib["style"] == "mask-type:luminance"
        assert len([
            node
            for node in flight_mask
            if node.attrib.get("fill") == "#777777"
        ]) == 3

        kinds = Counter(node.attrib["data-kind"] for node in flights)
        assert kinds == {
            "gameobject": 5,
            "component": 5,
            "image": 5,
            "sound": 5,
            "light": 5,
            "script": 5,
            "camera": 5,
            "material": 5,
            "animation": 5,
            "physics": 5,
        }

        routes: set[str] = set()
        colors: set[str] = set()
        actual_delays: list[float] = []
        for node in flights:
            classes = set(node.attrib["class"].split())
            routes.update(name for name in classes if name.startswith(
                "hero-flight-route-"
            ))
            colors.update(name for name in classes if name.startswith(
                "hero-generated-color-"
            ))
            delay = re.fullmatch(
                r"animation-delay:(\d+(?:\.\d+)?)s",
                node.attrib["style"],
            )
            assert delay is not None
            actual_delays.append(float(delay.group(1)))
            use = next(node.iter("{http://www.w3.org/2000/svg}use"))
            assert use.attrib["href"] == f"#hero-icon-{node.attrib['data-kind']}"

        batch_sizes = [3, 1, 5, 2, 4, 3, 2, 5, 1, 4, 5, 2, 3, 4, 1, 5]
        batch_starts = [
            1.58, 3.18, 4.78, 6.38,
            8.58, 10.18, 11.78, 13.38,
            15.58, 17.18, 18.78, 20.38,
            22.58, 24.18, 25.78, 27.38,
        ]
        expected_delays = [
            round(start + slot * 0.02, 2)
            for start, size in zip(batch_starts, batch_sizes, strict=True)
            for slot in range(size)
        ]
        assert actual_delays == pytest.approx(expected_delays)
        assert sum(batch_sizes) == 50
        assert all(1 <= size <= 5 for size in batch_sizes)
        assert {
            round((start - 0.18) % 7, 2)
            for start in batch_starts
        } == {1.4, 3.0, 4.6, 6.2}

        assert routes == {f"hero-flight-route-{index}" for index in range(1, 11)}
        assert colors == {
            f"hero-generated-color-{index}" for index in range(1, 11)
        }
        assert "hero-generated-flight 28s linear infinite both" in svg
        flight_paths = re.findall(
            r'\.hero-flight-route-\d+\s*\{\s*offset-path:\s*path\("([^"]+)"\)',
            svg,
        )
        assert len(flight_paths) == 10
        assert all(path.startswith("M584 104") for path in flight_paths)
        assert all(re.search(r"\d+-\d+$", path) for path in flight_paths)
        assert re.search(
            r"\.65%\s*\{[^{}]*opacity:\s*\.95;"
            r"[^{}]*offset-distance:\s*12%;",
            svg,
        )
        assert max(actual_delays) < 28

    def test_hero_fireflies_cross_both_lanes_and_orbit_the_core(self) -> None:
        root = ET.fromstring((ASSETS / "hero.svg").read_text(encoding="utf-8"))
        routes = [
            node.attrib["path"]
            for node in root.iter("{http://www.w3.org/2000/svg}animateMotion")
        ]
        assert len(routes) == 4
        assert all("153" in route and "487" in route for route in routes)
        assert all(("C" in route or "Q" in route) for route in routes)
        assert all("A58 " in route for route in routes)
        glowing_cores = [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}circle")
            if node.attrib.get("filter") == "url(#hero-trail-glow)"
        ]
        assert len(glowing_cores) == 4

    def test_hero_core_uses_living_biome_membranes(self) -> None:
        svg = (ASSETS / "hero.svg").read_text(encoding="utf-8")
        root = ET.fromstring(svg)
        membrane_groups = [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}g")
            if any(
                name in node.attrib.get("class", "").split()
                for name in (
                    "hero-biome-membrane-a",
                    "hero-biome-membrane-b",
                    "hero-biome-membrane-echo",
                )
            )
        ]
        assert len(membrane_groups) == 3
        assert "hero-membrane-a 7.8s" in svg
        assert "hero-membrane-b 9.1s" in svg
        assert "hero-membrane-echo 11.4s" in svg
        assert "perspective(" not in svg
        assert "rotateX(" not in svg
        assert "rotateY(" not in svg
        echo_paths = [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}path")
            if node.attrib.get("filter") == "url(#hero-trail-glow)"
        ]
        assert len(echo_paths) == 1
        veins = [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}g")
            if "hero-biome-veins" in node.attrib.get("class", "").split()
        ]
        assert len(veins) == 1
        assert len(list(veins[0])) == 4
        assert "hero-mist-ring" not in svg
        assert "hero-pulse-ring" not in svg
        assert "hero-orbit-" not in svg
        assert "@keyframes hero-ring-life" not in svg
        assert not [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}rect")
            if "hero-core" in node.attrib.get("class", "")
        ]

    def test_architecture_fireflies_follow_transport_center_lines(self) -> None:
        root = ET.fromstring(
            (ASSETS / "architecture.svg").read_text(encoding="utf-8")
        )
        assert not [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}path")
            if "architecture-firefly-route"
            in node.attrib.get("class", "").split()
        ]

        parents = {child: parent for parent in root.iter() for child in parent}
        lane_routes = [
            node.attrib["path"]
            for node in root.iter("{http://www.w3.org/2000/svg}animateMotion")
            if "architecture-lane-firefly"
            in parents[node].attrib.get("class", "").split()
        ]
        assert len(lane_routes) == 6
        expected_gaps = (
            ("148", "174"),
            ("302", "328"),
            ("456", "482"),
        )
        assert all(
            any(left in route and right in route for left, right in expected_gaps)
            for route in lane_routes
        )
        assert all("Q" in route and "L" in route and route.endswith("Z")
                   for route in lane_routes)
        assert all(
            any(y in route for y in ("111", "122", "304", "315"))
            for route in lane_routes
        )

    def test_comparison_labels_alternate_around_the_track(self) -> None:
        root = ET.fromstring(
            (ASSETS / "comparison-hero.svg").read_text(encoding="utf-8")
        )
        labels = [
            node
            for node in root.iter("{http://www.w3.org/2000/svg}text")
            if "comparison-label" in node.attrib.get("class", "").split()
        ]
        assert [float(node.attrib["y"]) for node in labels] == [
            216.0,
            104.0,
            216.0,
            104.0,
            216.0,
        ]
        assert [float(node.attrib["x"]) for node in labels] == [
            80.0,
            200.0,
            320.0,
            440.0,
            560.0,
        ]
        expected_lines = [
            ("Official",),
            ("Coplay",),
            ("Biome",),
            ("Ivan Murzak",),
            ("CoderGamester",),
        ]
        for label, expected in zip(labels, expected_lines, strict=True):
            lines = list(label.iter("{http://www.w3.org/2000/svg}tspan"))
            assert 1 <= len(lines) <= 2
            assert tuple(line.text for line in lines) == expected
            if len(lines) == 2:
                assert lines[1].attrib.get("dy") == "24"

    def test_comparison_fireflies_cross_the_full_track_and_return(self) -> None:
        root = ET.fromstring(
            (ASSETS / "comparison-hero.svg").read_text(encoding="utf-8")
        )
        parents = {child: parent for parent in root.iter() for child in parent}
        routes = [
            node.attrib["path"]
            for node in root.iter("{http://www.w3.org/2000/svg}animateMotion")
            if "comparison-firefly"
            in parents[node].attrib.get("class", "").split()
        ]
        assert len(routes) == 3
        assert all("80 160" in route or "80 170" in route for route in routes)
        assert all("560 160" in route or "560 170" in route for route in routes)
        assert all(("C" in route or "Q" in route) for route in routes)
        for route in routes:
            start = re.match(r"M(\d+) (\d+)", route)
            assert start is not None
            assert route.endswith(f"{start.group(1)} {start.group(2)}")

    def test_stats_breakdown_keeps_bottom_safe_area(self) -> None:
        root = ET.fromstring((ASSETS / "stats.svg").read_text(encoding="utf-8"))
        secondary = next(
            node
            for node in root.iter("{http://www.w3.org/2000/svg}text")
            if node.attrib.get("data-biome-marker") == "BREAKDOWN_SECONDARY"
        )
        assert 292 - float(secondary.attrib["y"]) >= 8

    def test_stats_use_inventory_language(self) -> None:
        stats = (ASSETS / "stats.svg").read_text(encoding="utf-8")
        assert "TEST INVENTORY" in stats
        assert "TESTS DISCOVERED" not in stats
        assert "TESTS PASSING" not in stats
        assert "#888919" not in stats

    def test_stats_cards_use_source_backed_values(self) -> None:
        stats = (ASSETS / "stats.svg").read_text(encoding="utf-8")
        for label in ("MCP TOOLS", "TEST INVENTORY"):
            assert label in stats
        assert "SERVER VERSION" not in stats
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
        assert badges.count("<img ") == 5
        assert badges.count('height="28"') == 5
        assert badges.count("style=for-the-badge") == 5
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
            f"<!-- BIOME:BREAKDOWN_PRIMARY -->{meta['tests_python']} regular / "
            f"{meta['tests_stress']} stress<!-- /BIOME:BREAKDOWN_PRIMARY -->"
            in stats
        )
        assert (
            f"<!-- BIOME:BREAKDOWN_SECONDARY -->{meta['tests_live']} live / "
            f"{meta['tests_unity']} Unity<!-- /BIOME:BREAKDOWN_SECONDARY -->"
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


PYRAMID_SVG_STUB = """\
<svg xmlns="http://www.w3.org/2000/svg">
  <rect data-biome-bar="CS" x="82" y="322" height="7" width="1" rx="3" fill="#59a7ff"/>
  <rect data-biome-bar="PY" x="82" y="337" height="7" width="1" rx="3" fill="#46e6a6"/>
  <rect data-biome-bar="STRESS" x="82" y="352" height="7" width="1" rx="3" fill="#f0a536"/>
  <rect data-biome-bar="LIVE" x="82" y="367" height="7" width="1" rx="3" fill="#e67e46"/>
  <text data-biome-bar-label="CS" x="478" y="331" text-anchor="end" fill="#b7b7bd" font-size="10">1 c#</text>
  <text data-biome-bar-label="PY" x="478" y="346" text-anchor="end" fill="#b7b7bd" font-size="10">1 py</text>
  <text data-biome-bar-label="STRESS" x="478" y="361" text-anchor="end" fill="#b7b7bd" font-size="10">1 stress</text>
  <text data-biome-bar-label="LIVE" x="478" y="376" text-anchor="end" fill="#b7b7bd" font-size="10">1 live</text>
</svg>"""

_PYRAMID_META = {
    "tests_unity": 6254,
    "tests_python": 4597,
    "tests_stress": 511,
    "tests_live": 287,
}


class TestBarWidth:
    def test_max_count_gives_max_px(self) -> None:
        assert rr._bar_width(100, 100) == 396

    def test_zero_max_count_returns_zero(self) -> None:
        assert rr._bar_width(5, 0) == 0

    def test_zero_count_returns_zero(self) -> None:
        assert rr._bar_width(0, 100) == 0

    def test_minimum_two_for_nonzero_count(self) -> None:
        assert rr._bar_width(1, 10000) >= 2


class TestSubstitutePyramidBars:
    def _apply(self, meta: dict | None = None) -> str:
        return rr.substitute_pyramid_bars(PYRAMID_SVG_STUB, meta or _PYRAMID_META)

    def test_cs_bar_is_widest_when_cs_count_is_max(self) -> None:
        result = self._apply()
        match = re.search(r'data-biome-bar="CS"[^>]*\bwidth="(\d+)"', result)
        assert match is not None
        assert int(match.group(1)) == 396

    def test_live_bar_is_minimum_two_px(self) -> None:
        result = self._apply()
        match = re.search(r'data-biome-bar="LIVE"[^>]*\bwidth="(\d+)"', result)
        assert match is not None
        assert int(match.group(1)) >= 2

    def test_zero_count_bar_has_zero_width(self) -> None:
        meta = {**_PYRAMID_META, "tests_live": 0}
        result = rr.substitute_pyramid_bars(PYRAMID_SVG_STUB, meta)
        match = re.search(r'data-biome-bar="LIVE"[^>]*\bwidth="(\d+)"', result)
        assert match is not None
        assert int(match.group(1)) == 0

    def test_idempotent(self) -> None:
        first = self._apply()
        second = rr.substitute_pyramid_bars(first, _PYRAMID_META)
        assert first == second

    def test_labels_contain_count_and_suffix(self) -> None:
        result = self._apply()
        assert "6254 c#" in result
