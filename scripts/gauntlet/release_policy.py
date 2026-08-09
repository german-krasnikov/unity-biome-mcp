from __future__ import annotations

import json
import re
from dataclasses import dataclass
from typing import TYPE_CHECKING

from gauntlet.evidence_schema import ProfileRequirement
from gauntlet.package_archives import SUPPORTED_ARTIFACT_TYPES
from gauntlet.policy_fields import (
    PolicyError,
    require_exact_keys,
    require_id,
    require_non_negative_int,
    require_positive_int,
    require_repo_path,
    require_scenario_ids,
    require_source_sha,
    require_text,
    require_unique_ids,
    validate_driver_contract,
)
from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    from pathlib import Path

_ROOT_KEYS = {
    "schema_version",
    "policy_version",
    "source_sha",
    "activation_package_version",
    "harness_lock_path",
    "contract_catalog_path",
    "artifact_types",
    "profiles",
}
_PROFILE_KEYS = {
    "id",
    "active",
    "driver",
    "os",
    "python",
    "unity",
    "plugin_scope",
    "required_workers",
    "worker_roles",
    "consumed_artifacts",
    "cleanup_obligations",
    "scenario_ids",
    "max_age_seconds",
}
_OPERATING_SYSTEMS = {"linux", "macos", "windows"}
_PLUGIN_SCOPES = {"none", "exact"}
_DRIVERS = {"public_stdio", "unity_editor"}


@dataclass(frozen=True, slots=True)
class ProfilePolicy:
    profile_id: str
    active: bool
    driver: str
    operating_system: str
    python_version: str
    unity_version: str | None
    plugin_scope: str
    required_workers: int
    worker_roles: tuple[str, ...]
    consumed_artifacts: tuple[str, ...]
    cleanup_obligations: tuple[str, ...]
    scenario_ids: tuple[str, ...]
    max_age_seconds: int
    manifest_sha: str

    @property
    def requirement(self) -> ProfileRequirement:
        return ProfileRequirement(
            profile_manifest_sha=self.manifest_sha,
            scenario_ids=self.scenario_ids,
            driver=self.driver,
            operating_system=self.operating_system,
            python_version=self.python_version,
            unity_version=self.unity_version,
            plugin_scope=self.plugin_scope,
            required_workers=self.required_workers,
            worker_roles=self.worker_roles,
            consumed_artifacts=self.consumed_artifacts,
            cleanup_obligations=self.cleanup_obligations,
            max_age_seconds=self.max_age_seconds,
        )


@dataclass(frozen=True, slots=True)
class ReleasePolicy:
    policy_version: str
    source_sha: str
    activation_package_version: str
    harness_lock_path: str
    contract_catalog_path: str
    artifact_types: tuple[str, ...]
    profiles: tuple[ProfilePolicy, ...]
    policy_sha: str

    @property
    def active_profiles(self) -> tuple[ProfilePolicy, ...]:
        return tuple(profile for profile in self.profiles if profile.active)

    @property
    def active_requirements(self) -> dict[str, ProfileRequirement]:
        return {profile.profile_id: profile.requirement for profile in self.active_profiles}


def load_release_policy(path: Path) -> ReleasePolicy:
    try:
        raw = path.read_text(encoding="utf-8")
    except OSError as exc:
        raise PolicyError("unable to read release policy") from exc
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as exc:
        raise PolicyError(f"release policy is not valid JSON at line {exc.lineno}, column {exc.colno}") from exc
    if not isinstance(data, dict):
        raise PolicyError("release policy root must be an object")
    require_exact_keys(data, _ROOT_KEYS, "release policy schema")
    if data["schema_version"] != 1:
        raise PolicyError("unsupported release policy schema version")

    policy_version = require_text(data["policy_version"], "policy version")
    source_sha = require_source_sha(data["source_sha"])
    activation_version = require_text(
        data["activation_package_version"],
        "activation package version",
    )
    harness_lock_path = require_repo_path(data["harness_lock_path"], "harness lock path")
    contract_catalog_path = require_repo_path(
        data["contract_catalog_path"],
        "contract catalog path",
    )
    artifact_types = require_unique_ids(data["artifact_types"], "artifact types")
    if set(artifact_types) != set(SUPPORTED_ARTIFACT_TYPES):
        raise PolicyError("release policy requires exactly the supported wheel and UPM artifacts")
    profiles = _parse_profiles(data["profiles"])
    if not any(profile.active for profile in profiles):
        raise PolicyError("release policy must contain at least one active profile")
    artifact_set = set(artifact_types)
    for profile in profiles:
        unknown = sorted(set(profile.consumed_artifacts) - artifact_set)
        if unknown:
            raise PolicyError(f"profile consumed artifact is unknown: {unknown}")
    consumed = {artifact for profile in profiles if profile.active for artifact in profile.consumed_artifacts}
    if consumed != artifact_set:
        raise PolicyError("active profiles must consume every release artifact exactly by type")

    return ReleasePolicy(
        policy_version=policy_version,
        source_sha=source_sha,
        activation_package_version=activation_version,
        harness_lock_path=harness_lock_path,
        contract_catalog_path=contract_catalog_path,
        artifact_types=artifact_types,
        profiles=profiles,
        policy_sha=content_hash(data),
    )


def _parse_profiles(value: object) -> tuple[ProfilePolicy, ...]:
    if not isinstance(value, list) or not value:
        raise PolicyError("release policy must contain at least one active profile")

    profiles: list[ProfilePolicy] = []
    seen_ids: set[str] = set()
    for raw_profile in value:
        if not isinstance(raw_profile, dict):
            raise PolicyError("profile schema requires an object")
        require_exact_keys(raw_profile, _PROFILE_KEYS, "profile schema")
        profile = _parse_profile(raw_profile)
        if profile.profile_id in seen_ids:
            raise PolicyError(f"duplicate profile id: {profile.profile_id}")
        seen_ids.add(profile.profile_id)
        profiles.append(profile)
    return tuple(profiles)


def _parse_profile(data: dict[str, object]) -> ProfilePolicy:
    profile_id = require_id(data["id"], "profile id")
    active = data["active"]
    if not isinstance(active, bool):
        raise PolicyError(f"profile {profile_id} active must be boolean")
    driver = require_text(data["driver"], "profile driver")
    if driver not in _DRIVERS:
        raise PolicyError(f"unsupported profile driver: {driver}")

    operating_system = require_text(data["os"], "operating system")
    if operating_system not in _OPERATING_SYSTEMS:
        raise PolicyError(f"unsupported operating system: {operating_system}")
    python_version = require_text(data["python"], "Python version")
    if not re.fullmatch(r"\d+\.\d+", python_version):
        raise PolicyError("Python version must use major.minor form")

    unity_value = data["unity"]
    unity_version = None if unity_value is None else require_text(unity_value, "Unity version")
    plugin_scope = require_text(data["plugin_scope"], "plugin scope")
    if plugin_scope not in _PLUGIN_SCOPES:
        raise PolicyError(f"unsupported plugin scope: {plugin_scope}")

    workers = require_non_negative_int(data["required_workers"], "required workers")
    worker_roles = require_unique_ids(data["worker_roles"], "worker roles", allow_empty=True)
    if len(worker_roles) != workers:
        raise PolicyError(f"worker role count for {profile_id} does not match required workers")
    consumed_artifacts = require_unique_ids(
        data["consumed_artifacts"],
        "consumed artifacts",
    )
    cleanup_obligations = require_unique_ids(
        data["cleanup_obligations"],
        "cleanup obligations",
    )
    scenario_ids = require_scenario_ids(data["scenario_ids"])
    max_age = require_positive_int(data["max_age_seconds"], "maximum evidence age")
    validate_driver_contract(
        driver,
        unity_version,
        plugin_scope,
        workers,
        consumed_artifacts,
    )

    canonical_profile = dict(data)
    canonical_profile["worker_roles"] = list(worker_roles)
    canonical_profile["consumed_artifacts"] = list(consumed_artifacts)
    canonical_profile["cleanup_obligations"] = list(cleanup_obligations)
    canonical_profile["scenario_ids"] = list(scenario_ids)
    return ProfilePolicy(
        profile_id=profile_id,
        active=active,
        driver=driver,
        operating_system=operating_system,
        python_version=python_version,
        unity_version=unity_version,
        plugin_scope=plugin_scope,
        required_workers=workers,
        worker_roles=worker_roles,
        consumed_artifacts=consumed_artifacts,
        cleanup_obligations=cleanup_obligations,
        scenario_ids=scenario_ids,
        max_age_seconds=max_age,
        manifest_sha=content_hash(canonical_profile),
    )
