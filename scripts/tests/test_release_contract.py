"""Release helper safety and mirrored package-history contracts."""

import pathlib
import re
import subprocess


REPO_ROOT = pathlib.Path(__file__).parent.parent.parent
RELEASE_HELPER = REPO_ROOT / "scripts" / "release.sh"


def test_release_helper_is_explicitly_preflight_only() -> None:
    content = RELEASE_HELPER.read_text(encoding="utf-8")
    assert "no longer publishes releases" in content
    assert not re.search(
        r"^\s*(?:git\s+(?:add|commit|tag|push)|gh\s+release\s+create)\b",
        content,
        re.MULTILINE,
    )


def test_legacy_release_invocation_fails_closed() -> None:
    result = subprocess.run(
        [str(RELEASE_HELPER), "1.3.0"],
        cwd=REPO_ROOT,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    assert result.returncode != 0
    assert "--preflight" in result.stderr


def test_plugin_changelog_is_exact_generated_mirror() -> None:
    root = (REPO_ROOT / "CHANGELOG.md").read_bytes()
    plugin = (REPO_ROOT / "unity-plugin" / "CHANGELOG.md").read_bytes()
    assert plugin == root
