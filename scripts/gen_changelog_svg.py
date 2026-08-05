"""Generate docs/assets/changelog.svg from CHANGELOG.md.

Pure stdlib, deterministic, no external deps.
Usage: python scripts/gen_changelog_svg.py   (from repo root)
"""
import html
import pathlib
import re
import sys
from typing import NamedTuple

import changelog_svg_templates as T

# ---------------------------------------------------------------------------
# Data types
# ---------------------------------------------------------------------------

class Version(NamedTuple):
    ver: str
    date: str
    caption: str


class Layout(NamedTuple):
    xs: list[float]
    opacities: list[float]
    ring_counts: list[int]
    packets: list[int]
    begins: list[float]
    viewbox_w: int


# ---------------------------------------------------------------------------
# Parser
# ---------------------------------------------------------------------------

_HEADING_RE = re.compile(
    r'^## \[v([\d.]+)(?:\s*/[^\]]+)?\]\s*—\s*([\d-]*)(?:\s*<!--\s*svg:\s*(.+?)\s*-->)?\s*$'
)
_BOLD_RE = re.compile(r'^\s*-\s*\*\*(.+?)\*\*')
_STOP_RE = re.compile(r'^## Earlier history', re.IGNORECASE)
_VER_HEADING_RE = re.compile(r'^## \[v')


def parse_changelog(text: str) -> list[Version]:
    """Parse CHANGELOG.md text → list of Version, oldest-first."""
    versions: list[Version] = []
    lines = text.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if _STOP_RE.match(line):
            break
        m = _HEADING_RE.match(line)
        if m:
            ver, date, caption_ann = m.group(1), m.group(2), m.group(3)
            caption = caption_ann.strip() if caption_ann else ""
            if not caption:
                j = i + 1
                while j < len(lines) and not _VER_HEADING_RE.match(lines[j]):
                    bm = _BOLD_RE.match(lines[j])
                    if bm:
                        raw = bm.group(1).lower()
                        caption = (raw[:40] + "…") if len(raw) > 40 else raw
                        break
                    j += 1
            versions.append(Version(ver=ver, date=date or "", caption=caption))
        i += 1

    if not versions:
        print("ERROR: no versions found in changelog", file=sys.stderr)
        raise ValueError("no versions found in changelog")

    versions.reverse()  # file is newest-first; return oldest-first
    return versions


# ---------------------------------------------------------------------------
# Layout
# ---------------------------------------------------------------------------

_MAX_NODES = 8


def layout(versions: list[Version]) -> Layout:
    """Compute layout for up to _MAX_NODES versions + 1 future node."""
    n = len(versions)
    total = n + 1

    viewbox_w = max(1200, 120 + total * 180)
    x_left, x_right = 140.0, float(viewbox_w - 120)
    step = (x_right - x_left) / max(n, 1)

    xs = [x_left + i * step for i in range(total)]
    opacities = [0.40 + 0.60 * (i / max(n - 1, 1)) for i in range(n)] + [0.30]
    if n == 1:
        opacities = [1.0, 0.30]

    ring_counts = [3 if op >= 0.75 else 1 for op in opacities[:n]] + [1]
    packets = [3 + round(5 * i / max(n - 1, 1)) for i in range(n)] + [2]
    if n == 1:
        packets = [3, 2]

    begins = [round(0.2 + i * 0.8, 1) for i in range(total)]

    return Layout(xs=xs, opacities=opacities, ring_counts=ring_counts,
                  packets=packets, begins=begins, viewbox_w=viewbox_w)


# ---------------------------------------------------------------------------
# Render helpers
# ---------------------------------------------------------------------------

def _ring(cx: float, cy: float, begin: str) -> str:
    return (
        f'<circle cx="{cx:.1f}" cy="{cy:.1f}" r="14" fill="none" stroke="#3ad29f" stroke-width="2">'
        f'<animate attributeName="r" values="14;46" dur="1.8s" begin="{begin}" repeatCount="indefinite"/>'
        f'<animate attributeName="opacity" values="0.6;0" dur="1.8s" begin="{begin}" repeatCount="indefinite"/>'
        f'<animate attributeName="stroke-width" values="2;0.2" dur="1.8s" begin="{begin}" repeatCount="indefinite"/>'
        f'</circle>'
    )


def _ring_grey(cx: float, cy: float) -> str:
    """Grey dashed expand-ring for the future node."""
    s = f'{cx:.1f}'
    cy_s = f'{cy:.1f}'
    return (
        f'<circle cx="{s}" cy="{cy_s}" r="14" fill="none" stroke="#888899" stroke-width="1.5" stroke-dasharray="3 4">'
        f'<animate attributeName="r" values="14;46" dur="1.8s" repeatCount="indefinite"/>'
        f'<animate attributeName="opacity" values="0.6;0" dur="1.8s" repeatCount="indefinite"/>'
        f'</circle>'
    )


def render_node(x: float, y: float, opacity: float, rings: int, is_supernova: bool) -> str:
    """Render a version node group (rings + halo + core + dot)."""
    cx, cy = f"{x:.1f}", f"{y:.1f}"
    parts = [f'<g opacity="{opacity:.2f}">']
    parts.extend(_ring(x, y, f"{k * 0.6:.1f}s") for k in range(rings))
    if is_supernova:
        parts.append(
            f'<circle cx="{cx}" cy="{cy}" r="14" fill="none" stroke="#e94560" stroke-width="2">'
            f'<animate attributeName="r" values="14;58" dur="3.6s" repeatCount="indefinite"/>'
            f'<animate attributeName="opacity" values="0;0;0.7;0;0" keyTimes="0;0.4;0.5;0.6;1" dur="3.6s" repeatCount="indefinite"/>'
            f'</circle>'
        )
    parts.append(
        f'<circle cx="{cx}" cy="{cy}" r="26" fill="url(#orbHalo)" filter="url(#soft)">'
        f'<animate attributeName="opacity" values="0.5;0.95;0.5" dur="1.8s" repeatCount="indefinite"/>'
        f'</circle>'
    )
    parts.append(
        f'<circle cx="{cx}" cy="{cy}" r="11" fill="url(#orbCore)">'
        f'<animate attributeName="r" values="9;13;9" dur="0.9s" repeatCount="indefinite"/>'
        f'</circle>'
    )
    parts.append(
        f'<circle cx="{cx}" cy="{cy}" r="3" fill="#eafff7">'
        f'<animate attributeName="opacity" values="0.85;1;0.85" dur="0.9s" repeatCount="indefinite"/>'
        f'</circle>'
    )
    parts.append('</g>')
    return "\n".join(parts)


def render_future_node(x: float, y: float) -> str:
    """Render the dashed grey future node."""
    cx, cy = f"{x:.1f}", f"{y:.1f}"
    return (
        f'<g opacity="0.30">'
        f'{_ring_grey(x, y)}'
        f'<circle cx="{cx}" cy="{cy}" r="11" fill="none" stroke="#888899" stroke-width="1.5" stroke-dasharray="3 4">'
        f'<animate attributeName="r" values="9;13;9" dur="0.9s" repeatCount="indefinite"/>'
        f'</circle>'
        f'</g>'
    )


def render_packets(x: float, y: float, count: int, node_idx: int, is_future: bool) -> str:
    """Render TCP packet emitter for a node."""
    color_a = "#888899" if is_future else "#3ad29f"
    color_b = "#888899" if is_future else "#ccccff"
    opacity_max = "0.5" if is_future else "0.7"
    parts: list[str] = []
    for p in range(count):
        dur = 2.6 + (p * 0.4 + node_idx * 0.1) % 1.4
        begin = p * 0.8
        dx = ((-1) ** p) * (3 + (p * 2 + node_idx) % 6)
        color = color_a if p % 2 == 0 else color_b
        parts.append(
            f'<circle cx="{x + dx:.1f}" cy="{y:.1f}" r="1.5" fill="{color}">'
            f'<animate attributeName="opacity" values="0;{opacity_max};0" dur="{dur:.1f}s" begin="{begin:.1f}s" repeatCount="indefinite"/>'
            f'<animateTransform attributeName="transform" type="translate" values="0 0; {dx} -120; {dx+1} -130"'
            f' dur="{dur:.1f}s" begin="{begin:.1f}s" repeatCount="indefinite"/>'
            f'</circle>'
        )
    return "\n".join(parts)


def render_label(x: float, y: float, ver: str, caption: str, begin: float, is_future: bool) -> str:
    """Render typewriter version label + caption sub-label."""
    ver_color = "#888899" if is_future else "#ccccff"
    begin_s = f"{begin:.1f}s"
    label = html.escape(ver if is_future else f"v{ver}")
    sub = html.escape("roadmap" if is_future else caption)
    return (
        f'<text x="{x:.1f}" y="{y+52:.1f}" font-size="15" font-weight="700" fill="{ver_color}" opacity="0">'
        f'{label}'
        f'<animate attributeName="opacity" values="0;1" dur="0.4s" begin="{begin_s}" fill="freeze" repeatCount="1"/>'
        f'</text>'
        f'<text x="{x:.1f}" y="{y+70:.1f}" font-size="10.5" fill="#888899" opacity="0">'
        f'{sub}'
        f'<animate attributeName="opacity" values="0;1" dur="0.4s" begin="{begin_s}" fill="freeze" repeatCount="1"/>'
        f'</text>'
    )


# ---------------------------------------------------------------------------
# Private render helpers for render_svg
# ---------------------------------------------------------------------------

def _render_nodes(lo: Layout, versions: list[Version], y: int) -> list[str]:
    n = len(versions)
    out = ['  <!-- LAYER 4: version nodes -->']
    for i, _ver in enumerate(versions):
        out.append(render_node(lo.xs[i], y, lo.opacities[i], lo.ring_counts[i], i == n - 1))
    out.append(render_future_node(lo.xs[n], y))
    return out


def _render_labels(lo: Layout, versions: list[Version], y: int, n_orig: int) -> list[str]:
    n = len(versions)
    prefix_caption = versions[0].caption
    if n_orig > _MAX_NODES:
        prefix_caption = "… " + prefix_caption
    out = ['  <!-- LAYER 8: typewriter labels -->',
           '  <g text-anchor="middle" font-family="ui-monospace, \'SFMono-Regular\', Menlo, Consolas, monospace">',
           T.CAPTION]
    for i, ver in enumerate(versions):
        cap = prefix_caption if i == 0 else ver.caption
        out.append(render_label(lo.xs[i], y, ver.ver, cap, lo.begins[i], is_future=False))
    out.append(render_label(lo.xs[n], y, "next", "roadmap", lo.begins[n], is_future=True))
    out.append('  </g>')
    return out


# ---------------------------------------------------------------------------
# Main assembler
# ---------------------------------------------------------------------------

def render_svg(versions: list[Version]) -> str:
    """Assemble the full SVG string from parsed versions."""
    n_orig = len(versions)
    if n_orig > _MAX_NODES:
        dropped = n_orig - _MAX_NODES
        print(f"WARNING: {dropped} version(s) dropped (showing last {_MAX_NODES})", file=sys.stderr)
        versions = versions[n_orig - _MAX_NODES:]

    lo = layout(versions)
    n = len(versions)
    w = lo.viewbox_w
    y = 240

    w3, w2, w23 = w // 3, w // 2, (w * 2) // 3
    wc = w - 50
    wend = w - 60

    first, last = versions[0].ver, versions[-1].ver
    aria = f"Unity Biome MCP changelog — animated heartbeat timeline of releases v{first} through v{last}"

    lines: list[str] = [
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {w} 360" width="100%" height="360"'
        f' preserveAspectRatio="xMidYMid meet" role="img" aria-label="{aria}">',
        T.DEFS,
        '  <!-- LAYER 0: background -->',
        f'  <rect width="{w}" height="360" fill="url(#bgGlow)"/>',
        T.GRID.format(w=w, w3=w3, w2=w2, w23=w23),
        T.SCANLINES.format(w=w),
        T.BASELINE.format(wend=wend, wc=wc),
    ]
    lines.extend(_render_nodes(lo, versions, y))
    lines += [
        '  <!-- LAYER 5: TCP data packets -->',
        '  <g fill="#ccccff">',
        *[render_packets(lo.xs[i], y, lo.packets[i], i, is_future=False) for i in range(n)],
        render_packets(lo.xs[n], y, lo.packets[n], n, is_future=True),
        '  </g>',
        T.SCANBEAM.format(w=w),
        '  <!-- LAYER 7: vignette -->',
        f'  <rect width="{w}" height="360" fill="url(#vignette)"/>',
    ]
    lines.extend(_render_labels(lo, versions, y, n_orig))
    lines.append('</svg>')

    return "\n".join(lines) + "\n"


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main() -> None:
    repo_root = pathlib.Path(__file__).parent.parent
    changelog = (repo_root / "CHANGELOG.md").read_text(encoding="utf-8")
    versions = parse_changelog(changelog)
    svg = render_svg(versions)
    out = repo_root / "docs" / "assets" / "changelog.svg"
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(svg)
    print(f"Written {out} ({len(svg)} bytes, {len(versions)} versions)")


if __name__ == "__main__":
    main()
