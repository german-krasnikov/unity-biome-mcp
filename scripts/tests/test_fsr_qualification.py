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

def _receipt(cell: str, **overrides) -> dict:
    payload = {
        "cell": cell,
        "outcome": "PASS",
        "checkout_sha": "a" * 40,
        "lock_base_product_sha": "a" * 40,
        "candidate_sha": "b" * 40,
    }
    payload.update(overrides)
    return payload


def _six_receipts() -> list:
    return [_receipt(cell) for cell in fq.EXPECTED_CELLS]


def _lock() -> dict:
    return {"base_product_sha": "a" * 40, "final_fsr_adapter_sha": "b" * 40}


def test_validate_receipt_set_passes_for_six_matching_pass_receipts():
    fq.validate_receipt_set(_six_receipts(), _lock())  # must not raise


def test_validate_receipt_set_raises_when_a_cell_is_missing():
    receipts = _six_receipts()[:-1]
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_on_duplicate_cell():
    receipts = _six_receipts()
    receipts[-1] = dict(receipts[0])
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_on_non_pass_outcome():
    receipts = _six_receipts()
    receipts[0]["outcome"] = "INFRASTRUCTURE_BLOCKED"
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_on_base_sha_mismatch():
    receipts = _six_receipts()
    receipts[0]["lock_base_product_sha"] = "c" * 40
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_on_candidate_sha_mismatch():
    receipts = _six_receipts()
    receipts[0]["candidate_sha"] = "c" * 40
    with pytest.raises(fq.FsrQualificationError):
        fq.validate_receipt_set(receipts, _lock())


def test_validate_receipt_set_raises_when_checkout_shas_differ():
    receipts = _six_receipts()
    receipts[0]["checkout_sha"] = "c" * 40
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
