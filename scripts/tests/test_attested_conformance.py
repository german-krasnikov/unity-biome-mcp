"""Policy-bound conformance selection and assessment tests."""

from __future__ import annotations

import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from gauntlet.attested_conformance import (  # noqa: E402
    AttestedConformanceError,
    assess_attested_junit,
    assess_attested_junit_bytes,
    load_observed_profile,
)
from gauntlet.pytest_attestation import ScenarioBinding  # noqa: E402
from gauntlet_test_fixtures import write_attested_junit  # noqa: E402
from release_source_test_support import prepare_source  # noqa: E402


def test_observed_profile_comes_from_exact_clean_git_head(tmp_path: Path) -> None:
    source = prepare_source(
        tmp_path / "source",
        version="1.27.0",
        profile_id="unity-linux",
        scenarios=("identity", "read-envelope"),
    )

    profile = load_observed_profile(
        source_root=source.root,
        policy_path=source.policy_path,
        expected_head_sha=source.head_sha,
        profile_id="unity-linux",
    )

    assert tuple(
        ScenarioBinding(scenario.scenario_id, scenario.pytest_node_id)
        for scenario in profile.scenarios
    ) == (
        ScenarioBinding("identity", "server/tests/contracts/test_release.py::test_contract_0"),
        ScenarioBinding(
            "read-envelope",
            "server/tests/contracts/test_release.py::test_contract_1",
        ),
    )


def test_attested_assessment_requires_exact_pairs_and_green_process(tmp_path: Path) -> None:
    expected = (
        ScenarioBinding("identity", "server/tests/test_contract.py::test_identity"),
        ScenarioBinding("read", "server/tests/test_contract.py::test_read"),
    )
    junit = tmp_path / "junit.xml"
    write_attested_junit(
        junit,
        ((binding.scenario_id, binding.pytest_node_id) for binding in expected),
    )

    result = assess_attested_junit(
        junit,
        process_exit_code=0,
        expected_bindings=expected,
    )

    assert result.passed == 2
    swapped = (
        ScenarioBinding("identity", expected[1].pytest_node_id),
        ScenarioBinding("read", expected[0].pytest_node_id),
    )
    with pytest.raises(AttestedConformanceError, match="mapping"):
        assess_attested_junit(junit, process_exit_code=0, expected_bindings=swapped)
    with pytest.raises(AttestedConformanceError, match="exit code"):
        assess_attested_junit(junit, process_exit_code=3, expected_bindings=expected)

    payload_result = assess_attested_junit_bytes(
        junit.read_bytes(),
        process_exit_code=0,
        expected_bindings=expected,
    )
    assert payload_result == result


def test_observed_profile_rejects_inactive_or_unknown_profile(tmp_path: Path) -> None:
    source = prepare_source(
        tmp_path / "source",
        version="1.27.0",
        profile_id="active-profile",
        scenarios=("identity",),
    )

    with pytest.raises(AttestedConformanceError, match="active"):
        load_observed_profile(
            source_root=source.root,
            policy_path=source.policy_path,
            expected_head_sha=source.head_sha,
            profile_id="missing-profile",
        )
