"""Policy-bound pytest profile selection and strict result assessment."""

from __future__ import annotations

import stat
from pathlib import Path, PurePosixPath
from typing import TYPE_CHECKING

from gauntlet.junit import (
    JUnitError,
    JUnitResult,
    parse_attested_pytest_junit,
    parse_attested_pytest_junit_bytes,
)
from gauntlet.pytest_attestation import ScenarioBinding
from gauntlet.release_policy import PolicyError, ProfilePolicy, parse_release_policy
from gauntlet.source_provenance import SourceProvenanceError, observe_source_checkout

if TYPE_CHECKING:
    from collections.abc import Sequence


class AttestedConformanceError(ValueError):
    """Raised when policy-bound collection or assessment cannot prove a PASS."""


def load_observed_profile(
    *,
    source_root: Path,
    policy_path: Path,
    expected_head_sha: str,
    profile_id: str,
) -> ProfilePolicy:
    """Load one active profile from policy bytes observed at exact Git HEAD."""
    try:
        policy_relative = _source_relative_path(source_root, policy_path)
        initial = observe_source_checkout(
            source_root,
            expected_head_sha=expected_head_sha,
            required_paths=(policy_relative,),
        )
        initial_policy = parse_release_policy(
            initial.file_payloads[policy_relative],
            source=policy_relative,
        )
        initial_profile = _active_profile(initial_policy.active_profiles, profile_id)
        test_paths = tuple(
            sorted({scenario.pytest_node_id.split("::", 1)[0] for scenario in initial_profile.scenarios})
        )
        observation = observe_source_checkout(
            source_root,
            expected_head_sha=expected_head_sha,
            required_paths=(policy_relative, *test_paths),
        )
        policy = parse_release_policy(
            observation.file_payloads[policy_relative],
            source=policy_relative,
        )
        profile = _active_profile(policy.active_profiles, profile_id)
    except (OSError, PolicyError, SourceProvenanceError) as exc:
        raise AttestedConformanceError(str(exc)) from exc
    if profile != initial_profile:
        raise AttestedConformanceError("release policy changed during source observation")
    return profile


def _active_profile(profiles: Sequence[ProfilePolicy], profile_id: str) -> ProfilePolicy:
    matches = [profile for profile in profiles if profile.profile_id == profile_id]
    if len(matches) != 1:
        raise AttestedConformanceError("requested conformance profile is not active")
    return matches[0]


def profile_bindings(profile: ProfilePolicy) -> tuple[ScenarioBinding, ...]:
    return tuple(
        ScenarioBinding(scenario.scenario_id, scenario.pytest_node_id)
        for scenario in profile.scenarios
    )


def assess_attested_junit(
    path: Path,
    *,
    process_exit_code: int,
    expected_bindings: Sequence[ScenarioBinding],
) -> JUnitResult:
    try:
        result = parse_attested_pytest_junit(path)
    except JUnitError as exc:
        raise AttestedConformanceError(f"JUnit evidence is invalid: {exc}") from exc
    return _assess_result(
        result,
        process_exit_code=process_exit_code,
        expected_bindings=expected_bindings,
    )


def assess_attested_junit_bytes(
    payload: bytes,
    *,
    process_exit_code: int,
    expected_bindings: Sequence[ScenarioBinding],
) -> JUnitResult:
    """Assess one immutable JUnit payload and return its proven result."""
    try:
        result = parse_attested_pytest_junit_bytes(payload)
    except JUnitError as exc:
        raise AttestedConformanceError(f"JUnit evidence is invalid: {exc}") from exc
    return _assess_result(
        result,
        process_exit_code=process_exit_code,
        expected_bindings=expected_bindings,
    )


def _assess_result(
    result: JUnitResult,
    *,
    process_exit_code: int,
    expected_bindings: Sequence[ScenarioBinding],
) -> JUnitResult:
    if process_exit_code != 0:
        raise AttestedConformanceError(f"test process failed with exit code {process_exit_code}")
    expected = tuple(sorted((item.scenario_id, item.pytest_node_id) for item in expected_bindings))
    if result.scenario_nodes != expected:
        raise AttestedConformanceError("JUnit scenario-to-pytest-node mapping differs from policy")
    if result.failed:
        raise AttestedConformanceError(f"failed conformance scenarios: {result.failed}")
    if result.skipped:
        raise AttestedConformanceError(f"unexpected skipped conformance scenarios: {result.skipped}")
    if result.passed != result.total or not result.total:
        raise AttestedConformanceError("conformance result totals are contradictory")
    return result


def _source_relative_path(root: Path, path: Path) -> str:
    try:
        metadata = path.lstat()
        resolved_root = root.resolve(strict=True)
        relative = path.resolve(strict=True).relative_to(resolved_root)
    except (OSError, ValueError) as exc:
        raise AttestedConformanceError("release policy must be inside the source root") from exc
    if not stat.S_ISREG(metadata.st_mode):
        raise AttestedConformanceError("release policy must be a regular file, not a link")
    value = PurePosixPath(relative.as_posix()).as_posix()
    if not value or value == ".":
        raise AttestedConformanceError("release policy must be a tracked source file")
    return value
