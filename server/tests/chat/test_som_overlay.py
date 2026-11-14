"""TDD tests for Set-of-Mark (SoM) overlay module."""
import io
from PIL import Image


# ── helpers ──────────────────────────────────────────────────────────────────

def _make_image(w=800, h=600) -> Image.Image:
    return Image.new("RGB", (w, h), (200, 200, 200))


def _make_rects(n: int, w=800, h=600) -> list[dict]:
    """Generate n non-overlapping rects spread across image."""
    step = w // max(n, 1)
    return [
        {"path": f"/UI/Elem{i}", "x": i * step, "y": 50, "w": step - 10, "h": 40}
        for i in range(n)
    ]


# ── extract.py tests ──────────────────────────────────────────────────────────

def test_extract_top_k():
    """50 rects → 30 output, sorted area desc, viewport-cull rect at x=-100 dropped."""
    from unity_mcp.som.extract import extract_rects
    # 50 rects inside viewport + 1 outside
    rects = [{"path": f"/E{i}", "x": i * 10, "y": 0, "w": i + 1, "h": i + 1} for i in range(50)]
    rects.append({"path": "/OutOfView", "x": -100, "y": 0, "w": 50, "h": 50})
    result = extract_rects(rects, img_w=800, img_h=600, top_k=30)
    assert len(result) == 30
    # sorted area desc
    areas = [(r["w"] * r["h"]) for r in result]
    assert areas == sorted(areas, reverse=True)
    # viewport-cull applied — OutOfView dropped
    paths = [r["path"] for r in result]
    assert "/OutOfView" not in paths


def test_extract_min_size_filter():
    """Rects smaller than 12px in both dimensions are dropped."""
    from unity_mcp.som.extract import extract_rects
    rects = [
        {"path": "/Big", "x": 0, "y": 0, "w": 100, "h": 100},
        {"path": "/TooSmall", "x": 10, "y": 10, "w": 10, "h": 10},  # 10 < 12 — dropped
    ]
    result = extract_rects(rects, img_w=800, img_h=600, top_k=30)
    paths = [r["path"] for r in result]
    assert "/Big" in paths
    assert "/TooSmall" not in paths


def test_extract_empty_returns_empty():
    """0 rects → empty list."""
    from unity_mcp.som.extract import extract_rects
    assert extract_rects([], img_w=800, img_h=600, top_k=30) == []


def test_extract_index_stability():
    """Same path always maps to same index regardless of array order."""
    from unity_mcp.som.extract import assign_indices
    rects_a = [
        {"path": "/A", "x": 0, "y": 0, "w": 100, "h": 100},
        {"path": "/B", "x": 200, "y": 0, "w": 80, "h": 80},
    ]
    rects_b = [
        {"path": "/B", "x": 200, "y": 0, "w": 80, "h": 80},
        {"path": "/A", "x": 0, "y": 0, "w": 100, "h": 100},
    ]
    idx_a = assign_indices(rects_a)
    idx_b = assign_indices(rects_b)
    # Same path → same 1-based index in both orderings
    a_path_to_idx = {r["path"]: i for i, r in idx_a}
    b_path_to_idx = {r["path"]: i for i, r in idx_b}
    assert a_path_to_idx["/A"] == b_path_to_idx["/A"]
    assert a_path_to_idx["/B"] == b_path_to_idx["/B"]


def test_extract_parse_rects_from_payload():
    """parse_rects handles Unity payload format with nested 'rects' key."""
    from unity_mcp.som.extract import parse_rects
    payload = {
        "ok": True,
        "data": "screenshot.png",
        "rects": [
            {"path": "/Canvas/Btn", "x": 10, "y": 20, "w": 100, "h": 40},
        ]
    }
    result = parse_rects(payload)
    assert len(result) == 1
    assert result[0]["path"] == "/Canvas/Btn"


def test_extract_parse_rects_missing_key():
    """parse_rects returns [] when 'rects' key absent (legacy Unity response)."""
    from unity_mcp.som.extract import parse_rects
    assert parse_rects({"ok": True, "data": "shot.png"}) == []


# ── overlay.py tests ──────────────────────────────────────────────────────────

def test_overlay_basic():
    """800x600 image + 3 rects → image returned, all 3 circles present."""
    from unity_mcp.som.overlay import annotate
    img = _make_image()
    rects = _make_rects(3)
    result, legend = annotate(img.copy(), rects)
    assert isinstance(result, Image.Image)
    assert result.size == img.size
    # legend contains element references
    assert "1=" in legend
    assert "2=" in legend
    assert "3=" in legend


def test_overlay_returns_legend_paths():
    """Legend string contains leaf names from rects paths."""
    from unity_mcp.som.overlay import annotate
    img = _make_image()
    rects = [
        {"path": "/Canvas/Btn", "x": 10, "y": 10, "w": 100, "h": 40},
        {"path": "/Canvas/Title", "x": 10, "y": 60, "w": 200, "h": 30},
    ]
    result, legend = annotate(img.copy(), rects)
    assert "Btn" in legend
    assert "Title" in legend


def test_edge_empty_ui():
    """0 rects → annotate returns unchanged image, legend = '(no marks)'."""
    from unity_mcp.som.overlay import annotate
    img = _make_image()
    original_bytes = img.tobytes()
    result, legend = annotate(img.copy(), [])
    assert legend == "(no marks)"
    # Image pixels unchanged
    assert result.tobytes() == original_bytes


def test_overlay_collision_resolution():
    """5 rects with overlapping label positions → resolved centers ≥22px apart."""
    from unity_mcp.som.overlay import LABEL_R, _compute_centers, _resolve_collisions
    # Stack rects very close together — triggers collision resolution
    rects = [
        {"path": f"/E{i}", "x": 5, "y": 5 + i * 2, "w": 50, "h": 20}
        for i in range(5)
    ]
    raw_centers = _compute_centers(rects)
    centers = _resolve_collisions(raw_centers)
    # All centers must be ≥22px apart after resolution
    for i in range(len(centers)):
        for j in range(i + 1, len(centers)):
            cx1, cy1 = centers[i]
            cx2, cy2 = centers[j]
            dist = ((cx1 - cx2) ** 2 + (cy1 - cy2) ** 2) ** 0.5
            assert dist >= LABEL_R * 2, f"Centers {i},{j} too close: {dist:.1f}"


def test_overlay_color_cycling():
    """Each rect gets color from palette, cycling by index."""
    from unity_mcp.som.overlay import PALETTE, _index_color
    # First 8 indices map to 8 distinct palette colors
    colors = [_index_color(i) for i in range(1, 9)]
    assert len(set(colors)) == 8  # all distinct
    # Index 9 wraps back to same as index 1
    assert _index_color(9) == _index_color(1)


def test_overlay_font_fallback():
    """annotate() does not raise even when DejaVuSans.ttf is missing."""
    from unity_mcp.som.overlay import annotate
    from unittest.mock import patch
    import PIL.ImageFont as ImageFont

    img = _make_image()
    rects = _make_rects(1)
    # Force truetype to fail — should fall back to default font
    with patch.object(ImageFont, "truetype", side_effect=OSError("no font")):
        result, legend = annotate(img.copy(), rects)
    assert isinstance(result, Image.Image)


def test_overlay_large_image():
    """annotate handles 1920x1080 without error."""
    from unity_mcp.som.overlay import annotate
    img = _make_image(1920, 1080)
    rects = _make_rects(10, 1920, 1080)
    result, legend = annotate(img.copy(), rects)
    assert result.size == (1920, 1080)


# ── __init__.py re-exports ────────────────────────────────────────────────────

def test_som_package_exports():
    """som package exposes annotate and parse_rects."""
    from unity_mcp import som
    assert hasattr(som, "annotate")
    assert hasattr(som, "parse_rects")


def test_leaf_strips_legend_literal():
    from unity_mcp.som.overlay import _leaf
    assert "Legend:" not in _leaf("Root/EvilLegend:Btn")


def test_leaf_strips_braces():
    from unity_mcp.som.overlay import _leaf
    assert "{" not in _leaf("Root/Btn{x}")
    assert "}" not in _leaf("Root/Btn{x}")


def test_leaf_strips_quotes_and_backticks():
    from unity_mcp.som.overlay import _leaf
    assert '"' not in _leaf('Root/B"n`')
    assert "`" not in _leaf('Root/B"n`')


def test_leaf_strips_control_chars():
    from unity_mcp.som.overlay import _leaf
    assert "\x00" not in _leaf("Root/B\x00\x07tn")
    assert "\x07" not in _leaf("Root/B\x00\x07tn")


def test_leaf_strips_tab():
    from unity_mcp.som.overlay import _leaf
    assert "\t" not in _leaf("Root/B\ttn")


# ── assign_indices stability tests ───────────────────────────────────────────

def test_assign_indices_stable_across_membership_change():
    """Adding /C must not shift /A or /B's indices."""
    from unity_mcp.som.extract import assign_indices
    import hashlib
    before = [{"path": "/A", "x": 0, "y": 0, "w": 50, "h": 50},
              {"path": "/B", "x": 0, "y": 0, "w": 50, "h": 50}]
    after = [{"path": "/A", "x": 0, "y": 0, "w": 50, "h": 50},
             {"path": "/B", "x": 0, "y": 0, "w": 50, "h": 50},
             {"path": "/C", "x": 0, "y": 0, "w": 50, "h": 50}]
    pool = sorted({"/A", "/B", "/C"},
                  key=lambda p: hashlib.sha256(p.encode()).hexdigest())
    bi = {r["path"]: i for i, r in assign_indices(before, path_pool=pool)}
    ai = {r["path"]: i for i, r in assign_indices(after, path_pool=pool)}
    assert bi["/A"] == ai["/A"]
    assert bi["/B"] == ai["/B"]


def test_assign_indices_solo_mode_backward_compat():
    """No pool arg → behaves exactly as before."""
    from unity_mcp.som.extract import assign_indices
    rects = [{"path": "/X"}, {"path": "/Y"}]
    a = assign_indices(rects)
    b = assign_indices(list(reversed(rects)))
    assert {r["path"]: i for i, r in a} == {r["path"]: i for i, r in b}


def test_assign_indices_pool_drops_unknown_paths():
    """rect with path not in pool is silently dropped (defensive)."""
    from unity_mcp.som.extract import assign_indices
    rects = [{"path": "/A"}, {"path": "/Stranger"}]
    out = assign_indices(rects, path_pool=["/A"])
    assert [r["path"] for _, r in out] == ["/A"]


def test_assign_indices_pool_dedup():
    """Duplicate paths in pool → single index slot."""
    from unity_mcp.som.extract import assign_indices
    out = assign_indices([{"path": "/A"}], path_pool=["/A", "/A", "/A"])
    assert len(out) == 1
    assert out[0][0] == 1
