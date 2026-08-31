"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: run_pilot's
target-version threading and exception handling.

Run 3 showed max-linux-x64/max-macos-arm64 pilots failing while their
min-* counterparts passed: run_pilot never called
create_worker(..., target_unity_version=..., target_unity_revision=...),
so every pilot worker declared U_MIN (6000.0.65f1) regardless of which
window's Editor actually launched it — for a max-* cell that is a headed
6000.5.10f1 Editor opening a 6000.0.65f1-declared project, which risks
Unity's interactive version-mismatch dialog (it blocks indefinitely outside
batchmode). Separately, HostedConformanceError (RuntimeError-based, not
OSError) was missing from run_pilot's except clause, so a real port-wait
timeout wasn't classified as INFRASTRUCTURE_BLOCKED and its message was
lost.

Runs in the standard `scripts/tests` lane: no Unity, no network —
create_worker/_launch/wait_for_port/durable.call/_stop are all
monkeypatched.
"""
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(SCRIPTS.parent))
import run_fsr_qualification_cell as cell_script  # noqa: E402


class _FakeProcess:
    def poll(self):
        return None


def _stub_common(monkeypatch: pytest.MonkeyPatch, *, create_worker_calls: list):
    monkeypatch.setattr(
        cell_script.worker,
        "create_worker",
        lambda *a, **k: create_worker_calls.append(k),
    )
    monkeypatch.setattr(cell_script, "_launch", lambda **k: _FakeProcess())
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)

    async def _stop(process):
        return None

    monkeypatch.setattr(cell_script, "_stop", _stop)


def test_run_pilot_forwards_target_unity_version_to_create_worker(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            unity_version="6000.5.10f1",
            unity_revision="3bd4f66ad299",
        )
    )

    assert len(calls) == 1
    assert calls[0]["target_unity_version"] == "6000.5.10f1"
    assert calls[0]["target_unity_revision"] == "3bd4f66ad299"


def test_run_pilot_defaults_target_version_to_none_when_no_window(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
        )
    )

    assert calls[0]["target_unity_version"] is None
    assert calls[0]["target_unity_revision"] is None


def test_run_pilot_classifies_hosted_conformance_timeout_as_infrastructure_blocked(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    def _wait_for_port(**kwargs):
        raise cell_script.HostedConformanceError(
            "Timed out waiting for Unity MCP port 127.0.0.1:9600: timed out"
        )

    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", _wait_for_port)

    written = {}

    def _write_pilot_evidence(evidence_out, **kwargs):
        written.update(kwargs)

    monkeypatch.setattr(cell_script.fq, "write_pilot_evidence", _write_pilot_evidence)

    import asyncio

    with pytest.raises(cell_script.HostedConformanceError):
        asyncio.run(
            cell_script.run_pilot(
                unity=tmp_path / "Unity",
                source_project=tmp_path / "source",
                work_root=tmp_path / "work",
                port=9600,
                startup_timeout=1.0,
                evidence_out=tmp_path / "evidence",
            )
        )

    assert written["outcome"] == "INFRASTRUCTURE_BLOCKED"
    assert "Timed out" in written["error"]


# ---------------------------------------------------------------------------
# _launch's force_d3d11 wiring (Windows-only diagnostic experiment)
# ---------------------------------------------------------------------------

def test_launch_forces_d3d11_only_on_windows(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    captured: dict = {}

    def _fake_build(unity, project, log, *, force_d3d11=False):
        captured["force_d3d11"] = force_d3d11
        return [str(unity)]

    monkeypatch.setattr(cell_script.fq, "build_headed_unity_command", _fake_build)
    monkeypatch.setattr(cell_script, "write_mcp_project_settings", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.subprocess, "Popen", lambda *a, **k: _FakeProcess())
    # CREATE_NEW_PROCESS_GROUP only exists on a real Windows Python build;
    # inject it so the os.name == "nt" branch is exercisable on any dev/CI
    # platform, matching test_hosted_conformance_runner.py's
    # test_windows_signal_process_does_not_require_sigkill precedent for
    # testing this same Windows-only branch cross-platform.
    monkeypatch.setattr(cell_script.subprocess, "CREATE_NEW_PROCESS_GROUP", 0x200, raising=False)

    monkeypatch.setattr(cell_script.os, "name", "nt")
    cell_script._launch(unity=tmp_path / "Unity", project=tmp_path / "p", port=9600, log=tmp_path / "l")
    assert captured["force_d3d11"] is True

    monkeypatch.setattr(cell_script.os, "name", "posix")
    cell_script._launch(unity=tmp_path / "Unity", project=tmp_path / "p", port=9600, log=tmp_path / "l")
    assert captured["force_d3d11"] is False


# ---------------------------------------------------------------------------
# preseed wiring — Run 4 root cause (FSR's first-run modal dialog blocks
# ProcessInitializeOnLoadAttributes on a headed Editor). Defense-in-depth:
# preseed runs before the Editor launches, and a failed preseed must never
# abort the pilot.
# ---------------------------------------------------------------------------

def test_run_pilot_calls_preseed_before_launch_and_embeds_receipt_on_success(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    monkeypatch.setattr(
        cell_script.preseed,
        "preseed_editor_prefs",
        lambda project, *, os_name: {"mechanism": "linux_prefs_xml", "applied": True, "keys": []},
    )

    written = {}
    monkeypatch.setattr(
        cell_script.fq,
        "write_pilot_evidence",
        lambda evidence_out, **kwargs: written.update(kwargs),
    )

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            os_name="Linux",
            evidence_out=tmp_path / "evidence",
        )
    )

    assert written["preseed"]["mechanism"] == "linux_prefs_xml"
    assert written["preseed"]["applied"] is True


def test_run_pilot_survives_a_failed_preseed(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    """Preseed is defense-in-depth, never the primary fix — a preseed
    failure must not abort the pilot, only be recorded honestly."""
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)

    def _fail(project, *, os_name):
        raise cell_script.preseed.EditorPrefsPreseedError("no ProjectSettings.asset")

    monkeypatch.setattr(cell_script.preseed, "preseed_editor_prefs", _fail)

    written = {}
    monkeypatch.setattr(
        cell_script.fq,
        "write_pilot_evidence",
        lambda evidence_out, **kwargs: written.update(kwargs),
    )

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            os_name="Windows",
            evidence_out=tmp_path / "evidence",
        )
    )

    assert written["preseed"]["applied"] is False
    assert "no ProjectSettings.asset" in written["preseed"]["error"]


# ---------------------------------------------------------------------------
# Run 5 finding: min-linux-x64 still hung even with preseed applied once at
# cell start — an intermediate Unity process exit can overwrite the
# underlying prefs store (Unity's own exit-time prefs flush), losing the
# preseeded values before the actually-vulnerable launch (steps 4-6,
# package installed) runs. Preseed must be (re)applied before EVERY Unity
# launch in run_full, not just once at cell start.
# ---------------------------------------------------------------------------

def test_apply_preseed_returns_receipt_on_success(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setattr(
        cell_script.preseed,
        "preseed_editor_prefs",
        lambda project, *, os_name: {"mechanism": "linux_prefs_xml", "applied": True, "keys": []},
    )
    receipt = cell_script._apply_preseed(tmp_path / "worker", os_name="Linux")
    assert receipt["applied"] is True


def test_apply_preseed_survives_failure(tmp_path: Path, monkeypatch: pytest.MonkeyPatch):
    def _fail(project, *, os_name):
        raise cell_script.preseed.EditorPrefsPreseedError("boom")

    monkeypatch.setattr(cell_script.preseed, "preseed_editor_prefs", _fail)
    receipt = cell_script._apply_preseed(tmp_path / "worker", os_name="Linux")
    assert receipt["applied"] is False
    assert "boom" in receipt["error"]


def test_run_full_applies_preseed_before_every_unity_launch(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    """3 launches in run_full (steps 1-2, steps 4-6, steps 8-9) — preseed
    must be called immediately before each one, not just once at cell
    start."""
    preseed_calls: list = []
    launch_calls: list = []

    monkeypatch.setattr(
        cell_script.preseed,
        "preseed_editor_prefs",
        lambda project, *, os_name: preseed_calls.append(os_name) or {"applied": True, "mechanism": os_name},
    )
    monkeypatch.setattr(cell_script.worker, "create_worker", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.worker, "rewrite_manifest_pin", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "install_fixture", lambda *a, **k: None)
    monkeypatch.setattr(cell_script.harness, "validate_installed_fixture", lambda *a, **k: None)
    monkeypatch.setattr(
        cell_script, "_launch", lambda **k: launch_calls.append(1) or _FakeProcess()
    )
    monkeypatch.setattr(cell_script.fq, "wait_for_port_diagnosed", lambda **k: None)

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
    monkeypatch.setattr(
        cell_script,
        "_git_head_sha",
        lambda: "a" * 40,
    )
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

    assert len(preseed_calls) == 3
    assert len(launch_calls) == 3


# ---------------------------------------------------------------------------
# _apply_preseed evidence capture — Run 6: capture the real prefs file
# content right after each preseed write, so a future run can compare
# against what Unity itself writes instead of guessing the format again.
# ---------------------------------------------------------------------------

def test_apply_preseed_writes_snapshot_file_when_evidence_out_given(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    monkeypatch.setattr(
        cell_script.preseed,
        "preseed_editor_prefs",
        lambda project, *, os_name: {"applied": True, "mechanism": "linux_prefs_xml"},
    )
    monkeypatch.setattr(
        cell_script.preseed, "read_prefs_snapshot", lambda os_name, **k: "<unity_prefs>...</unity_prefs>"
    )
    evidence_out = tmp_path / "evidence"

    cell_script._apply_preseed(
        tmp_path / "worker", os_name="Linux", evidence_out=evidence_out, label="steps1-2"
    )

    snapshot_path = evidence_out / "prefs-after-preseed-steps1-2.txt"
    assert snapshot_path.is_file()
    assert "unity_prefs" in snapshot_path.read_text(encoding="utf-8")


def test_apply_preseed_skips_snapshot_file_when_unavailable(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    monkeypatch.setattr(
        cell_script.preseed,
        "preseed_editor_prefs",
        lambda project, *, os_name: {"applied": True, "mechanism": "windows_registry"},
    )
    monkeypatch.setattr(cell_script.preseed, "read_prefs_snapshot", lambda os_name, **k: None)
    evidence_out = tmp_path / "evidence"

    cell_script._apply_preseed(
        tmp_path / "worker", os_name="Windows", evidence_out=evidence_out, label="pilot"
    )

    assert not evidence_out.exists() or not list(evidence_out.glob("prefs-*"))


def test_write_final_prefs_snapshot_writes_file_when_available(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    monkeypatch.setattr(
        cell_script.preseed, "read_prefs_snapshot", lambda os_name, **k: "<unity_prefs>final</unity_prefs>"
    )
    evidence_out = tmp_path / "evidence"

    cell_script._write_final_prefs_snapshot(evidence_out, os_name="Linux")

    assert (evidence_out / "prefs-final.txt").read_text(encoding="utf-8") == "<unity_prefs>final</unity_prefs>"


def test_write_final_prefs_snapshot_noop_when_evidence_out_is_none(monkeypatch: pytest.MonkeyPatch):
    monkeypatch.setattr(cell_script.preseed, "read_prefs_snapshot", lambda os_name, **k: "x")
    cell_script._write_final_prefs_snapshot(None, os_name="Linux")  # must not raise


def test_write_final_prefs_snapshot_noop_when_snapshot_unavailable(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    monkeypatch.setattr(cell_script.preseed, "read_prefs_snapshot", lambda os_name, **k: None)
    evidence_out = tmp_path / "evidence"

    cell_script._write_final_prefs_snapshot(evidence_out, os_name="Windows")

    assert not evidence_out.exists()


# ---------------------------------------------------------------------------
# discovery wiring — Run 7: reveal what Unity actually touches under
# ~/.config and ~/.local/share during a real pilot run, since the tracked
# prefs file is proven stable/untouched yet Unity's behavior still implies
# it read a non-default kAutoRefreshMode from somewhere.
# ---------------------------------------------------------------------------

def test_run_pilot_writes_discovery_report_on_linux(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    monkeypatch.setattr(
        cell_script.preseed, "preseed_editor_prefs", lambda project, *, os_name: {"applied": True}
    )
    marker_calls: list = []
    monkeypatch.setattr(
        cell_script.preseed, "create_discovery_marker", lambda path: marker_calls.append(path)
    )
    monkeypatch.setattr(
        cell_script.preseed,
        "discover_touched_config_files",
        lambda *, marker, home=None: "=== discovery report ===",
    )

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            os_name="Linux",
            evidence_out=tmp_path / "evidence",
        )
    )

    assert len(marker_calls) == 1
    report_path = tmp_path / "evidence" / "prefs-discovery.txt"
    assert report_path.read_text(encoding="utf-8") == "=== discovery report ==="


def test_run_pilot_skips_discovery_on_windows(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    calls: list = []
    _stub_common(monkeypatch, create_worker_calls=calls)

    async def _call(port, command, args):
        return "ok"

    monkeypatch.setattr(cell_script.durable, "call", _call)
    monkeypatch.setattr(
        cell_script.preseed, "preseed_editor_prefs", lambda project, *, os_name: {"applied": True}
    )
    marker_calls: list = []
    monkeypatch.setattr(
        cell_script.preseed, "create_discovery_marker", lambda path: marker_calls.append(path)
    )

    import asyncio

    asyncio.run(
        cell_script.run_pilot(
            unity=tmp_path / "Unity",
            source_project=tmp_path / "source",
            work_root=tmp_path / "work",
            port=9600,
            startup_timeout=1.0,
            os_name="Windows",
            evidence_out=tmp_path / "evidence",
        )
    )

    assert marker_calls == []
    assert not (tmp_path / "evidence" / "prefs-discovery.txt").exists()


# ---------------------------------------------------------------------------
# adaptive preseed wiring — Run 7 (b): before steps 4-6 (the vulnerable
# phase), read pilot's own discovery report and extend preseed to any
# candidate paths it found, in addition to the already-known path.
# ---------------------------------------------------------------------------

def test_apply_adaptive_preseed_reads_pilot_discovery_and_extends(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
):
    evidence_out = tmp_path / "evidence"
    pilot_dir = evidence_out / "pilot"
    pilot_dir.mkdir(parents=True)
    (pilot_dir / "prefs-discovery.txt").write_text("some unity discovery report", encoding="utf-8")

    calls: list = []
    monkeypatch.setattr(
        cell_script.preseed,
        "adaptive_preseed_from_discovery",
        lambda report, *, product_name, home=None: calls.append(report) or {"candidates_found": [], "attempts": []},
    )
    monkeypatch.setattr(
        cell_script.preseed, "resolve_product_name", lambda project: "Unity Biome MCP Demo"
    )

    result = cell_script._apply_adaptive_preseed(evidence_out, os_name="Linux", project=tmp_path / "worker")

    assert calls == ["some unity discovery report"]
    assert result == {"candidates_found": [], "attempts": []}


def test_apply_adaptive_preseed_noop_when_no_pilot_report(tmp_path: Path):
    evidence_out = tmp_path / "evidence"
    result = cell_script._apply_adaptive_preseed(evidence_out, os_name="Linux", project=tmp_path / "worker")
    assert result is None


def test_apply_adaptive_preseed_noop_on_windows(tmp_path: Path):
    evidence_out = tmp_path / "evidence"
    pilot_dir = evidence_out / "pilot"
    pilot_dir.mkdir(parents=True)
    (pilot_dir / "prefs-discovery.txt").write_text("report", encoding="utf-8")

    result = cell_script._apply_adaptive_preseed(evidence_out, os_name="Windows", project=tmp_path / "worker")

    assert result is None
