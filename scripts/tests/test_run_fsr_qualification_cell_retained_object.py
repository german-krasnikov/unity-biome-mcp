"""P1-20 qualification matrix: _phase_on_retained_object's handling of the
deliberately-invalid mutation.

Run 6 (33390881487): min-macos-arm64 got past the source_patch_write
routing fix (Run 5's bug) and crashed one step deeper — the retained-object
scenario deliberately proves an invalid (non body-only) mutation is
rejected pre-effect ("1 -> 2 -> invalid stays 2 -> 3", §6 P0-80): a
rejection for the "invalid" kind is the CORRECT, expected outcome, not a
cell failure. The driver let durable.call's RunnerError for that specific
call propagate and abort the whole cell, exactly the receipt observed:
"source_patch_write failed: STATE: source patch rejected the replacement
body; no effect".

Runs in the standard `scripts/tests` lane: no Unity, no network —
durable.call is monkeypatched.
"""
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS.parent))
import run_fsr_qualification_cell as cell_script  # noqa: E402


def test_phase_on_retained_object_survives_expected_invalid_rejection(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
):
    calls: list[tuple[str, dict]] = []
    target = tmp_path / "target.cs"
    target.write_bytes(cell_script.harness.target_body("v0").encode("utf-8"))

    async def _call(port, command, args):
        calls.append((command, args))
        if command == "source_patch_write" and "System.Func<int>" in args.get("content", ""):
            raise cell_script.durable.RunnerError(
                "source_patch_write failed: STATE: source patch rejected the "
                "replacement body; no effect"
            )
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    asyncio.run(cell_script._phase_on_retained_object(port=9600, target_path=target))  # must not raise

    write_calls = [c for c in calls if c[0] == "source_patch_write"]
    assert len(write_calls) == 4  # v1, v2, invalid, v3 — all attempted


def test_phase_on_retained_object_raises_when_invalid_is_unexpectedly_accepted(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
):
    target = tmp_path / "target.cs"
    target.write_bytes(cell_script.harness.target_body("v0").encode("utf-8"))

    async def _call(port, command, args):
        return "ok"  # invalid mutation is (wrongly) accepted every time

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    with pytest.raises(cell_script.FsrQualificationCellError, match="unexpectedly accepted"):
        asyncio.run(cell_script._phase_on_retained_object(port=9600, target_path=target))


def test_phase_on_retained_object_reraises_unrelated_error_for_invalid_kind(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
):
    """Only a genuine rejection is expected for "invalid" — any other
    RunnerError (e.g. a real infra failure) must still fail the cell, not
    be silently swallowed."""
    target = tmp_path / "target.cs"
    target.write_bytes(cell_script.harness.target_body("v0").encode("utf-8"))

    async def _call(port, command, args):
        if command == "source_patch_write" and "System.Func<int>" in args.get("content", ""):
            raise cell_script.durable.RunnerError("source_patch_write failed: connection reset")
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    with pytest.raises(cell_script.durable.RunnerError, match="connection reset"):
        asyncio.run(cell_script._phase_on_retained_object(port=9600, target_path=target))


def test_phase_on_retained_object_still_propagates_valid_kind_failures(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
):
    target = tmp_path / "target.cs"
    target.write_bytes(cell_script.harness.target_body("v0").encode("utf-8"))

    async def _call(port, command, args):
        if command == "source_patch_write" and '"v1"' not in str(args) and "return 1;" in args.get("content", ""):
            raise cell_script.durable.RunnerError("source_patch_write failed: boom")
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    with pytest.raises(cell_script.durable.RunnerError, match="boom"):
        asyncio.run(cell_script._phase_on_retained_object(port=9600, target_path=target))


# ---------------------------------------------------------------------------
# BOM/redundant-write fix — Run 6 (33390881487): min-macos-arm64 got past
# the source_patch_write routing fix and failed one step deeper:
# "source_patch_write failed: STATE: source patch rejected the replacement
# body; no effect" on the very first ON-mode write (v1). Verified in
# AssetDatabaseHelper.cs: the legacy write_text route uses
# File.WriteAllText(abs, content, System.Text.Encoding.UTF8) — .NET's
# BOM-including UTF8 — while SourcePatchModePolicy.TryApplyWrite builds its
# own newBytes via Encoding.UTF8.GetBytes(content), which never adds one.
# _phase_off_legacy_compile wrote v0 to disk directly (install_fixture)
# AFTER Unity was already running, then immediately re-wrote the same
# content again through the legacy TCP route (BOM-introducing) — a
# redundant write that bypasses Unity's normal startup asset-import
# entirely. Fix: install the fixture before Unity ever launches (picked up
# by its own startup AssetDatabase scan, matching the proven local P0-80
# shape), and drop the redundant post-launch legacy write.
# ---------------------------------------------------------------------------

def test_phase_off_legacy_compile_no_longer_writes_the_fixture_itself(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """The fixture must already be on disk before this phase launches
    Unity — this phase only launches and waits for the port."""
    write_calls: list = []

    async def _call(port, command, args):
        write_calls.append((command, args))
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    install_calls: list = []
    monkeypatch.setattr(
        cell_script.harness, "install_fixture", lambda project: install_calls.append(project)
    )
    monkeypatch.setattr(cell_script, "_launch", lambda **k: cell_script.subprocess.Popen)

    class _FakeProc:
        def poll(self):
            return None

    monkeypatch.setattr(cell_script, "_launch", lambda **k: _FakeProc())
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)

    import asyncio

    asyncio.run(
        cell_script._phase_off_legacy_compile(
            unity=tmp_path / "Unity",
            project=tmp_path / "worker",
            port=9600,
            log=tmp_path / "unity.log",
            startup_timeout=1.0,
            evidence_out=tmp_path / "evidence",
            os_name="Linux",
        )
    )

    assert install_calls == []
    assert write_calls == []


def test_run_full_installs_fixture_before_launching_unity(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """install_fixture must happen before the first Unity launch of the
    cell — before Unity ever starts, not after, so Unity's own startup
    AssetDatabase scan imports it with no BOM-introducing legacy write."""
    order: list[str] = []

    def _create_worker_stub(source_project, project, **k):
        (project / "Packages").mkdir(parents=True, exist_ok=True)
        (project / "Packages" / "manifest.json").write_text('{"dependencies": {}}', encoding="utf-8")

    monkeypatch.setattr(cell_script.worker, "create_worker", _create_worker_stub)
    monkeypatch.setattr(cell_script.worker, "rewrite_manifest_pin", lambda *a, **k: None)
    monkeypatch.setattr(
        cell_script.harness, "install_fixture", lambda project: order.append("install_fixture")
    )
    monkeypatch.setattr(cell_script.harness, "validate_installed_fixture", lambda *a, **k: None)
    monkeypatch.setattr(
        cell_script, "_launch", lambda **k: order.append("_launch") or _FakeProcessForOrder()
    )
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)
    monkeypatch.setattr(
        cell_script.preseed, "preseed_editor_prefs", lambda project, *, os_name: {"applied": True}
    )

    async def _stop(process):
        return None

    monkeypatch.setattr(cell_script, "_stop", _stop)

    async def _off_disable_stub(*, port, project):
        return {"epoch_delta_is_one": True}

    monkeypatch.setattr(cell_script, "_phase_off_disable_evidence", _off_disable_stub)
    monkeypatch.setattr(cell_script, "_manifest_matches_pre_pin", lambda project, pre_pin: True)

    async def _final_restore_stub(*, port, project, target_path):
        return {"restore_sha_matches": True}

    monkeypatch.setattr(cell_script, "_phase_final_restore", _final_restore_stub)

    async def _call(port, command, args):
        if command == "source_patch_write" and "System.Func<int>" in args.get("content", ""):
            raise cell_script.durable.RunnerError(
                "source_patch_write failed: STATE: source patch rejected the "
                "replacement body; no effect"
            )
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    monkeypatch.setattr(cell_script, "_git_head_sha", lambda: "a" * 40)
    monkeypatch.setattr(cell_script, "_git_changed_paths", lambda base_sha: [])

    lock_path = tmp_path / "lock.json"
    lock_path.write_text(
        '{"base_product_sha": "' + "a" * 40 + '", "final_fsr_adapter_sha": "' + "b" * 40
        + '", "cells": {"u_min": {"unity_version": "6000.0.65f1", '
        '"unity_revision": "a18e2220bd50", "utf_version": "1.6.0"}, '
        '"u_max": {"unity_version": "6000.5.10f1", "unity_revision": "3bd4f66ad299", '
        '"utf_version": "1.6.0"}}}',
        encoding="utf-8",
    )
    pin_path = tmp_path / "pin.json"
    pin_path.write_text("{}", encoding="utf-8")

    import asyncio

    asyncio.run(
        cell_script.run_full(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            window="u_min",
            lock_path=lock_path,
            provider_pin=pin_path,
            evidence_out=tmp_path / "evidence",
            port=9600,
            startup_timeout=1.0,
            cell_name="min-linux-x64",
            os_name="Linux",
            arch="x64",
        )
    )

    assert order[0] == "install_fixture"
    assert order[1] == "_launch"


class _FakeProcessForOrder:
    def poll(self):
        return None


# ---------------------------------------------------------------------------
# byte-level diagnostics — Run 7: capture sha256 + first/last 32 hex bytes
# of the actual before (on disk) and after (about to be sent) content for
# every ON-mode write, embedded in the receipt, so a live run's real bytes
# can be compared against what is proven to work offline through the real
# classifier (verified locally: the exact v0->v1 pair is cleanly ADMITTED).
# ---------------------------------------------------------------------------

def test_phase_on_retained_object_returns_byte_diagnostics_for_every_write(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    target = tmp_path / "FastReloadTarget.cs"
    target.write_bytes(cell_script.harness.target_body("v0").encode("utf-8"))

    async def _call(port, command, args):
        if command == "source_patch_write" and "System.Func<int>" in args.get("content", ""):
            raise cell_script.durable.RunnerError(
                "source_patch_write failed: STATE: source patch rejected the "
                "replacement body; no effect"
            )
        if command == "source_patch_write":
            target.write_bytes(args["content"].encode("utf-8"))
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    diagnostics = asyncio.run(
        cell_script._phase_on_retained_object(port=9600, target_path=target)
    )

    assert len(diagnostics) == 4  # v1, v2, invalid, v3
    assert diagnostics[0]["kind"] == "v1"
    assert diagnostics[0]["result"] == "applied"
    assert "sha256" in diagnostics[0]["before"]
    assert "sha256" in diagnostics[0]["after"]
    assert diagnostics[2]["kind"] == "invalid"
    assert diagnostics[2]["result"].startswith("rejected")


def test_phase_on_retained_object_diagnostics_survive_unexpected_failure(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """Even when the scenario ultimately raises, whatever diagnostics were
    collected before the failure must still be attached to the raised
    error so they are not lost."""
    target = tmp_path / "FastReloadTarget.cs"
    target.write_bytes(cell_script.harness.target_body("v0").encode("utf-8"))

    async def _call(port, command, args):
        if command == "source_patch_write" and "return 1;" in args.get("content", ""):
            raise cell_script.durable.RunnerError("source_patch_write failed: boom")
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    with pytest.raises(cell_script.durable.RunnerError) as exc_info:
        asyncio.run(cell_script._phase_on_retained_object(port=9600, target_path=target))

    assert hasattr(exc_info.value, "byte_diagnostics")
    assert len(exc_info.value.byte_diagnostics) == 1


# ---------------------------------------------------------------------------
# Run 8 (33396935103) root cause: the on-mode-write-diagnostics.json byte
# capture (added for this exact purpose) showed the failing write's before
# and after sha256 were IDENTICAL ("5b5d7de4..." both sides, "return 2;").
# The old kind rotation ("v1", "v2", "invalid", "v2", "v3") repeats "v2"
# right after "invalid" is correctly rejected pre-effect — since the
# rejection left the file still at "v2", writing "v2" again is a genuine
# no-op that any correct body-only classifier legitimately refuses as
# "no-body-change". This was never the classifier, BOM, or JSON-unescape —
# it was a duplicate no-op step in the driver's own scenario, present since
# the very first commit (f5c5b746) that created the matrix. A faithful mock
# (reject when after == current on-disk content, mirroring the real
# classifier) exposes it; the old permissive mocks (reject only on
# "System.Func<int>") never did.
# ---------------------------------------------------------------------------

def test_phase_on_retained_object_never_attempts_a_no_op_duplicate_write(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch,
):
    target = tmp_path / "target.cs"
    target.write_bytes(cell_script.harness.target_body("v0").encode("utf-8"))
    state = {"current": target.read_bytes()}

    async def _call(port, command, args):
        if command != "source_patch_write":
            return "ok"
        content = args["content"].encode("utf-8")
        if content == state["current"] or "System.Func<int>" in args["content"]:
            raise cell_script.durable.RunnerError(
                "source_patch_write failed: STATE: source patch rejected the "
                "replacement body; no effect"
            )
        state["current"] = content
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    diagnostics = asyncio.run(
        cell_script._phase_on_retained_object(port=9600, target_path=target)
    )  # must not raise against a classifier that faithfully refuses no-op writes

    kinds = [d["kind"] for d in diagnostics]
    assert kinds == list(cell_script.ON_MODE_KIND_SEQUENCE)
    for i in range(len(kinds) - 1):
        assert kinds[i] != kinds[i + 1], "consecutive duplicate kind writes identical content — a no-op the real classifier rejects"


def test_on_mode_kind_sequence_has_no_consecutive_duplicates():
    seq = cell_script.ON_MODE_KIND_SEQUENCE
    for i in range(len(seq) - 1):
        assert seq[i] != seq[i + 1]
