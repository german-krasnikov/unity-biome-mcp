"""Derive profile results from immutable JUnit, journal, and receipt bytes."""


from dataclasses import dataclass
from typing import TYPE_CHECKING

from gauntlet.attestations import (
    AttestationError,
    parse_file_reference,
    parse_receipt_bytes,
    read_verified_file,
)
from gauntlet.journal_validation import validate_terminal_journal
from gauntlet.junit import JUnitError, JUnitResult, parse_attested_pytest_junit_bytes
from gauntlet.package_contracts import UNITY_UPM_ARTIFACT_TYPES
from gauntlet.receipts import JournalError, verify_journal_bytes

if TYPE_CHECKING:
    from collections.abc import Mapping
    from pathlib import Path

    from gauntlet.evidence_schema import ProfileRequirement

_ARTIFACT_KEYS = {"junit", "journal", "runtime", "workers", "cleanup"}


class ProfileEvidenceError(ValueError):
    """Raised when profile artifacts do not prove the declared result."""


@dataclass(frozen=True, slots=True)
class DerivedProfileEvidence:
    junit: JUnitResult
    run_id: str
    finished_at: str
    runtime_exit_code: int
    worker_count: int


def verify_profile_artifacts(
    value: object,
    *,
    bundle_root: Path,
    profile_id: str,
    requirement: ProfileRequirement,
    expected_head_sha: str,
    expected_artifacts: Mapping[str, str],
    expected_run_manifest_sha: str,
) -> DerivedProfileEvidence:
    """Read and verify every content-addressed artifact exactly once."""
    if not isinstance(value, dict) or set(value) != _ARTIFACT_KEYS:
        raise ProfileEvidenceError("profile evidence artifact schema mismatch")
    try:
        junit_reference = parse_file_reference(value["junit"])
        journal_reference = parse_file_reference(value["journal"])
        junit_bytes = read_verified_file(junit_reference, bundle_root)
        journal_bytes = read_verified_file(journal_reference, bundle_root)
        runtime = _read_receipt(value["runtime"], bundle_root, "runtime")
        workers = _read_receipts(value["workers"], bundle_root, "worker_identity")
        cleanup = _read_receipts(value["cleanup"], bundle_root, "cleanup")
        junit = parse_attested_pytest_junit_bytes(junit_bytes)
        events = verify_journal_bytes(journal_bytes)
        journal = validate_terminal_journal(
            events,
            requirement.scenario_ids,
            expected_profile=profile_id,
            expected_run_manifest_sha=expected_run_manifest_sha,
            expected_worker_roles=requirement.worker_roles,
        )
    except (AttestationError, JUnitError, JournalError) as exc:
        raise ProfileEvidenceError(str(exc)) from exc

    _validate_runtime(
        runtime,
        profile_id,
        requirement,
        expected_head_sha,
        expected_artifacts,
        journal.run_id,
        expected_run_manifest_sha,
        junit_reference.sha256,
        journal_reference.sha256,
    )
    worker_ids = _validate_workers(
        workers,
        profile_id,
        journal.run_id,
        expected_run_manifest_sha,
        requirement,
        expected_artifacts,
    )
    if worker_ids != journal.worker_ids:
        raise ProfileEvidenceError("journal worker leases do not match worker receipts")
    _validate_cleanup(
        cleanup,
        profile_id,
        journal.run_id,
        expected_run_manifest_sha,
        requirement,
    )
    if tuple(junit.scenario_ids) != tuple(requirement.scenario_ids):
        raise ProfileEvidenceError("JUnit scenario set does not exactly match policy")
    expected_nodes = tuple(sorted(zip(requirement.scenario_ids, requirement.pytest_node_ids, strict=True)))
    if junit.scenario_nodes != expected_nodes:
        raise ProfileEvidenceError("JUnit pytest node mapping does not exactly match policy")
    return DerivedProfileEvidence(
        junit=junit,
        run_id=journal.run_id,
        finished_at=journal.finished_at,
        runtime_exit_code=int(runtime["exit_code"]),
        worker_count=len(workers),
    )


def _read_ref(value: object, root: Path) -> bytes:
    reference = parse_file_reference(value)
    return read_verified_file(reference, root)


def _read_receipt(
    value: object,
    root: Path,
    kind: str,
) -> dict[str, object]:
    return parse_receipt_bytes(_read_ref(value, root), kind)


def _read_receipts(
    value: object,
    root: Path,
    kind: str,
) -> tuple[dict[str, object], ...]:
    if not isinstance(value, list):
        raise ProfileEvidenceError(f"{kind} artifact references must be a list")
    return tuple(_read_receipt(item, root, kind) for item in value)


def _validate_runtime(
    receipt: dict[str, object],
    profile_id: str,
    requirement: ProfileRequirement,
    expected_head_sha: str,
    expected_artifacts: Mapping[str, str],
    run_id: str,
    run_manifest_sha: str,
    junit_sha: str,
    journal_sha: str,
) -> None:
    expected_consumed = {artifact: expected_artifacts[artifact] for artifact in requirement.consumed_artifacts}
    expected = {
        "profile": profile_id,
        "run_id": run_id,
        "run_manifest_sha": run_manifest_sha,
        "driver": requirement.driver,
        "head_sha": expected_head_sha,
        "os": requirement.operating_system,
        "python": requirement.python_version,
        "unity": requirement.unity_version,
        "plugin_scope": requirement.plugin_scope,
        "consumed_artifacts": expected_consumed,
        "junit_sha": junit_sha,
        "journal_sha": journal_sha,
        "exit_code": 0,
    }
    mismatched = [key for key, value in expected.items() if receipt.get(key) != value]
    if mismatched:
        raise ProfileEvidenceError(f"runtime identity mismatch: {sorted(mismatched)}")


def _validate_workers(
    receipts: tuple[dict[str, object], ...],
    profile_id: str,
    run_id: str,
    run_manifest_sha: str,
    requirement: ProfileRequirement,
    expected_artifacts: Mapping[str, str],
) -> tuple[str, ...]:
    if len(receipts) != requirement.required_workers:
        raise ProfileEvidenceError("worker receipt count does not match policy")
    roles = tuple(sorted(str(receipt.get("role")) for receipt in receipts))
    if roles != tuple(requirement.worker_roles):
        raise ProfileEvidenceError("worker roles do not exactly match policy")
    loaded_expected = {
        artifact: expected_artifacts[artifact]
        for artifact in requirement.consumed_artifacts
        if artifact in UNITY_UPM_ARTIFACT_TYPES
    }
    identities: set[str] = set()
    projects: set[str] = set()
    ports: set[int] = set()
    for receipt in receipts:
        expected = {
            "profile": profile_id,
            "run_id": run_id,
            "run_manifest_sha": run_manifest_sha,
            "os": requirement.operating_system,
            "unity": requirement.unity_version,
            "plugin_scope": requirement.plugin_scope,
            "loaded_artifacts": loaded_expected,
        }
        if any(receipt.get(key) != value for key, value in expected.items()):
            raise ProfileEvidenceError("worker runtime identity does not match policy")
        identities.add(str(receipt["worker_id"]))
        projects.add(str(receipt["project_id"]))
        ports.add(int(receipt["port"]))
    if len(identities) != len(receipts):
        raise ProfileEvidenceError("worker identities are not distinct")
    if len(projects) != len(receipts):
        raise ProfileEvidenceError("worker projects are not distinct")
    if len(ports) != len(receipts):
        raise ProfileEvidenceError("worker ports are not distinct")
    return tuple(sorted(identities))


def _validate_cleanup(
    receipts: tuple[dict[str, object], ...],
    profile_id: str,
    run_id: str,
    run_manifest_sha: str,
    requirement: ProfileRequirement,
) -> None:
    obligations = tuple(sorted(str(receipt.get("obligation")) for receipt in receipts))
    if obligations != tuple(requirement.cleanup_obligations):
        raise ProfileEvidenceError("cleanup obligations do not exactly match policy")
    if any(receipt.get("profile") != profile_id for receipt in receipts):
        raise ProfileEvidenceError("cleanup receipt profile mismatch")
    if any(receipt.get("run_id") != run_id for receipt in receipts):
        raise ProfileEvidenceError("cleanup receipt run identity mismatch")
    if any(receipt.get("run_manifest_sha") != run_manifest_sha for receipt in receipts):
        raise ProfileEvidenceError("cleanup receipt manifest identity mismatch")
