from __future__ import annotations

import sys
from datetime import datetime, timedelta, timezone
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from gauntlet.evidence_schema import evidence_hash, run_manifest_hash  # noqa: E402
from gauntlet.release_evidence import (  # noqa: E402
    EvidenceError,
    ProfileRequirement,
    build_conformance_evidence,
    load_conformance_evidence,
    validate_release_evidence,
    validate_release_evidence_bundle,
    write_conformance_evidence,
)

HEAD_SHA = "a" * 40
SOURCE_OBSERVATION_SHA = "9" * 64
POLICY_SHA = "b" * 64
CONTRACT_CATALOG_SHA = "8" * 64
HARNESS_LOCK_SHA = "c" * 64
ARTIFACT_MANIFEST_SHA = "d" * 64
ARTIFACTS = {"python_wheel": "e" * 64, "unity_upm": "f" * 64}
PROFILE_MANIFEST_SHA = "1" * 64
SCENARIOS = ("identity-handshake", "read-envelope")
RUN_ID = "run-evidence-test"


def _requirement(
    *,
    workers: int = 2,
    scenarios: tuple[str, ...] = SCENARIOS,
    manifest_sha: str = PROFILE_MANIFEST_SHA,
) -> ProfileRequirement:
    return ProfileRequirement(
        profile_manifest_sha=manifest_sha,
        scenario_ids=scenarios,
        driver="unity_editor" if workers else "public_stdio",
        operating_system="linux",
        python_version="3.10",
        unity_version="6000.0.65f1" if workers else None,
        plugin_scope="exact" if workers else "none",
        required_workers=workers,
        worker_roles=tuple(f"worker-{index}" for index in range(workers)),
        consumed_artifacts=("python_wheel", "unity_upm") if workers else ("python_wheel",),
        cleanup_obligations=("process-tree",),
        max_age_seconds=86400,
    )


def _valid_evidence(**overrides: object) -> dict[str, object]:
    values: dict[str, object] = {
        "run_id": RUN_ID,
        "head_sha": HEAD_SHA,
        "source_observation_sha": SOURCE_OBSERVATION_SHA,
        "policy_version": "1.0.0",
        "policy_sha": POLICY_SHA,
        "contract_catalog_sha": CONTRACT_CATALOG_SHA,
        "harness_lock_sha": HARNESS_LOCK_SHA,
        "artifact_manifest_sha": ARTIFACT_MANIFEST_SHA,
        "artifacts": ARTIFACTS,
        "profile": "dual-project",
        "profile_manifest_sha": PROFILE_MANIFEST_SHA,
        "expected_scenario_ids": SCENARIOS,
        "executed_scenario_ids": SCENARIOS,
        "selected_tests": 2,
        "passed": 2,
        "failed": 0,
        "skipped": 0,
        "blocked": 0,
        "untested": 0,
        "exit_code": 0,
        "required_workers": 2,
        "evidence_artifacts": {"owned": "bundle"},
    }
    values.update(overrides)
    values.setdefault(
        "run_manifest_sha",
        run_manifest_hash(
            head_sha=str(values["head_sha"]),
            source_observation_sha=str(values["source_observation_sha"]),
            policy_sha=str(values["policy_sha"]),
            contract_catalog_sha=str(values["contract_catalog_sha"]),
            profile_manifest_sha=str(values["profile_manifest_sha"]),
            harness_lock_sha=str(values["harness_lock_sha"]),
            artifact_manifest_sha=str(values["artifact_manifest_sha"]),
            artifacts=values["artifacts"],
        ),
    )
    return build_conformance_evidence(**values)


def _validate(evidence: dict[str, object]) -> None:
    validate_release_evidence(
        evidence,
        expected_head_sha=HEAD_SHA,
        expected_source_observation_sha=SOURCE_OBSERVATION_SHA,
        expected_policy_version="1.0.0",
        expected_policy_sha=POLICY_SHA,
        expected_contract_catalog_sha=CONTRACT_CATALOG_SHA,
        expected_harness_lock_sha=HARNESS_LOCK_SHA,
        expected_artifact_manifest_sha=ARTIFACT_MANIFEST_SHA,
        expected_artifacts=ARTIFACTS,
        profile_requirement=_requirement(),
    )


def test_release_evidence_accepts_exact_complete_gate() -> None:
    _validate(_valid_evidence())


def test_release_evidence_hashing_rejects_non_finite_numbers() -> None:
    with pytest.raises(EvidenceError, match="serializable"):
        evidence_hash({"value": float("-inf")})


@pytest.mark.parametrize(
    ("override", "message"),
    [
        ({"head_sha": "0" * 40}, "head sha"),
        ({"source_observation_sha": "0" * 64}, "source observation"),
        ({"policy_sha": "0" * 64}, "policy"),
        ({"contract_catalog_sha": "0" * 64}, "contract catalog"),
        ({"harness_lock_sha": "0" * 64}, "harness lock"),
        ({"artifact_manifest_sha": "0" * 64}, "artifact manifest"),
        ({"artifacts": {"python_wheel": "0" * 64}}, "artifact digest"),
        ({"skipped": 1, "passed": 1}, "skip"),
        ({"blocked": 1, "passed": 1}, "blocked"),
        ({"untested": 1, "passed": 1}, "untested"),
        ({"failed": 1, "passed": 1}, "failed"),
        ({"exit_code": 1}, "exit code"),
        ({"evidence_artifacts": []}, "evidence artifacts"),
    ],
)
def test_release_evidence_rejects_false_green(override: dict[str, object], message: str) -> None:
    with pytest.raises(EvidenceError, match=message):
        _validate(_valid_evidence(**override))


@pytest.mark.parametrize(
    "executed",
    [
        SCENARIOS[:1],
        (*SCENARIOS, "unexpected-case"),
    ],
)
def test_release_evidence_requires_exact_scenario_manifest(
    executed: tuple[str, ...],
) -> None:
    with pytest.raises(EvidenceError, match="scenario"):
        _validate(_valid_evidence(executed_scenario_ids=executed))


def test_release_evidence_allows_parallel_leaf_completion_order() -> None:
    _validate(_valid_evidence(executed_scenario_ids=tuple(reversed(SCENARIOS))))


def test_release_evidence_rejects_unrequired_profile_manifest() -> None:
    with pytest.raises(EvidenceError, match="profile manifest"):
        _validate(_valid_evidence(profile_manifest_sha="0" * 64))


def test_release_bundle_requires_every_profile_exactly_once() -> None:
    single_scenarios = ("single-identity",)
    single = _valid_evidence(
        profile="single-project",
        profile_manifest_sha="7" * 64,
        expected_scenario_ids=single_scenarios,
        executed_scenario_ids=single_scenarios,
        selected_tests=1,
        passed=1,
        required_workers=1,
    )
    dual = _valid_evidence()
    requirements = {
        "single-project": _requirement(
            workers=1,
            scenarios=single_scenarios,
            manifest_sha="7" * 64,
        ),
        "dual-project": _requirement(),
    }
    kwargs = {
        "expected_head_sha": HEAD_SHA,
        "expected_source_observation_sha": SOURCE_OBSERVATION_SHA,
        "expected_policy_version": "1.0.0",
        "expected_policy_sha": POLICY_SHA,
        "expected_contract_catalog_sha": CONTRACT_CATALOG_SHA,
        "expected_harness_lock_sha": HARNESS_LOCK_SHA,
        "expected_artifact_manifest_sha": ARTIFACT_MANIFEST_SHA,
        "expected_artifacts": ARTIFACTS,
        "required_profiles": requirements,
    }

    validate_release_evidence_bundle([single, dual], **kwargs)

    with pytest.raises(EvidenceError, match="missing profile"):
        validate_release_evidence_bundle([single], **kwargs)
    with pytest.raises(EvidenceError, match="duplicate profile"):
        validate_release_evidence_bundle([single, single, dual], **kwargs)


def test_release_bundle_rejects_wrong_worker_requirement() -> None:
    with pytest.raises(EvidenceError, match="worker requirement"):
        validate_release_evidence_bundle(
            [_valid_evidence()],
            expected_head_sha=HEAD_SHA,
            expected_source_observation_sha=SOURCE_OBSERVATION_SHA,
            expected_policy_version="1.0.0",
            expected_policy_sha=POLICY_SHA,
            expected_contract_catalog_sha=CONTRACT_CATALOG_SHA,
            expected_harness_lock_sha=HARNESS_LOCK_SHA,
            expected_artifact_manifest_sha=ARTIFACT_MANIFEST_SHA,
            expected_artifacts=ARTIFACTS,
            required_profiles={"dual-project": _requirement(workers=1)},
        )


def test_release_bundle_rejects_stale_or_future_evidence() -> None:
    now = datetime(2026, 8, 9, 12, 0, tzinfo=timezone.utc)
    stale = _valid_evidence(created_at="2026-08-08T11:59:59Z")
    future = _valid_evidence(created_at="2026-08-09T12:06:00Z")
    kwargs = {
        "expected_head_sha": HEAD_SHA,
        "expected_source_observation_sha": SOURCE_OBSERVATION_SHA,
        "expected_policy_version": "1.0.0",
        "expected_policy_sha": POLICY_SHA,
        "expected_contract_catalog_sha": CONTRACT_CATALOG_SHA,
        "expected_harness_lock_sha": HARNESS_LOCK_SHA,
        "expected_artifact_manifest_sha": ARTIFACT_MANIFEST_SHA,
        "expected_artifacts": ARTIFACTS,
        "required_profiles": {"dual-project": _requirement()},
        "now": now,
        "max_age": timedelta(hours=24),
    }

    with pytest.raises(EvidenceError, match="stale"):
        validate_release_evidence_bundle([stale], **kwargs)
    with pytest.raises(EvidenceError, match="future"):
        validate_release_evidence_bundle([future], **kwargs)


def test_release_evidence_round_trip_is_strict_and_atomic(tmp_path: Path) -> None:
    evidence = _valid_evidence()
    path = tmp_path / "dual-project.json"

    write_conformance_evidence(path, evidence)
    loaded = load_conformance_evidence(path)

    assert loaded == evidence
    assert not list(tmp_path.glob(".dual-project.json.*.tmp"))


@pytest.mark.parametrize(
    "body",
    [
        "not-json",
        "[]",
        '{"schema_version": 3}',
    ],
)
def test_release_evidence_loader_rejects_invalid_artifact(
    tmp_path: Path,
    body: str,
) -> None:
    path = tmp_path / "evidence.json"
    path.write_text(body, encoding="utf-8")

    with pytest.raises(EvidenceError):
        load_conformance_evidence(path)


def test_release_evidence_rejects_declared_policy_version_mismatch() -> None:
    evidence = _valid_evidence(policy_version="unexpected")

    with pytest.raises(EvidenceError, match="policy version"):
        _validate(evidence)
