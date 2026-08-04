"""Render README metadata, SVG statistics, and Shields endpoints."""

import html
import json
import pathlib
import re
import xml.etree.ElementTree as ET
from collections.abc import Mapping

_MARKER_NAME = r"[A-Z][A-Z0-9_]*"
_MARKER_TOKEN_RE = re.compile(
    rf"<!--\s*(?P<closing>/)?BIOME:(?P<name>{_MARKER_NAME})\s*-->"
)
_XML_COMMENT_RE = re.compile(r"<!--(?P<body>.*?)-->", re.DOTALL)
_BIOME_LIKE_RE = re.compile(r"(?i)/?\s*BIOME\s*:")
_MARKER_PAIR_RE = re.compile(
    rf"(?P<open><!--\s*BIOME:(?P<name>{_MARKER_NAME})\s*-->)"
    rf"(?P<value>.*?)"
    rf"(?P<close><!--\s*/BIOME:(?P=name)\s*-->)",
    re.DOTALL,
)
STATS_SVG_MARKERS = frozenset(
    {
        "STATS_DESC",
        "TOOLS",
        "TESTS",
        "BREAKDOWN_PRIMARY",
        "BREAKDOWN_SECONDARY",
    }
)
STATS_SVG_MARKER_PARENTS = {
    "STATS_DESC": "desc",
    "TOOLS": "text",
    "TESTS": "text",
    "BREAKDOWN_PRIMARY": "text",
    "BREAKDOWN_SECONDARY": "text",
}
SVG_MARKER_ALLOWLIST = {
    "stats.svg": STATS_SVG_MARKERS,
}


def _read_text_exact(path: pathlib.Path) -> str:
    with path.open("r", encoding="utf-8", newline="") as stream:
        return stream.read()


def _write_text_exact(path: pathlib.Path, content: str) -> None:
    with path.open("w", encoding="utf-8", newline="") as stream:
        stream.write(content)


def read_meta_json(repo_root: pathlib.Path) -> dict:
    path = repo_root / "docs" / "assets" / "_meta.json"
    return json.loads(_read_text_exact(path))


def _unity_source_label(source: str | None) -> str:
    labels = {
        "static_grep": "static source scan",
        "unavailable": "unavailable",
    }
    return labels.get(source, "unknown")


def stats_summary(meta: dict) -> str:
    """Return the canonical accessible description for generated inventory."""
    required = (
        "tools",
        "tests_total",
        "tests_python",
        "tests_stress",
        "tests_unity",
        "tests_unity_source",
        "tests_live",
        "server_version",
    )
    missing = [key for key in required if key not in meta or meta[key] is None]
    if missing:
        raise ValueError(f"README metadata is missing: {', '.join(missing)}")

    tools = meta["tools"]
    total = meta["tests_total"]
    python = meta["tests_python"]
    stress = meta["tests_stress"]
    unity = meta["tests_unity"]
    live = meta["tests_live"]
    source = _unity_source_label(meta["tests_unity_source"])
    version = meta["server_version"]
    return (
        f"{tools} registered MCP tools. Test inventory: {total} entries: "
        f"{python} regular Python, {stress} Python stress, {live} live Python, "
        f"and {unity} Unity source attributes. Unity count source: {source}. "
        f"Server package version: v{version}."
    )


def _validate_svg_marker_contract(
    svg: str,
    expected_markers: frozenset[str],
) -> None:
    tokens = list(_MARKER_TOKEN_RE.finditer(svg))
    recognized_spans = {match.span() for match in tokens}
    malformed = [
        match.group(0)
        for match in _XML_COMMENT_RE.finditer(svg)
        if _BIOME_LIKE_RE.search(match.group("body"))
        and match.span() not in recognized_spans
    ]
    if malformed:
        raise ValueError(f"Malformed BIOME marker: {malformed[0]}")

    token_names = {match.group("name") for match in tokens}
    missing = expected_markers - token_names
    unexpected = token_names - expected_markers
    if missing:
        raise ValueError(
            f"Missing BIOME marker(s): {', '.join(sorted(missing))}"
        )
    if unexpected:
        raise ValueError(
            f"Unexpected BIOME marker(s): {', '.join(sorted(unexpected))}"
        )

    for name in sorted(expected_markers):
        opens = sum(
            match.group("name") == name and not match.group("closing")
            for match in tokens
        )
        closes = sum(
            match.group("name") == name and bool(match.group("closing"))
            for match in tokens
        )
        if opens != 1 or closes != 1:
            raise ValueError(
                f"Expected exactly one BIOME:{name} marker pair"
            )

    active_name: str | None = None
    for token in tokens:
        name = token.group("name")
        if not token.group("closing"):
            if active_name is not None:
                raise ValueError("BIOME markers must not be nested")
            active_name = name
        else:
            if active_name != name:
                raise ValueError("BIOME markers are incomplete or mismatched")
            active_name = None
    if active_name is not None:
        raise ValueError("BIOME markers are incomplete or mismatched")

    pairs = list(_MARKER_PAIR_RE.finditer(svg))
    if len(pairs) != len(expected_markers):
        raise ValueError("BIOME markers are incomplete, mismatched, or nested")
    for pair in pairs:
        if "<" in pair.group("value"):
            raise ValueError(
                f"BIOME:{pair.group('name')} payload must be scalar text"
            )


def _validate_svg_marker_locations(
    svg: str,
    marker_parents: Mapping[str, str],
) -> None:
    parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    root = ET.fromstring(svg, parser=parser)
    located: dict[str, list[ET.Element]] = {}
    for element in root.iter():
        name = element.attrib.get("data-biome-marker")
        if name:
            located.setdefault(name, []).append(element)

    unexpected = set(located) - set(marker_parents)
    if unexpected:
        raise ValueError(
            f"Unexpected data-biome-marker value(s): {', '.join(sorted(unexpected))}"
        )

    for name, expected_tag in marker_parents.items():
        elements = located.get(name, [])
        if len(elements) != 1:
            raise ValueError(
                f"Expected exactly one data-biome-marker={name} element"
            )
        element = elements[0]
        tag = str(element.tag).rsplit("}", 1)[-1]
        if tag != expected_tag:
            raise ValueError(
                f"BIOME:{name} must be a direct child payload of <{expected_tag}>"
            )

        children = list(element)
        if (
            len(children) != 2
            or any(child.tag is not ET.Comment for child in children)
            or (children[0].text or "").strip() != f"BIOME:{name}"
            or (children[1].text or "").strip() != f"/BIOME:{name}"
        ):
            raise ValueError(
                f"BIOME:{name} markers must be direct children of their owner"
            )
        if (element.text or "").strip() or (children[1].tail or "").strip():
            raise ValueError(
                f"BIOME:{name} owner contains text outside its marker payload"
            )


def substitute_svg_markers(
    svg: str,
    meta: dict,
    expected_markers: frozenset[str] = STATS_SVG_MARKERS,
    marker_parents: Mapping[str, str] = STATS_SVG_MARKER_PARENTS,
) -> str:
    """Replace allowlisted marker payloads while preserving all other SVG bytes."""
    replacements = {
        "TOOLS": meta["tools"],
        "TESTS": meta["tests_total"],
        "BREAKDOWN_PRIMARY": (
            f"{meta['tests_python']} regular / {meta['tests_stress']} stress"
        ),
        "BREAKDOWN_SECONDARY": (
            f"{meta['tests_live']} live / {meta['tests_unity']} Unity"
        ),
        "STATS_DESC": stats_summary(meta),
    }
    if frozenset(replacements) != expected_markers:
        raise ValueError("Renderer values do not match the SVG marker allowlist")

    try:
        ET.fromstring(svg)
    except ET.ParseError as error:
        raise ValueError(f"SVG is not valid XML: {error}") from error
    _validate_svg_marker_contract(svg, expected_markers)
    _validate_svg_marker_locations(svg, marker_parents)

    def replace_payload(match: re.Match[str]) -> str:
        value = html.escape(str(replacements[match.group("name")]), quote=False)
        return f"{match.group('open')}{value}{match.group('close')}"

    updated, replacement_count = _MARKER_PAIR_RE.subn(replace_payload, svg)
    if replacement_count != len(expected_markers):
        raise ValueError("Not every allowlisted BIOME marker was updated")
    try:
        ET.fromstring(updated)
    except ET.ParseError as error:
        raise ValueError(f"Generated SVG is not valid XML: {error}") from error
    return updated


def update_readme_stats(readme: str, meta: dict) -> str:
    """Regenerate the stats image tag from the same facts used by stats.svg."""
    pattern = re.compile(
        r"(<!-- README_STATS_START -->).*?(<!-- README_STATS_END -->)",
        re.DOTALL,
    )
    image = (
        '<img src="docs/assets/stats.svg" width="100%" '
        f'alt="{html.escape(stats_summary(meta), quote=True)}">'
    )
    updated, count = pattern.subn(
        lambda match: f"{match.group(1)}\n{image}\n{match.group(2)}",
        readme,
    )
    if count != 1:
        raise ValueError("Expected exactly one README stats marker pair")
    return updated


def parse_latest_changelog(text: str) -> tuple[str, str]:
    """Return the version and date of the latest dated release."""
    match = re.search(
        r"^## \[(v[^\]]+)\]\s*[—–-]\s*(\d{4}-\d{2}-\d{2})",
        text,
        flags=re.MULTILINE,
    )
    if not match:
        return "?", "?"
    return match.group(1), match.group(2)


def generate_changelog_summary(text: str) -> str:
    """Generate one current-release line; CHANGELOG.md owns release details."""
    version, date = parse_latest_changelog(text)
    if version == "?":
        return "[Read the full changelog.](CHANGELOG.md)"
    return (
        f"**Current release: {version} ({date}).** "
        "[Read the full changelog.](CHANGELOG.md)"
    )


def inject_changelog_into_readme(readme: str, content: str) -> str:
    pattern = re.compile(
        r"(<!-- CHANGELOG_START -->).*?(<!-- CHANGELOG_END -->)",
        re.DOTALL,
    )
    updated, count = pattern.subn(
        lambda match: f"{match.group(1)}\n{content}\n{match.group(2)}",
        readme,
    )
    if count != 1:
        raise ValueError("Expected exactly one README changelog marker pair")
    return updated


def make_badge_json(label: str, message: str, color: str) -> dict:
    return {
        "schemaVersion": 1,
        "label": label,
        "message": message,
        "color": color,
    }


def _apply_or_check(changes: list[tuple[pathlib.Path, str]], check: bool) -> None:
    stale: list[pathlib.Path] = []
    for path, content in changes:
        if path.exists() and _read_text_exact(path) == content:
            if not check:
                print(f"  unchanged {path.name}")
            continue
        if check:
            stale.append(path)
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        _write_text_exact(path, content)
        print(f"  updated {path.name}")

    if stale:
        print("STALE (run python3 scripts/update_readme.py --render):")
        for path in stale:
            print(f"  {path}")
        raise SystemExit(1)
    if check:
        print("OK - all generated files are up to date")


def _sync_repo_description(meta: dict) -> None:
    """Update GitHub repo description with current tool count."""
    import shutil
    import subprocess

    if not shutil.which("gh"):
        return
    tools = meta.get("tools", "?")
    desc = f"MCP server for Unity Editor — {tools} tools for scene, assets, animation, VFX, playtest & more"
    subprocess.run(
        ["gh", "repo", "edit", "--description", desc],
        check=False, capture_output=True,
    )


def render(repo_root: pathlib.Path, meta: dict, check: bool = False) -> list[pathlib.Path]:
    """Regenerate every output controlled by the README metadata pipeline."""
    changes: list[tuple[pathlib.Path, str]] = []

    badges_dir = repo_root / ".github" / "badges"
    tests_badge = make_badge_json(
        "tests",
        f"{meta['tests_total']:,} inventory",
        "59a7ff",
    )
    tools_badge = make_badge_json(
        "tools",
        f"{meta.get('tools', '?')} MCP",
        "46e6a6",
    )
    changes.extend(
        [
            (
                badges_dir / "tests.json",
                json.dumps(tests_badge, indent=2) + "\n",
            ),
            (
                badges_dir / "tools.json",
                json.dumps(tools_badge, indent=2) + "\n",
            ),
        ]
    )

    readme_path = repo_root / "README.md"
    changelog = _read_text_exact(repo_root / "CHANGELOG.md")
    readme = _read_text_exact(readme_path)
    readme = update_readme_stats(readme, meta)
    readme = inject_changelog_into_readme(
        readme,
        generate_changelog_summary(changelog),
    )
    changes.append((readme_path, readme))

    mkdocs_path = repo_root / "mkdocs.yml"
    mkdocs_text = _read_text_exact(mkdocs_path)
    mkdocs_text = re.sub(
        r"^(site_description:\s*MCP server for Unity Editor\s*—\s*)\d+(\s*tools.*)$",
        rf"\g<1>{meta.get('tools', '?')}\2",
        mkdocs_text,
        count=1,
        flags=re.MULTILINE,
    )
    changes.append((mkdocs_path, mkdocs_text))

    assets_dir = repo_root / "docs" / "assets"
    for name, expected_markers in SVG_MARKER_ALLOWLIST.items():
        path = assets_dir / name
        svg = _read_text_exact(path)
        changes.append(
            (path, substitute_svg_markers(svg, meta, expected_markers))
        )

    _apply_or_check(changes, check)
    if not check:
        _sync_repo_description(meta)
    return [path for path, _ in changes]
