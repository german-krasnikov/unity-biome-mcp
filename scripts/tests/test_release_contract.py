"""Release helper safety and mirrored package-history contracts."""

import pathlib
import re
import subprocess
import sys
from collections import Counter

import pytest

REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
RELEASE_HELPER = REPO_ROOT / "scripts" / "release.sh"
CHANGELOG = REPO_ROOT / "CHANGELOG.md"
RELEASE_HEADING_RE = re.compile(
    r"^## \[(v\d+\.\d+\.\d+)\](?:[ \t]+.*)?$",
    re.MULTILINE,
)


def test_release_helper_is_explicitly_preflight_only() -> None:
    content = RELEASE_HELPER.read_text(encoding="utf-8")
    assert "no longer publishes releases" in content
    assert not re.search(
        r"^\s*(?:git\s+(?:add|commit|tag|push)|gh\s+release\s+create)\b",
        content,
        re.MULTILINE,
    )


@pytest.mark.skipif(sys.platform == "win32", reason="bash script not executable on Windows")
def test_legacy_release_invocation_fails_closed() -> None:
    result = subprocess.run(
        [str(RELEASE_HELPER), "1.3.0"],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
        timeout=60,
    )
    assert result.returncode != 0
    assert "--preflight" in result.stderr


def test_plugin_changelog_is_exact_generated_mirror() -> None:
    root = CHANGELOG.read_bytes()
    plugin = (REPO_ROOT / "unity-plugin" / "CHANGELOG.md").read_bytes()
    assert plugin == root


def test_changelog_release_headings_are_unique_and_canonical() -> None:
    content = CHANGELOG.read_text(encoding="utf-8")
    release_lines = [line for line in content.splitlines() if line.startswith("## [")]
    assert release_lines[0] == "## [Unreleased]"

    release_matches = [RELEASE_HEADING_RE.fullmatch(line) for line in release_lines[1:]]
    malformed = [
        line
        for line, match in zip(release_lines[1:], release_matches)
        if match is None
    ]
    assert not malformed, f"non-canonical release headings: {malformed}"
    assert all("— Unreleased" not in line for line in release_lines[1:])

    labels = ["Unreleased", *(match.group(1) for match in release_matches if match)]
    duplicates = [label for label, count in Counter(labels).items() if count > 1]
    assert not duplicates, f"duplicate release headings: {duplicates}"
    assert {
        "v0.12.0",
        "v0.13.1",
        "v0.13.3",
        "v0.13.4",
        "v0.17.37",
        "v0.70.0",
        "v1.19.0",
    } <= set(labels)
    assert "## [v0.17.37] — 2026-06-07" in release_lines
    assert any(line.startswith("## [v0.70.0] — 2026-07-03") for line in release_lines)


def test_latest_changelog_compare_links_follow_release_chain() -> None:
    content = CHANGELOG.read_text(encoding="utf-8")
    latest, previous = [
        match.group(1) for match in RELEASE_HEADING_RE.finditer(content)
    ][:2]
    links = dict(re.findall(r"^\[([^]]+)\]:\s+(\S+)$", content, re.MULTILINE))

    assert links["Unreleased"].endswith(f"/compare/{latest}...HEAD")
    assert links[latest].endswith(f"/compare/{previous}...{latest}")
