from __future__ import annotations

import hashlib
import json
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from collections.abc import Mapping

SCHEMA_VERSION = 3
EVIDENCE_KEYS = frozenset(
    {
        "schema_version",
        "run_id",
        "run_manifest_sha",
        "created_at",
        "head_sha",
        "policy_version",
        "policy_sha",
        "harness_lock_sha",
        "artifact_manifest_sha",
        "artifacts",
        "profile",
        "profile_manifest_sha",
        "expected_scenario_ids",
        "executed_scenario_ids",
        "selected_tests",
        "passed",
        "failed",
        "skipped",
        "blocked",
        "untested",
        "exit_code",
        "required_workers",
        "evidence_artifacts",
        "evidence_hash",
    }
)


class EvidenceError(ValueError):
    """Raised when release evidence is incomplete, stale, or contradictory."""


@dataclass(frozen=True, slots=True)
class ProfileRequirement:
    profile_manifest_sha: str
    scenario_ids: tuple[str, ...]
    driver: str
    operating_system: str
    python_version: str
    unity_version: str | None
    plugin_scope: str
    required_workers: int
    worker_roles: tuple[str, ...]
    consumed_artifacts: tuple[str, ...]
    cleanup_obligations: tuple[str, ...]
    max_age_seconds: int = 86400

    def __post_init__(self) -> None:
        canonical_scenario_ids(self.scenario_ids, "profile scenario manifest")
        if self.driver not in {"public_stdio", "unity_editor"}:
            raise EvidenceError("profile driver is invalid")
        if (
            isinstance(self.required_workers, bool)
            or not isinstance(self.required_workers, int)
            or self.required_workers < 0
        ):
            raise EvidenceError("profile worker requirement is invalid")
        if len(self.worker_roles) != self.required_workers:
            raise EvidenceError("profile worker roles do not match worker requirement")
        if self.driver == "public_stdio" and (
            self.unity_version is not None
            or self.plugin_scope != "none"
            or self.required_workers != 0
            or self.consumed_artifacts != ("python_wheel",)
        ):
            raise EvidenceError("public stdio profile requirement is inconsistent")
        if self.driver == "unity_editor" and (
            self.unity_version is None
            or self.plugin_scope != "exact"
            or self.required_workers < 1
            or self.consumed_artifacts != ("python_wheel", "unity_upm")
        ):
            raise EvidenceError("Unity Editor profile requirement is inconsistent")
        if not self.consumed_artifacts:
            raise EvidenceError("profile consumed artifacts must not be empty")
        if not self.cleanup_obligations:
            raise EvidenceError("profile cleanup obligations must not be empty")
        if (
            isinstance(self.max_age_seconds, bool)
            or not isinstance(self.max_age_seconds, int)
            or self.max_age_seconds <= 0
        ):
            raise EvidenceError("profile evidence age limit is invalid")


def evidence_hash(evidence: Mapping[str, object]) -> str:
    try:
        canonical = json.dumps(
            evidence,
            ensure_ascii=False,
            separators=(",", ":"),
            sort_keys=True,
        ).encode("utf-8")
    except (TypeError, ValueError) as exc:
        raise EvidenceError(f"evidence is not JSON serializable: {exc}") from exc
    return hashlib.sha256(canonical).hexdigest()


def run_manifest_hash(
    *,
    head_sha: str,
    policy_sha: str,
    profile_manifest_sha: str,
    harness_lock_sha: str,
    artifact_manifest_sha: str,
    artifacts: Mapping[str, str],
) -> str:
    """Bind every run artifact to one reviewed source/profile contract."""
    return evidence_hash(
        {
            "head_sha": head_sha,
            "policy_sha": policy_sha,
            "profile_manifest_sha": profile_manifest_sha,
            "harness_lock_sha": harness_lock_sha,
            "artifact_manifest_sha": artifact_manifest_sha,
            "artifacts": dict(sorted(artifacts.items())),
        }
    )


def validate_shape_and_hash(evidence: dict[str, object]) -> None:
    if set(evidence) != EVIDENCE_KEYS:
        missing = sorted(EVIDENCE_KEYS - set(evidence))
        extra = sorted(set(evidence) - EVIDENCE_KEYS)
        raise EvidenceError(f"evidence schema mismatch: missing={missing}, extra={extra}")
    if evidence.get("schema_version") != SCHEMA_VERSION:
        raise EvidenceError("unsupported evidence schema version")
    if not isinstance(evidence.get("run_id"), str) or not evidence["run_id"]:
        raise EvidenceError("run_id must be a non-empty string")
    require_digest(evidence.get("run_manifest_sha"), "run manifest")
    if not isinstance(evidence.get("policy_version"), str) or not evidence["policy_version"]:
        raise EvidenceError("policy version must be a non-empty string")

    supplied_hash = evidence.get("evidence_hash")
    unhashed = dict(evidence)
    unhashed.pop("evidence_hash")
    if supplied_hash != evidence_hash(unhashed):
        raise EvidenceError("evidence hash mismatch")


def canonical_scenario_ids(value: object, label: str) -> list[str]:
    if not isinstance(value, (list, tuple)) or not value:
        raise EvidenceError(f"{label} must be a non-empty list")
    if any(not isinstance(item, str) or not item for item in value):
        raise EvidenceError(f"{label} contains an invalid ID")
    if len(set(value)) != len(value):
        raise EvidenceError(f"{label} contains duplicate IDs")
    return sorted(value)


def require_digest(value: object, label: str) -> None:
    if (
        not isinstance(value, str)
        or len(value) != 64
        or any(character not in "0123456789abcdef" for character in value.lower())
    ):
        raise EvidenceError(f"{label} digest must be 64 hexadecimal characters")


def non_negative_int(evidence: dict[str, object], key: str) -> int:
    value = evidence.get(key)
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise EvidenceError(f"{key} must be a non-negative integer")
    return value


def parse_timestamp(value: object) -> datetime:
    if not isinstance(value, str) or not value:
        raise EvidenceError("created_at must be an RFC3339 timestamp")
    try:
        parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    except ValueError as exc:
        raise EvidenceError("created_at must be an RFC3339 timestamp") from exc
    if parsed.tzinfo is None:
        raise EvidenceError("created_at must include a timezone")
    return parsed.astimezone(timezone.utc)
