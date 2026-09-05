"""PR-04R: forbidden-dependency trip-wire for the Reload module's C# owner
files. No physical Reload asmdef exists yet (asmdef extraction is Step C,
deferred), so this cannot be an asmdef-reference check the way
test_pure_core_asmdef_boundaries.py works. It follows the source-scan
precedent already in this repo (test_source_patch_boundary.py): a plain text
scan of the owner files for forbidden fragments, with a negative-sentinel
companion proving the detector can actually fail.

Runs in the standard 'not live' suite: no Unity, no live marker, hermetic
file reads only.
"""
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]

# Reload's algorithm/evidence owner files (see Plans/PR-04R.md inventory table).
# CommandRouter.Registration.cs is intentionally excluded: host registration
# wiring referencing Reload's public commands is expected, not a violation.
_RELOAD_OWNER_FILES = (
    "unity-plugin/Editor/SyncHelper.cs",
    "unity-plugin/Editor/CompileNotifier.cs",
    "unity-plugin/Editor/CompileErrorCapture.cs",
    "unity-plugin/Editor/DiagnoseCommand.cs",
)

_FORBIDDEN_FRAGMENTS = ("UnityMCP.Editor.Chat", "SourcePatch", "Playtest", "Scenario")


def _first_forbidden_fragment(text: str) -> str | None:
    """Return the first forbidden fragment found in text, or None."""
    for frag in _FORBIDDEN_FRAGMENTS:
        if frag in text:
            return frag
    return None


def test_forbidden_fragment_detector_actually_detects():
    """Guard against a silently-vacuous check below: prove the detector fires
    on each forbidden fragment, and stays quiet on ordinary Reload text."""
    for frag in _FORBIDDEN_FRAGMENTS:
        assert _first_forbidden_fragment(f"using UnityMCP.Editor.Chat;\n{frag}") is not None
    assert _first_forbidden_fragment("using UnityMCP.Editor.Chat;") == "UnityMCP.Editor.Chat"
    assert _first_forbidden_fragment("namespace UnityMCP.Editor { public static class SyncHelper {} }") is None


def test_reload_owner_files_have_no_chat_sourcepatch_scenario_references():
    """Reload's algorithm/evidence owner files must never reference Chat,
    SourcePatch, Playtest, or Scenario types — Reload is a one-way dependency
    target for those modules, never the other way around (plan §8.4:
    "Reload не импортирует Chat/SourcePatch/Scenario")."""
    offenders = {}
    for rel_path in _RELOAD_OWNER_FILES:
        path = REPO_ROOT / rel_path
        text = path.read_text(encoding="utf-8")
        hit = _first_forbidden_fragment(text)
        if hit is not None:
            offenders[rel_path] = hit
    assert not offenders, f"forbidden fragment references found: {offenders}"
