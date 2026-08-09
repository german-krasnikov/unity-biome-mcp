from __future__ import annotations

from datetime import datetime, timedelta, timezone
from typing import TYPE_CHECKING

from gauntlet.evidence_schema import (
    SCHEMA_VERSION,
    EvidenceError,
    ProfileRequirement,
    canonical_scenario_ids,
    evidence_hash,
    non_negative_int,
    parse_timestamp,
    run_manifest_hash,
    validate_shape_and_hash,
)
from gauntlet.json_io import JsonFileError, atomic_write_json, load_json_object

if TYPE_CHECKING:
    from collections.abc import Mapping, Sequence
    from pathlib import Path

    from gauntlet.profile_evidence import DerivedProfileEvidence


def _utc_now() -> str:
    return datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")


def build_conformance_evidence(
    *,
    run_id: str,
    run_manifest_sha: str,
    head_sha: str,
    policy_version: str,
    policy_sha: str,
    harness_lock_sha: str,
    artifact_manifest_sha: str,
    artifacts: Mapping[str, str],
    profile: str,
    profile_manifest_sha: str,
    expected_scenario_ids: Sequence[str],
    executed_scenario_ids: Sequence[str],
    selected_tests: int,
    passed: int,
    failed: int,
    skipped: int,
    blocked: int,
    untested: int,
    exit_code: int,
    required_workers: int,
    evidence_artifacts: Mapping[str, object],
    created_at: str | None = None,
) -> dict[str, object]:
    evidence: dict[str, object] = {
        "schema_version": SCHEMA_VERSION,
        "run_id": run_id,
        "run_manifest_sha": run_manifest_sha,
        "created_at": created_at or _utc_now(),
        "head_sha": head_sha,
        "policy_version": policy_version,
        "policy_sha": policy_sha,
        "harness_lock_sha": harness_lock_sha,
        "artifact_manifest_sha": artifact_manifest_sha,
        "artifacts": dict(artifacts),
        "profile": profile,
        "profile_manifest_sha": profile_manifest_sha,
        "expected_scenario_ids": list(expected_scenario_ids),
        "executed_scenario_ids": list(executed_scenario_ids),
        "selected_tests": selected_tests,
        "passed": passed,
        "failed": failed,
        "skipped": skipped,
        "blocked": blocked,
        "untested": untested,
        "exit_code": exit_code,
        "required_workers": required_workers,
        "evidence_artifacts": dict(evidence_artifacts),
    }
    evidence["evidence_hash"] = evidence_hash(evidence)
    return evidence


def load_conformance_evidence(path: Path) -> dict[str, object]:
    try:
        evidence = load_json_object(path)
    except JsonFileError as exc:
        raise EvidenceError(str(exc)) from exc
    validate_shape_and_hash(evidence)
    return evidence


def write_conformance_evidence(path: Path, evidence: dict[str, object]) -> None:
    validate_shape_and_hash(evidence)
    try:
        atomic_write_json(path, evidence)
    except JsonFileError as exc:
        raise EvidenceError(str(exc)) from exc


def validate_release_evidence(
    evidence: dict[str, object],
    *,
    expected_head_sha: str,
    expected_policy_version: str,
    expected_policy_sha: str,
    expected_harness_lock_sha: str,
    expected_artifact_manifest_sha: str,
    expected_artifacts: Mapping[str, str],
    profile_requirement: ProfileRequirement,
) -> None:
    validate_shape_and_hash(evidence)

    if evidence.get("head_sha") != expected_head_sha:
        raise EvidenceError("head sha does not match the release commit")
    if evidence.get("policy_version") != expected_policy_version:
        raise EvidenceError("policy version does not match the release policy")
    if evidence.get("policy_sha") != expected_policy_sha:
        raise EvidenceError("policy digest does not match the release policy")
    if evidence.get("harness_lock_sha") != expected_harness_lock_sha:
        raise EvidenceError("harness lock digest does not match")
    if evidence.get("artifact_manifest_sha") != expected_artifact_manifest_sha:
        raise EvidenceError("artifact manifest digest does not match")
    if evidence.get("artifacts") != dict(expected_artifacts):
        raise EvidenceError("artifact digest mapping does not match")
    if evidence.get("profile_manifest_sha") != profile_requirement.profile_manifest_sha:
        raise EvidenceError("profile manifest digest does not match")
    expected_run_manifest_sha = run_manifest_hash(
        head_sha=expected_head_sha,
        policy_sha=expected_policy_sha,
        profile_manifest_sha=profile_requirement.profile_manifest_sha,
        harness_lock_sha=expected_harness_lock_sha,
        artifact_manifest_sha=expected_artifact_manifest_sha,
        artifacts=expected_artifacts,
    )
    if evidence.get("run_manifest_sha") != expected_run_manifest_sha:
        raise EvidenceError("run manifest digest does not match release inputs")

    expected_scenarios = canonical_scenario_ids(
        profile_requirement.scenario_ids,
        "policy scenario manifest",
    )
    declared_scenarios = canonical_scenario_ids(
        evidence.get("expected_scenario_ids"),
        "expected scenario manifest",
    )
    executed_scenarios = canonical_scenario_ids(
        evidence.get("executed_scenario_ids"),
        "executed scenario manifest",
    )
    if declared_scenarios != expected_scenarios:
        raise EvidenceError("expected scenario manifest does not match policy")
    if executed_scenarios != expected_scenarios:
        raise EvidenceError("executed scenario manifest does not exactly match policy")

    selected = non_negative_int(evidence, "selected_tests")
    passed = non_negative_int(evidence, "passed")
    failed = non_negative_int(evidence, "failed")
    skipped = non_negative_int(evidence, "skipped")
    blocked = non_negative_int(evidence, "blocked")
    untested = non_negative_int(evidence, "untested")
    if selected == 0:
        raise EvidenceError("selected test count must be greater than zero")
    if selected != len(expected_scenarios):
        raise EvidenceError("selected test count differs from the scenario manifest")
    if selected != passed + failed + skipped + blocked + untested:
        raise EvidenceError("selected test count does not match result totals")
    if skipped:
        raise EvidenceError(f"unexpected skip count: {skipped}")
    if blocked:
        raise EvidenceError(f"blocked scenario count: {blocked}")
    if untested:
        raise EvidenceError(f"untested scenario count: {untested}")
    if failed:
        raise EvidenceError(f"failed test count: {failed}")
    if evidence.get("exit_code") != 0:
        raise EvidenceError("test process exit code was nonzero")

    required_workers = non_negative_int(evidence, "required_workers")
    if required_workers != profile_requirement.required_workers:
        raise EvidenceError(
            f"worker requirement mismatch: expected {profile_requirement.required_workers}, got {required_workers}"
        )
    evidence_artifacts = evidence.get("evidence_artifacts")
    if not isinstance(evidence_artifacts, dict) or not evidence_artifacts:
        raise EvidenceError("evidence artifacts must be a non-empty object")


def validate_evidence_matches_artifacts(
    evidence: dict[str, object],
    derived: DerivedProfileEvidence,
) -> None:
    """Reject caller-declared counts that differ from parsed evidence bytes."""
    junit = derived.junit
    comparisons = {
        "run_id": derived.run_id,
        "created_at": derived.finished_at,
        "executed_scenario_ids": list(junit.scenario_ids),
        "selected_tests": junit.total,
        "passed": junit.passed,
        "failed": junit.failed,
        "skipped": junit.skipped,
        "exit_code": derived.runtime_exit_code,
        "required_workers": derived.worker_count,
    }
    for key, expected in comparisons.items():
        actual = evidence.get(key)
        if key == "executed_scenario_ids":
            actual = canonical_scenario_ids(actual, "executed scenario manifest")
        if actual != expected:
            raise EvidenceError(f"declared {key} does not match parsed evidence artifacts")


def validate_release_evidence_bundle(
    evidences: Sequence[dict[str, object]],
    *,
    expected_head_sha: str,
    expected_policy_version: str,
    expected_policy_sha: str,
    expected_harness_lock_sha: str,
    expected_artifact_manifest_sha: str,
    expected_artifacts: Mapping[str, str],
    required_profiles: Mapping[str, ProfileRequirement],
    now: datetime | None = None,
    max_age: timedelta | None = None,
) -> None:
    if not required_profiles:
        raise EvidenceError("required profile set must not be empty")
    if max_age is not None and max_age <= timedelta(0):
        raise EvidenceError("maximum evidence age must be positive")

    by_profile: dict[str, dict[str, object]] = {}
    for evidence in evidences:
        profile = evidence.get("profile")
        if not isinstance(profile, str) or not profile:
            raise EvidenceError("profile must be a non-empty string")
        if profile in by_profile:
            raise EvidenceError(f"duplicate profile evidence: {profile}")
        by_profile[profile] = evidence

    expected_profiles = set(required_profiles)
    unexpected = sorted(set(by_profile) - expected_profiles)
    if unexpected:
        raise EvidenceError(f"unexpected profile evidence: {', '.join(unexpected)}")
    missing = sorted(expected_profiles - set(by_profile))
    if missing:
        raise EvidenceError(f"missing profile evidence: {', '.join(missing)}")

    current_time = now or datetime.now(timezone.utc)
    if current_time.tzinfo is None:
        raise EvidenceError("current time must include a timezone")

    for profile, requirement in required_profiles.items():
        evidence = by_profile[profile]
        validate_release_evidence(
            evidence,
            expected_head_sha=expected_head_sha,
            expected_policy_version=expected_policy_version,
            expected_policy_sha=expected_policy_sha,
            expected_harness_lock_sha=expected_harness_lock_sha,
            expected_artifact_manifest_sha=expected_artifact_manifest_sha,
            expected_artifacts=expected_artifacts,
            profile_requirement=requirement,
        )
        created_at = parse_timestamp(evidence.get("created_at"))
        if created_at > current_time + timedelta(minutes=5):
            raise EvidenceError(f"future evidence timestamp for profile {profile}")
        age_limit = max_age or timedelta(seconds=requirement.max_age_seconds)
        if current_time - created_at > age_limit:
            raise EvidenceError(f"stale evidence for profile {profile}")
