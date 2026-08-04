import hashlib
import time

from .metrics import METRICS
from .sampling import SamplingService
from .sampling_postproc import normalize
from .visual_diff_pixel import DIFF_PROMPTS, DiffMode, PixelDiff, _format_pixel, _pixel_diff

# Re-exports for backward compatibility
__all__ = [
    "PixelDiff", "_pixel_diff", "_format_pixel",
    "DiffMode", "DIFF_PROMPTS", "DiffCache", "visual_diff",
    "_build_som_prompt", "_build_legend_for_diff",
]


class DiffCache:
    """In-memory cache keyed by (before_hash, after_hash, prompt_hash) with TTL."""

    def __init__(self, max_entries: int = 64, ttl: float = 300.0):
        from collections import OrderedDict
        self._store: OrderedDict[str, tuple[str, float]] = OrderedDict()
        self._max = max_entries
        self._ttl = ttl

    def _hash_file(self, path: str) -> str:
        try:
            with open(path, "rb") as f:
                return hashlib.sha256(f.read()).hexdigest()[:16]
        except Exception:
            return path

    def key(self, before: str, after: str, prompt: str) -> str:
        return (f"{self._hash_file(before)}:{self._hash_file(after)}:"
                f"{hashlib.md5(prompt.encode()).hexdigest()[:8]}")

    def get(self, key: str) -> str | None:
        entry = self._store.get(key)
        if not entry:
            METRICS.inc("diffcache.miss")
            return None
        value, ts = entry
        if time.time() - ts > self._ttl:
            del self._store[key]
            METRICS.inc("diffcache.miss")
            return None
        self._store.move_to_end(key)
        METRICS.inc("diffcache.hit")
        return value

    def put(self, key: str, value: str) -> None:
        if key in self._store:
            self._store.move_to_end(key)
        self._store[key] = (value, time.time())
        while len(self._store) > self._max:
            self._store.popitem(last=False)


_cache = DiffCache()


def _build_som_prompt(base_prompt: str, legend: str) -> str:
    """Inject SoM legend into prompt if legend has content."""
    if not legend or legend == "(no marks)":
        return base_prompt
    return (
        f"{base_prompt}\n\n"
        f"Numbered elements on image — Legend: {legend}\n"
        "Reference elements by number when describing changes."
    )


def _build_legend_for_diff(
    rects: list[dict],
    rects_after: list[dict] | None,
) -> str:
    """Build a combined legend with stable indices for before+after frames."""
    from .som.extract import assign_indices, build_path_pool
    from .som.overlay import _leaf
    if not rects and not rects_after:
        return "(no marks)"
    # Build canonical pool — union of both frames, capped at 30 for small indices
    pool = build_path_pool(rects or [], rects_after)
    # Use single merged rect list with paired pool for stable indices
    merged = [{"path": p} for p in pool]
    indexed = assign_indices(merged, path_pool=pool)
    # Use leaf-only path — saves ~200 tokens per call; sanitize \n/\r
    return " ".join(f"{i}={_leaf(r['path'])}" for i, r in indexed)


async def visual_diff(before: str, after: str, *, mode: str = "auto",
                      question: str | None = None,
                      pixel_threshold: float = 1.0,
                      sampling: SamplingService | None = None,
                      mark: bool = False,
                      rects: list | None = None,
                      rects_after: list | None = None) -> str:
    """Three-tier visual diff: pixel -> structural -> targeted (Haiku).

    mark=True: annotate both frames with SoM numbered overlays and inject
    legend into the prompt. Requires rects (list of {path,x,y,w,h}).
    rects_after defaults to rects if not provided (stable indices across frames).
    """
    if not 0 <= pixel_threshold <= 100:
        return f"ERROR: pixel_threshold must be in [0, 100], got {pixel_threshold}"

    px = _pixel_diff(before, after)

    if px.corrupt:
        return "ERROR: image file unreadable or corrupt"

    if mode == "pixel":
        return _format_pixel(px)

    if mode == "targeted" and not question:
        return "ERROR: targeted mode requires question="

    if mode not in ("auto", "structural", "targeted", "ui_layout",
                    "animation", "color", "position", "regression"):
        return f"ERROR: unknown mode {mode}"

    if mode == "auto":
        if px.identical:
            return "IDENTICAL (pixel)"
        if px.similarity >= (100 - pixel_threshold) and px.max_diff < 5:
            return f"NEAR_IDENTICAL: {px.similarity:.1f}% (sub-threshold, skipped semantic)"
        mode = "structural"

    prompt_key = "general" if mode == "structural" else mode
    if prompt_key not in DIFF_PROMPTS:
        return f"ERROR: unknown mode {mode}"

    prompt = DIFF_PROMPTS[prompt_key]
    if mode == "targeted":
        # Sanitize: cap length, strip control chars, prevent .format() crash via .replace()
        import re as _re
        safe_q = _re.sub(r"[\x00-\x1f]", " ", str(question))[:300].strip()
        prompt = prompt.replace("{question}", safe_q)

    # SoM: annotate images + inject legend into prompt
    som_active = mark and rects is not None
    if som_active:
        legend = _build_legend_for_diff(rects, rects_after)
        prompt = _build_som_prompt(prompt, legend)

    sampling = sampling or SamplingService()
    cache_key = _cache.key(before, after, prompt)
    cached = _cache.get(cache_key)
    if cached:
        return f"[cached]\n{cached}"

    from .degrade import degrade
    feature = "som_visual" if som_active else "visual_diff"

    if som_active:
        from .som.diff_annotate import diff_with_annotation
        result = await diff_with_annotation(before, after, rects, rects_after, prompt, sampling, feature)
        if not result:
            return _format_pixel(px) + "\n(semantic disabled: set UNITY_MCP_VISUAL_VERIFY=1)"
        _cache.put(cache_key, result)
        return f"PIXEL: {px.similarity:.1f}% | SEMANTIC ({mode}):\n{result}"

    if mode == "targeted":
        step_name, result = await degrade(feature, [
            ("haiku_targeted", lambda: sampling.verify_visual_diff(before, after, prompt, feature=feature)),
            ("pixel_only_targeted", lambda: _format_pixel(px) if not px.unavailable else None),
            ("feature_unavailable", lambda: "PIXEL: unavailable | SEMANTIC: disabled"),
        ])
    else:
        step_name, result = await degrade(feature, [
            ("haiku_two_image", lambda: sampling.verify_visual_diff(before, after, prompt, feature=feature)),
            ("pixel_only", lambda: _format_pixel(px) if not px.unavailable else None),
            ("feature_unavailable", lambda: "PIXEL: unavailable | SEMANTIC: disabled"),
        ])

    first_step = "haiku_targeted" if mode == "targeted" else "haiku_two_image"
    if step_name != first_step:
        from .degrade import wrap_degraded
        return wrap_degraded(feature, step_name, result)

    result, refused = normalize(result, "verdict")
    if refused or result is None:
        from .degrade import wrap_degraded
        return wrap_degraded(feature, "haiku_refused", _format_pixel(px) + "\n(semantic refused)")

    _cache.put(cache_key, result)
    return f"PIXEL: {px.similarity:.1f}% | SEMANTIC ({mode}):\n{result}"
