"""Render README metadata, SVG statistics, and Shields endpoints."""

import html
import json
import pathlib
import re


def read_meta_json(repo_root: pathlib.Path) -> dict:
    path = repo_root / "docs" / "assets" / "_meta.json"
    return json.loads(path.read_text(encoding="utf-8"))


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
    return (
        f"{tools} registered MCP tools. Test inventory: {total} entries: "
        f"{python} regular Python, {stress} Python stress, {live} live Python, "
        f"and {unity} Unity source attributes. Unity count source: {source}."
    )


def _replace_marker(text: str, name: str, value: object) -> str:
    pattern = re.compile(
        rf"(<!--\s*STAT:{re.escape(name)}\s*-->).*?(<!--\s*/STAT\s*-->)",
        re.DOTALL,
    )
    marker_count = len(re.findall(rf"<!--\s*STAT:{re.escape(name)}\s*-->", text))
    if marker_count == 0:
        return text
    escaped = html.escape(str(value), quote=False)
    updated, replacement_count = pattern.subn(
        lambda match: f"{match.group(1)}{escaped}{match.group(2)}",
        text,
    )
    if marker_count != 1 or replacement_count != 1:
        raise ValueError(f"Expected exactly one complete STAT:{name} marker pair")
    return updated


def substitute_svg_markers(svg: str, meta: dict) -> str:
    """Replace explicit SVG statistic markers without touching unrelated text."""
    replacements = {
        "TOOLS": meta["tools"],
        "TESTS": meta["tests_total"],
        "BREAKDOWN": (
            f"{meta['tests_python']} regular · "
            f"{meta['tests_stress']} stress · "
            f"{meta['tests_live']} live · "
            f"{meta['tests_unity']} Unity"
        ),
        "UNITY_SOURCE": _unity_source_label(meta["tests_unity_source"]),
        "STATS_DESC": stats_summary(meta),
    }
    for name in replacements:
        marker_count = len(
            re.findall(rf"<!--\s*STAT:{re.escape(name)}\s*-->", svg)
        )
        if marker_count != 1:
            raise ValueError(f"Expected exactly one complete STAT:{name} marker pair")
    for name, value in replacements.items():
        svg = _replace_marker(svg, name, value)
    return svg


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
        if path.exists() and path.read_text(encoding="utf-8") == content:
            if not check:
                print(f"  unchanged {path.name}")
            continue
        if check:
            stale.append(path)
            continue
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(content, encoding="utf-8")
        print(f"  updated {path.name}")

    if stale:
        print("STALE (run python3 scripts/update_readme.py --render):")
        for path in stale:
            print(f"  {path}")
        raise SystemExit(1)
    if check:
        print("OK - all generated files are up to date")


def render(repo_root: pathlib.Path, meta: dict, check: bool = False) -> list[pathlib.Path]:
    """Regenerate every output controlled by the README metadata pipeline."""
    changes: list[tuple[pathlib.Path, str]] = []

    badges_dir = repo_root / ".github" / "badges"
    tests_badge = make_badge_json(
        "tests",
        f"{meta['tests_total']} inventoried",
        "46e6a6",
    )
    tools_badge = make_badge_json(
        "tools",
        f"{meta.get('tools', '?')} MCP",
        "e94560",
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
    changelog = (repo_root / "CHANGELOG.md").read_text(encoding="utf-8")
    readme = readme_path.read_text(encoding="utf-8")
    readme = update_readme_stats(readme, meta)
    readme = inject_changelog_into_readme(
        readme,
        generate_changelog_summary(changelog),
    )
    changes.append((readme_path, readme))

    assets_dir = repo_root / "docs" / "assets"
    for name in ("stats.svg",):
        path = assets_dir / name
        svg = path.read_text(encoding="utf-8")
        changes.append((path, substitute_svg_markers(svg, meta)))

    _apply_or_check(changes, check)
    return [path for path, _ in changes]
