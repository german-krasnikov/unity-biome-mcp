"""Parse and filter rects from Unity screenshot payload.

TODO: Unity plugin will populate 'rects' field in screenshot response in a
follow-up phase. For now, Python-side works with rects passed directly.
"""
import hashlib
from typing import Any

MIN_SIZE = 12  # px — filter out tiny rects below this threshold


def parse_rects(payload: dict[str, Any]) -> list[dict]:
    """Extract rects list from Unity response payload. Returns [] if absent."""
    return payload.get("rects", [])


def extract_rects(
    rects: list[dict],
    img_w: int,
    img_h: int,
    top_k: int = 30,
) -> list[dict]:
    """Viewport-cull, min-size filter, sort by area desc, cap at top_k."""
    filtered = []
    for r in rects:
        x, y, w, h = r.get("x", 0), r.get("y", 0), r.get("w", 0), r.get("h", 0)
        # viewport cull — rect must overlap [0, img_w) x [0, img_h)
        if x + w <= 0 or y + h <= 0 or x >= img_w or y >= img_h:
            continue
        # min-size filter
        if w < MIN_SIZE or h < MIN_SIZE:
            continue
        filtered.append(r)
    # sort by area desc
    filtered.sort(key=lambda r: r.get("w", 0) * r.get("h", 0), reverse=True)
    return filtered[:top_k]


def assign_indices(
    rects: list[dict],
    path_pool: list | None = None,
) -> list[tuple[int, dict]]:
    """Assign stable 1-based indices sorted by hash(path).

    path_pool: if provided, indices come from canonical sorted set.
               Use union(before_paths, after_paths) for paired diff calls.
               If None, falls back to paths from rects (single-frame mode).
    """
    def _key(p: str) -> str:
        return hashlib.sha256(p.encode()).hexdigest()

    if path_pool is None:
        # Solo mode — current behavior preserved
        sorted_rects = sorted(rects, key=lambda r: _key(r.get("path", "")))
        return [(i + 1, r) for i, r in enumerate(sorted_rects)]

    # Paired mode — index = position in canonical pool
    pool_sorted = sorted(set(path_pool), key=_key)
    idx_of = {p: i + 1 for i, p in enumerate(pool_sorted)}
    out = [(idx_of[r.get("path", "")], r) for r in rects if r.get("path", "") in idx_of]
    out.sort(key=lambda t: t[0])
    return out


def build_path_pool(
    rects: list[dict],
    rects_after: list[dict] | None = None,
    cap: int = 30,
) -> list[str]:
    """Canonical SHA-256-sorted union of paths from before+after frames, capped."""
    after = rects_after if rects_after is not None else rects
    paths = (
        {r.get("path") for r in rects if r.get("path")} |
        {r.get("path") for r in after if r.get("path")}
    )
    return sorted(paths, key=lambda p: hashlib.sha256(p.encode()).hexdigest())[:cap]
