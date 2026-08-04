"""Pillow-based Set-of-Mark overlay renderer.

Draws numbered circles at top-left of each rect, 2px stroke box,
8-color palette cycling by index. Collision avoidance via diagonal push.
"""
from __future__ import annotations

import re

from PIL import Image, ImageDraw, ImageFont

LABEL_R = 11        # circle radius px
STROKE_W = 2        # rect stroke width
MIN_DIST = LABEL_R * 2  # min center-to-center distance

# 8-color palette (colorblind-friendly, high-contrast on grey backgrounds)
PALETTE = [
    (220, 50,  50),   # red
    (50,  150, 220),  # blue
    (50,  200, 80),   # green
    (240, 170, 30),   # orange
    (170, 70,  220),  # purple
    (30,  210, 200),  # teal
    (220, 100, 180),  # pink
    (120, 90,  50),   # brown
]


def _index_color(index: int) -> tuple[int, int, int]:
    """1-based index → palette color, cycling every 8."""
    return PALETTE[(index - 1) % len(PALETTE)]


def _load_font(size: int = 14) -> ImageFont.ImageFont | None:
    try:
        return ImageFont.truetype("DejaVuSans.ttf", size)
    except Exception:
        pass
    try:
        # Pillow 10+ accepts size=; Pillow <10 raises TypeError
        return ImageFont.load_default(size=size)
    except Exception:
        pass
    try:
        return ImageFont.load_default()
    except Exception:
        return None


def _compute_centers(rects: list[dict]) -> list[tuple[int, int]]:
    """Initial label center = top-left corner of each rect."""
    return [(r.get("x", 0) + LABEL_R, r.get("y", 0) + LABEL_R) for r in rects]


def _resolve_collisions(centers: list[tuple[int, int]], max_iter: int = 16) -> list[tuple[int, int]]:
    # TODO: implement leader-line fallback for >16-iter dense clusters
    """Push overlapping label centers along diagonal, max_iter passes."""
    pts = list(centers)
    for _ in range(max_iter):
        changed = False
        for i in range(len(pts)):
            for j in range(i + 1, len(pts)):
                cx1, cy1 = pts[i]
                cx2, cy2 = pts[j]
                dist = ((cx1 - cx2) ** 2 + (cy1 - cy2) ** 2) ** 0.5
                if dist < MIN_DIST and dist > 0:
                    # push smaller index (i) along diagonal
                    dx = (cx1 - cx2) / dist
                    dy = (cy1 - cy2) / dist
                    gap = (MIN_DIST - dist) / 2 + 1
                    pts[i] = (int(cx1 + dx * gap), int(cy1 + dy * gap))
                    pts[j] = (int(cx2 - dx * gap), int(cy2 - dy * gap))
                    changed = True
                elif dist == 0:
                    # coincident — push diagonally
                    pts[i] = (cx1 + LABEL_R, cy1 - LABEL_R)
                    pts[j] = (cx2 - LABEL_R, cy2 + LABEL_R)
                    changed = True
        if not changed:
            break
    return pts


_LEAF_STRIP = re.compile(r'[\x00-\x1f"`{}]')


def _leaf(path: str) -> str:
    """Return leaf path component, sanitized — saves ~200 tokens per call."""
    leaf = path.rsplit("/", 1)[-1] if "/" in path else path
    leaf = _LEAF_STRIP.sub("", leaf)
    leaf = leaf.replace("Legend:", "")
    return leaf


def _build_legend(indexed: list[tuple[int, dict]]) -> str:
    """Build plain-text legend string: '1=Btn 2=Title ...' (leaf names only)."""
    return " ".join(f"{idx}={_leaf(r.get('path', '?'))}" for idx, r in indexed)


def annotate(
    img: Image.Image,
    rects: list[dict],
    path_pool: list | None = None,
) -> tuple[Image.Image, str]:
    """Draw numbered rect overlays on img. Returns (annotated_img, legend).

    If rects is empty, returns img unchanged and legend='(no marks)'.
    Rects must be pre-filtered (extract_rects / assign_indices not called here).

    path_pool: if provided, indices come from canonical sorted union.
               Use union(before_paths, after_paths) for paired diff annotations.
               If None, falls back to solo mode (paths from rects).
    """
    if not rects:
        return img, "(no marks)"

    from .extract import assign_indices
    indexed = assign_indices(rects, path_pool=path_pool)

    draw = ImageDraw.Draw(img)
    font = _load_font(14)

    # Compute initial label centers and resolve collisions
    centers = _compute_centers([r for _, r in indexed])
    centers = _resolve_collisions(centers)

    for (idx, rect), (cx, cy) in zip(indexed, centers, strict=False):
        color = _index_color(idx)
        x, y, w, h = rect.get("x", 0), rect.get("y", 0), rect.get("w", 0), rect.get("h", 0)

        # Draw 2px stroke rect (no fill)
        for s in range(STROKE_W):
            draw.rectangle(
                [x + s, y + s, x + w - s - 1, y + h - s - 1],
                outline=color,
            )

        # Draw opaque circle label
        draw.ellipse(
            [cx - LABEL_R, cy - LABEL_R, cx + LABEL_R, cy + LABEL_R],
            fill=color,
            outline=(255, 255, 255),
        )
        # White digit centered in circle — skip silently if font totally unavailable
        label = str(idx)
        try:
            if font is not None:
                try:
                    bbox = font.getbbox(label)
                    tw, th = bbox[2] - bbox[0], bbox[3] - bbox[1]
                except AttributeError:
                    tw, th = 8, 10
                draw.text((cx - tw // 2, cy - th // 2), label, fill=(255, 255, 255), font=font)
            else:
                # No font at all — draw text without font kwarg (Pillow uses internal default)
                tw, th = 8, 10
                draw.text((cx - tw // 2, cy - th // 2), label, fill=(255, 255, 255))
        except Exception:
            pass  # font completely unavailable — circle drawn, digit skipped

    legend = _build_legend(indexed)
    return img, legend
