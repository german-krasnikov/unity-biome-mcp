from __future__ import annotations

import json
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
if str(SCRIPTS) not in sys.path:
    sys.path.insert(0, str(SCRIPTS))

from gauntlet.release_policy import PolicyError, load_release_policy  # noqa: E402


def _policy_data() -> dict[str, object]:
    return {
        "schema_version": 3,
        "policy_version": "1.0.0",
        "activation_product_version": "1.27.0",
        "harness_lock_path": "server/uv.lock",
        "contract_catalog_path": "scripts/gauntlet/contracts.json",
        "contract_catalog_sha": "c" * 64,
        "artifact_types": ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
        "profiles": [
            {
                "id": "public-stdio-linux-py310",
                "active": True,
                "driver": "public_stdio",
                "os": "linux",
                "python": "3.10",
                "unity": None,
                "plugin_scope": "none",
                "required_workers": 0,
                "worker_roles": [],
                "consumed_artifacts": ["python_wheel"],
                "cleanup_obligations": ["stdio-process", "tcp-peer"],
                "scenarios": [
                    {
                        "id": "tests.contracts::test_schema_parity[stdio]",
                        "pytest_node_id": (
                            "server/tests/contracts/test_public_stdio.py::"
                            "test_schema_parity[stdio]"
                        ),
                    },
                    {
                        "id": "tests.contracts::test_version_handshake",
                        "pytest_node_id": (
                            "server/tests/contracts/test_public_stdio.py::"
                            "test_version_handshake"
                        ),
                    },
                ],
                "max_age_seconds": 86400,
            },
            {
                "id": "unity-dual-macos",
                "active": True,
                "driver": "unity_editor",
                "os": "macos",
                "python": "3.12",
                "unity": "6000.0.65f1",
                "plugin_scope": "exact",
                "required_workers": 2,
                "worker_roles": ["worker-a", "worker-b"],
                "consumed_artifacts": [
                    "python_wheel",
                    "unity_editor_upm",
                    "unity_reload_upm",
                ],
                "cleanup_obligations": ["process-tree", "worker-a", "worker-b"],
                "scenarios": [
                    {
                        "id": "tests.cross_project.test_identity::test_route_a_b_a",
                        "pytest_node_id": (
                            "server/tests/cross_project/test_identity.py::test_route_a_b_a"
                        ),
                    },
                    {
                        "id": "tests.cross_project.test_isolation::test_state_isolation",
                        "pytest_node_id": (
                            "server/tests/cross_project/test_isolation.py::test_state_isolation"
                        ),
                    },
                ],
                "max_age_seconds": 21600,
            },
        ],
    }


def _write_policy(path: Path, data: dict[str, object]) -> Path:
    path.write_text(json.dumps(data, indent=2), encoding="utf-8")
    return path


def test_policy_loads_active_requirements_and_deterministic_digests(tmp_path: Path) -> None:
    data = _policy_data()
    first = load_release_policy(_write_policy(tmp_path / "first.json", data))
    second_path = tmp_path / "second.json"
    second_path.write_text(json.dumps(data, separators=(",", ":")), encoding="utf-8")
    second = load_release_policy(second_path)

    assert first.policy_sha == second.policy_sha
    assert first.artifact_types == (
        "python_wheel",
        "unity_editor_upm",
        "unity_reload_upm",
    )
    assert [profile.profile_id for profile in first.active_profiles] == [
        "public-stdio-linux-py310",
        "unity-dual-macos",
    ]
    requirement = first.active_requirements["public-stdio-linux-py310"]
    assert requirement.required_workers == 0
    assert requirement.scenario_ids == (
        "tests.contracts::test_schema_parity[stdio]",
        "tests.contracts::test_version_handshake",
    )
    assert first.active_profiles[0].pytest_node_ids == (
        "server/tests/contracts/test_public_stdio.py::test_schema_parity[stdio]",
        "server/tests/contracts/test_public_stdio.py::test_version_handshake",
    )
    assert requirement.pytest_node_ids == first.active_profiles[0].pytest_node_ids
    assert requirement.cleanup_obligations == ("stdio-process", "tcp-peer")
    assert len(requirement.profile_manifest_sha) == 64


def test_profile_digest_changes_with_contract(tmp_path: Path) -> None:
    baseline = _policy_data()
    changed = _policy_data()
    changed_profile = changed["profiles"][0]
    assert isinstance(changed_profile, dict)
    scenarios = changed_profile["scenarios"]
    assert isinstance(scenarios, list)
    changed_scenario = scenarios[1]
    assert isinstance(changed_scenario, dict)
    changed_scenario["id"] = "tests.contracts::test_extra_contract"
    changed_scenario["pytest_node_id"] = (
        "server/tests/contracts/test_public_stdio.py::test_extra_contract"
    )

    first = load_release_policy(_write_policy(tmp_path / "first.json", baseline))
    second = load_release_policy(_write_policy(tmp_path / "second.json", changed))

    assert first.profiles[0].manifest_sha != second.profiles[0].manifest_sha
    assert first.policy_sha != second.policy_sha


@pytest.mark.parametrize(
    ("mutate", "message"),
    [
        (lambda data: data.update({"unknown": True}), "schema"),
        (
            lambda data: data.update({"activation_product_version": "latest"}),
            "semantic version",
        ),
        (lambda data: data.update({"artifact_types": ["python_wheel", "python_wheel"]}), "artifact"),
        (lambda data: data.update({"artifact_types": ["python_wheel"]}), "supported"),
        (lambda data: data.update({"profiles": []}), "active profile"),
        (lambda data: data["profiles"].append(dict(data["profiles"][0])), "duplicate profile"),
        (lambda data: data["profiles"][0].update({"required_workers": 1}), "worker role"),
        (lambda data: data["profiles"][0].update({"scenarios": []}), "scenario"),
        (
            lambda data: data["profiles"][0].update(
                {
                    "scenarios": [
                        {
                            "id": "bad\nscenario",
                            "pytest_node_id": "server/tests/test_bad.py::test_bad",
                        }
                    ]
                }
            ),
            "scenario",
        ),
        (
            lambda data: data["profiles"][0]["scenarios"][0].update(
                {"pytest_node_id": "../outside.py::test_bad"}
            ),
            "pytest node",
        ),
        (
            lambda data: data["profiles"][0]["scenarios"][1].update(
                {
                    "pytest_node_id": data["profiles"][0]["scenarios"][0][
                        "pytest_node_id"
                    ]
                }
            ),
            "pytest node",
        ),
        (
            lambda data: data["profiles"][0].update({"consumed_artifacts": ["unknown"]}),
            "Python wheel",
        ),
        (
            lambda data: data["profiles"][0].update({"cleanup_obligations": []}),
            "cleanup",
        ),
        (lambda data: data["profiles"][0].update({"os": "amiga"}), "operating system"),
        (lambda data: data["profiles"][0].update({"plugin_scope": "guess"}), "plugin scope"),
        (
            lambda data: data["profiles"][0].update({"unity": "6000.0.65f1", "plugin_scope": "exact"}),
            "public stdio",
        ),
        (
            lambda data: data["profiles"][1].update({"required_workers": 0, "worker_roles": []}),
            "Unity Editor",
        ),
        (
            lambda data: data["profiles"][1].update(
                {"consumed_artifacts": ["python_wheel", "unity_editor_upm"]}
            ),
            "both UPM",
        ),
    ],
)
def test_policy_rejects_ambiguous_or_incomplete_contracts(
    tmp_path: Path,
    mutate: object,
    message: str,
) -> None:
    data = _policy_data()
    assert callable(mutate)
    mutate(data)

    with pytest.raises(PolicyError, match=message):
        load_release_policy(_write_policy(tmp_path / "policy.json", data))
