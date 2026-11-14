"""PR-02 Track C: pure-core / neutral asmdef boundary checks.

Mechanizes two manual audits into permanent regression tests:
- UnityMCP.Playtest.Core.asmdef must stay engine-free (noEngineReferences,
  zero references) -- it is compiled standalone in ci-pure-dotnet.yml.
- UnityMCP.Editor.SourcePatch.asmdef must never reference a specific FSR
  provider (AI/source-patch-release-evidence.md section 1.2's manual grep).

No Unity, no network -- follows the exact precedent of
scripts/tests/test_lanes_config.py: parses tracked JSON files directly, no
companion scripts/*.py module.
"""
import json
from pathlib import Path

TESTS = Path(__file__).resolve().parent
REPO_ROOT = TESTS.parent.parent

PLAYTEST_CORE_ASMDEF = (
    REPO_ROOT / "unity-plugin" / "Runtime" / "Playtest" / "Core"
    / "UnityMCP.Playtest.Core.asmdef"
)
SOURCE_PATCH_ASMDEF = (
    REPO_ROOT / "unity-plugin" / "Editor" / "SourcePatch"
    / "UnityMCP.Editor.SourcePatch.asmdef"
)
ROSLYN_ASMDEF = (
    REPO_ROOT / "unity-plugin" / "Editor" / "Roslyn"
    / "UnityMCP.Editor.Roslyn.asmdef"
)
TESTRUNS_ASMDEF = (
    REPO_ROOT / "unity-plugin" / "Editor" / "TestRuns"
    / "UnityMCP.Editor.TestRuns.asmdef"
)
PRIMITIVES_ASMDEF = (
    REPO_ROOT / "unity-plugin" / "Editor" / "Primitives"
    / "UnityMCP.Editor.Primitives.asmdef"
)

# Case-insensitive substrings identifying a concrete FSR (or similar
# runtime-patching) provider -- SourcePatch's public asmdef must stay neutral.
_PROVIDER_DENYLIST = ("fastscriptreload", "harmony", "monomod", "handzlik")


def _load_asmdef(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def test_playtest_core_asmdef_has_no_engine_references():
    """UnityMCP.Playtest.Core.asmdef must stay engine-free -- it is compiled
    standalone by ci-pure-dotnet.yml against a plain .csproj, so any Engine
    reference would silently break that build."""
    asmdef = _load_asmdef(PLAYTEST_CORE_ASMDEF)

    assert asmdef["noEngineReferences"] is True
    assert asmdef["references"] == []


def test_source_patch_neutral_asmdef_has_no_provider_references():
    """UnityMCP.Editor.SourcePatch.asmdef must never reference a specific FSR
    (or similar) provider -- mechanizes the manual audit in
    AI/source-patch-release-evidence.md section 1.2."""
    asmdef = _load_asmdef(SOURCE_PATCH_ASMDEF)

    assert asmdef["references"] == []
    violations = _asmdef_denylist_violations(asmdef, _PROVIDER_DENYLIST)
    assert violations == []


def test_roslyn_asmdef_has_no_core_reference():
    """UnityMCP.Editor.Roslyn.asmdef must have zero custom references -- Core
    calls into Roslyn through the RoslynHost delegate seam (Task 0.1), never
    the other way. Regression guard against re-adding a Core back-reference."""
    asmdef = _load_asmdef(ROSLYN_ASMDEF)

    assert asmdef["references"] == []
    assert asmdef["noEngineReferences"] is False


def test_testruns_asmdef_has_only_primitives_reference():
    """UnityMCP.Editor.TestRuns.asmdef must depend on nothing but the
    UnityMCP.Editor.Primitives leaf (plus the two UTF engine/editor assemblies
    its TestRunEnvironmentController/TestRunAssemblyFingerprint need) -- never
    back on UnityMCP.Editor itself. Regression guard for the Phase 4 Task B/C
    cycle-breaking strategy (Plans/task-p4-testruns-asmdef.md): asserts the
    asmdef's real reference list, not the plan's narrower draft of it, which
    undercounted TestRunEnvironmentController's UnityEditor.TestRunner need."""
    asmdef = _load_asmdef(TESTRUNS_ASMDEF)

    assert asmdef["references"] == [
        "UnityMCP.Editor.Primitives", "UnityEditor.TestRunner", "UnityEngine.TestRunner",
    ]
    assert asmdef["noEngineReferences"] is False


def test_primitives_asmdef_has_no_references():
    """UnityMCP.Editor.Primitives.asmdef (MainThreadDispatcher.cs, AtomicFile.cs,
    JsonHelper.cs, BiomeLabel.cs) must stay a zero-reference leaf -- it is the
    one shared dependency that lets both UnityMCP.Editor and
    UnityMCP.Editor.TestRuns depend on it without creating a cycle between
    themselves. Regression guard against re-adding any reference to it."""
    asmdef = _load_asmdef(PRIMITIVES_ASMDEF)

    assert asmdef["references"] == []
    assert asmdef["noEngineReferences"] is False


def _asmdef_denylist_violations(asmdef: dict, denylist: tuple[str, ...]) -> list[str]:
    """Return every references/precompiledReferences entry containing a
    case-insensitive denylist substring. Empty list = clean."""
    hits = []
    for field in ("references", "precompiledReferences"):
        hits.extend(
            entry for entry in asmdef.get(field, [])
            if any(term in entry.lower() for term in denylist)
        )
    return hits


def test_boundary_checker_flags_forbidden_reference():
    """Negative sentinel: proves _asmdef_denylist_violations actually detects
    a forbidden reference rather than vacuously returning an empty list."""
    poisoned = {
        "references": ["Vendor.FastScriptReload.Adapter"],
        "precompiledReferences": [],
    }

    violations = _asmdef_denylist_violations(poisoned, _PROVIDER_DENYLIST)

    assert violations == ["Vendor.FastScriptReload.Adapter"]
