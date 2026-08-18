"""One fail-closed decision over policy, artifacts, harness, and evidence."""


import stat
from dataclasses import dataclass
from pathlib import PurePosixPath
from typing import TYPE_CHECKING

from gauntlet.artifacts import (
    ArtifactError,
    load_artifact_manifest,
    verify_artifact_files,
)
from gauntlet.contract_catalog import CatalogError, parse_contract_catalog
from gauntlet.evidence_schema import run_manifest_hash
from gauntlet.package_contracts import (
    PACKAGE_CONTENT_ROOTS,
    PACKAGE_SOURCE_PATHS,
    PackageIdentity,
)
from gauntlet.player_playtest_gate import (
    PlayerPlaytestGateError,
    validate_player_playtest_evidence_set,
)
from gauntlet.profile_evidence import ProfileEvidenceError, verify_profile_artifacts
from gauntlet.release_evidence import (
    EvidenceError,
    load_conformance_evidence,
    validate_evidence_matches_artifacts,
    validate_release_evidence_bundle,
)
from gauntlet.release_policy import PolicyError, load_release_policy, parse_release_policy
from gauntlet.source_packages import (
    SourcePackageError,
    SourcePackageIdentity,
    parse_source_package_identities,
)
from gauntlet.source_provenance import SourceProvenanceError, observe_source_checkout

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
    product_version: str
    policy_version: str
    profiles: tuple[str, ...]
    artifact_manifest_sha: str
    source_observation_sha: str
    contract_catalog_sha: str
    player_playtest_matrices: tuple[str, ...]


def validate_release_gate(
    *,
    policy_path: Path,
    source_root: Path,
    artifact_manifest_path: Path,
    artifact_root: Path,
    evidence_paths: Sequence[Path],
    expected_head_sha: str,
    player_playtest_evidence_paths: Sequence[Path] = (),
) -> GateSummary:
    """Validate the exact release inputs; never infer missing attestations."""
    try:
        policy_relative = _source_relative_path(source_root, policy_path, "release policy")
        candidate_policy = load_release_policy(policy_path)
        source_observation = observe_source_checkout(
            source_root,
            expected_head_sha=expected_head_sha,
            required_paths=(
                policy_relative,
                candidate_policy.contract_catalog_path,
                candidate_policy.harness_lock_path,
                *PACKAGE_SOURCE_PATHS.values(),
            ),
            package_content_roots=PACKAGE_CONTENT_ROOTS,
        )
        policy = parse_release_policy(
            source_observation.file_payloads[policy_relative],
            source=policy_relative,
        )
        if (
            policy.contract_catalog_path != candidate_policy.contract_catalog_path
            or policy.harness_lock_path != candidate_policy.harness_lock_path
        ):
            raise GateError("release policy routing changed during source observation")
        catalog = parse_contract_catalog(
            source_observation.file_payloads[policy.contract_catalog_path],
            source=policy.contract_catalog_path,
        )
        if catalog.catalog_sha != policy.contract_catalog_sha:
            raise GateError("contract catalog digest does not match release policy")
        manifest = load_artifact_manifest(artifact_manifest_path)
        source_packages = parse_source_package_identities(source_observation.file_payloads)
        verified_packages = verify_artifact_files(manifest, artifact_root)
        _validate_manifest_policy(
            policy,
            manifest,
            source_observation.head_sha,
            source_packages,
            dict(source_observation.package_content_digests),
            verified_packages,
        )
        harness_lock_sha = source_observation.file_digests[policy.harness_lock_path]
        evidence_records = _load_evidences(evidence_paths)
        evidences = [evidence for evidence, _ in evidence_records]
        for evidence, bundle_root in evidence_records:
            profile_id = evidence.get("profile")
            if not isinstance(profile_id, str) or profile_id not in policy.active_requirements:
                raise GateError("evidence profile is not active in release policy")
            requirement = policy.active_requirements[profile_id]
            expected_run_manifest_sha = run_manifest_hash(
                head_sha=source_observation.head_sha,
                source_observation_sha=source_observation.observation_sha,
                policy_sha=policy.policy_sha,
                contract_catalog_sha=catalog.catalog_sha,
                profile_manifest_sha=requirement.profile_manifest_sha,
                harness_lock_sha=harness_lock_sha,
                artifact_manifest_sha=manifest.manifest_sha,
                artifacts=manifest.archive_digests,
            )
            derived = verify_profile_artifacts(
                evidence.get("evidence_artifacts"),
                bundle_root=bundle_root,
                profile_id=profile_id,
                requirement=requirement,
                expected_head_sha=source_observation.head_sha,
                expected_artifacts=manifest.archive_digests,
                expected_run_manifest_sha=expected_run_manifest_sha,
            )
            validate_evidence_matches_artifacts(evidence, derived)
        validate_release_evidence_bundle(
            evidences,
            expected_head_sha=source_observation.head_sha,
            expected_source_observation_sha=source_observation.observation_sha,
            expected_policy_version=policy.policy_version,
            expected_policy_sha=policy.policy_sha,
            expected_contract_catalog_sha=catalog.catalog_sha,
            expected_harness_lock_sha=harness_lock_sha,
            expected_artifact_manifest_sha=manifest.manifest_sha,
            expected_artifacts=manifest.archive_digests,
            required_profiles=policy.active_requirements,
        )
        player_playtest_matrices = validate_player_playtest_evidence_set(
            player_playtest_evidence_paths,
            expected_head_sha=source_observation.head_sha,
        )
    except (
        ArtifactError,
        CatalogError,
        EvidenceError,
        PlayerPlaytestGateError,
        PolicyError,
        ProfileEvidenceError,
        SourceProvenanceError,
        SourcePackageError,
        OSError,
    ) as exc:
        raise GateError(str(exc)) from exc

    return GateSummary(
        head_sha=source_observation.head_sha,
        product_version=manifest.product_version,
        policy_version=policy.policy_version,
        profiles=tuple(sorted(policy.active_requirements)),
        artifact_manifest_sha=manifest.manifest_sha,
        source_observation_sha=source_observation.observation_sha,
        contract_catalog_sha=catalog.catalog_sha,
        player_playtest_matrices=player_playtest_matrices,
    )


def _validate_manifest_policy(
    policy: ReleasePolicy,
    manifest: ArtifactManifest,
    head_sha: str,
    source_packages: dict[str, SourcePackageIdentity],
    source_content_digests: dict[str, str],
    verified_packages: dict[str, PackageIdentity],
) -> None:
    if manifest.head_sha != head_sha:
        raise GateError("artifact manifest head SHA does not match the release commit")
    if manifest.product_version != policy.activation_product_version:
        raise GateError("artifact product version does not match policy activation")
    if set(manifest.archive_digests) != set(policy.artifact_types):
        raise GateError("artifact types do not exactly match release policy")
    records = {record.artifact_type: record for record in manifest.artifacts}
    for artifact_type, source_identity in source_packages.items():
        record = records[artifact_type]
        if (
            record.package_name != source_identity.package_name
            or record.package_version != source_identity.package_version
        ):
            raise GateError(f"{artifact_type} archive identity does not match observed source")
        if record.content_sha256 != source_content_digests.get(artifact_type):
            raise GateError(f"{artifact_type} archive content does not match observed source")
        verified = verified_packages.get(artifact_type)
        if (
            verified is None
            or verified.runtime_contract_sha256 != source_identity.runtime_contract_sha256
        ):
            raise GateError(f"{artifact_type} runtime contract does not match observed source")


def _load_evidences(
    paths: Sequence[Path],
) -> list[tuple[dict[str, object], Path]]:
    if not paths:
        raise GateError("at least one evidence file is required")
    return [(load_conformance_evidence(path), path.parent) for path in paths]


def _source_relative_path(root: Path, path: Path, label: str) -> str:
    try:
        metadata = path.lstat()
        relative = path.resolve(strict=True).relative_to(root.resolve(strict=True))
    except (OSError, ValueError) as exc:
        raise GateError(f"{label} must be inside the source root") from exc
    if not stat.S_ISREG(metadata.st_mode):
        raise GateError(f"{label} must be a regular file, not a link")
    value = PurePosixPath(relative.as_posix()).as_posix()
    if not value or value == ".":
        raise GateError(f"{label} must be a tracked source file")
    return value
