"""Disposable tracked source inputs for release-gate tests."""


import json
import os
import subprocess
from dataclasses import dataclass
from pathlib import Path  # noqa: TC003

from gauntlet.contract_catalog import ContractCatalog, load_contract_catalog
from gauntlet.package_contracts import PACKAGE_CONTENT_ROOTS, PACKAGE_SOURCE_PATHS
from gauntlet.release_policy import ReleasePolicy, load_release_policy
from gauntlet.source_provenance import SourceObservation, observe_source_checkout

POLICY_RELATIVE = "scripts/gauntlet/release-policy.json"
CATALOG_RELATIVE = "scripts/gauntlet/contracts.json"
HARNESS_LOCK_RELATIVE = "server/uv.lock"


@dataclass(frozen=True, slots=True)
class SourceFixture:
    root: Path
    policy_path: Path
    catalog_path: Path
    harness_lock_path: Path
    head_sha: str
    policy: ReleasePolicy
    catalog: ContractCatalog
    observation: SourceObservation


def prepare_source(
    root: Path,
    *,
    version: str,
    profile_id: str,
    scenarios: tuple[str, ...],
    reload_version: str = "0.1.4",
    reload_name: str = "com.unity-biome-mcp.reload",
    python_package_payload: str = "__version__ = 'test'\n",
) -> SourceFixture:
    policy_path = root / POLICY_RELATIVE
    catalog_path = root / CATALOG_RELATIVE
    harness_lock_path = root / HARNESS_LOCK_RELATIVE
    catalog_path.parent.mkdir(parents=True)
    harness_lock_path.parent.mkdir(parents=True)
    catalog_path.write_text(json.dumps(_catalog_data()), encoding="utf-8")
    catalog = load_contract_catalog(catalog_path)
    policy_path.write_text(
        json.dumps(_policy_data(version, profile_id, scenarios, catalog.catalog_sha)),
        encoding="utf-8",
    )
    harness_lock_path.write_text("locked-dependencies", encoding="utf-8")
    (root / PACKAGE_SOURCE_PATHS["python_wheel"]).write_text(
        f'[project]\nname = "unity-biome-mcp"\nversion = "{version}"\n',
        encoding="utf-8",
    )
    editor_package = root / PACKAGE_SOURCE_PATHS["unity_editor_upm"]
    reload_package = root / PACKAGE_SOURCE_PATHS["unity_reload_upm"]
    editor_package.parent.mkdir(parents=True, exist_ok=True)
    reload_package.parent.mkdir(parents=True, exist_ok=True)
    editor_package.write_text(
        json.dumps({"name": "com.unity-biome-mcp.editor", "version": version}),
        encoding="utf-8",
    )
    reload_package.write_text(
        json.dumps({"name": reload_name, "version": reload_version}),
        encoding="utf-8",
    )
    package_init = root / "server/src/unity_mcp/__init__.py"
    package_init.parent.mkdir(parents=True, exist_ok=True)
    package_init.write_text(python_package_payload, encoding="utf-8")
    test_path = root / "server/tests/contracts/test_release.py"
    test_path.parent.mkdir(parents=True, exist_ok=True)
    test_path.write_text(
        "\n\n".join(
            f"def test_contract_{index}():\n    raise AssertionError('fixture leaf was not replaced')"
            for index, _ in enumerate(scenarios)
        )
        + "\n",
        encoding="utf-8",
    )
    head_sha = _commit_source(root)
    policy = load_release_policy(policy_path)
    observation = observe_source_checkout(
        root,
        expected_head_sha=head_sha,
        required_paths=(
            POLICY_RELATIVE,
            CATALOG_RELATIVE,
            HARNESS_LOCK_RELATIVE,
            *PACKAGE_SOURCE_PATHS.values(),
        ),
        package_content_roots=PACKAGE_CONTENT_ROOTS,
    )
    return SourceFixture(
        root,
        policy_path,
        catalog_path,
        harness_lock_path,
        head_sha,
        policy,
        catalog,
        observation,
    )


def _policy_data(
    version: str,
    profile_id: str,
    scenarios: tuple[str, ...],
    catalog_sha: str,
) -> dict[str, object]:
    return {
        "schema_version": 3,
        "policy_version": "1.0.0",
        "activation_product_version": version,
        "harness_lock_path": HARNESS_LOCK_RELATIVE,
        "contract_catalog_path": CATALOG_RELATIVE,
        "contract_catalog_sha": catalog_sha,
        "artifact_types": ["python_wheel", "unity_editor_upm", "unity_reload_upm"],
        "profiles": [
            {
                "id": profile_id,
                "active": True,
                "driver": "unity_editor",
                "os": "linux",
                "python": "3.10",
                "unity": "6000.0.65f1",
                "plugin_scope": "exact",
                "required_workers": 1,
                "worker_roles": ["worker-a"],
                "consumed_artifacts": [
                    "python_wheel",
                    "unity_editor_upm",
                    "unity_reload_upm",
                ],
                "cleanup_obligations": ["process-tree", "worker-a"],
                "scenarios": [
                    {
                        "id": scenario,
                        "pytest_node_id": (
                            "server/tests/contracts/test_release.py::"
                            f"test_contract_{index}"
                        ),
                    }
                    for index, scenario in enumerate(scenarios)
                ],
                "max_age_seconds": 86400,
            }
        ],
    }


def _catalog_data() -> dict[str, object]:
    return {
        "schema_version": 2,
        "catalog_version": "1.0.0",
        "scope": "builtin",
        "owner": None,
        "contracts": [
            {
                "id": "status-clean-read",
                "action": "mcp_status",
                "effects": ["pure_read"],
                "retry": "blind_safe",
                "arguments": {},
                "preconditions": {"connected": True},
                "expect_error": False,
                "forbidden_success_patterns": ["^error:"],
            }
        ],
    }


def _commit_source(root: Path) -> str:
    commands = (
        ("init", "-q"),
        ("config", "user.name", "Gauntlet Test"),
        ("config", "user.email", "gauntlet@example.invalid"),
        ("add", "."),
    )
    for arguments in commands:
        subprocess.run(["git", "-C", str(root), *arguments], check=True)
    environment = os.environ.copy()
    environment.update(
        {
            "GIT_AUTHOR_DATE": "2026-01-01T00:00:00Z",
            "GIT_COMMITTER_DATE": "2026-01-01T00:00:00Z",
        }
    )
    subprocess.run(
        ["git", "-C", str(root), "commit", "-q", "-m", "release inputs"],
        check=True,
        env=environment,
    )
    result = subprocess.run(
        ["git", "-C", str(root), "rev-parse", "HEAD"],
        check=True,
        capture_output=True,
        text=True,
        encoding="utf-8",
    )
    return result.stdout.strip()
