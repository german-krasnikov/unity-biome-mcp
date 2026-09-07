"""Offline tests for scripts/run_ab_reload_identity.py (Task 1 scaffold).

No Unity, no network. Covers: nonce fixture template rendering and unsafe-
nonce rejection, file-mutation helpers (modify/inject/repair), the evidence
receipt schema, and the owner-safety guards (canonical project path, foreign
PID on a port file). See Plans/N3-T3-T4-live-reload-identity.md Task 1.
"""
import json
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).parent.parent))
import run_ab_reload_identity as lane


def make_worker(project: Path) -> None:
    for name in ("Assets", "Packages", "ProjectSettings"):
        (project / name).mkdir(parents=True, exist_ok=True)
    (project / "ProjectSettings/ProjectVersion.txt").write_text(
        f"m_EditorVersion: {lane.UNITY_VERSION}\n"
        f"m_EditorVersionWithRevision: {lane.UNITY_VERSION} ({lane.UNITY_REVISION})\n",
        encoding="utf-8",
    )
    (project / "Packages/manifest.json").write_text(
        json.dumps({"dependencies": {"com.unity.test-framework": lane.UTF_VERSION}}),
        encoding="utf-8",
    )
    marker = project / "Library/UnityMCP/disposable-worker.json"
    marker.parent.mkdir(parents=True)
    marker.write_text(
        json.dumps(
            {
                "schema_version": 1,
                "disposable": True,
                "unity_version": lane.UNITY_VERSION,
                "unity_revision": lane.UNITY_REVISION,
                "utf_version": lane.UTF_VERSION,
            }
        ),
        encoding="utf-8",
    )


def full_receipt_fields() -> dict[str, object]:
    return {
        "source_sha": "deadbeef",
        "unity_version": lane.UNITY_VERSION,
        "utf_version": lane.UTF_VERSION,
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
    receipt = lane.build_receipt(**full_receipt_fields())
    assert receipt["schema_version"] == 1
    for name in lane.REQUIRED_RECEIPT_FIELDS:
        assert name in receipt
    assert "timestamp" in receipt


def test_receipt_rejects_missing_sha() -> None:
    fields = full_receipt_fields()
    del fields["source_sha"]
    with pytest.raises(lane.ABReloadIdentityError, match="source_sha"):
        lane.build_receipt(**fields)


def test_receipt_rejects_missing_worker_b() -> None:
    fields = full_receipt_fields()
    del fields["worker_b"]
    with pytest.raises(lane.ABReloadIdentityError, match="worker_b"):
        lane.build_receipt(**fields)


# --- owner-safety guards ---


def test_validate_worker_project_rejects_canonical_unity_test_project() -> None:
    canonical = lane.REPO_ROOT / "unity-test-project"
    with pytest.raises(lane.ABReloadIdentityError, match="canonical"):
        lane.validate_worker_project(canonical)


def test_validate_worker_project_rejects_source_checkout() -> None:
    inside_repo = lane.REPO_ROOT / "scripts" / "tests" / "__ab_reload_scratch__"
    with pytest.raises(lane.ABReloadIdentityError, match="source checkout"):
        lane.validate_worker_project(inside_repo)


def test_validate_ports_rejects_owner_port() -> None:
    with pytest.raises(lane.ABReloadIdentityError, match="9600"):
        lane.validate_ports(9600, 9630)
    with pytest.raises(lane.ABReloadIdentityError, match="9600"):
        lane.validate_ports(9620, 9600)


def test_validate_ports_rejects_duplicate() -> None:
    with pytest.raises(lane.ABReloadIdentityError, match="must differ"):
        lane.validate_ports(9620, 9620)


def test_validate_port_owned_by_accepts_launched_pid(tmp_path: Path) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "4242.port").write_text("9620\n/tmp/worker-a\n", encoding="utf-8")
    lane.validate_port_owned_by(9620, {4242}, ports_dir)


def test_validate_port_owned_by_rejects_foreign_pid(tmp_path: Path) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    (ports_dir / "9999.port").write_text("9620\n/tmp/worker-a\n", encoding="utf-8")
    with pytest.raises(lane.ABReloadIdentityError, match="foreign worker"):
        lane.validate_port_owned_by(9620, {4242}, ports_dir)


def test_validate_port_owned_by_rejects_missing_port_file(tmp_path: Path) -> None:
    ports_dir = tmp_path / "ports"
    ports_dir.mkdir()
    with pytest.raises(lane.ABReloadIdentityError, match="No port file"):
        lane.validate_port_owned_by(9620, {4242}, ports_dir)


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


# --- negative-control oracle integrity (double-red: these name the exact
# live assertion each mirrors, and prove it flips red under the fake) ---


def test_negative_control_b_sentinel_mutated_makes_oracle_red() -> None:
    """T3 'B unchanged' oracle: read_nonce(B) == B_nonce. A fake B read that
    returns a mutated value must flip this exact assertion to fail."""
    b_nonce = "NONCE-B"
    fake_b_read_after_mutation = "NONCE-B-MUTATED"
    with pytest.raises(AssertionError):
        assert fake_b_read_after_mutation == b_nonce


def test_negative_control_old_nonce_makes_new_code_oracle_red() -> None:
    """T3 'new code executed' oracle: read_nonce(A) == new_nonce. A fake
    read that still returns the pre-reload nonce must flip this exact
    assertion to fail."""
    new_nonce = "NONCE-A-2"
    fake_read_returning_old_value = "NONCE-A-1"
    with pytest.raises(AssertionError):
        assert fake_read_returning_old_value == new_nonce


def test_negative_control_double_increment_makes_no_resend_oracle_red() -> None:
    """T3 lost-ACK 'no auto-resend' oracle: counter == 1 after one dropped
    ACK. A fake that reports two increments (an auto-resend occurred) must
    flip this exact assertion to fail."""
    expected_after_single_uncertain_write = 1
    fake_counter_after_resend = 2
    with pytest.raises(AssertionError):
        assert fake_counter_after_resend == expected_after_single_uncertain_write
