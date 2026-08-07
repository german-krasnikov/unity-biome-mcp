"""Package manifest validation tests."""
import json
import re
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[2]
PKG = ROOT / "unity-plugin" / "package.json"
PYPROJECT = ROOT / "server" / "pyproject.toml"


@pytest.fixture
def pkg():
    return json.loads(PKG.read_text(encoding="utf-8"))


def test_valid_json():
    json.loads(PKG.read_text(encoding="utf-8"))


def test_required_upm_fields(pkg):
    for field in ("name", "version", "displayName", "description", "unity"):
        assert field in pkg, f"Missing required UPM field: {field}"


def test_description_not_empty(pkg):
    assert pkg["description"]
    assert len(pkg["description"]) <= 800


def test_url_fields_present(pkg):
    for field in ("documentationUrl", "changelogUrl", "licensesUrl"):
        assert field in pkg, f"Missing URL field: {field}"
        assert pkg[field].startswith("https://"), f"{field} must be HTTPS"


def test_keywords_present(pkg):
    assert "keywords" in pkg
    assert len(pkg["keywords"]) >= 10


def test_author_has_url(pkg):
    assert "author" in pkg
    assert "url" in pkg["author"]


def test_version_matches_pyproject(pkg):
    text = PYPROJECT.read_text(encoding="utf-8")
    match = re.search(r'^version\s*=\s*"(.+)"', text, re.MULTILINE)
    assert match, "Could not find version in pyproject.toml"
    assert pkg["version"] == match.group(1)


def test_license_file_exists():
    assert (ROOT / "unity-plugin" / "LICENSE.md").exists()


def test_description_has_bullet_structure(pkg):
    """UPM Details panel renders \\n as newline and ▪ as bullet."""
    assert "▪" in pkg["description"], "description must have ▪ bullet lines"
    assert "\n" in pkg["description"], "description must have line breaks"


def test_keyword_mcp_present(pkg):
    """'mcp' must be in keywords for UPM discoverability."""
    assert "mcp" in pkg["keywords"]


def test_documentation_url_is_pages(pkg):
    """documentationUrl must point to GitHub Pages, not raw README."""
    assert pkg["documentationUrl"] == "https://german-krasnikov.github.io/unity-biome-mcp/"


def test_package_type_is_tool(pkg):
    """type='tool' must be present for UPM editor-tool categorization."""
    assert pkg.get("type") == "tool"


def test_keywords_no_duplicates(pkg):
    kw = pkg["keywords"]
    assert len(kw) == len(set(kw)), f"duplicate keywords: {[k for k in kw if kw.count(k) > 1]}"


def test_keywords_all_lowercase(pkg):
    for kw in pkg["keywords"]:
        assert kw == kw.lower(), f"keyword must be lowercase: {kw}"


def test_keywords_no_spaces(pkg):
    for kw in pkg["keywords"]:
        assert " " not in kw, f"keyword must not contain spaces: {kw}"


def test_description_unicode_parses_clean(pkg):
    """▪ bullet char must survive JSON round-trip."""
    raw = PKG.read_text(encoding="utf-8")
    reparsed = json.loads(raw)
    assert "▪" in reparsed["description"]
