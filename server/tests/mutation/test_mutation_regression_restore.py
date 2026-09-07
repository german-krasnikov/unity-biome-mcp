"""S14-S16 mutation-mode domain-reload/restore stories: explicit disable
compiles a real new domain, an exact byte-for-byte source restore recompiles
clean, and provider-object isolation across a full enable->patch->disable
cycle.

See Plans/MUTATION-REGRESSION-MODULE.md matrix rows S14-S16 and
Plans/Reviews/build-readiness-implementation-2026-09-06/LIVE-QUALIFICATION.md
("Explicit disable completed and compiled behavior verified" -- epoch+1, Off,
new game-assembly MVID, DSL 202 recompiled).

S11-S13 (enable/patch/block stories, no domain reload) live in
test_mutation_regression.py -- split for file-size, not by behavior. Shared
enable/disable/patch flow helpers live in _canary.py.
"""
import hashlib
import json

import pytest
from gauntlet.readiness_canary import OwnedFileEdit, render_dsl, target_body
from gauntlet.readiness_dsl_sdk import qualify_receipt
from mcp.server.fastmcp.exceptions import ToolError

from tests.mutation._canary import (
    _disable_mutation_mode,
    _quiet,
    _status_field,
    _wait_source_patch_off,
    make_raw_send,
)
from unity_mcp.tools.sync import _parse_status

pytestmark = [pytest.mark.live, pytest.mark.mutation_live, pytest.mark.timeout(300)]

# S14: SyncHelper.ComputeStamp() (diagnose()'s mvid= field) only aggregates
# assemblies whose name starts with "UnityMCP." -- it never includes
# Assembly-CSharp, where the canary Target.cs actually recompiles, so it is
# structurally unable to observe a game-assembly reload (confirmed by reading
# DiagnoseCommand.cs/SyncHelper.ComputeStamp() -- name.StartsWith("UnityMCP.")).
# AppDomain.CurrentDomain.GetAssemblies() reflection via execute_code was tried
# and empirically falsified live: it reported an unchanged Assembly-CSharp
# ModuleVersionId across a disable that independently, verifiably rewrote
# Library/ScriptAssemblies/Assembly-CSharp.dll on disk (different sha256,
# advanced mtime) -- so that in-process reflection reads a stale snapshot here.
# The on-disk compiled DLL is the ground truth: identical bytes/sha256 mean an
# in-place patch, a changed sha256 means a genuine recompiled domain.
_ASSEMBLY_CSHARP_DLL_REL = ("Library", "ScriptAssemblies", "Assembly-CSharp.dll")


def _assembly_csharp_sha256(owned_canary: dict) -> str:
    project_root = owned_canary["target_path"].parents[4]  # .../Assets/TestsTemp/MutationLive/<uid8>/file.cs
    dll = project_root.joinpath(*_ASSEMBLY_CSHARP_DLL_REL)
    return hashlib.sha256(dll.read_bytes()).hexdigest()


# S16: BiomeSourcePatchDispatcher is created live by the external FSR provider
# adapter package (not committed to this repo) -- there is no C# source here to
# read the name from. Search the running worker instead of trusting statics.
_DISPATCHER_SEARCH_CODE = (
    "int byName = 0, byType = 0;\n"
    "foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())\n"
    "    if (go.name == \"BiomeSourcePatchDispatcher\") byName++;\n"
    "foreach (var comp in Resources.FindObjectsOfTypeAll<Component>())\n"
    "    if (comp != null && comp.GetType().Name.Contains(\"SourcePatchDispatcher\")) byType++;\n"
    "int previewCount = UnityEditor.SceneManagement.EditorSceneManager.previewSceneCount;\n"
    "return $\"dispatcher_by_name={byName}\\ndispatcher_by_type={byType}\\npreview_count={previewCount}\";"
)

# S16: exact expected leak count while the known bug is open. Root cause: the
# fork adapter's BiomeFsrSourcePatchProvider.GetOrCreateDispatcher leaks exactly
# one BiomeSourcePatchDispatcher HideAndDontSave GameObject per enable->patch->
# disable cycle -- its static _dispatcher field resets on every domain reload
# while the HideAndDontSave host GameObject survives the reload, so the next
# cycle can never find it and creates another one.
_KNOWN_DISPATCHER_LEAK_DELTA = 1


async def test_explicit_disable_compiles_new_domain(owned_canary, mutation_sdk):
    """S14: with mutation ON and a retained-instance 202 patch already applied
    (S12 proves that step in isolation -- not re-asserted here), an explicit
    disable causes exactly one domain reload: sync epoch advances by exactly
    one, source_patch_state settles to Off with no lingering receipt/coordinator
    (source_patch_op=none), the compiled Assembly-CSharp.dll changes on disk (a
    real reload, not another in-place patch -- see _assembly_csharp_sha256's
    docstring for why diagnose()'s mvid= field cannot be used here), and the
    newly compiled domain still observes the patched body (202) while the
    pre-patch body (101) is gone."""
    raw_send = make_raw_send(mutation_sdk.bridge)
    try:
        await _quiet(mutation_sdk)  # owned_canary's own setup left writes pending
        enabled = await mutation_sdk.editor(action="mutation_mode", enable=True)
        assert enabled.strip() == "mutation_mode:true", enabled

        write_result = await mutation_sdk.asset(
            action="write_text", path=owned_canary["target_rel"],
            content=target_body(202).decode("utf-8"))
        assert write_result.startswith("ok:write"), write_result

        raw_202 = await mutation_sdk.run_playtest(
            script=render_dsl(owned_canary["object_path"], 202), format="json", timeout=10)
        qualify_receipt(json.loads(raw_202), expect_failure=False)

        epoch_before, _, _ = _parse_status(await raw_send("sync_status", {}))
        dll_sha_before = _assembly_csharp_sha256(owned_canary)

        await _quiet(mutation_sdk)  # about to assert the disable's own exact-string result
        disable_result = await mutation_sdk.editor(action="mutation_mode", enable=False)
        assert disable_result.strip() == "requested", disable_result
        await _wait_source_patch_off(raw_send)

        status_after = await raw_send("sync_status", {})
        epoch_after, state_after, _ = _parse_status(status_after)
        assert epoch_after == epoch_before + 1, (epoch_before, epoch_after)
        assert state_after == "ready", status_after

        public_status = await raw_send("get_status", {})
        assert _status_field(public_status, "source_patch_state") == "Off", public_status
        assert _status_field(public_status, "source_patch_op") == "none", public_status
        assert _status_field(public_status, "source_patch_recovery") == "false", public_status

        dll_sha_after = _assembly_csharp_sha256(owned_canary)
        assert dll_sha_after != dll_sha_before, (dll_sha_before, dll_sha_after)

        raw_202_after = await mutation_sdk.run_playtest(
            script=render_dsl(owned_canary["object_path"], 202), format="json", timeout=10)
        qualify_receipt(json.loads(raw_202_after), expect_failure=False)

        with pytest.raises(ToolError) as exc_info:
            await mutation_sdk.run_playtest(
                script=render_dsl(owned_canary["object_path"], 101), format="json", timeout=10)
        qualify_receipt(json.loads(str(exc_info.value)), expect_failure=True)
    finally:
        await _disable_mutation_mode(mutation_sdk, raw_send)


async def test_exact_source_restore_101(owned_canary, mutation_sdk, tmp_path):
    """S15: continuing the S14 shape (enable -> patch 202 -> disable), restore
    the exact original 101 bytes through the ordinary Off-route asset write
    (SourcePatchHost.WriteText only routes through the provider while OnReady),
    then an ordinary sync_unity() recompiles it -- DSL 101 passes, DSL 202 now
    fails, and the on-disk bytes/sha256 match the pre-patch original exactly."""
    raw_send = make_raw_send(mutation_sdk.bridge)
    # Captured before any mutation -- read-only source of truth for the exact
    # original bytes. Never call .replace()/.restore() on this: every actual
    # disk write below goes through Unity's own public asset route, and doing
    # a second, competing Python-side write would desync OwnedFileEdit's CAS
    # state against what Unity actually wrote.
    edit = OwnedFileEdit(owned_canary["target_path"], tmp_path)
    try:
        await _quiet(mutation_sdk)  # owned_canary's own setup left writes pending
        enabled = await mutation_sdk.editor(action="mutation_mode", enable=True)
        assert enabled.strip() == "mutation_mode:true", enabled

        write_202 = await mutation_sdk.asset(
            action="write_text", path=owned_canary["target_rel"],
            content=target_body(202).decode("utf-8"))
        assert write_202.startswith("ok:write"), write_202

        raw_202 = await mutation_sdk.run_playtest(
            script=render_dsl(owned_canary["object_path"], 202), format="json", timeout=10)
        qualify_receipt(json.loads(raw_202), expect_failure=False)

        # Reused mid-test, not just as cleanup: it is exactly "disable, wait
        # Off, settle sync" -- OnReady(202) -> Off, new domain compiled with 202.
        await _disable_mutation_mode(mutation_sdk, raw_send)

        await _quiet(mutation_sdk)
        write_101 = await mutation_sdk.asset(
            action="write_text", path=owned_canary["target_rel"],
            content=edit.before.decode("utf-8"))
        assert write_101.startswith("ok:write"), write_101

        synced = await mutation_sdk.sync_unity(timeout=120)
        assert synced == "sync clean", synced

        clean = await mutation_sdk.diagnose(expected_compile=False)
        assert clean == "CLEAN-LIVE", clean

        raw_101 = await mutation_sdk.run_playtest(
            script=render_dsl(owned_canary["object_path"], 101), format="json", timeout=10)
        qualify_receipt(json.loads(raw_101), expect_failure=False)

        with pytest.raises(ToolError) as exc_info:
            await mutation_sdk.run_playtest(
                script=render_dsl(owned_canary["object_path"], 202), format="json", timeout=10)
        qualify_receipt(json.loads(str(exc_info.value)), expect_failure=True)

        # AssetDatabaseHelper.WriteText's File.WriteAllText(abs, content,
        # Encoding.UTF8) always emits a UTF-8 BOM on write (a real, consistent
        # characteristic of this production write route -- verified live: the
        # 202 write earlier in this same test went through the identical BOM-
        # adding path). install_canary's own original bytes never had one (a
        # raw Python write_bytes). Strip it before the exact-content compare
        # so this oracle checks the restored TEXT, not this route's encoding
        # artifact -- it would still catch any real corruption beyond the BOM.
        on_disk = owned_canary["target_path"].read_bytes()
        if on_disk.startswith(b"\xef\xbb\xbf"):
            on_disk = on_disk[3:]
        assert hashlib.sha256(on_disk).hexdigest() == hashlib.sha256(edit.before).hexdigest()
    finally:
        await _disable_mutation_mode(mutation_sdk, raw_send)


async def test_provider_isolation_after_disable(owned_canary, mutation_sdk):
    """S16 (disposable-worker scope -- deviation from the plan's manifest-CAS/
    full-provider-removal variant, documented here): after a full
    enable->patch->disable cycle, verify the cycle ITSELF leaves no NEW owned
    dispatcher/preview objects and a clean, non-dirty scene -- a before/after
    delta, not an absolute zero (this worker is shared across every test in
    this module/session, so an absolute count is not order-independent).

    PD-2: this used to carry @pytest.mark.xfail(strict=True) on the whole
    function, which also reported an unrelated setup/teardown exception as
    XFAIL -- teardown failures became invisible (false-green). Replaced with
    an imperative dispatch at the one known-flaky assertion point: any other
    failure (including in owned_canary/mutation_sdk setup or teardown) now
    surfaces as a normal ERROR/FAILURE, not masked.

    The dispatcher-delta oracle itself is unchanged: delta 0 means the fork
    fix landed (fail so this dispatch/marker gets removed); delta
    _KNOWN_DISPATCHER_LEAK_DELTA (1) is the confirmed, currently-open leak
    (xfail); any other delta is an unexpected shape (hard fail).
    The provider package is never removed from the worker manifest here --
    that full removal/reinstall closure is out of scope for this disposable-
    worker cell. The 516-file project baseline is asserted independently by
    the session `project_baseline_guard` fixture, not re-checked in this test
    body."""
    raw_send = make_raw_send(mutation_sdk.bridge)
    try:
        await _quiet(mutation_sdk)  # owned_canary's own setup left writes pending
        before_search = await mutation_sdk.execute_code(code=_DISPATCHER_SEARCH_CODE)
        before_by_name = int(_status_field(before_search, "dispatcher_by_name"))
        before_by_type = int(_status_field(before_search, "dispatcher_by_type"))

        await _quiet(mutation_sdk)  # owned_canary's own setup left writes pending
        enabled = await mutation_sdk.editor(action="mutation_mode", enable=True)
        assert enabled.strip() == "mutation_mode:true", enabled

        write_result = await mutation_sdk.asset(
            action="write_text", path=owned_canary["target_rel"],
            content=target_body(202).decode("utf-8"))
        assert write_result.startswith("ok:write"), write_result

        raw_202 = await mutation_sdk.run_playtest(
            script=render_dsl(owned_canary["object_path"], 202), format="json", timeout=10)
        qualify_receipt(json.loads(raw_202), expect_failure=False)

        await _disable_mutation_mode(mutation_sdk, raw_send)

        intent = await mutation_sdk.editor(action="mutation_mode")
        assert intent.strip() == "mutation_mode:false", intent

        public_status = await raw_send("get_status", {})
        assert _status_field(public_status, "source_patch_state") == "Off", public_status

        await _quiet(mutation_sdk)  # about to parse this execute_code result exactly
        search = await mutation_sdk.execute_code(code=_DISPATCHER_SEARCH_CODE)
        after_by_name = int(_status_field(search, "dispatcher_by_name"))
        after_by_type = int(_status_field(search, "dispatcher_by_type"))
        # dispatcher_by_type is an independent, orthogonal oracle -- proven live
        # to be 0 both before and after on this fork build (its host GameObject
        # carries no Component whose type name contains "SourcePatchDispatcher"),
        # so it never observes the by-name leak and must not be coupled to its
        # delta. Asserted against the known-0 expectation directly (not against
        # each other) so a probe-methodology change that shifts both counts in
        # lockstep is still caught.
        assert before_by_type == 0, f"unexpected pre-cycle by_type {before_by_type}: {search}"
        assert after_by_type == 0, f"unexpected post-cycle by_type {after_by_type}: {search}"
        # preview_count is reported as evidence only (task note) -- not asserted:
        # Unity's own asset-thumbnail preview scenes are an unrelated source of
        # nonzero counts, so it is not a reliable owned-object oracle here.

        state = await mutation_sdk.editor(action="state")
        assert _status_field(state, "dirty", sep=":") == "False", state

        delta = after_by_name - before_by_name
        if delta == 0:
            pytest.fail("dispatcher leak fixed — remove the xfail dispatch")
        elif delta == _KNOWN_DISPATCHER_LEAK_DELTA:
            pytest.xfail(
                "fork adapter leaks one BiomeSourcePatchDispatcher HideAndDontSave "
                "GameObject per enable->patch->disable cycle (static _dispatcher lost "
                "on domain reload); remove xfail after the fork fix lands")
        else:
            pytest.fail(f"unexpected dispatcher delta {delta}")
    finally:
        await _disable_mutation_mode(mutation_sdk, raw_send)
