"""TDD integration tests for visual_diff + SoM mark=True."""
import os
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

pytest.importorskip("PIL")
from PIL import Image  # noqa: E402


def _write_png(path, color=(100, 100, 100), size=(100, 100)):
    Image.new("RGB", size, color).save(path)


MOCK_RECTS = [
    {"path": "/Canvas/Btn", "x": 10, "y": 10, "w": 80, "h": 40},
    {"path": "/Canvas/Title", "x": 10, "y": 60, "w": 200, "h": 30},
]


# ── visual_diff mark=True ─────────────────────────────────────────────────────

async def test_visual_diff_mark_true_legend_in_prompt(tmp_path):
    """mark=True: prompt passed to Haiku contains 'Legend:' and 'element'."""
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "before.png"
    b = tmp_path / "after.png"
    _write_png(a, (0, 0, 0))
    _write_png(b, (255, 0, 0))

    captured_prompts = []

    async def mock_verify(before, after, prompt, **kwargs):
        captured_prompts.append(prompt)
        return "Color changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    result = await visual_diff(
        str(a), str(b), mode="structural", sampling=mock_svc,
        mark=True, rects=MOCK_RECTS
    )
    assert len(captured_prompts) == 1
    prompt = captured_prompts[0]
    assert "Legend:" in prompt
    assert "element" in prompt.lower() or "Btn" in prompt


async def test_visual_diff_mark_same_path_same_index(tmp_path):
    """Same path → same index on before AND after frames."""
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "before.png"
    b = tmp_path / "after.png"
    _write_png(a, (0, 0, 50))
    _write_png(b, (0, 50, 0))

    captured_prompts = []

    async def mock_verify(before, after, prompt, **kwargs):
        captured_prompts.append(prompt)
        return "Changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    rects_before = [{"path": "/UI/A", "x": 0, "y": 0, "w": 50, "h": 50}]
    rects_after = [{"path": "/UI/A", "x": 5, "y": 0, "w": 50, "h": 50}]  # same path, moved

    await visual_diff(
        str(a), str(b), mode="structural", sampling=mock_svc,
        mark=True, rects=rects_before, rects_after=rects_after
    )
    # Both before and after use same index for /UI/A
    prompt = captured_prompts[0]
    # The legend should reference the leaf "A" (from /UI/A) with N=A format
    assert any(f"{n}=A" in prompt for n in range(1, 32)), \
        f"Expected 'N=A' in prompt but got: {prompt}"


async def test_visual_diff_mark_false_no_legend(tmp_path):
    """mark=False (default): prompt does NOT contain 'Legend:'."""
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "before2.png"
    b = tmp_path / "after2.png"
    _write_png(a, (5, 5, 5))
    _write_png(b, (200, 10, 10))

    captured_prompts = []

    async def mock_verify(before, after, prompt, **kwargs):
        captured_prompts.append(prompt)
        return "Changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    await visual_diff(str(a), str(b), mode="structural", sampling=mock_svc)
    assert "Legend:" not in captured_prompts[0]


async def test_visual_diff_mark_empty_rects_no_legend(tmp_path):
    """mark=True but rects=[] → no Legend injected, fallback prompt."""
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "e_before.png"
    b = tmp_path / "e_after.png"
    _write_png(a, (20, 20, 20))
    _write_png(b, (180, 20, 20))

    captured_prompts = []

    async def mock_verify(before, after, prompt, **kwargs):
        captured_prompts.append(prompt)
        return "Changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    await visual_diff(str(a), str(b), mode="structural", sampling=mock_svc,
                      mark=True, rects=[])
    assert "Legend:" not in captured_prompts[0]


# ── screenshot_describe mark=True ─────────────────────────────────────────────

async def test_describer_mark_true_uses_som_prompt(tmp_path):
    """ScreenshotDescriber with mark=True uses SoM prompt variant."""
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber
    from unity_mcp.screenshot_describe.cache import FingerprintCache

    img_path = tmp_path / "scene.png"
    _write_png(img_path)

    captured = []

    async def mock_describe(prompt, path, max_tokens=150):
        captured.append(prompt)
        return "SoM description."

    mock_svc = MagicMock()
    mock_svc.describe_image = AsyncMock(side_effect=mock_describe)

    describer = ScreenshotDescriber(mock_svc, FingerprintCache())
    result = await describer.describe(
        str(img_path), "auto", None,
        mark=True, rects=MOCK_RECTS, legend="1=/Canvas/Btn 2=/Canvas/Title"
    )
    assert result == "SoM description."
    assert len(captured) == 1
    assert "Legend:" in captured[0] or "mark" in captured[0].lower()


async def test_describer_mark_false_no_som_prompt(tmp_path):
    """ScreenshotDescriber with mark=False uses standard prompt."""
    from unity_mcp.screenshot_describe.describer import ScreenshotDescriber
    from unity_mcp.screenshot_describe.cache import FingerprintCache

    img_path = tmp_path / "scene2.png"
    _write_png(img_path)

    captured = []

    async def mock_describe(prompt, path, max_tokens=150):
        captured.append(prompt)
        return "Standard description."

    mock_svc = MagicMock()
    mock_svc.describe_image = AsyncMock(side_effect=mock_describe)

    describer = ScreenshotDescriber(mock_svc, FingerprintCache())
    result = await describer.describe(str(img_path), "auto", None)
    assert result == "Standard description."
    assert "Legend:" not in captured[0]


# ── budget registry ───────────────────────────────────────────────────────────

def test_som_visual_in_budget_registry():
    """'som_visual' feature registered with image=True."""
    from unity_mcp.budget.registry import get_feature
    meta = get_feature("som_visual")
    assert meta.image is True
    assert meta.est_in >= 1700
    assert meta.est_out >= 200


async def test_diff_annotate_cleanup_after_completion(tmp_path):
    """Verify tmp dir lives until verify_visual_diff completes."""
    from unity_mcp.som.diff_annotate import diff_with_annotation

    before = tmp_path / "before.png"
    after = tmp_path / "after.png"
    Image.new("RGB", (50, 50), (0, 0, 0)).save(str(before))
    Image.new("RGB", (50, 50), (255, 0, 0)).save(str(after))
    rects = [{"path": "/Btn", "x": 5, "y": 5, "w": 20, "h": 10}]

    tmp_paths_at_call = []

    async def mock_verify(b, a, prompt, *, feature="visual_diff", **kw):
        # Verify both annotated files exist while sampling is invoked
        tmp_paths_at_call.append((os.path.exists(b), os.path.exists(a)))
        return "OK"

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(side_effect=mock_verify)

    result = await diff_with_annotation(
        str(before), str(after), rects, None, "prompt", mock_svc, "som_visual"
    )

    assert result == "OK"
    assert len(tmp_paths_at_call) == 1
    before_exists, after_exists = tmp_paths_at_call[0]
    assert before_exists, "annotated before.png gone before verify_visual_diff returned"
    assert after_exists, "annotated after.png gone before verify_visual_diff returned"


async def test_visual_diff_mark_subset_after_same_index(tmp_path):
    """REGRESSION: before has /X /Y /Z, after only /X /Z (drops /Y).

    sha256 sort order is [/Y, /X, /Z], so pool indices are 1=Y 2=X 3=Z.
    Without pool propagation to annotate():
      after solo-mode assigns 1=X 2=Z  ← /X gets index 1 instead of 2 — BROKEN

    With fix: both frames use the same pool, so /X=2 in both.
    """
    from unity_mcp.som.overlay import annotate

    # sha256 sort order: /Y < /X < /Z  (verified: pool=[/Y, /X, /Z])
    before_rects = [
        {"path": "/X", "x": 10,  "y": 10, "w": 50, "h": 50},
        {"path": "/Y", "x": 100, "y": 10, "w": 50, "h": 50},
        {"path": "/Z", "x": 200, "y": 10, "w": 50, "h": 50},
    ]
    after_rects = [
        # /Y removed — after is a strict subset
        {"path": "/X", "x": 10,  "y": 10, "w": 50, "h": 50},
        {"path": "/Z", "x": 200, "y": 10, "w": 50, "h": 50},
    ]

    img = Image.new("RGB", (300, 100), "white")

    import hashlib
    pool = sorted(
        {r["path"] for r in before_rects} | {r["path"] for r in after_rects},
        key=lambda p: hashlib.sha256(p.encode()).hexdigest(),
    )

    # With fix: pass pool to both annotate calls
    _, legend_before = annotate(img.copy(), before_rects, path_pool=pool)
    _, legend_after = annotate(img.copy(), after_rects, path_pool=pool)

    def index_of_leaf(legend: str, leaf: str) -> int:
        for entry in legend.split():
            if "=" in entry:
                idx, name = entry.split("=", 1)
                if name == leaf:
                    return int(idx)
        return -1

    idx_x_before = index_of_leaf(legend_before, "X")
    idx_x_after = index_of_leaf(legend_after, "X")

    assert idx_x_before == idx_x_after, \
        f"/X index unstable: before={idx_x_before}, after={idx_x_after}"
    assert idx_x_before > 0, "Legend missing /X entry"

    # Also verify solo-mode WOULD give a different answer (non-vacuous proof)
    _, legend_solo_after = annotate(img.copy(), after_rects, path_pool=None)
    idx_x_solo = index_of_leaf(legend_solo_after, "X")
    assert idx_x_solo != idx_x_before, \
        "Expected solo-mode to produce different index (test is vacuous without fix)"
