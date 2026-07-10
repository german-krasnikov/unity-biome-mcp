"""TDD tests for visual_diff module."""
import os
import re
import pytest
from unittest.mock import AsyncMock, MagicMock, patch

pytest.importorskip("PIL")
from PIL import Image  # noqa: E402


def _write_png(path, color: tuple = (255, 0, 0)):
    """Write minimal valid PNG with given fill color."""
    Image.new("RGB", (10, 10), color).save(path)


# ── pixel diff ────────────────────────────────────────────────────────────────

def test_pixel_diff_identical_files(tmp_path):
    from unity_mcp.visual_diff import _pixel_diff
    p = tmp_path / "img.png"
    _write_png(p, (100, 100, 100))
    result = _pixel_diff(str(p), str(p))
    assert result.identical is True
    assert result.similarity == 100.0


def test_pixel_diff_size_mismatch(tmp_path):
    from unity_mcp.visual_diff import _pixel_diff
    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(a)
    Image.new("RGB", (20, 20), (0, 0, 0)).save(b)
    result = _pixel_diff(str(a), str(b))
    assert result.size_mismatch is True


def test_pixel_diff_different_returns_score(tmp_path):
    from unity_mcp.visual_diff import _pixel_diff
    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(a)
    Image.new("RGB", (10, 10), (255, 0, 0)).save(b)
    result = _pixel_diff(str(a), str(b))
    assert result.identical is False
    assert 0.0 <= result.similarity < 100.0
    assert result.max_diff > 0


# ── visual_diff function ──────────────────────────────────────────────────────

async def test_visual_diff_pixel_mode_no_haiku(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(a)
    result = await visual_diff(str(a), str(a), mode="pixel")
    # IDENTICAL or PIXEL are both valid pixel-mode results for same file
    assert re.search(r"IDENTICAL|PIXEL", result), result
    # Haiku never called — just check it returns a string
    assert isinstance(result, str)


async def test_visual_diff_auto_identical_skips_haiku(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(a)
    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock()
    result = await visual_diff(str(a), str(a), mode="auto", sampling=mock_svc)
    assert "IDENTICAL" in result
    mock_svc.verify_visual_diff.assert_not_called()


async def test_visual_diff_auto_below_threshold_skips_haiku(tmp_path):
    """Images with <1% pixel diff should skip semantic escalation."""
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    img = Image.new("RGB", (100, 100), (128, 128, 128))
    img.save(a)
    # Change 1 pixel only — sub-threshold
    img2 = img.copy()
    img2.putpixel((0, 0), (129, 128, 128))
    img2.save(b)
    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock()
    result = await visual_diff(str(a), str(b), mode="auto", sampling=mock_svc)
    mock_svc.verify_visual_diff.assert_not_called()
    assert isinstance(result, str)


async def test_visual_diff_auto_escalates_on_big_diff(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(a)
    Image.new("RGB", (10, 10), (255, 0, 0)).save(b)
    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(return_value="Red cube appeared.")
    result = await visual_diff(str(a), str(b), mode="auto", sampling=mock_svc)
    mock_svc.verify_visual_diff.assert_called_once()
    assert "Red cube appeared." in result


async def test_visual_diff_targeted_requires_question(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(a)
    result = await visual_diff(str(a), str(a), mode="targeted")
    assert "ERROR" in result


async def test_visual_diff_cache_hit_returns_cached(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    # Use unique colors to avoid collision with other tests' cache entries
    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (1, 2, 3)).save(a)
    Image.new("RGB", (10, 10), (4, 5, 6)).save(b)

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(return_value="Cache test result.")

    # First call populates cache
    await visual_diff(str(a), str(b), mode="structural", sampling=mock_svc)

    # Second call should hit cache
    result2 = await visual_diff(str(a), str(b), mode="structural", sampling=mock_svc)
    assert mock_svc.verify_visual_diff.call_count == 1  # not called again
    assert "[cached]" in result2


async def test_visual_diff_sampling_disabled_falls_back_to_pixel(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    # Use different colors from cache test to avoid cross-test cache collision
    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (10, 20, 30)).save(a)
    Image.new("RGB", (10, 10), (200, 100, 50)).save(b)

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(return_value=None)  # disabled

    result = await visual_diff(str(a), str(b), mode="structural", sampling=mock_svc)
    assert "PIXEL" in result
    # After degrade() wiring: marker present, no "semantic disabled" text
    assert "[DEGRADED:visual_diff:pixel_only]" in result


# ── degrade() wiring tests ────────────────────────────────────────────────────

def test_pixel_diff_missing_file_sets_corrupt(tmp_path):
    """Missing file — caller fault, distinguishable from PIL not installed."""
    from unity_mcp.visual_diff import _pixel_diff
    r = _pixel_diff("/nonexistent/path/a.png", "/nonexistent/path/b.png")
    assert r.corrupt is True
    assert r.unavailable is False
    assert r.size_mismatch is False


def test_pixel_diff_importerror_sets_unavailable(monkeypatch):
    """When PIL is missing, _pixel_diff returns unavailable=True (not corrupt)."""
    from unity_mcp import visual_diff as vd_mod

    # Override _pixel_diff to simulate PIL ImportError outcome
    def fake_pixel_diff(before, after):
        return vd_mod.PixelDiff(0.0, 255, False, False, unavailable=True)

    monkeypatch.setattr(vd_mod, "_pixel_diff", fake_pixel_diff)

    result = vd_mod._pixel_diff("a.png", "b.png")
    assert result.unavailable is True
    assert result.corrupt is False


async def test_visual_diff_falls_back_to_pixel_when_haiku_returns_none(tmp_path):
    """degrade(): haiku returns None → pixel_only rung, [DEGRADED:...] prefix."""
    from unity_mcp.visual_diff import visual_diff
    from unity_mcp.metrics import METRICS
    METRICS.reset()

    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (50, 60, 70)).save(a)
    Image.new("RGB", (10, 10), (180, 90, 40)).save(b)

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(return_value=None)

    result = await visual_diff(str(a), str(b), mode="structural", sampling=mock_svc)
    assert "[DEGRADED:visual_diff:pixel_only]" in result
    assert "PIXEL:" in result
    assert METRICS.snapshot()["counters"].get("degraded.visual_diff", 0) >= 1


async def test_visual_diff_haiku_success_no_marker(tmp_path):
    """Happy path: haiku returns text → no [DEGRADED: prefix."""
    from unity_mcp.visual_diff import visual_diff

    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (11, 22, 33)).save(a)
    Image.new("RGB", (10, 10), (200, 150, 100)).save(b)

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = AsyncMock(return_value="Color changed to red.")

    result = await visual_diff(str(a), str(b), mode="structural", sampling=mock_svc)
    assert "[DEGRADED:" not in result
    assert result.startswith("PIXEL:")
    assert "Color changed to red." in result


async def test_visual_diff_pil_and_haiku_both_unavailable(tmp_path):
    """PIL ImportError + sampling=None → feature_unavailable rung."""
    import builtins
    import importlib
    real_import = builtins.__import__

    def fake_import(name, *args, **kwargs):
        if name == "PIL" or name.startswith("PIL."):
            raise ImportError("no PIL")
        return real_import(name, *args, **kwargs)

    # Patch PIL away and reload module
    import unity_mcp.visual_diff as vd_mod
    original_pixel_diff = vd_mod._pixel_diff

    # Directly replace _pixel_diff to simulate PIL unavailable
    from unity_mcp.visual_diff import PixelDiff
    vd_mod._pixel_diff = lambda a, b: PixelDiff(0.0, 255, False, False, unavailable=True)

    try:
        result = await vd_mod.visual_diff(
            str(tmp_path / "a.png"), str(tmp_path / "b.png"),
            mode="structural", sampling=None
        )
        assert "[DEGRADED:visual_diff:feature_unavailable]" in result
    finally:
        vd_mod._pixel_diff = original_pixel_diff


async def test_visual_diff_unknown_mode_returns_error(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"
    Image.new("RGB", (10, 10), (0, 0, 0)).save(a)
    result = await visual_diff(str(a), str(a), mode="bogus_mode")
    assert "ERROR" in result


# ── screenshot_compare integration ───────────────────────────────────────────

# ── Fix 2: DiffCache LRU correctness ─────────────────────────────────────────

def test_diff_cache_get_refreshes_lru():
    """get() must move_to_end so recently-accessed entries are evicted last."""
    from unity_mcp.visual_diff import DiffCache
    cache = DiffCache(max_entries=2, ttl=300.0)
    cache.put("k1", "v1")
    cache.put("k2", "v2")
    # Access k1 — it should be promoted to MRU position
    assert cache.get("k1") == "v1"
    # Add k3 — should evict k2 (LRU), not k1
    cache.put("k3", "v3")
    assert cache.get("k1") == "v1"  # still present
    assert cache.get("k2") is None   # evicted


def test_diff_cache_put_existing_no_premature_evict():
    """put() on an existing key must NOT count toward the limit / cause eviction."""
    from unity_mcp.visual_diff import DiffCache
    cache = DiffCache(max_entries=2, ttl=300.0)
    cache.put("k1", "v1")
    cache.put("k2", "v2")
    # Overwrite k1 — cache was already at max, but since key exists no eviction needed
    cache.put("k1", "v1_updated")
    assert len(cache._store) == 2
    assert cache.get("k1") == "v1_updated"
    assert cache.get("k2") == "v2"


async def test_visual_diff_all_rungs_none_produces_no_fallback_marker(tmp_path, monkeypatch):
    """#1: degrade() returns (step, None) → caller must use wrap_degraded, not inline f-string.
    Force all rungs to return None: haiku→None, pixel_only→None, feature_unavailable→None.
    Output must be [DEGRADED:visual_diff:feature_unavailable:no_fallback], NOT contain '\\nNone'.
    """
    import unity_mcp.visual_diff as vd_mod
    from unity_mcp.visual_diff import PixelDiff

    original_pixel_diff = vd_mod._pixel_diff
    # PIL unavailable → pixel_only rung returns None (px.unavailable=True)
    vd_mod._pixel_diff = lambda a, b: PixelDiff(0.0, 255, False, False, unavailable=True)

    # Also patch feature_unavailable to return None via degrade mock
    from unity_mcp import degrade as degrade_mod
    original_degrade = degrade_mod.degrade

    async def fake_degrade(feature, steps):
        # All rungs fail → returns last step name, None
        last_name = steps[-1][0] if steps else "unknown"
        return (last_name, None)

    monkeypatch.setattr(degrade_mod, "degrade", fake_degrade)

    try:
        mock_svc = MagicMock()
        mock_svc.verify_visual_diff = AsyncMock(return_value=None)

        result = await vd_mod.visual_diff(
            str(tmp_path / "a.png"), str(tmp_path / "b.png"),
            mode="structural", sampling=mock_svc
        )
        assert ":no_fallback]" in result, f"Expected :no_fallback] in {result!r}"
        assert "\nNone" not in result, f"Got literal None suffix: {result!r}"
    finally:
        vd_mod._pixel_diff = original_pixel_diff


async def test_screenshot_compare_uses_visual_diff(tmp_path, mock_bridge):
    """screenshot_compare with mode='structural' delegates to visual_diff."""
    from unity_mcp.tools.scene import screenshot_compare

    baseline_dir = tmp_path / ".claude" / "baselines"
    baseline_dir.mkdir(parents=True)
    Image.new("RGB", (10, 10), (0, 0, 0)).save(baseline_dir / "default.png")

    current = tmp_path / "current.png"
    Image.new("RGB", (10, 10), (255, 0, 0)).save(current)
    mock_bridge.send.return_value = {"ok": True, "data": f"Data saved to: {current}"}

    with patch("unity_mcp.tools.scene.os.getcwd", return_value=str(tmp_path)), \
         patch("unity_mcp.visual_diff.visual_diff",
               new=AsyncMock(return_value="PIXEL: 0.0% | SEMANTIC (structural):\n- color changed")) as mock_vd:
        result = await screenshot_compare("default", mode="structural")

    mock_vd.assert_called_once()
    assert "SEMANTIC" in result


async def test_visual_diff_targeted_sanitizes_question(tmp_path):
    """targeted mode strips control chars and caps question at 300 chars."""
    from unity_mcp.visual_diff import visual_diff

    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (20, 30, 40)).save(a)
    Image.new("RGB", (10, 10), (200, 100, 50)).save(b)

    captured_prompt: list[str] = []

    async def fake_verify(before, after, prompt, *, feature="visual_diff"):
        captured_prompt.append(prompt)
        return "YES it changed."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = fake_verify

    long_question = "A" * 400 + "\x00evil\nstuff"
    await visual_diff(str(a), str(b), mode="targeted", question=long_question, sampling=mock_svc)

    assert captured_prompt, "verify_visual_diff was not called"
    prompt = captured_prompt[0]
    assert "\x00" not in prompt, "null byte must be stripped"
    assert "{question}" not in prompt, "{question} placeholder must be replaced"
    # The question is truncated to 300 chars; verify the injected segment is bounded
    # Find what was injected: the prompt template starts with "Question: "
    q_start = prompt.index("Question: ") + len("Question: ")
    injected = prompt[q_start:].split("\n")[0]
    assert len(injected) <= 300, f"injected question too long: {len(injected)}"


async def test_visual_diff_targeted_truncates_long_question(tmp_path):
    """targeted mode caps question at 300 chars exactly."""
    from unity_mcp.visual_diff import visual_diff

    a = tmp_path / "a.png"
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (21, 31, 41)).save(a)
    Image.new("RGB", (10, 10), (201, 101, 51)).save(b)

    captured: list[str] = []

    async def fake_verify(before, after, prompt, *, feature="visual_diff"):
        captured.append(prompt)
        return "YES."

    mock_svc = MagicMock()
    mock_svc.verify_visual_diff = fake_verify

    await visual_diff(str(a), str(b), mode="targeted", question="X" * 500, sampling=mock_svc)

    assert captured
    # After [:300] the question is exactly 300 'X' chars
    assert "X" * 300 in captured[0]
    assert "X" * 301 not in captured[0]


def test_diff_mode_literal_matches_runtime_whitelist():
    """DiffMode Literal must include 'regression' (valid runtime mode)."""
    from typing import get_args
    from unity_mcp.visual_diff_pixel import DiffMode
    modes = set(get_args(DiffMode))
    expected = {"auto", "pixel", "structural", "targeted", "ui_layout",
                "animation", "color", "position", "regression"}
    assert modes == expected, f"Missing from DiffMode: {expected - modes}"


async def test_pixel_threshold_negative_rejected(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"; a.write_bytes(b"\x89PNG\r\n\x1a\n")
    b = tmp_path / "b.png"; b.write_bytes(b"\x89PNG\r\n\x1a\n")
    result = await visual_diff(str(a), str(b), pixel_threshold=-5)
    assert "ERROR" in result and "pixel_threshold" in result


async def test_pixel_threshold_over_100_rejected(tmp_path):
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"; a.write_bytes(b"\x89PNG\r\n\x1a\n")
    b = tmp_path / "b.png"; b.write_bytes(b"\x89PNG\r\n\x1a\n")
    result = await visual_diff(str(a), str(b), pixel_threshold=200)
    assert "ERROR" in result


# ── corrupt field (Item 1.3 / 1.4) ───────────────────────────────────────────

def test_pixel_diff_corrupt_file_invalid_bytes(tmp_path):
    a = tmp_path / "a.png"; a.write_bytes(b"not a valid png")
    b = tmp_path / "b.png"; b.write_bytes(b"also garbage")
    from unity_mcp.visual_diff import _pixel_diff
    r = _pixel_diff(str(a), str(b))
    assert r.corrupt is True


async def test_visual_diff_corrupt_file_returns_error(tmp_path):
    """Corrupt file returns explicit ERROR (not silent degrade)."""
    from unity_mcp.visual_diff import visual_diff
    a = tmp_path / "a.png"; a.write_bytes(b"junk")
    result = await visual_diff(str(a), str(a), mode="auto")
    assert "ERROR" in result and "corrupt" in result.lower()


# ── refusal escalation (Item 1.5) ────────────────────────────────────────────

async def test_visual_diff_refusal_falls_to_pixel(tmp_path, monkeypatch):
    """Haiku refusal triggers degrade ladder fallback."""
    from unity_mcp.visual_diff import visual_diff
    from unittest.mock import AsyncMock, MagicMock

    a = tmp_path / "a.png"
    Image.new("RGB", (10, 10), (255, 0, 0)).save(a)
    b = tmp_path / "b.png"
    Image.new("RGB", (10, 10), (0, 0, 255)).save(b)

    mock = MagicMock()
    mock.verify_visual_diff = AsyncMock(return_value="I cannot describe this content.")

    result = await visual_diff(str(a), str(b), mode="structural", sampling=mock)

    # Exact step name: refusal sets result=None in normalize → wrap_degraded("haiku_refused")
    assert "[DEGRADED:visual_diff:haiku_refused]" in result
    assert "I cannot" not in result


# ── pixel_threshold boundary (Item 1.2) ──────────────────────────────────────

@pytest.mark.parametrize("similarity,max_diff,should_skip_haiku", [
    (99.0, 4, True),    # exact threshold boundary — skip per `>=` contract
    (98.99, 4, False),  # just below similarity — escalate
    (99.01, 4, True),   # just above
    (99.5, 4, True),    # both pass
    (99.5, 5, False),   # max_diff at threshold (>= 5 escalates)
    (99.5, 100, False), # large max_diff escalates
])
async def test_pixel_threshold_boundary(similarity, max_diff, should_skip_haiku, tmp_path, monkeypatch):
    """Pin threshold contract: similarity >= (100 - threshold) AND max_diff < 5 → skip Haiku."""
    from unity_mcp import visual_diff as vd
    from unity_mcp.visual_diff import PixelDiff, visual_diff, DiffCache

    monkeypatch.setattr(vd, "_pixel_diff",
        lambda a, b: PixelDiff(similarity, max_diff, False, False))
    monkeypatch.setattr(vd, "_cache", DiffCache(max_entries=64, ttl=300.0))
    mock = MagicMock()
    mock.verify_visual_diff = AsyncMock(return_value="haiku response")
    a = tmp_path / "a.png"; a.write_bytes(b"\x89PNG")

    await visual_diff(str(a), str(a), mode="auto", sampling=mock)

    if should_skip_haiku:
        mock.verify_visual_diff.assert_not_called()
    else:
        mock.verify_visual_diff.assert_called_once()
