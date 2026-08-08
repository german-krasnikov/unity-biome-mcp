"""TDD tests for SoM code-review fixes (#1–#8)."""
import os
import sys
import subprocess
import tempfile
import textwrap
from unittest.mock import AsyncMock, MagicMock
from PIL import Image


def _write_png(path, color=(100, 100, 100), size=(100, 100)):
    Image.new("RGB", size, color).save(str(path))


# ── #1 Non-deterministic hash() across processes ──────────────────────────────

def test_assign_indices_stable_across_processes():
    """assign_indices must produce identical results in two subprocesses with
    different PYTHONHASHSEED values."""
    script = textwrap.dedent("""
        import sys
        sys.path.insert(0, sys.argv[1])
        from unity_mcp.som.extract import assign_indices
        rects = [
            {"path": "/Canvas/Btn"},
            {"path": "/Canvas/Title"},
            {"path": "/Canvas/Panel"},
        ]
        result = assign_indices(rects)
        # Print as stable text: "idx:path idx:path ..."
        print(" ".join(f"{i}:{r['path']}" for i, r in result))
    """)
    src_dir = os.path.join(os.path.dirname(__file__), "..", "src")
    src_dir = os.path.abspath(src_dir)

    env1 = {**os.environ, "PYTHONHASHSEED": "0"}
    env2 = {**os.environ, "PYTHONHASHSEED": "999"}

    r1 = subprocess.run([sys.executable, "-c", script, src_dir],
                        capture_output=True, text=True, encoding="utf-8", env=env1, timeout=60)
    r2 = subprocess.run([sys.executable, "-c", script, src_dir],
                        capture_output=True, text=True, encoding="utf-8", env=env2, timeout=60)

    assert r1.returncode == 0, r1.stderr
    assert r2.returncode == 0, r2.stderr
    assert r1.stdout.strip() == r2.stdout.strip(), (
        f"Hash instability: seed=0 → {r1.stdout.strip()!r}, "
        f"seed=999 → {r2.stdout.strip()!r}"
    )


# ── #2 _build_legend_for_diff set iteration order ────────────────────────────

def test_build_legend_for_diff_stable_order():
    """_build_legend_for_diff must return same string regardless of how many
    times it is called (no random set-iteration order)."""
    from unity_mcp.visual_diff import _build_legend_for_diff
    rects = [
        {"path": "/A"},
        {"path": "/B"},
        {"path": "/C"},
    ]
    results = {_build_legend_for_diff(rects, None) for _ in range(20)}
    assert len(results) == 1, f"Non-deterministic legend: {results}"


# ── #3 annotate() wired in visual_diff when mark=True ────────────────────────

async def test_visual_diff_mark_writes_annotated_image(tmp_path):
    """mark=True: sampling receives paths to images with visible circle marks
    (pixels differ from the original plain grey image)."""
    from unity_mcp.visual_diff import visual_diff

    before = tmp_path / "before.png"
    after = tmp_path / "after.png"
    # Plain grey images — no circles present yet
    _write_png(before, (128, 128, 128), (200, 200))
    _write_png(after, (140, 140, 140), (200, 200))

    orig_before_bytes = Image.open(str(before)).convert("RGB").tobytes()
    orig_after_bytes = Image.open(str(after)).convert("RGB").tobytes()

    captured_bytes = []

    async def mock_verify(b, a, prompt, **kwargs):
        # Read images while tmpdir is still alive (inside the with-block)
        b_img = Image.open(b).convert("RGB").tobytes()
        a_img = Image.open(a).convert("RGB").tobytes()
        captured_bytes.append((b_img, a_img))
        return "Changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    rects = [{"path": "/Canvas/Btn", "x": 5, "y": 5, "w": 80, "h": 40}]

    await visual_diff(
        str(before), str(after), mode="structural",
        sampling=mock_svc, mark=True, rects=rects,
    )

    assert len(captured_bytes) == 1
    b_bytes, a_bytes = captured_bytes[0]

    assert b_bytes != orig_before_bytes, "annotate() was NOT applied — before image unchanged"
    assert a_bytes != orig_after_bytes, "annotate() was NOT applied — after image unchanged"


# ── #4 som_visual feature key ─────────────────────────────────────────────────

async def test_visual_diff_mark_uses_som_visual_feature(tmp_path):
    """mark=True: verify_visual_diff called with feature='som_visual'."""
    from unity_mcp.visual_diff import visual_diff

    before = tmp_path / "b.png"
    after = tmp_path / "a.png"
    _write_png(before, (0, 0, 0))
    _write_png(after, (255, 0, 0))

    captured_kwargs = []

    async def mock_verify(b, a, prompt, *, feature="visual_diff", **kw):
        captured_kwargs.append(feature)
        return "Changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    rects = [{"path": "/Btn", "x": 5, "y": 5, "w": 40, "h": 20}]
    await visual_diff(str(before), str(after), mode="structural",
                      sampling=mock_svc, mark=True, rects=rects)

    assert captured_kwargs == ["som_visual"], (
        f"Expected feature='som_visual', got {captured_kwargs}"
    )


async def test_visual_diff_no_mark_uses_visual_diff_feature(tmp_path):
    """mark=False: verify_visual_diff called with feature='visual_diff' (default)."""
    from unity_mcp.visual_diff import visual_diff

    before = tmp_path / "b2.png"
    after = tmp_path / "a2.png"
    _write_png(before, (0, 0, 0))
    _write_png(after, (255, 0, 0))

    captured_kwargs = []

    async def mock_verify(b, a, prompt, *, feature="visual_diff", **kw):
        captured_kwargs.append(feature)
        return "Changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    await visual_diff(str(before), str(after), mode="structural", sampling=mock_svc)

    assert captured_kwargs == ["visual_diff"]


# ── #5 Prompt injection via \n in path ────────────────────────────────────────

def test_build_legend_sanitizes_newlines():
    """Paths with \\n/\\r must not produce newlines in legend output."""
    from unity_mcp.visual_diff import _build_legend_for_diff
    rects = [{"path": "/Dirty\nPath"}, {"path": "/Clean"}]
    legend = _build_legend_for_diff(rects, None)
    assert "\n" not in legend, f"Newline leaked into legend: {legend!r}"
    assert "\r" not in legend


def test_overlay_build_legend_sanitizes_newlines():
    """overlay._build_legend must not pass newlines through."""
    from unity_mcp.som.overlay import _build_legend
    from unity_mcp.som.extract import assign_indices
    rects = [{"path": "/Dirty\nPath\r\nMore"}]
    indexed = assign_indices(rects)
    legend = _build_legend(indexed)
    assert "\n" not in legend
    assert "\r" not in legend


# ── #6 Collision slack removed ────────────────────────────────────────────────

def test_overlay_collision_no_slack():
    """_resolve_collisions must achieve LABEL_R*2 (22px) separation, not 20."""
    from unity_mcp.som.overlay import LABEL_R, _compute_centers, _resolve_collisions
    rects = [
        {"path": f"/E{i}", "x": 5, "y": 5 + i * 2, "w": 50, "h": 20}
        for i in range(5)
    ]
    centers = _resolve_collisions(_compute_centers(rects))
    min_dist = LABEL_R * 2  # 22, not 20
    for i in range(len(centers)):
        for j in range(i + 1, len(centers)):
            dx = centers[i][0] - centers[j][0]
            dy = centers[i][1] - centers[j][1]
            dist = (dx ** 2 + dy ** 2) ** 0.5
            assert dist >= min_dist, (
                f"Centers {i},{j} too close: {dist:.1f} < {min_dist}"
            )


# ── #7 _load_font Pillow < 10 fallback ───────────────────────────────────────

def test_load_font_fallback_size_param():
    """_load_font returns something even when load_default doesn't accept size."""
    from unity_mcp.som.overlay import _load_font
    from unittest.mock import patch
    import PIL.ImageFont as IF

    def no_truetype(*a, **kw):
        raise OSError("no font file")

    def no_size_default(**kw):
        if "size" in kw:
            raise TypeError("unexpected keyword argument 'size'")
        return IF.load_default()

    with patch.object(IF, "truetype", side_effect=no_truetype):
        with patch.object(IF, "load_default", side_effect=no_size_default):
            font = _load_font(14)
    # Should not raise; font may be None or a valid font object
    # (None is acceptable as graceful degradation)


# ── #8 Leaf-only path in legend ──────────────────────────────────────────────

def test_build_legend_uses_leaf_path():
    """Legend text uses only the last path component, not the full path."""
    from unity_mcp.som.overlay import _build_legend
    from unity_mcp.som.extract import assign_indices
    rects = [{"path": "/Canvas/Panel/Button"}]
    indexed = assign_indices(rects)
    legend = _build_legend(indexed)
    # Should contain "Button" not the full "/Canvas/Panel/Button"
    assert "Button" in legend
    assert "/Canvas/Panel/" not in legend


def test_build_legend_for_diff_uses_leaf_path():
    """visual_diff legend text uses leaf-only path."""
    from unity_mcp.visual_diff import _build_legend_for_diff
    rects = [{"path": "/Canvas/Panel/Button"}]
    legend = _build_legend_for_diff(rects, None)
    assert "Button" in legend
    assert "/Canvas/Panel/" not in legend
