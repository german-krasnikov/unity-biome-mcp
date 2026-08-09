"""One fail-closed decision over policy, artifacts, harness, and evidence."""

from __future__ import annotations

import hashlib
from dataclasses import dataclass
from typing import TYPE_CHECKING

from gauntlet.artifacts import (
    ArtifactError,
    load_artifact_manifest,
    verify_artifact_files,
)
from gauntlet.evidence_schema import run_manifest_hash
from gauntlet.profile_evidence import ProfileEvidenceError, verify_profile_artifacts
from gauntlet.release_evidence import (
    EvidenceError,
    load_conformance_evidence,
    validate_evidence_matches_artifacts,
    validate_release_evidence_bundle,
)
from gauntlet.release_policy import PolicyError, load_release_policy

if TYPE_CHECKING:
    from collections.abc import Sequence
    from pathlib import Path

    from gauntlet.artifacts import ArtifactManifest
    from gauntlet.release_policy import ReleasePolicy


class GateError(ValueError):
    """Raised when release evidence is absent, contradictory, or stale."""


@dataclass(frozen=True, slots=True)
class GateSummary:
    head_sha: str
    package_version: str
    policy_version: str
    profiles: tuple[str, ...]
    artifact_manifest_sha: str


def validate_release_gate(
    *,
    policy_path: Path,
    artifact_manifest_path: Path,
    artifact_root: Path,
    evidence_paths: Sequence[Path],
    harness_lock_path: Path,
    expected_head_sha: str,
) -> GateSummary:
    """Validate the exact release inputs; never infer missing attestations."""
    try:
        policy = load_release_policy(policy_path)
        manifest = load_artifact_manifest(artifact_manifest_path)
        _validate_manifest_policy(policy, manifest, expected_head_sha)
        verify_artifact_files(manifest, artifact_root)
        harness_lock_sha = _file_sha256(harness_lock_path, "harness lock")
        evidence_records = _load_evidences(evidence_paths)
        evidences = [evidence for evidence, _ in evidence_records]
        for evidence, bundle_root in evidence_records:
            profile_id = evidence.get("profile")
            if not isinstance(profile_id, str) or profile_id not in policy.active_requirements:
                raise GateError("evidence profile is not active in release policy")
            requirement = policy.active_requirements[profile_id]
            expected_run_manifest_sha = run_manifest_hash(
                head_sha=expected_head_sha,
                policy_sha=policy.policy_sha,
                profile_manifest_sha=requirement.profile_manifest_sha,
                harness_lock_sha=harness_lock_sha,
                artifact_manifest_sha=manifest.manifest_sha,
                artifacts=manifest.artifact_digests,
            )
            derived = verify_profile_artifacts(
                evidence.get("evidence_artifacts"),
                bundle_root=bundle_root,
                profile_id=profile_id,
                requirement=requirement,
                expected_head_sha=expected_head_sha,
                expected_artifacts=manifest.artifact_digests,
                expected_run_manifest_sha=expected_run_manifest_sha,
            )
            validate_evidence_matches_artifacts(evidence, derived)
        validate_release_evidence_bundle(
            evidences,
            expected_head_sha=expected_head_sha,
            expected_policy_version=policy.policy_version,
            expected_policy_sha=policy.policy_sha,
            expected_harness_lock_sha=harness_lock_sha,
            expected_artifact_manifest_sha=manifest.manifest_sha,
            expected_artifacts=manifest.artifact_digests,
            required_profiles=policy.active_requirements,
        )
    except (
        ArtifactError,
        EvidenceError,
        PolicyError,
        ProfileEvidenceError,
        OSError,
    ) as exc:
        raise GateError(str(exc)) from exc

    return GateSummary(
        head_sha=expected_head_sha,
        package_version=manifest.package_version,
        policy_version=policy.policy_version,
        profiles=tuple(sorted(policy.active_requirements)),
        artifact_manifest_sha=manifest.manifest_sha,
    )


def _validate_manifest_policy(
    policy: ReleasePolicy,
    manifest: ArtifactManifest,
    head_sha: str,
) -> None:
    if policy.source_sha != head_sha:
        raise GateError("release policy source SHA does not match the release commit")
    if manifest.head_sha != head_sha:
        raise GateError("artifact manifest head SHA does not match the release commit")
    if manifest.package_version != policy.activation_package_version:
        raise GateError("artifact package version does not match policy activation")
    if set(manifest.artifact_digests) != set(policy.artifact_types):
        raise GateError("artifact types do not exactly match release policy")


def _load_evidences(
    paths: Sequence[Path],
) -> list[tuple[dict[str, object], Path]]:
    if not paths:
        raise GateError("at least one evidence file is required")
    return [(load_conformance_evidence(path), path.parent) for path in paths]


def _file_sha256(path: Path, label: str) -> str:
    if not path.is_file():
        raise GateError(f"{label} is not a regular file")
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()
