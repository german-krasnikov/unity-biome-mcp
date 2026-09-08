"""S16b/S16c provider lifecycle regression: adding/removing the FSR UPM
package during a live mutation session is now a NORMAL situation, not a
crash -- owner report (2026-09-07, translated): "it feels like verifying
mutation and adding the FSR package resets all progress and the agent stops
working". See Plans/MUTATION-REGRESSION-MODULE.md "Provider Lifecycle
Regression" and
Plans/Reviews/mutation-regression-2026-09-07/provider-lifecycle-investigation.md
(the controlled procedure these stories mirror: 3.1s/10.2s TCP downtime,
6.6s/15.0s total wall-clock, epoch monotonic, no orphan files, byte-exact
restore -- both product defects it found, sync_unity's compile-guard race
(PD-1) and S16's xfail masking (PD-2), are already fixed).

No owned_canary here -- these stories mutate Packages/manifest.json +
packages-lock.json directly, not Assets/TestsTemp scaffolding. Shared
enable/disable/status helpers live in _canary.py; manifest edit/restore/
probe helpers live in _provider_lifecycle.py (this module's private
companion -- _canary.py is already 273 lines, close to the 300-line cap).
"""
import hashlib
import time
from pathlib import Path

import pytest
from mcp.server.fastmcp.exceptions import ToolError

from tests.mutation._canary import _disable_mutation_mode, _quiet, _status_field, make_raw_send
from tests.mutation._provider_lifecycle import (
    CLEAN_SYNC_RESULTS,
    probe_provider_state,
    remove_provider_from_manifest,
    restore_provider_to_manifest,
    snapshot_bridge_files,
)
from tests.mutation.conftest import MUTATION_PORT, MUTATION_PROJECT
from unity_mcp.tools.sync import _parse_status

# S16c does two real PM-resolve-triggering sync_unity() calls in its own
# body (remove, then restore) plus a third in provider_lifecycle's teardown
# -- unlike the other story files' single reload per test, so pytest's own
# per-item wall clock (call + teardown) needs a larger bound than the 300s
# used elsewhere. First live run measured 103.69s/110.51s (call/teardown) for
# S16b and 240.58s call for S16c (git-backed PM resolve is not always warm-
# cache-fast) -- 900s keeps ~3x headroom over that worst-observed total.
pytestmark = [pytest.mark.live, pytest.mark.mutation_live, pytest.mark.timeout(900)]

# Investigation measured 3.1s (remove) / 10.2s (re-add) TCP downtime, 6.6s /
# 15.0s total wall-clock to a clean, ready worker (PM resolve + git clone +
# compile + domain reload dominates re-add) under warm-cache conditions; the
# first live run of this file measured up to 240.58s for a single sync_unity
# call under a colder cache. 300s (2.5x SESSION_TIMEOUT's 120s default, and
# well over the worst observed call) keeps one sync_unity call from starving
# the rest of a test's own pytest-timeout(900) budget.
_SYNC_TIMEOUT_S = 300.0

# unity-plugin/Editor/SourcePatchModePolicy.cs:114,118 raises exactly
# InvalidOperationException("source patch provider absent — cannot enable")
# when EnableOn() finds no registered provider; ErrorClassifier.cs:27,39 maps
# InvalidOperationException to the "STATE: {message}" wire text.
_PROVIDER_ABSENT_ENABLE_ERROR = "STATE: source patch provider absent — cannot enable"


@pytest.fixture
async def provider_lifecycle(mutation_sdk):
    """Snapshot Packages/manifest.json + packages-lock.json (bytes + sha256)
    and the bridge port/state file names, yield, then ALWAYS restore the
    manifest/lock byte-exact and re-sync -- even when the test body failed --
    so the session `project_baseline_guard` stays green when only one of
    these two tests runs."""
    project = Path(MUTATION_PROJECT).resolve()
    manifest_path = project / "Packages" / "manifest.json"
    lock_path = project / "Packages" / "packages-lock.json"
    saved = {"manifest_bytes": manifest_path.read_bytes(), "lock_bytes": lock_path.read_bytes()}
    ports_before, state_before = snapshot_bridge_files()
    info = {
        "project": project,
        "saved": saved,
        "manifest_sha": hashlib.sha256(saved["manifest_bytes"]).hexdigest(),
        "lock_sha": hashlib.sha256(saved["lock_bytes"]).hexdigest(),
        "ports_before": ports_before,
        "state_before": state_before,
    }
    try:
        yield info
    finally:
        restore_provider_to_manifest(project, saved)
        raw_send = make_raw_send(mutation_sdk.bridge)
        result = await mutation_sdk.sync_unity(resolve=True, timeout=_SYNC_TIMEOUT_S)
        assert result in CLEAN_SYNC_RESULTS, f"provider_lifecycle teardown resync: {result!r}"
        status = await raw_send("get_status", {})
        state = probe_provider_state(status)
        assert state["provider"] == "installed", status
        assert state["state"] == "Off", status
        errors = await raw_send("get_compile_errors", {})
        assert errors == "No compilation errors", errors


async def test_provider_remove_recover(mutation_sdk, provider_lifecycle):
    """S16b: remove FSR from the manifest mid-session; sync_unity absorbs the
    resulting compile/reload and returns 'sync clean' within the bound,
    compile stays clean, source_patch_state settles to Unavailable (provider
    absent, by design -- SourcePatchProviderSlot is a static field wiped by
    the domain reload), sync epoch advances, mutation_mode enable is
    correctly rejected with the exact provider-absent error, and the TCP
    port is unchanged (every call below still reaches MUTATION_PORT)."""
    raw_send = make_raw_send(mutation_sdk.bridge)

    # Negative control: provider installed, Off, before any mutation.
    baseline_status = await raw_send("get_status", {})
    baseline = probe_provider_state(baseline_status)
    assert baseline["provider"] == "installed", baseline_status
    assert baseline["state"] == "Off", baseline_status
    epoch_before, sync_state_before, _ = _parse_status(await raw_send("sync_status", {}))
    assert sync_state_before in ("ready", "idle"), sync_state_before

    remove_provider_from_manifest(provider_lifecycle["project"])

    t0 = time.monotonic()
    result = await mutation_sdk.sync_unity(resolve=True, timeout=_SYNC_TIMEOUT_S)
    elapsed = time.monotonic() - t0
    assert result in CLEAN_SYNC_RESULTS, f"{result!r} after {elapsed:.1f}s"
    assert elapsed <= _SYNC_TIMEOUT_S, f"sync_unity took {elapsed:.1f}s (bound {_SYNC_TIMEOUT_S}s)"

    errors = await raw_send("get_compile_errors", {})
    assert errors == "No compilation errors", errors

    status_after = await raw_send("get_status", {})
    after = probe_provider_state(status_after)
    assert after["provider"] == "absent", status_after
    assert after["state"] == "Unavailable", status_after

    epoch_after, sync_state_after, _ = _parse_status(await raw_send("sync_status", {}))
    assert epoch_after > epoch_before, (epoch_before, epoch_after)
    assert sync_state_after in ("ready", "idle"), sync_state_after

    await _quiet(mutation_sdk)  # about to assert the enable attempt's exact error text
    with pytest.raises(ToolError) as exc_info:
        await mutation_sdk.editor(action="mutation_mode", enable=True)
    assert str(exc_info.value) == _PROVIDER_ABSENT_ENABLE_ERROR, str(exc_info.value)

    # Port unchanged: mutation_sdk.bridge is hardwired to MUTATION_PORT and
    # never reconnects to a different port -- every raw_send call above only
    # succeeded because the reload's bridge came back up on the same port.
    port_status = await raw_send("get_status", {})
    assert f"port={MUTATION_PORT}" in port_status, port_status


async def test_provider_readd_recover(mutation_sdk, provider_lifecycle):
    """S16c: after removal (S16b's own steps, repeated here), restore the
    manifest+lock byte-exact and re-sync -- compile clean, provider
    reinstalled, source_patch_state back to Off, epoch keeps advancing, an
    enable->disable round trip works, the restored files hash back to the
    original snapshot, no new orphaned bridge port/state files appear, and
    the scene is not left dirty."""
    raw_send = make_raw_send(mutation_sdk.bridge)

    remove_provider_from_manifest(provider_lifecycle["project"])
    removed = await mutation_sdk.sync_unity(resolve=True, timeout=_SYNC_TIMEOUT_S)
    assert removed in CLEAN_SYNC_RESULTS, removed
    epoch_removed, _, _ = _parse_status(await raw_send("sync_status", {}))
    removed_state = probe_provider_state(await raw_send("get_status", {}))
    assert removed_state["provider"] == "absent", removed_state

    restore_provider_to_manifest(provider_lifecycle["project"], provider_lifecycle["saved"])
    restored_manifest = provider_lifecycle["project"] / "Packages" / "manifest.json"
    restored_lock = provider_lifecycle["project"] / "Packages" / "packages-lock.json"
    assert hashlib.sha256(restored_manifest.read_bytes()).hexdigest() == provider_lifecycle["manifest_sha"]
    assert hashlib.sha256(restored_lock.read_bytes()).hexdigest() == provider_lifecycle["lock_sha"]

    t0 = time.monotonic()
    readd = await mutation_sdk.sync_unity(resolve=True, timeout=_SYNC_TIMEOUT_S)
    elapsed = time.monotonic() - t0
    assert readd in CLEAN_SYNC_RESULTS, f"{readd!r} after {elapsed:.1f}s"
    assert elapsed <= _SYNC_TIMEOUT_S, f"sync_unity took {elapsed:.1f}s (bound {_SYNC_TIMEOUT_S}s)"

    errors = await raw_send("get_compile_errors", {})
    assert errors == "No compilation errors", errors

    readd_state = probe_provider_state(await raw_send("get_status", {}))
    assert readd_state["provider"] == "installed", readd_state
    assert readd_state["state"] == "Off", readd_state

    epoch_readded, _, _ = _parse_status(await raw_send("sync_status", {}))
    assert epoch_readded > epoch_removed, (epoch_removed, epoch_readded)

    try:
        await _quiet(mutation_sdk)
        enabled = await mutation_sdk.editor(action="mutation_mode", enable=True)
        assert enabled.strip() == "mutation_mode:true", enabled
    finally:
        await _disable_mutation_mode(mutation_sdk, raw_send)  # asserts settle == sync clean

    ports_after, state_after = snapshot_bridge_files()
    assert ports_after == provider_lifecycle["ports_before"], (provider_lifecycle["ports_before"], ports_after)
    assert state_after == provider_lifecycle["state_before"], (provider_lifecycle["state_before"], state_after)

    scene_state = await mutation_sdk.editor(action="state")
    assert _status_field(scene_state, "dirty", sep=":") == "False", scene_state
