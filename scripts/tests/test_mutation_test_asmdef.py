"""A4: C# asmdef isolation guard for the mutation regression assembly.

`UnityMCP.Editor.Tests.Mutation.asmdef` must only compile when the optional
FSR provider package is installed (UNITYMCP_HAS_FSR_PROVIDER, bound via
versionDefines to com.handzlikchris.fastscriptreload). This keeps the
assembly -- and its tests -- invisible to every existing lane, which never
installs that package (see Plans/MUTATION-REGRESSION-MODULE.md section 5).

No Unity, no network -- parses the tracked JSON directly, following the exact
precedent of scripts/tests/test_pure_core_asmdef_boundaries.py.
"""
import json
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent.parent
MUTATION_ASMDEF = (
    REPO_ROOT / "unity-plugin" / "Editor" / "Tests" / "Mutation"
    / "UnityMCP.Editor.Tests.Mutation.asmdef"
)

_PROVIDER_DEFINE = "UNITYMCP_HAS_FSR_PROVIDER"
_PROVIDER_PACKAGE = "com.handzlikchris.fastscriptreload"


def _load_asmdef() -> dict:
    return json.loads(MUTATION_ASMDEF.read_text(encoding="utf-8"))


def test_mutation_asmdef_has_expected_name():
    asmdef = _load_asmdef()
    assert asmdef["name"] == "UnityMCP.Editor.Tests.Mutation"


def test_mutation_asmdef_define_constraint_gates_on_fsr_provider():
    asmdef = _load_asmdef()
    assert _PROVIDER_DEFINE in asmdef["defineConstraints"]


def test_mutation_asmdef_version_define_binds_provider_package_to_define():
    asmdef = _load_asmdef()
    version_defines = asmdef["versionDefines"]
    matches = [
        vd for vd in version_defines
        if vd.get("name") == _PROVIDER_PACKAGE and vd.get("define") == _PROVIDER_DEFINE
    ]
    assert matches, (
        f"expected a versionDefines entry binding {_PROVIDER_PACKAGE!r} to "
        f"{_PROVIDER_DEFINE!r}, got {version_defines!r}"
    )


def test_mutation_asmdef_references_source_patch():
    asmdef = _load_asmdef()
    assert "UnityMCP.Editor.SourcePatch" in asmdef["references"]


def test_mutation_asmdef_not_auto_referenced():
    asmdef = _load_asmdef()
    assert asmdef["autoReferenced"] is False


def test_mutation_asmdef_define_constraint_includes_unity_include_tests():
    asmdef = _load_asmdef()
    assert "UNITY_INCLUDE_TESTS" in asmdef["defineConstraints"]


def test_mutation_asmdef_include_platforms_is_editor_only():
    asmdef = _load_asmdef()
    assert asmdef["includePlatforms"] == ["Editor"]


def test_mutation_asmdef_overrides_references():
    asmdef = _load_asmdef()
    assert asmdef["overrideReferences"] is True
