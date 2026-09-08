"""PR-06 Gap C: static CI lock proving SourcePatch's own control-plane files
never call SyncHelper.TriggerSync directly, bypassing the sanctioned
ISourcePatchReloadPort adapter (PR-04R). Mirrors test_reload_module_boundary.py
exactly but scans the opposite direction: SourcePatch -> Reload, not
Reload -> SourcePatch/Chat/Playtest/Scenario.

Runs in the standard 'not live' suite: no Unity, no live marker, hermetic
file reads only.
"""
from pathlib import Path

import pytest

pytestmark = pytest.mark.csharp_parity

REPO_ROOT = Path(__file__).resolve().parents[2]

# SourcePatch's own control-plane files - everything that touches
# SourcePatchHost.CurrentState or the Disabling receipt. Deliberately
# excludes SourcePatchReloadPort.cs itself, the one sanctioned adapter
# (plan §8.4: PR-04R's ISourcePatchReloadPort).
_SOURCE_PATCH_CONTROL_FILES = (
    "unity-plugin/Editor/SourcePatchHost.cs",
    "unity-plugin/Editor/SourcePatchModePolicy.cs",
    "unity-plugin/Editor/SourcePatchUnityPorts.cs",
    "unity-plugin/Editor/SourcePatchPathGuard.cs",
    "unity-plugin/Editor/SourcePatchReceiptStore.cs",
    "unity-plugin/Editor/SourcePatchDisableReceipt.cs",
)

_FORBIDDEN_CALL = "SyncHelper.TriggerSync("


def test_forbidden_call_detector_actually_detects():
    assert _FORBIDDEN_CALL in "x = SyncHelper.TriggerSync(resolve: false);"
    assert _FORBIDDEN_CALL not in "SyncHelper.CurrentEpoch"  # read, not control - allowed


def test_source_patch_control_files_never_call_sync_helper_trigger_sync_directly():
    """PR-06 Gap C: the reload-control call site must stay confined to
    SourcePatchReloadPort.cs's SyncHelperReloadPort adapter (plan §8.4:
    "SourcePatch владеет patch/disable/reconcile, Reload — подтверждением
    compile/domain outcome"). SourcePatchModePolicyReloadPortTests.cs already
    proves this dynamically for one call site; this is the static lock so a
    future edit cannot silently reintroduce a second direct call."""
    offenders = {}
    for rel_path in _SOURCE_PATCH_CONTROL_FILES:
        text = (REPO_ROOT / rel_path).read_text(encoding="utf-8")
        if _FORBIDDEN_CALL in text:
            offenders[rel_path] = _FORBIDDEN_CALL
    assert not offenders, f"direct SyncHelper.TriggerSync call(s) found outside the port adapter: {offenders}"
