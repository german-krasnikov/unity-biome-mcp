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
    monkeypatch: pytest.MonkeyPatch,
):
    calls: list[tuple[str, dict]] = []

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

    asyncio.run(cell_script._phase_on_retained_object(port=9600))  # must not raise

    write_calls = [c for c in calls if c[0] == "source_patch_write"]
    assert len(write_calls) == 5  # v1, v2, invalid, v2, v3 — all attempted


def test_phase_on_retained_object_raises_when_invalid_is_unexpectedly_accepted(
    monkeypatch: pytest.MonkeyPatch,
):
    async def _call(port, command, args):
        return "ok"  # invalid mutation is (wrongly) accepted every time

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    with pytest.raises(cell_script.FsrQualificationCellError, match="unexpectedly accepted"):
        asyncio.run(cell_script._phase_on_retained_object(port=9600))


def test_phase_on_retained_object_reraises_unrelated_error_for_invalid_kind(
    monkeypatch: pytest.MonkeyPatch,
):
    """Only a genuine rejection is expected for "invalid" — any other
    RunnerError (e.g. a real infra failure) must still fail the cell, not
    be silently swallowed."""

    async def _call(port, command, args):
        if command == "source_patch_write" and "System.Func<int>" in args.get("content", ""):
            raise cell_script.durable.RunnerError("source_patch_write failed: connection reset")
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    with pytest.raises(cell_script.durable.RunnerError, match="connection reset"):
        asyncio.run(cell_script._phase_on_retained_object(port=9600))


def test_phase_on_retained_object_still_propagates_valid_kind_failures(
    monkeypatch: pytest.MonkeyPatch,
):
    async def _call(port, command, args):
        if command == "source_patch_write" and '"v1"' not in str(args) and "return 1;" in args.get("content", ""):
            raise cell_script.durable.RunnerError("source_patch_write failed: boom")
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    with pytest.raises(cell_script.durable.RunnerError, match="boom"):
        asyncio.run(cell_script._phase_on_retained_object(port=9600))


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

    monkeypatch.setattr(cell_script.worker, "create_worker", lambda *a, **k: None)
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
