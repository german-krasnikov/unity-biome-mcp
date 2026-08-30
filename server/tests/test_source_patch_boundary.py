"""P0-30: architecture-denylist guards for the not-yet-built Source Patch
(FSR-backed body-only mutation) boundary. See
Plans/HotReload/V2/FSR-MVP-CLEAN/04-PARETO-COMPLETION-HANDOFF.md §3/§6 P0-30.

None of the guarded things exist yet (no SourcePatch asmdef, no
source_patch_write command, no provider adapter). Every test here is a
trip-wire: green today because there is nothing to violate, and it must stay
green (or be consciously rewritten with the real contract) as P0-40/50/60/70
land the real feature. Do not delete a test here just because a later task
makes it "point at nothing" — see the P0-50/P0-60 references inline.

Runs in the standard 'not live' suite: no Unity, no live marker, hermetic
file/JSON/registry reads only.
"""
import json
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
PLUGIN_ROOT = REPO_ROOT / "unity-plugin"

# Real assembly/package name fragments, not human-readable feature names —
# chosen to match what would actually appear in an asmdef "references" array
# or a package.json dependency key if the boundary were ever violated.
_FORBIDDEN_NAME_FRAGMENTS = (
    "fastscriptreload",
    "harmony",
    "monomod",
    "cecil",
    "codeanalysis",  # Microsoft.CodeAnalysis[.CSharp] == Roslyn
    "sourcepatch",
)


def _asmdef_paths() -> list[Path]:
    return sorted(PLUGIN_ROOT.glob("**/*.asmdef"))


def _references_contain_forbidden_fragment(references: list) -> str | None:
    """Return the first forbidden fragment found in an asmdef references list, or None."""
    for ref in references:
        low = str(ref).lower()
        for frag in _FORBIDDEN_NAME_FRAGMENTS:
            if frag in low:
                return frag
    return None


# ---------------------------------------------------------------------------
# Base package: asmdef / package.json denylist (must be green forever — no
# package-absent/installed distinction applies to the BASE package itself).
# ---------------------------------------------------------------------------

def test_base_asmdefs_have_no_forbidden_references():
    """No shipped asmdef references FSR/Harmony/MonoMod/Cecil/provider-Roslyn/
    SourcePatch today. §3.1: base never references the optional package."""
    offenders = {}
    for path in _asmdef_paths():
        data = json.loads(path.read_text(encoding="utf-8"))
        hit = _references_contain_forbidden_fragment(data.get("references", []))
        if hit:
            offenders[path.name] = hit
    assert not offenders, f"forbidden asmdef references found: {offenders}"


def test_forbidden_reference_detector_actually_detects():
    """Guard against a silently-vacuous check above: prove the detector fires
    on a synthetic asmdef containing a real provider assembly name."""
    hit = _references_contain_forbidden_fragment(["UnityEngine", "0Harmony", "UnityMCP.Editor"])
    assert hit == "harmony"
    assert _references_contain_forbidden_fragment(["UnityEngine", "UnityMCP.Editor"]) is None


def test_package_json_has_no_forbidden_dependency_or_keyword():
    """package.json (name/description/keywords/dependencies/...) never mentions
    a provider/engine name. Scans the whole document, not just known keys, so
    a future field addition can't silently bypass the guard."""
    data = json.loads((PLUGIN_ROOT / "package.json").read_text(encoding="utf-8"))
    blob = json.dumps(data).lower()
    hits = [frag for frag in _FORBIDDEN_NAME_FRAGMENTS if frag in blob]
    assert not hits, f"package.json mentions forbidden fragment(s): {hits}"


def test_at_most_one_source_patch_asmdef_exists():
    """§3.1: exactly one neutral Source Patch asmdef is allowed once P0-40
    lands; today there must be zero. Guard is '<= 1' per the plan's own
    wording so P0-40 does not need to touch this test."""
    matches = [p.name for p in _asmdef_paths() if "sourcepatch" in p.name.lower()]
    assert len(matches) <= 1, f"more than one Source Patch asmdef found: {matches}"
    assert matches == [], "P0-30 baseline: zero Source Patch asmdefs exist yet"


_PROVIDER_IF_PATTERN = re.compile(r"#if.*(FSR|HARMONY|SOURCE_PATCH|PROVIDER)", re.IGNORECASE)


def _line_has_forbidden_provider_conditional(line: str) -> bool:
    """True if a #if line's expression mentions an FSR/Harmony/SourcePatch/
    provider compile symbol. Plain substring match on purpose — real Unity
    compile symbols are SCREAMING_SNAKE_CASE and commonly carry a prefix/suffix
    (e.g. FSR_ENABLED, UNITYMCP_SOURCE_PATCH), so a \b-bounded regex would
    miss them."""
    return bool(_PROVIDER_IF_PATTERN.search(line))


def test_provider_conditional_detector_actually_detects():
    """Guard against a silently-vacuous check below: prove the detector fires
    on realistic compile-symbol shapes, and stays quiet on ordinary ones."""
    assert _line_has_forbidden_provider_conditional("#if FSR_ENABLED")
    assert _line_has_forbidden_provider_conditional("#if UNITYMCP_SOURCE_PATCH_ON")
    assert _line_has_forbidden_provider_conditional("#if HARMONY_PROVIDER")
    assert not _line_has_forbidden_provider_conditional("#if UNITY_EDITOR")
    assert not _line_has_forbidden_provider_conditional("#if UNITY_6000_4_OR_NEWER")


def test_no_provider_conditional_compilation_outside_adapter():
    """No #if directive anywhere in the base plugin references an
    FSR/Harmony/SourcePatch/provider compile symbol. §3.1: provider knowledge
    is confined to the optional adapter package, which does not exist yet —
    so today this must be zero everywhere in unity-plugin/."""
    offenders = []
    for path in PLUGIN_ROOT.glob("**/*.cs"):
        for lineno, line in enumerate(path.read_text(encoding="utf-8", errors="ignore").splitlines(), start=1):
            if _line_has_forbidden_provider_conditional(line):
                offenders.append(f"{path.relative_to(REPO_ROOT)}:{lineno}: {line.strip()}")
    assert not offenders, f"provider-conditional compilation found outside any adapter: {offenders}"


# ---------------------------------------------------------------------------
# Public MCP / batch surface: source_patch_write must never be reachable
# through the public schema, and (once it exists) never through batch.
# ---------------------------------------------------------------------------

def _registered_tool_names() -> frozenset[str]:
    from unity_mcp.server import mcp
    return frozenset(t.name for t in mcp._tool_manager.list_tools())


def test_no_public_source_patch_tool():
    """§6 P0-30 / §3.2: there is no public MCP tool named source_patch*.
    mutation_mode stays on the existing `editor` tool; nothing new is public."""
    registered = _registered_tool_names()
    offenders = {n for n in registered if n.startswith("source_patch")}
    assert not offenders, f"public source_patch* tool(s) registered: {offenders}"


def test_source_patch_write_absent_or_internal_direct_only():
    """§3.2: 'Python adds at most one internal/direct-only async command
    source_patch_write; it is not MCP-decorated and batch/intent cannot
    invoke it.' Today it must not exist at all. The moment P0-50 adds it,
    this test starts enforcing the real contract instead of mere absence —
    update the spec-shape assertion below alongside that change, do not
    delete this test (see P0-50 in the handoff doc)."""
    assert "source_patch_write" not in _registered_tool_names(), \
        "source_patch_write must never be a public MCP tool"

    from unity_mcp.tools.tool_specs import _SPECS
    spec = _SPECS.get("source_patch_write")
    if spec is not None:
        assert spec.direct_only, "source_patch_write must be direct_only (unreachable from batch)"
        assert spec.category == "_INTERNAL", "source_patch_write must be a protocol-only _INTERNAL entry"
