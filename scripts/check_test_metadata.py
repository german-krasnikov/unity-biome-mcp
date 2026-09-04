"""C18: static lint enforcing the C13 taxonomy -- unknown category/tag = CI failure.

Does NOT reimplement pytest's own `--strict-markers` (already enforced via
`server/pyproject.toml`'s `addopts` -- an unregistered pytest marker already
fails collection today). This lint's actual jobs:

1. `[Category(...)]` resolution: every site under unity-plugin/ and
   unity-test-project/ must resolve to a `TestCategories.*` const declared in
   TestCategories.cs, or the one explicitly allow-listed wrapper
   `CleanupOrderingSentinel.Category` (allow-listed **by name**, never "any
   wrapped constant") -- catches a bare string literal that bypasses the
   registry entirely.
2. Lanes/map cross-validation: every taxonomy-shaped value in
   Tests/biome-test-lanes.json's lane filters must be a dimension declared in
   Tests/taxonomy-map.json.
3. `.playtest` `@needs` values against the map: every `@needs` value actually
   observed in the real `.playtest` corpus (via B18's
   scripts/playtest_header.py `scan()` -- never a second ad-hoc `@`-line
   regex) must correspond to a taxonomy-map.json dimension whose
   `dsl_header_value` matches. No-ops gracefully if the map declares zero
   `dsl_header_value` rows yet.

Every `.cs`/`.playtest`/`.json` read passes `encoding="utf-8"` explicitly
(`.claude/skills/encoding.md`, standards #17) -- hundreds of files are
walked here, so a single mis-decoded file would be a false lint failure.

`TestCategories.Perf` (0 usages, verified 2026-09-05, see
Tests/taxonomy-map.json's `_audit_notes`) is intentionally left declared but
unused: this lint only flags a `[Category(...)]` *usage site* that bypasses
the registry, never an unused-but-declared const, so a reserved category
needs no allow-list entry of its own. Deleting the const would be a C#
change, out of this lint's (and this session's Python/YAML-only) scope.
"""
from __future__ import annotations

import json
import re
import sys
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SCRIPTS_DIR = Path(__file__).resolve().parent
if str(SCRIPTS_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPTS_DIR))
import playtest_header  # noqa: E402

LANES_PATH = REPO_ROOT / "Tests" / "biome-test-lanes.json"
TAXONOMY_PATH = REPO_ROOT / "Tests" / "taxonomy-map.json"
TEST_CATEGORIES_PATH = REPO_ROOT / "unity-plugin" / "Editor" / "TestSupport" / "TestCategories.cs"
# unity-test-project/Assets only, never the project root: Library/PackageCache
# under it holds vendored third-party Unity/UGUI/render-pipelines source with
# its own unrelated [Category(...)] usages -- not ours to lint, and not even
# git-tracked (verified: only unity-test-project/Assets/**/*.cs is).
CS_ROOTS = (REPO_ROOT / "unity-plugin", REPO_ROOT / "unity-test-project" / "Assets")
PLAYTEST_ROOT = REPO_ROOT / "unity-test-project"

_CATEGORY_ATTR_RE = re.compile(r"\[Category\(([^()]*)\)\]")
_CATEGORY_CONST_REF_RE = re.compile(r"TestCategories\.(\w+)")
_CSHARP_CONST_RE = re.compile(r'public const string (\w+) = "')
_ALLOWLISTED_WRAPPERS = frozenset({"CleanupOrderingSentinel.Category"})
_LANE_TAXONOMY_FIELDS = ("modes", "speeds", "include_tags", "exclude_tags", "exclude_capabilities")


def load_dimensions() -> dict:
    return json.loads(TAXONOMY_PATH.read_text(encoding="utf-8"))["dimensions"]


def load_lanes() -> dict:
    return json.loads(LANES_PATH.read_text(encoding="utf-8"))["lanes"]


def declared_csharp_categories() -> frozenset[str]:
    text = TEST_CATEGORIES_PATH.read_text(encoding="utf-8")
    return frozenset(_CSHARP_CONST_RE.findall(text))


def find_category_violations(cs_roots=CS_ROOTS, declared: frozenset[str] | None = None) -> list[str]:
    if declared is None:
        declared = declared_csharp_categories()
    violations: list[str] = []
    for root in cs_roots:
        for path in sorted(root.rglob("*.cs")):
            text = path.read_text(encoding="utf-8")
            for arg in _CATEGORY_ATTR_RE.findall(text):
                arg = arg.strip()
                if arg in _ALLOWLISTED_WRAPPERS:
                    continue
                const_ref = _CATEGORY_CONST_REF_RE.fullmatch(arg)
                if const_ref and const_ref.group(1) in declared:
                    continue
                violations.append(
                    f"{path}: [Category({arg})] is not routed through TestCategories.* "
                    "or an allow-listed wrapper"
                )
    return violations


def check_lanes(lanes: dict, dimensions: dict) -> list[str]:
    violations: list[str] = []
    for lane_name, lane in lanes.items():
        for field_name in _LANE_TAXONOMY_FIELDS:
            violations.extend(
                f"lane {lane_name!r} field {field_name!r} references unknown taxonomy dimension {value!r}"
                for value in lane["filter"][field_name]
                if value not in dimensions
            )
    return violations


def check_playtest_headers(playtest_root: Path, dimensions: dict) -> list[str]:
    known_header_values = {row["dsl_header_value"] for row in dimensions.values() if row.get("dsl_header_value")}
    if not known_header_values:
        return []  # map declares no DSL-header-backed dimensions yet -- no-op

    violations: list[str] = []
    for path in sorted(playtest_root.rglob("*.playtest")):
        header = playtest_header.scan(path.read_text(encoding="utf-8"))
        if header.needs_editmode and "editmode" not in known_header_values:
            violations.append(f"{path}: '@needs editmode' has no matching taxonomy-map.json dsl_header_value")
        if header.needs_playmode and "playmode" not in known_header_values:
            violations.append(f"{path}: '@needs playmode' has no matching taxonomy-map.json dsl_header_value")
    return violations


def main() -> int:
    dimensions = load_dimensions()
    violations = [
        *find_category_violations(CS_ROOTS),
        *check_lanes(load_lanes(), dimensions),
        *check_playtest_headers(PLAYTEST_ROOT, dimensions),
    ]
    if violations:
        for violation in violations:
            print(violation, file=sys.stderr)
        print(f"{len(violations)} taxonomy violation(s)", file=sys.stderr)
        return 1
    print("check_test_metadata: clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
