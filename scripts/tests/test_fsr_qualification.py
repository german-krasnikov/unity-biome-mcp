"""P1-20 six-cell frozen U_MIN..U_MAX compatibility matrix: lock loading,
cell resolution, runtime receipts, the base-SHA-untouched aggregate guard,
and the headed (non-batchmode) Unity launch command/environment.

`ci-conformance.yml`'s hosted-disposable-unity job proves batchmode Unity
launch/port-wait/termination already works on all three OSes
(`gauntlet/hosted_conformance.py`); this module reuses `wait_for_port`,
`terminate_workers` and `write_mcp_project_settings` from there and adds
only what differs for FSR qualification: a headed (no -batchmode
-nographics) launch, because "their batchmode lanes are not FSR
qualification evidence" (§7 P1-20).

Runs in the standard `scripts/tests` lane: no Unity, no network, hermetic
tmp_path/JSON only.
"""
import hashlib
import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
import gauntlet.fsr_qualification as fq

LOCK_PATH = SCRIPTS / "fsr_qualification_lock.json"
PIN_PATH = SCRIPTS / "source_patch_provider_pin.json"


# ---------------------------------------------------------------------------
# The tracked lock file itself
# ---------------------------------------------------------------------------

def test_tracked_lock_pin_sha256_matches_real_pin_file_bytes():
    """Double-red guard: fails if the tracked lock's recorded hash drifts
    from the pin file's real bytes, independent of any fixture constant in
    this test module."""
    payload = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    real_sha256 = hashlib.sha256(PIN_PATH.read_bytes()).hexdigest()
    assert payload["provider_pin_sha256"] == real_sha256


def test_tracked_lock_final_adapter_sha_matches_pin_ref():
    lock = json.loads(LOCK_PATH.read_text(encoding="utf-8"))
    pin = json.loads(PIN_PATH.read_text(encoding="utf-8"))
    assert lock["final_fsr_adapter_sha"] == pin["ref"]


def test_tracked_lock_loads_and_resolves_both_frozen_cells():
    lock = fq.load_lock(LOCK_PATH)
    u_min = fq.resolve_cell(lock, "u_min")
    u_max = fq.resolve_cell(lock, "u_max")
    assert u_min["unity_version"] == "6000.0.65f1"
    assert u_min["unity_revision"] == "a18e2220bd50"
    assert u_max["unity_version"] == "6000.5.10f1"
    assert u_max["unity_revision"] == "3bd4f66ad299"


# ---------------------------------------------------------------------------
# load_lock
# ---------------------------------------------------------------------------

def _write_lock(tmp_path: Path, **overrides) -> Path:
    payload = {
        "schema_version": 1,
        "base_product_sha": "7875430f73d28a043806742164ab478145dedafe",
        "final_fsr_adapter_sha": "e50d43dda33e2d62c68be25278d48bc07f6003ff",
        "cells": {
            "u_min": {"unity_version": "6000.0.65f1", "unity_revision": "a18e2220bd50", "utf_version": "1.6.0"},
            "u_max": {"unity_version": "6000.5.10f1", "unity_revision": "3bd4f66ad299", "utf_version": "1.6.0"},
        },
    }
    payload.update(overrides)
    path = tmp_path / "lock.json"
    path.write_text(json.dumps(payload), encoding="utf-8")
    return path


def test_load_lock_missing_file_raises(tmp_path: Path):
    with pytest.raises(fq.FsrQualificationError):
        fq.load_lock(tmp_path / "absent.json")


@pytest.mark.parametrize("missing_key", ["base_product_sha", "final_fsr_adapter_sha", "cells"])
def test_load_lock_missing_required_key_raises(tmp_path: Path, missing_key: str):
    path = _write_lock(tmp_path)
    payload = json.loads(path.read_text(encoding="utf-8"))
    del payload[missing_key]
    path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(fq.FsrQualificationError):
        fq.load_lock(path)


@pytest.mark.parametrize("missing_window", ["u_min", "u_max"])
def test_load_lock_missing_cell_window_raises(tmp_path: Path, missing_window: str):
    path = _write_lock(tmp_path)
    payload = json.loads(path.read_text(encoding="utf-8"))
    del payload["cells"][missing_window]
    path.write_text(json.dumps(payload), encoding="utf-8")

    with pytest.raises(fq.FsrQualificationError):
        fq.load_lock(path)


# ---------------------------------------------------------------------------
# resolve_cell
# ---------------------------------------------------------------------------

def test_resolve_cell_unknown_window_raises(tmp_path: Path):
    lock = fq.load_lock(_write_lock(tmp_path))
    with pytest.raises(fq.FsrQualificationError):
        fq.resolve_cell(lock, "u_medium")


# ---------------------------------------------------------------------------
# build_runtime_receipt
# ---------------------------------------------------------------------------

def test_build_runtime_receipt_includes_both_shas_and_outcome():
    receipt = fq.build_runtime_receipt(
        cell="min-macos-arm64",
        os_name="macOS",
        arch="arm64",
        unity_version="6000.0.65f1",
        unity_revision="a18e2220bd50",
        setup_ok=True,
        license_ok=True,
        display_ok=True,
        checkout_sha="7875430f73d28a043806742164ab478145dedafe",
        lock_base_product_sha="7875430f73d28a043806742164ab478145dedafe",
        candidate_sha="e50d43dda33e2d62c68be25278d48bc07f6003ff",
        outcome="PASS",
    )
    assert receipt["cell"] == "min-macos-arm64"
    assert receipt["checkout_sha"] == receipt["lock_base_product_sha"]
    assert receipt["candidate_sha"] == "e50d43dda33e2d62c68be25278d48bc07f6003ff"
    assert receipt["outcome"] == "PASS"


@pytest.mark.parametrize("bad_outcome", ["ok", "green", "passed", ""])
def test_build_runtime_receipt_rejects_non_enum_outcome(bad_outcome: str):
    with pytest.raises(fq.FsrQualificationError):
        fq.build_runtime_receipt(
            cell="min-macos-arm64",
            os_name="macOS",
            arch="arm64",
            unity_version="6000.0.65f1",
            unity_revision="a18e2220bd50",
            setup_ok=True,
            license_ok=True,
            display_ok=True,
            checkout_sha="a" * 40,
            lock_base_product_sha="a" * 40,
            candidate_sha="b" * 40,
            outcome=bad_outcome,
        )


def test_build_runtime_receipt_missing_secret_is_infrastructure_blocked_not_skip():
    """A missing secret must fail the cell, never appear as a green skip."""
    receipt = fq.build_runtime_receipt(
        cell="min-windows-x64",
        os_name="Windows",
        arch="x64",
        unity_version="6000.0.65f1",
        unity_revision="a18e2220bd50",
        setup_ok=True,
        license_ok=False,
        display_ok=True,
        checkout_sha="a" * 40,
        lock_base_product_sha="a" * 40,
        candidate_sha="b" * 40,
        outcome="INFRASTRUCTURE_BLOCKED",
    )
    assert receipt["outcome"] == "INFRASTRUCTURE_BLOCKED"
    assert receipt["license_ok"] is False


# ---------------------------------------------------------------------------
# assert_base_sha_untouched — aggregate guard: CI commits over base_product_sha
# never touch unity-plugin/** or server/src/**
# ---------------------------------------------------------------------------

def test_assert_base_sha_untouched_passes_for_ci_only_paths():
    fq.assert_base_sha_untouched(
        [
            ".github/workflows/fsr-qualification.yml",
            "scripts/run_fsr_qualification_cell.py",
            "scripts/fsr_qualification_lock.json",
        ]
    )  # must not raise


@pytest.mark.parametrize(
    "changed_path",
    ["unity-plugin/Editor/SourcePatchHost.cs", "server/src/unity_mcp/server.py"],
)
def test_assert_base_sha_untouched_raises_for_guarded_prefix(changed_path: str):
    with pytest.raises(fq.FsrQualificationError):
        fq.assert_base_sha_untouched(["scripts/foo.py", changed_path])


def test_assert_base_sha_untouched_empty_diff_passes():
    fq.assert_base_sha_untouched([])  # must not raise


# ---------------------------------------------------------------------------
# build_headed_unity_command / build_headed_unity_environment
# ---------------------------------------------------------------------------

def test_headed_unity_command_has_no_batchmode_nographics_or_quit(tmp_path: Path):
    command = fq.build_headed_unity_command(
        tmp_path / "Unity", tmp_path / "project", tmp_path / "Editor.log"
    )
    joined = " ".join(command)
    assert "-batchmode" not in joined
    assert "-nographics" not in joined
    assert "-quit" not in joined
    assert "-projectPath" in command
    assert "-logFile" in command


def test_headed_unity_environment_omits_batchmode_marker():
    env = fq.build_headed_unity_environment(
        {"PATH": "/usr/bin"}, port=9600, project=Path("/tmp/worker")
    )
    assert "UNITY_MCP_ENABLE_BATCHMODE" not in env
    assert env["UNITY_MCP_PORT"] == "9600"
    assert env["PATH"] == "/usr/bin"


# ---------------------------------------------------------------------------
# validate_receipt_set — aggregate guard: exactly 6 unique PASS receipts,
# one SHA set
# ---------------------------------------------------------------------------

def _pass_receipt(cell: str, **overrides) -> dict:
    payload = {
        "cell": cell,
        "outcome": "PASS",
        "checkout_sha": "a" * 40,
        "lock_base_product_sha": "a" * 40,
        "candidate_sha": "b" * 40,
        "off_mode_evidence": {"epoch_delta_is_one": True, "same_pid": True},
    }
    payload.update(overrides)
    return payload


def _blocked_receipt(cell: str, **overrides) -> dict:
    """Windows: no checkout_sha/lock_base_product_sha/candidate_sha —
    matches the real workflow fallback receipt, written when pilot/setup
    itself fails before run_full ever resolves the lock."""
    payload = {"cell": cell, "outcome": "INFRASTRUCTURE_BLOCKED"}
    payload.update(overrides)
    return payload


def _three_receipts() -> list:
    return [
        *(_pass_receipt(cell) for cell in fq.REQUIRED_PASS_CELLS),
        *(_blocked_receipt(cell) for cell in fq.DOCUMENTED_BLOCKED_CELLS),
    ]


def _lock() -> dict:
    return {"base_product_sha": "a" * 40, "final_fsr_adapter_sha": "b" * 40}


def test_validate_receipt_set_passes_for_two_pass_plus_one_documented_blocked():
    fq.validate_receipt_set(_three_receipts(), _lock())  # must not raise


def test_validate_receipt_set_raises_when_a_required_pass_cell_is_missing():
    receipts = [r for r in _three_receipts() if r["cell"] != fq.REQUIRED_PASS_CELLS[0]]
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_when_the_blocked_cell_is_missing():
    """Windows must never be silently absent — "documented-blocked, not a
    green skip"."""
    receipts = [r for r in _three_receipts() if r["cell"] not in fq.DOCUMENTED_BLOCKED_CELLS]
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_on_duplicate_cell():
    receipts = _three_receipts()
    receipts[-1] = dict(receipts[0])
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_when_required_pass_cell_is_not_pass():
    receipts = _three_receipts()
    receipts[0]["outcome"] = "INFRASTRUCTURE_BLOCKED"
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_does_not_require_blocked_cell_to_pass():
    """The whole point: Windows INFRASTRUCTURE_BLOCKED must not fail the
    aggregate — only a missing or invalid-outcome blocked receipt should."""
    receipts = _three_receipts()
    assert receipts[-1]["outcome"] == "INFRASTRUCTURE_BLOCKED"
    fq.validate_receipt_set(receipts, _lock())  # must not raise


def test_validate_receipt_set_raises_when_blocked_cell_outcome_is_not_a_valid_enum_value():
    """Never a green skip: the blocked cell must carry a real, honest
    outcome value, not an arbitrary/fabricated one."""
    receipts = _three_receipts()
    receipts[-1]["outcome"] = "SKIPPED"
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_on_base_sha_mismatch():
    receipts = _three_receipts()
    receipts[0]["lock_base_product_sha"] = "c" * 40
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_on_candidate_sha_mismatch():
    receipts = _three_receipts()
    receipts[0]["candidate_sha"] = "c" * 40
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_when_required_pass_checkout_shas_differ():
    receipts = _three_receipts()
    receipts[0]["checkout_sha"] = "c" * 40
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_when_required_pass_cell_missing_off_mode_evidence():
    """P1-20 reviewer gap #2: a required-pass cell's receipt must carry
    structural off-mode evidence, not just an "outcome": "PASS" string —
    this is the aggregate-level half of "validates independently"."""
    receipts = _three_receipts()
    del receipts[0]["off_mode_evidence"]
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_when_required_pass_cell_off_mode_evidence_is_empty():
    receipts = _three_receipts()
    receipts[0]["off_mode_evidence"] = {}
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


# ---------------------------------------------------------------------------
# write_pilot_evidence — the fixture-free GUI baseline previously discarded
# all diagnostic evidence on failure (Run 2: min-windows-x64/max-windows-x64
# INFRASTRUCTURE_BLOCKED with nothing but a bare exception message uploaded).
# ---------------------------------------------------------------------------

def test_write_pilot_evidence_writes_receipt_and_log_tail(tmp_path: Path):
    evidence_out = tmp_path / "evidence"
    log = tmp_path / "pilot-unity.log"
    log.write_text("line one\nline two\n", encoding="utf-8")

    fq.write_pilot_evidence(
        evidence_out, cell="min-windows-x64", os_name="Windows", arch="x64",
        log_path=log, outcome="PASS", error=None,
    )

    receipt = json.loads((evidence_out / "pilot-receipt.json").read_text(encoding="utf-8"))
    assert receipt["outcome"] == "PASS"
    assert receipt["cell"] == "min-windows-x64"
    assert (evidence_out / "pilot-unity-log-tail.txt").read_text(encoding="utf-8") == "line one\nline two"


def test_write_pilot_evidence_records_missing_log_explicitly(tmp_path: Path):
    evidence_out = tmp_path / "evidence"
    missing_log = tmp_path / "does-not-exist.log"

    fq.write_pilot_evidence(
        evidence_out, cell="min-windows-x64", os_name="Windows", arch="x64",
        log_path=missing_log, outcome="INFRASTRUCTURE_BLOCKED",
        error="Timed out waiting for Unity MCP port 127.0.0.1:9600: timed out",
    )

    receipt = json.loads((evidence_out / "pilot-receipt.json").read_text(encoding="utf-8"))
    assert receipt["outcome"] == "INFRASTRUCTURE_BLOCKED"
    assert "Timed out" in receipt["error"]
    tail = (evidence_out / "pilot-unity-log-tail.txt").read_text(encoding="utf-8")
    assert "not found" in tail.lower()


# ---------------------------------------------------------------------------
# build_runtime_receipt error capture — Run 3's min-linux-x64/min-macos-
# arm64 reached real semantic execution and failed for real, but the
# receipt had no error field at all: only the raw GH Actions job log (only
# retrievable after the whole run completes) carried the actual Python
# exception message.
# ---------------------------------------------------------------------------

def test_build_runtime_receipt_captures_error_message_when_given():
    receipt = fq.build_runtime_receipt(
        cell="min-linux-x64", os_name="Linux", arch="x64",
        unity_version="6000.0.65f1", unity_revision="a18e2220bd50",
        setup_ok=True, license_ok=True, display_ok=True,
        checkout_sha="a" * 40, lock_base_product_sha="a" * 40,
        candidate_sha="b" * 40, outcome="FAIL",
        error="editor mutation_mode failed: some real reason",
    )
    assert receipt["error"] == "editor mutation_mode failed: some real reason"


def test_build_runtime_receipt_omits_error_key_when_not_given():
    receipt = fq.build_runtime_receipt(
        cell="min-linux-x64", os_name="Linux", arch="x64",
        unity_version="6000.0.65f1", unity_revision="a18e2220bd50",
        setup_ok=True, license_ok=True, display_ok=True,
        checkout_sha="a" * 40, lock_base_product_sha="a" * 40,
        candidate_sha="b" * 40, outcome="PASS",
    )
    assert "error" not in receipt


# ---------------------------------------------------------------------------
# build_headed_unity_command -force-d3d11 — Windows-only diagnostic. GPU-less
# Windows CI VMs are a known source of Unity Editor startup hangs during
# graphics-backend auto-detection; min-windows-x64's Editor.log never even
# reached the "Unity Editor version:" banner line across 3 consecutive
# matrix runs, unlike every Linux/macOS cell, which always logs that line
# within the first second. -force-d3d11 is a standard, documented Unity
# Editor argument that skips backend auto-detection.
# ---------------------------------------------------------------------------

def test_headed_unity_command_forces_d3d11_only_when_requested():
    windows_command = fq.build_headed_unity_command(
        Path("/tmp/Unity"), Path("/tmp/project"), Path("/tmp/log"), force_d3d11=True
    )
    assert "-force-d3d11" in windows_command

    default_command = fq.build_headed_unity_command(
        Path("/tmp/Unity"), Path("/tmp/project"), Path("/tmp/log")
    )
    assert "-force-d3d11" not in default_command


# ---------------------------------------------------------------------------
# Windows timeout diagnostics — Run 4: min-windows-x64/max-windows-x64
# produced zero log content across 4 consecutive matrix runs with no
# visibility into why. Every poll_interval seconds during the wait, and
# once more on final timeout, capture process liveness, -logFile
# presence/size, the OS-default Editor.log fallback location, and a
# Unity process list, so a timeout finally produces real evidence.
# ---------------------------------------------------------------------------

class _FakeProcess:
    def __init__(self, returncode=None):
        self._returncode = returncode
        self.returncode = returncode

    def poll(self):
        return self._returncode


def test_default_editor_log_path_per_os(tmp_path: Path):
    windows = fq.default_editor_log_path(os_name="Windows", home=tmp_path)
    macos = fq.default_editor_log_path(os_name="macOS", home=tmp_path)
    linux = fq.default_editor_log_path(os_name="Linux", home=tmp_path)
    assert windows == tmp_path / "AppData" / "Local" / "Unity" / "Editor" / "Editor.log"
    assert macos == tmp_path / "Library" / "Logs" / "Unity" / "Editor.log"
    assert linux == tmp_path / ".config" / "unity3d" / "Editor.log"


def test_capture_wait_diagnostics_reports_log_presence_and_process_liveness(tmp_path: Path):
    log = tmp_path / "unity.log"
    log.write_text("hello", encoding="utf-8")
    process = _FakeProcess(returncode=None)

    snapshot = fq.capture_wait_diagnostics(
        process=process, log=log, os_name="Linux", home=tmp_path
    )

    assert snapshot["poll"] is None
    assert snapshot["log_exists"] is True
    assert snapshot["log_size"] == 5
    assert snapshot["default_log_exists"] is False
    assert "unity_processes" in snapshot


def test_capture_wait_diagnostics_reports_missing_log(tmp_path: Path):
    process = _FakeProcess(returncode=None)

    snapshot = fq.capture_wait_diagnostics(
        process=process, log=tmp_path / "absent.log", os_name="Windows", home=tmp_path
    )

    assert snapshot["log_exists"] is False
    assert snapshot["log_size"] is None


def test_wait_for_port_diagnosed_returns_once_port_open(tmp_path: Path):
    import socket

    server = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server.bind(("127.0.0.1", 0))
    server.listen(1)
    port = server.getsockname()[1]
    process = _FakeProcess(returncode=None)

    fq.wait_for_port_diagnosed(
        host="127.0.0.1", port=port, process=process, log=tmp_path / "unity.log",
        timeout=5.0, evidence_out=tmp_path / "evidence", os_name="Linux",
        home=tmp_path, poll_interval=30.0,
    )  # must not raise
    server.close()


def test_wait_for_port_diagnosed_raises_when_process_exits_early(tmp_path: Path):
    process = _FakeProcess(returncode=1)

    with pytest.raises(fq.HostedConformanceError, match="exited early"):
        fq.wait_for_port_diagnosed(
            host="127.0.0.1", port=59321, process=process, log=tmp_path / "unity.log",
            timeout=5.0, evidence_out=tmp_path / "evidence", os_name="Windows",
            home=tmp_path, poll_interval=30.0,
        )


def test_wait_for_port_diagnosed_times_out_and_writes_diagnostics(tmp_path: Path):
    process = _FakeProcess(returncode=None)
    evidence_out = tmp_path / "evidence"

    with pytest.raises(fq.HostedConformanceError, match="Timed out"):
        fq.wait_for_port_diagnosed(
            host="127.0.0.1", port=59322, process=process, log=tmp_path / "unity.log",
            timeout=0.3, evidence_out=evidence_out, os_name="Windows",
            home=tmp_path, poll_interval=0.1,
        )

    diagnostics = (evidence_out / "wait-diagnostics.jsonl").read_text(encoding="utf-8")
    assert diagnostics.strip()
    lines = [json.loads(line) for line in diagnostics.strip().splitlines()]
    assert len(lines) >= 1
    assert all(line["poll"] is None for line in lines)


# ---------------------------------------------------------------------------
# preseed honesty in receipts — "зафиксируй preseed-значения в receipt
# ячейки (честность среды)"
# ---------------------------------------------------------------------------

def test_write_pilot_evidence_embeds_preseed_receipt_when_given(tmp_path: Path):
    evidence_out = tmp_path / "evidence"
    log = tmp_path / "pilot-unity.log"

    fq.write_pilot_evidence(
        evidence_out, cell="min-windows-x64", os_name="Windows", arch="x64",
        log_path=log, outcome="PASS", error=None,
        preseed={"mechanism": "windows_registry", "applied": True, "keys": ["kAutoRefreshMode"]},
    )

    receipt = json.loads((evidence_out / "pilot-receipt.json").read_text(encoding="utf-8"))
    assert receipt["preseed"]["mechanism"] == "windows_registry"
    assert receipt["preseed"]["applied"] is True


def test_write_pilot_evidence_omits_preseed_key_when_not_given(tmp_path: Path):
    evidence_out = tmp_path / "evidence"
    log = tmp_path / "pilot-unity.log"

    fq.write_pilot_evidence(
        evidence_out, cell="min-windows-x64", os_name="Windows", arch="x64",
        log_path=log, outcome="PASS", error=None,
    )

    receipt = json.loads((evidence_out / "pilot-receipt.json").read_text(encoding="utf-8"))
    assert "preseed" not in receipt


def test_build_runtime_receipt_embeds_preseed_receipt_when_given():
    receipt = fq.build_runtime_receipt(
        cell="min-linux-x64", os_name="Linux", arch="x64",
        unity_version="6000.0.65f1", unity_revision="a18e2220bd50",
        setup_ok=True, license_ok=True, display_ok=True,
        checkout_sha="a" * 40, lock_base_product_sha="a" * 40,
        candidate_sha="b" * 40, outcome="PASS",
        preseed={"mechanism": "linux_prefs_xml", "applied": True, "keys": ["kAutoRefreshMode"]},
    )
    assert receipt["preseed"]["mechanism"] == "linux_prefs_xml"


# ---------------------------------------------------------------------------
# off_mode_evidence — P1-20 reviewer gap #2 (GO_WITH_GAPS): OFF/uninstall/
# package-absent step evidence previously existed only as unstructured
# unity.log text, never validated independently by the receipt itself
# (AI doc: "§10 Exact-SHA evidence validates independently"). Embeds the
# structural step6/7/8/9 evidence dict directly in the terminal receipt.
# ---------------------------------------------------------------------------

def test_build_runtime_receipt_embeds_off_mode_evidence_when_given():
    receipt = fq.build_runtime_receipt(
        cell="min-linux-x64", os_name="Linux", arch="x64",
        unity_version="6000.0.65f1", unity_revision="a18e2220bd50",
        setup_ok=True, license_ok=True, display_ok=True,
        checkout_sha="a" * 40, lock_base_product_sha="a" * 40,
        candidate_sha="b" * 40, outcome="PASS",
        off_mode_evidence={"epoch_delta_is_one": True, "same_pid": True},
    )
    assert receipt["off_mode_evidence"] == {"epoch_delta_is_one": True, "same_pid": True}


def test_build_runtime_receipt_omits_off_mode_evidence_key_when_not_given():
    receipt = fq.build_runtime_receipt(
        cell="min-linux-x64", os_name="Linux", arch="x64",
        unity_version="6000.0.65f1", unity_revision="a18e2220bd50",
        setup_ok=True, license_ok=True, display_ok=True,
        checkout_sha="a" * 40, lock_base_product_sha="a" * 40,
        candidate_sha="b" * 40, outcome="PASS",
    )
    assert "off_mode_evidence" not in receipt


# ---------------------------------------------------------------------------
# detect_dialog_suppressed — Run 5 correction: "FSR: asset auto refresh
# enabled..." prints unconditionally before the StopShowing gate
# (FastScriptReloadWelcomeScreen.cs L984 vs the DisplayDialogComplex call
# at L986), so its presence does NOT mean the dialog was shown. The real
# suppression marker is the absence of a DisplayDialogComplex stack frame.
# ---------------------------------------------------------------------------

def test_classify_dialog_evidence_shown_when_dialog_stack_frame_present():
    log_text = (
        "FSR: Fast Script Reload - asset auto refresh enabled\n"
        "UnityEditor.EditorUtility:DisplayDialogComplex "
        "(string,string,string,string,string)\n"
    )
    assert fq.classify_dialog_evidence(log_text) == "shown"


def test_classify_dialog_evidence_inconclusive_when_log_just_stops():
    """Run 6 correction: a killed process's log looks IDENTICAL whether
    the dialog blocked it or something else entirely did — anything after
    a real block is equally absent either way. Absence of the
    DisplayDialogComplex frame is never, by itself, proof of suppression."""
    log_text = (
        "FSR: Fast Script Reload - asset auto refresh enabled - full reload "
        "will be triggered unless editor preference adjusted\n"
        "UnityEditor.EditorAssemblies:ProcessInitializeOnLoadAttributes "
        "(System.Type[])\n"
    )
    assert fq.classify_dialog_evidence(log_text) == "inconclusive"


def test_classify_dialog_evidence_inconclusive_for_empty_log():
    assert fq.classify_dialog_evidence("") == "inconclusive"


def test_classify_dialog_evidence_inconclusive_when_warning_never_reached():
    """The warning line itself never appearing is equally inconclusive —
    something blocked even earlier, or the phase simply never got that
    far; still not proof either way."""
    log_text = "Unity Editor version:    6000.0.65f1 (a18e2220bd50)\n"
    assert fq.classify_dialog_evidence(log_text) == "inconclusive"


# ---------------------------------------------------------------------------
# byte_diagnostic — Run 7: min-macos-arm64's classifier rejection could not
# be explained by the BOM theory (fixed and disproven) nor by reproducing
# the exact content through the real classifier locally (ADMITTED cleanly).
# The coordinator's ask: capture sha256 + first/last 32 hex bytes of the
# actual before (read from disk) and after (about to be sent) content for
# every ON-mode write, so a live run's exact bytes can be compared against
# what is proven to work offline.
# ---------------------------------------------------------------------------

def test_byte_diagnostic_reports_sha256_size_and_edges():
    data = b"hello world, this is more than thirty-two bytes long for sure"
    diag = fq.byte_diagnostic(data)
    import hashlib
    assert diag["sha256"] == hashlib.sha256(data).hexdigest()
    assert diag["size"] == len(data)
    assert diag["first32_hex"] == data[:32].hex()
    assert diag["last32_hex"] == data[-32:].hex()


def test_byte_diagnostic_handles_short_data():
    data = b"short"
    diag = fq.byte_diagnostic(data)
    assert diag["first32_hex"] == data.hex()
    assert diag["last32_hex"] == data.hex()
