"""C13: single cross-language taxonomy source of truth.

`Tests/taxonomy-map.json` maps each test-taxonomy dimension (a capability,
speed class, or DSL mode) onto its representation in each language:
- `pytest_marker`   -> a marker name registered in `server/pyproject.toml`'s
  `[tool.pytest.ini_options].markers` list.
- `csharp_category` -> a `public const string` name declared in
  `unity-plugin/Editor/TestSupport/TestCategories.cs`.
- `dsl_header_value` -> a token recognized by a `.playtest` `# @needs`/
  `# @tags` header directive (`unity-plugin/Editor/PlaytestHeaderScanner.cs`).

This is attempt #6 at this vocabulary (plan item C13) - the only thing
meant to make it stick this time is that a later item (C18) turns an
unknown dimension into a CI failure. Until then, this module is the
cross-check: every non-null `pytest_marker`/`csharp_category` value here
must resolve against its real source of truth, independently in both
directions, so drift on either side goes red.

Runs in the standard scripts/tests lane: no Unity, no network, reads two
tracked files only.
"""
import json
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TAXONOMY_MAP_PATH = REPO_ROOT / "Tests" / "taxonomy-map.json"
PYPROJECT_PATH = REPO_ROOT / "server" / "pyproject.toml"
TEST_CATEGORIES_PATH = REPO_ROOT / "unity-plugin" / "Editor" / "TestSupport" / "TestCategories.cs"

# Isolates the `markers = [...]` TOML array specifically, so a quoted
# "word: ..." string elsewhere in pyproject.toml (e.g. filterwarnings'
# "error::EncodingWarning") is never mistaken for a registered marker.
_MARKERS_BLOCK_RE = re.compile(r"^markers = \[(.*?)^\]", re.MULTILINE | re.DOTALL)
# Matches a registered marker's name inside that block,
# e.g. `    "live: requires running Unity Editor ..."` -> "live".
_MARKER_NAME_RE = re.compile(r'"(\w+):')
_CSHARP_CONST_RE = re.compile(r'public const string (\w+) = "')


def load_taxonomy_map() -> dict:
    return json.loads(TAXONOMY_MAP_PATH.read_text(encoding="utf-8"))


def registered_pytest_markers() -> frozenset[str]:
    text = PYPROJECT_PATH.read_text(encoding="utf-8")
    block = _MARKERS_BLOCK_RE.search(text)
    assert block is not None, "markers = [...] block not found in server/pyproject.toml"
    return frozenset(_MARKER_NAME_RE.findall(block.group(1)))


def declared_csharp_categories() -> frozenset[str]:
    text = TEST_CATEGORIES_PATH.read_text(encoding="utf-8")
    return frozenset(_CSHARP_CONST_RE.findall(text))


def known_dimension_names() -> frozenset[str]:
    """Reused by C15's scripts/tests/test_lanes_config.py to validate lane values."""
    return frozenset(load_taxonomy_map()["dimensions"])


def test_taxonomy_map_parses_and_has_dimensions():
    data = load_taxonomy_map()
    assert isinstance(data["dimensions"], dict)
    assert len(data["dimensions"]) > 0


def test_every_pytest_marker_resolves_against_registered_markers():
    markers = registered_pytest_markers()
    assert "live" in markers  # sanity: the extractor found real markers at all
    dimensions = load_taxonomy_map()["dimensions"]
    for name, row in dimensions.items():
        marker = row.get("pytest_marker")
        if marker is not None:
            assert marker in markers, (
                f"dimension {name!r}: pytest_marker {marker!r} not registered in server/pyproject.toml"
            )


def test_every_csharp_category_resolves_against_test_categories():
    categories = declared_csharp_categories()
    assert "Slow" in categories  # sanity: the extractor found real consts at all
    dimensions = load_taxonomy_map()["dimensions"]
    for name, row in dimensions.items():
        category = row.get("csharp_category")
        if category is not None:
            assert category in categories, (
                f"dimension {name!r}: csharp_category {category!r} not declared in TestCategories.cs"
            )


def test_slow_row_is_present_and_resolves():
    dimensions = load_taxonomy_map()["dimensions"]
    assert "slow" in dimensions
    row = dimensions["slow"]
    assert row["pytest_marker"] == "slow"
    assert row["csharp_category"] == "Slow"
    assert row["csharp_category"] in declared_csharp_categories()
