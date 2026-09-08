"""Offline tests for scripts/run_ab_reload_identity.py (Task 1 scaffold +
Task 2 review SHOULDs). No Unity, no network. Covers: nonce fixture template
rendering and unsafe-nonce rejection, file-mutation helpers (modify/inject/
repair), the evidence receipt schema, and the owner-safety guards (canonical
project path, disposable-marker fields, foreign-but-alive PID on a port
file). See Plans/N3-T3-T4-live-reload-identity.md Task 1/Task 2 Part 0.
"""
import json
import os
import subprocess
import sys
from pathlib import Path

import pytest

import unity_mcp.lockfile as lockfile

sys.path.insert(0, str(Path(__file__).parent.parent))
import gauntlet.ab_reload_identity as t3
import gauntlet.ab_reload_owner_safety as owner_safety
import gauntlet.ab_reload_receipt as receipt_schema
import run_ab_reload_identity as lane


def make_worker(project: Path) -> None:
    for name in ("Assets", "Packages", "ProjectSettings"):
        (project / name).mkdir(parents=True, exist_ok=True)
    (project / "ProjectSettings/ProjectVersion.txt").write_text(
        f"m_EditorVersion: {owner_safety.UNITY_VERSION}\n"
        f"m_EditorVersionWithRevision: {owner_safety.UNITY_VERSION} ({owner_safety.UNITY_REVISION})\n",
        encoding="utf-8",
    )
    (project / "Packages/manifest.json").write_text(
        json.dumps({"dependencies": {"com.unity.test-framework": owner_safety.UTF_VERSION}}),
        encoding="utf-8",
    )
    marker = project / "Library/UnityMCP/disposable-worker.json"
    marker.parent.mkdir(parents=True)
    marker.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "disposable": True,
                "unity_version": owner_safety.UNITY_VERSION,
                "unity_revision": owner_safety.UNITY_REVISION,
                "utf_version": owner_safety.UTF_VERSION,
            }
        ),
        encoding="utf-8",
    )


def full_receipt_fields() -> dict[str, object]:
    return {
        "source_sha": "deadbeef",
        "unity_version": owner_safety.UNITY_VERSION,
        "utf_version": owner_safety.UTF_VERSION,
        "worker_a": {"pid": 111, "port": 9620, "project_path": "/tmp/a"},
        "worker_b": {"pid": 222, "port": 9630, "project_path": "/tmp/b"},
        "t3_identity_reload": {},
        "t3_cross_identity": {},
        "t3_lost_ack": {},
        "t3_negative_controls": {},
        "t4_compile_error": {},
        "t4_sentinel": {},
        "cleanup": {},
    }


# --- nonce fixture template ---


def test_nonce_fixture_template_substitution(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    make_worker(project)
    target = lane.install_nonce_fixture(project, "NONCE-A-1")
    content = (target / "AbReloadNonce.cs").read_text(encoding="utf-8")
    assert 'Nonce => "NONCE-A-1"' in content
    assert "NONCE_PLACEHOLDER" not in content
    assert (target / "UnityMCP.Worker.ABReloadHarness.asmdef").is_file()


def test_nonce_fixture_rejects_unsafe_nonce(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    make_worker(project)
    with pytest.raises(lane.ABReloadIdentityError, match="Unsafe nonce"):
        lane.install_nonce_fixture(project, 'bad";Counter=999;//')
    assert not (project / lane.FIXTURE_RELATIVE / "AbReloadNonce.cs").exists()


# --- file mutation helpers ---


def test_modify_nonce_updates_file_on_disk(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    make_worker(project)
    lane.install_nonce_fixture(project, "NONCE-A-1")
    lane.modify_nonce(project, "NONCE-A-2")
    content = (project / lane.FIXTURE_RELATIVE / "AbReloadNonce.cs").read_text(encoding="utf-8")
    assert 'Nonce => "NONCE-A-2"' in content
    assert "NONCE-A-1" not in content


def test_inject_compile_error_produces_invalid_cs(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    make_worker(project)
    lane.install_nonce_fixture(project, "NONCE-A-2")
    lane.inject_compile_error(project)
    content = (project / lane.FIXTURE_RELATIVE / "AbReloadNonce.cs").read_text(encoding="utf-8")
    assert "SYNTAX ERROR;" in content
    assert 'Nonce => "NONCE-A-2"' in content


def test_repair_restores_valid_cs_with_new_nonce(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    make_worker(project)
    lane.install_nonce_fixture(project, "NONCE-A-2")
    lane.inject_compile_error(project)
    lane.repair_compile_error(project, "NONCE-A-3")
    content = (project / lane.FIXTURE_RELATIVE / "AbReloadNonce.cs").read_text(encoding="utf-8")
    assert "SYNTAX ERROR;" not in content
    assert 'Nonce => "NONCE-A-3"' in content


# --- evidence receipt schema ---


def test_receipt_contains_all_fields() -> None:
    receipt = receipt_schema.build_receipt(**full_receipt_fields())
    assert receipt["schema_version"] == 1
    for name in receipt_schema.REQUIRED_RECEIPT_FIELDS:
        assert name in receipt
    assert "timestamp" in receipt


def test_receipt_rejects_missing_sha() -> None:
    fields = full_receipt_fields()
    del fields["source_sha"]
    with pytest.raises(receipt_schema.ABReloadIdentityError, match="source_sha"):
        receipt_schema.build_receipt(**fields)


def test_receipt_rejects_missing_worker_b() -> None:
    fields = full_receipt_fields()
    del fields["worker_b"]
    with pytest.raises(receipt_schema.ABReloadIdentityError, match="worker_b"):
        receipt_schema.build_receipt(**fields)


# --- receipt content check (validate_receipt): a build_receipt() call with
# {} placeholder slices passes build_receipt() (the field exists) but must
# fail validate_receipt() (the field has no actual evidence) -- so a
# never-run slice can never be mistaken for a completed receipt. ---


def full_slice_evidence() -> dict[str, dict[str, object]]:
    return {
        "t3_identity_reload": dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t3_identity_reload"], "x"),
        "t3_cross_identity": dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t3_cross_identity"], "x"),
        "t3_lost_ack": dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t3_lost_ack"], "x"),
        "t3_negative_controls": dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t3_negative_controls"], "x"),
        "t4_compile_error": dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t4_compile_error"], "x"),
        "t4_sentinel": dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t4_sentinel"], "x"),
    }


def test_validate_receipt_passes_on_full_evidence() -> None:
    fields = full_receipt_fields()
    fields.update(full_slice_evidence())
    receipt_schema.validate_receipt(receipt_schema.build_receipt(**fields))  # no raise


def test_validate_receipt_rejects_empty_placeholder_slice() -> None:
    """The exact placeholder main() currently writes for t3_lost_ack/
    t4_compile_error/t4_sentinel before those phases are wired live."""
    fields = full_receipt_fields()
    fields["t3_identity_reload"] = dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t3_identity_reload"], "x")
    fields["t3_cross_identity"] = dict.fromkeys(receipt_schema._SLICE_REQUIRED_KEYS["t3_cross_identity"], "x")
    receipt = receipt_schema.build_receipt(**fields)  # t3_lost_ack stays {}
    with pytest.raises(receipt_schema.ABReloadIdentityError, match="t3_lost_ack.* must be a non-empty dict"):
        receipt_schema.validate_receipt(receipt)


def test_validate_receipt_rejects_slice_missing_a_required_key() -> None:
    fields = full_receipt_fields()
    fields.update(full_slice_evidence())
    fields["t4_compile_error"] = dict(fields["t4_compile_error"])
    del fields["t4_compile_error"]["repair_verdict"]
    receipt = receipt_schema.build_receipt(**fields)
    with pytest.raises(receipt_schema.ABReloadIdentityError, match="missing required key.*repair_verdict"):
        receipt_schema.validate_receipt(receipt)


# --- owner-safety guards ---


def test_validate_worker_project_rejects_canonical_unity_test_project() -> None:
    canonical = lane.REPO_ROOT / "unity-test-project"
    with pytest.raises(owner_safety.ABReloadIdentityError, match="canonical"):
        owner_safety.validate_worker_project(canonical)


def test_validate_worker_project_rejects_source_checkout() -> None:
    inside_repo = lane.REPO_ROOT / "scripts" / "tests" / "__ab_reload_scratch__"
    with pytest.raises(owner_safety.ABReloadIdentityError, match="source checkout"):
        owner_safety.validate_worker_project(inside_repo)


def test_validate_worker_project_rejects_wrong_unity_version(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    make_worker(project)
    marker_path = project / "Library/UnityMCP/disposable-worker.json"
    marker = json.loads(marker_path.read_text(encoding="utf-8"))
    marker["unity_version"] = "2099.1.1f1"
    marker_path.write_text(json.dumps(marker), encoding="utf-8")
    with pytest.raises(owner_safety.ABReloadIdentityError, match="marker mismatch"):
        owner_safety.validate_worker_project(project)


def test_validate_worker_project_rejects_non_disposable_marker(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    make_worker(project)
    marker_path = project / "Library/UnityMCP/disposable-worker.json"
    marker = json.loads(marker_path.read_text(encoding="utf-8"))
    marker["disposable"] = False
    marker_path.write_text(json.dumps(marker), encoding="utf-8")
    with pytest.raises(owner_safety.ABReloadIdentityError, match="marker mismatch"):
        owner_safety.validate_worker_project(project)


def test_validate_worker_project_rejects_missing_required_dirs(tmp_path: Path) -> None:
    project = tmp_path / "worker-a"
    project.mkdir()
    (project / "Assets").mkdir()
    # Packages and ProjectSettings intentionally absent.
    with pytest.raises(owner_safety.ABReloadIdentityError, match="Not a Unity project"):
        owner_safety.validate_worker_project(project)


def test_render_nonce_source_rejects_template_with_multiple_placeholders(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    fixture_dir = tmp_path / "fixtures"
    fixture_dir.mkdir()
    (fixture_dir / lane.NONCE_FILE_NAME).write_text(
        "NONCE_PLACEHOLDER NONCE_PLACEHOLDER", encoding="utf-8"
    )
    monkeypatch.setattr(lane, "FIXTURE_SOURCE", fixture_dir)
    with pytest.raises(lane.ABReloadIdentityError, match="exactly one"):
        lane._render_nonce_source("NONCE-X")


def test_render_nonce_source_replaces_placeholder_only_once(
    tmp_path: Path, monkeypatch: pytest.MonkeyPatch
) -> None:
    fixture_dir = tmp_path / "fixtures"
    fixture_dir.mkdir()
    (fixture_dir / lane.NONCE_FILE_NAME).write_text("X NONCE_PLACEHOLDER Y", encoding="utf-8")
    monkeypatch.setattr(lane, "FIXTURE_SOURCE", fixture_dir)
    assert lane._render_nonce_source("NONCE-X") == "X NONCE-X Y"


def test_validate_ports_rejects_owner_port() -> None:
    with pytest.raises(owner_safety.ABReloadIdentityError, match="9600"):
        owner_safety.validate_ports(9600, 9630)
    with pytest.raises(owner_safety.ABReloadIdentityError, match="9600"):
        owner_safety.validate_ports(9620, 9600)


def test_validate_ports_rejects_duplicate() -> None:
    with pytest.raises(owner_safety.ABReloadIdentityError, match="must differ"):
        owner_safety.validate_ports(9620, 9620)


def test_validate_port_owned_by_accepts_launched_pid(tmp_path: Path) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    pid = os.getpid()  # alive (the test process itself)
    (ports_dir / f"{pid}.port").write_text("9620\n/tmp/worker-a\n", encoding="utf-8")
    owner_safety.validate_port_owned_by(9620, {pid}, ports_dir)


def test_validate_port_owned_by_rejects_foreign_pid(tmp_path: Path) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    foreign_pid = os.getpid()  # alive but not in launched_pids below
    (ports_dir / f"{foreign_pid}.port").write_text("9620\n/tmp/worker-a\n", encoding="utf-8")
    with pytest.raises(owner_safety.ABReloadIdentityError, match="foreign worker"):
        owner_safety.validate_port_owned_by(9620, {4242}, ports_dir)


def test_validate_port_owned_by_rejects_missing_port_file(tmp_path: Path) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    with pytest.raises(owner_safety.ABReloadIdentityError, match="No port file"):
        owner_safety.validate_port_owned_by(9620, {4242}, ports_dir)


def _finished_pid() -> int:
    """A PID that surely does not exist: a subprocess launched and reaped."""
    process = subprocess.Popen(["true"], stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL)
    process.wait()
    return process.pid


def test_validate_port_owned_by_ignores_stale_dead_pid_but_still_rejects_live_foreign_pid(
    tmp_path: Path,
) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    dead_pid = _finished_pid()
    (ports_dir / f"{dead_pid}.port").write_text("9620\n/tmp/dead\n", encoding="utf-8")
    live_foreign_pid = os.getpid()
    (ports_dir / f"{live_foreign_pid}.port").write_text("9620\n/tmp/live\n", encoding="utf-8")
    with pytest.raises(owner_safety.ABReloadIdentityError, match="foreign worker"):
        owner_safety.validate_port_owned_by(9620, {4242}, ports_dir)


def test_validate_port_owned_by_treats_only_dead_pid_file_as_no_owner(tmp_path: Path) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    dead_pid = _finished_pid()
    (ports_dir / f"{dead_pid}.port").write_text("9620\n/tmp/dead\n", encoding="utf-8")
    with pytest.raises(owner_safety.ABReloadIdentityError, match="No port file"):
        owner_safety.validate_port_owned_by(9620, {4242}, ports_dir)


def test_pid_alive_delegates_to_lockfile_is_pid_alive(monkeypatch: pytest.MonkeyPatch) -> None:
    """On Windows, signal.CTRL_C_EVENT == 0, so os.kill(pid, 0) calls
    GenerateConsoleCtrlEvent(CTRL_C_EVENT, pid) -- it sends a real Ctrl+C to
    the process's console group instead of probing liveness (this is what
    interrupted the Windows CI job at cb050d35: the test process signaled
    itself). lockfile.is_pid_alive already carries the Windows-safe
    OpenProcess probe, so _pid_alive must delegate to it rather than calling
    os.kill directly."""
    recorded_pids: list[int] = []

    def fake_is_pid_alive(pid: int) -> bool:
        recorded_pids.append(pid)
        return True

    monkeypatch.setattr(lockfile, "is_pid_alive", fake_is_pid_alive)

    assert owner_safety._pid_alive(4242) is True
    assert recorded_pids == [4242]


# --- CLI safety gate ---


def test_main_requires_confirm_disposable_worker(tmp_path: Path) -> None:
    argv = [
        "--worker-a-dir", str(tmp_path / "a"),
        "--worker-b-dir", str(tmp_path / "b"),
        "--unity", "/Applications/Unity/Hub/Editor/x/Unity",
        "--receipt", str(tmp_path / "receipt.json"),
    ]
    with pytest.raises(lane.ABReloadIdentityError, match="confirm-disposable-worker"):
        lane.main(argv)


# --- negative/positive-control oracle integrity: these call the REAL oracle
# functions in gauntlet.ab_reload_identity with a wrong/right observation,
# proving the oracle itself flips red/stays green -- not a bare assertion
# that would pass even if the oracle function were deleted. ---


def test_check_sentinel_unchanged_raises_on_mutated_b() -> None:
    with pytest.raises(t3.ABReloadIdentityError, match="B sentinel changed"):
        t3.check_sentinel_unchanged("NONCE-B", "NONCE-B-MUTATED")


def test_check_sentinel_unchanged_passes_when_equal() -> None:
    t3.check_sentinel_unchanged("NONCE-B", "NONCE-B")  # no raise


def test_check_new_code_executed_raises_on_old_nonce() -> None:
    with pytest.raises(t3.ABReloadIdentityError, match="old code presented as new"):
        t3.check_new_code_executed("NONCE-A-2", "NONCE-A-1", "NONCE-A-1")


def test_check_new_code_executed_raises_on_unexpected_nonce() -> None:
    with pytest.raises(t3.ABReloadIdentityError, match="new code not executed"):
        t3.check_new_code_executed("NONCE-A-2", "NONCE-A-3", "NONCE-A-1")


def test_check_new_code_executed_passes_on_expected_nonce() -> None:
    t3.check_new_code_executed("NONCE-A-2", "NONCE-A-2", "NONCE-A-1")  # no raise


def test_check_no_resend_raises_on_double_increment() -> None:
    with pytest.raises(t3.ABReloadIdentityError, match="auto-resend detected"):
        t3.check_no_resend(counter_before=0, counter_after=2, sent_count=1)


def test_check_no_resend_passes_on_exact_delta() -> None:
    t3.check_no_resend(counter_before=0, counter_after=1, sent_count=1)  # no raise
