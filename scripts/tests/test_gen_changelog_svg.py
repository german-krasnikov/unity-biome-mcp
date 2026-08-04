"""Tests for gen_changelog_svg.py — written FIRST (TDD Red phase)."""
import pathlib
import sys
import xml.etree.ElementTree as ET

import pytest

# Allow importing the script from parent dir
sys.path.insert(0, str(pathlib.Path(__file__).parent.parent))
import gen_changelog_svg as gen

# ---------------------------------------------------------------------------
# Parser tests
# ---------------------------------------------------------------------------

class TestParseChangelog:
    def test_real_changelog_returns_expected_versions(self, real_changelog: str) -> None:
        """Test (1): real CHANGELOG parses at least 8 versions (capped display limit)."""
        versions = gen.parse_changelog(real_changelog)
        assert len(versions) >= gen._MAX_NODES

    def test_real_changelog_oldest_first(self, real_changelog: str) -> None:
        """Test (2): oldest version (v0.2.6) is index 0; v0.6.0 exists somewhere."""
        versions = gen.parse_changelog(real_changelog)
        assert versions[0].ver == "0.2.6"
        assert any(v.ver == "0.6.0" for v in versions)

    def test_caption_from_annotation(self, real_changelog: str) -> None:
        """Test (3): caption comes from <!-- svg: ... --> annotation."""
        versions = gen.parse_changelog(real_changelog)
        # v0.6.0 has the svg annotation regardless of its position in the list
        v060 = next(v for v in versions if v.ver == "0.6.0")
        assert v060.caption == "Aura pill + native theme + perms gating"

    def test_fallback_to_bold_title_lowercased(self, sample_changelog: str) -> None:
        """Test (4): version without annotation falls back to first bold title, lowercased+truncated."""
        versions = gen.parse_changelog(sample_changelog)
        # v0.4.0 has no annotation — sample has "Fallback Title Bold"
        v040 = next(v for v in versions if v.ver == "0.4.0")
        assert v040.caption == "fallback title bold"

    def test_earlier_history_ignored(self, sample_changelog: str) -> None:
        """Test (5): ## Earlier history section and its content are ignored."""
        versions = gen.parse_changelog(sample_changelog)
        captions = [v.caption for v in versions]
        assert not any("should not appear" in c.lower() for c in captions)
        # only 3 versions in sample
        assert len(versions) == 3

    def test_missing_date_parses_with_empty_string(self, sample_changelog: str) -> None:
        """Test (6): version with empty date group still parses, date=''."""
        versions = gen.parse_changelog(sample_changelog)
        v030 = next(v for v in versions if v.ver == "0.3.0")
        assert v030.date == ""

    def test_single_version_file_returns_one(self) -> None:
        """Test (7): file with only one version returns a list of length 1."""
        text = "## [v1.0.0] — 2026-01-01 <!-- svg: only one -->\n\n- **Bullet** — text\n"
        versions = gen.parse_changelog(text)
        assert len(versions) == 1

    def test_zero_versions_raises(self) -> None:
        """Test (8): file with no versions raises SystemExit or ValueError."""
        text = "# Changelog\n\nNo versions here.\n"
        with pytest.raises((SystemExit, ValueError)):
            gen.parse_changelog(text)


# ---------------------------------------------------------------------------
# Layout tests
# ---------------------------------------------------------------------------

class TestLayout:
    def test_displayed_versions_produce_n_plus_one_x_positions(self, real_changelog: str) -> None:
        """Test (9): N displayed versions → N+1 x-positions (N real + 1 future)."""
        versions = gen.parse_changelog(real_changelog)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        assert len(lo.xs) == n + 1

    def test_x_positions_endpoints(self, real_changelog: str) -> None:
        """Test (9b): x[0]=140, x[-1]=viewbox_w-120."""
        versions = gen.parse_changelog(real_changelog)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        assert lo.xs[0] == pytest.approx(140.0)
        assert lo.xs[-1] == pytest.approx(lo.viewbox_w - 120.0)

    def test_x_positions_evenly_spaced(self, real_changelog: str) -> None:
        """Test (9c): x positions are evenly spaced."""
        versions = gen.parse_changelog(real_changelog)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        steps = [lo.xs[i+1] - lo.xs[i] for i in range(len(lo.xs) - 1)]
        for s in steps:
            assert s == pytest.approx(steps[0], rel=1e-6)

    def test_opacity_ramp_monotonic(self, real_changelog: str) -> None:
        """Test (10): opacity[0]=0.40, opacity[N-1]=1.00, monotonically increasing."""
        versions = gen.parse_changelog(real_changelog)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        ops = lo.opacities[:len(displayed)]  # exclude future node
        assert ops[0] == pytest.approx(0.40)
        assert ops[-1] == pytest.approx(1.00)
        for i in range(len(ops) - 1):
            assert ops[i+1] >= ops[i]

    def test_ring_counts(self, real_changelog: str) -> None:
        """Test (11): ring_count=3 if opacity>=0.75 else 1."""
        versions = gen.parse_changelog(real_changelog)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        for i, (op, rings) in enumerate(zip(lo.opacities[:len(displayed)], lo.ring_counts[:len(displayed)], strict=False)):
            expected = 3 if op >= 0.75 else 1
            assert rings == expected, f"node {i}: opacity={op}, expected rings={expected}, got {rings}"

    def test_packet_counts_monotonic(self, real_changelog: str) -> None:
        """Test (12): packet counts are 3..8 monotonically non-decreasing for real nodes."""
        versions = gen.parse_changelog(real_changelog)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        pkts = lo.packets[:len(displayed)]
        assert pkts[0] == 3
        assert pkts[-1] == 8
        for i in range(len(pkts) - 1):
            assert pkts[i+1] >= pkts[i]


# ---------------------------------------------------------------------------
# Output / SVG tests
# ---------------------------------------------------------------------------

class TestRenderSvg:
    @pytest.fixture
    def svg_5ver(self, real_changelog: str) -> str:
        versions = gen.parse_changelog(real_changelog)
        return gen.render_svg(versions)

    def test_no_script_tags(self, svg_5ver: str) -> None:
        """Test (13): zero <script tags in output."""
        assert "<script" not in svg_5ver.lower()

    def test_well_formed_xml(self, svg_5ver: str) -> None:
        """Test (14): output parses as valid XML."""
        ET.fromstring(svg_5ver)  # raises if malformed

    def test_idempotent(self, real_changelog: str) -> None:
        """Test (15): generate twice from same input → byte-identical."""
        versions = gen.parse_changelog(real_changelog)
        svg1 = gen.render_svg(versions)
        svg2 = gen.render_svg(versions)
        assert svg1 == svg2

    def test_supernova_contains_red_color(self, svg_5ver: str) -> None:
        """Test (16): last real node group contains #e94560."""
        assert "#e94560" in svg_5ver

    def test_future_node_has_stroke_dasharray(self, svg_5ver: str) -> None:
        """Test (17): future node uses stroke-dasharray."""
        assert "stroke-dasharray" in svg_5ver

    def test_begin_times_formula(self, real_changelog: str) -> None:
        """Test (18): begin times match 0.2 + i*0.8 for i in 0..N."""
        versions = gen.parse_changelog(real_changelog)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        for i in range(n + 1):
            expected = round(0.2 + i * 0.8, 1)
            assert lo.begins[i] == pytest.approx(expected)

    def test_all_version_strings_appear(self, real_changelog: str) -> None:
        """Test (19): every displayed version string appears as text in SVG."""
        versions = gen.parse_changelog(real_changelog)
        svg = gen.render_svg(versions)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        for v in displayed:
            assert f"v{v.ver}" in svg


# ---------------------------------------------------------------------------
# Edge case tests
# ---------------------------------------------------------------------------

class TestEdgeCases:
    def test_single_version_is_supernova_opacity_one(self) -> None:
        """Test (20): single version → opacity=1.0, is supernova, no crash."""
        text = "## [v9.9.9] — 2026-01-01 <!-- svg: solo -->\n\n- **Bullet**\n"
        versions = gen.parse_changelog(text)
        lo = gen.layout(versions)
        assert lo.opacities[0] == pytest.approx(1.0)
        # render should not crash
        svg = gen.render_svg(versions)
        assert "#e94560" in svg  # it IS the supernova

    def test_twelve_versions_truncates_to_eight_with_warning(self, capsys) -> None:
        """Test (21): 12 versions → only last 8 (newest) in SVG + stderr warning.

        File order is newest-first: line i=0 → v0.0.0 (first-in-file/newest), i=11 → v11.0.0 (last-in-file/oldest).
        After reversal oldest-first: [v11, v10, ..., v0].
        Last 8 newest = last 8 in oldest-first list = v7,v6,...,v0.
        Dropped (oldest 4) = v11, v10, v9, v8.
        """
        lines = []
        for i in range(12):
            # i=0 at top of file = first-in-file (newest semver)
            lines.append(f"## [v{i}.0.0] — 2026-01-01 <!-- svg: caption {i} -->")
            lines.append("")
            lines.append("- **Bullet** — text")
            lines.append("")
        text = "\n".join(lines)
        versions = gen.parse_changelog(text)
        svg = gen.render_svg(versions)
        captured = capsys.readouterr()
        # warning on stderr
        assert "dropped" in captured.err.lower() or "truncat" in captured.err.lower()
        # oldest 4 should be dropped (v11 through v8 in file = oldest after reversal)
        assert "v11.0.0" not in svg   # dropped (oldest)
        assert "v8.0.0" not in svg    # dropped (oldest)
        assert "v0.0.0" in svg        # kept (newest)


# ---------------------------------------------------------------------------
# TDD Red: new tests for critical/major fixes
# ---------------------------------------------------------------------------

class TestBaselinePath:
    def test_baseline_ends_at_w_minus_60(self, real_changelog: str) -> None:
        """Test (22): ECG baseline path must end at L{w-60} 240, not L{w//2}."""
        versions = gen.parse_changelog(real_changelog)
        svg = gen.render_svg(versions)
        # layout uses the capped list (same as render_svg does internally)
        n = min(len(versions), gen._MAX_NODES)
        displayed = versions[-n:] if len(versions) > n else versions
        lo = gen.layout(displayed)
        w = lo.viewbox_w
        wend = w - 60
        assert f"L{wend} 240" in svg, f"Expected baseline to end at L{wend} 240 (w={w})"
        assert f"L{w // 2} 240" not in svg, f"Baseline must not stop at midpoint L{w // 2} 240"


class TestXmlEscape:
    def test_caption_with_ampersand_and_lt_is_escaped(self) -> None:
        """Test (23): caption containing & and < must appear as &amp;/&lt; in SVG text."""
        text = '## [v1.0.0] — 2026-01-01 <!-- svg: a & b < c -->\n\n- **Bullet**\n'
        versions = gen.parse_changelog(text)
        svg = gen.render_svg(versions)
        # Raw chars must not appear in text elements
        assert "a &amp; b &lt; c" in svg
        # And the SVG must still parse as valid XML
        root = ET.fromstring(svg)
        # Find the text element containing our caption
        texts = root.findall(".//{http://www.w3.org/2000/svg}text")
        caption_texts = [t.text for t in texts if t.text and "a & b < c" in t.text]
        assert len(caption_texts) >= 1, "Escaped caption must appear as decoded text in XML tree"
