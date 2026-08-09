"""End-to-end tests for exact policy-bound pytest collection."""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from gauntlet.junit import parse_attested_pytest_junit  # noqa: E402
from gauntlet.pytest_attestation import (  # noqa: E402
    AttestationError,
    ScenarioBinding,
    load_attestation_manifest,
    write_attestation_manifest,
)


def _source(tmp_path: Path, body: str) -> tuple[Path, Path]:
    root = tmp_path / "source"
    test_path = root / "server" / "tests" / "test_contract.py"
    test_path.parent.mkdir(parents=True)
    test_path.write_text(body, encoding="utf-8")
    return root, test_path


def _run(
    root: Path,
    manifest: Path,
    manifest_sha: str,
    selected_nodes: tuple[str, ...],
    *extra: str,
) -> subprocess.CompletedProcess[str]:
    junit = root / "junit.xml"
    environment = os.environ.copy()
    environment["PYTHONPATH"] = os.pathsep.join(
        part for part in (str(SCRIPTS), environment.get("PYTHONPATH", "")) if part
    )
    environment["PYTEST_DISABLE_PLUGIN_AUTOLOAD"] = "1"
    environment.pop("PYTEST_ADDOPTS", None)
    return subprocess.run(
        [
            sys.executable,
            "-m",
            "pytest",
            "-p",
            "gauntlet.pytest_attestation",
            *selected_nodes,
            f"--gauntlet-manifest={manifest}",
            f"--gauntlet-manifest-sha={manifest_sha}",
            f"--gauntlet-source-root={root}",
            f"--junitxml={junit}",
            "-q",
            *extra,
        ],
        cwd=root,
        env=environment,
        capture_output=True,
        text=True,
        timeout=30,
    )


def test_manifest_round_trip_is_content_addressed_and_one_to_one(tmp_path: Path) -> None:
    path = tmp_path / "manifest.json"
    bindings = (
        ScenarioBinding("identity", "server/tests/test_contract.py::test_identity"),
        ScenarioBinding("read-envelope", "server/tests/test_contract.py::test_read"),
    )

    digest = write_attestation_manifest(path, "public-stdio", bindings)
    manifest = load_attestation_manifest(path, expected_sha=digest)

    assert manifest.profile_id == "public-stdio"
    assert manifest.bindings == bindings
    assert manifest.manifest_sha == digest
    with pytest.raises(AttestationError, match="digest"):
        load_attestation_manifest(path, expected_sha="0" * 64)


@pytest.mark.parametrize(
    "bindings",
    [
        (
            ScenarioBinding("same", "server/tests/test_contract.py::test_one"),
            ScenarioBinding("same", "server/tests/test_contract.py::test_two"),
        ),
        (
            ScenarioBinding("one", "server/tests/test_contract.py::test_same"),
            ScenarioBinding("two", "server/tests/test_contract.py::test_same"),
        ),
    ],
)
def test_manifest_rejects_duplicate_scenarios_or_pytest_nodes(
    tmp_path: Path,
    bindings: tuple[ScenarioBinding, ...],
) -> None:
    with pytest.raises(AttestationError, match="duplicate"):
        write_attestation_manifest(tmp_path / "manifest.json", "profile", bindings)


def test_plugin_emits_harness_owned_junit_identity(tmp_path: Path) -> None:
    root, _ = _source(
        tmp_path,
        """def test_contract(record_property):
    record_property("gauntlet_scenario_id", "substituted")
    record_property("gauntlet_pytest_node_id", "server/tests/other.py::test_fake")
    assert True
""",
    )
    node = "server/tests/test_contract.py::test_contract"
    manifest = root / "manifest.json"
    digest = write_attestation_manifest(
        manifest,
        "profile",
        (ScenarioBinding("real-contract", node),),
    )

    result = _run(root, manifest, digest, (node,))

    assert result.returncode == 0, result.stdout + result.stderr
    junit = parse_attested_pytest_junit(root / "junit.xml")
    assert junit.scenario_nodes == (("real-contract", node),)


@pytest.mark.parametrize(
    "selected",
    [
        ("server/tests/test_contract.py::test_extra",),
        (
            "server/tests/test_contract.py::test_expected",
            "server/tests/test_contract.py::test_extra",
        ),
    ],
)
def test_collection_drift_aborts_before_first_test(
    tmp_path: Path,
    selected: tuple[str, ...],
) -> None:
    sentinel = tmp_path / "executed.txt"
    root, _ = _source(
        tmp_path,
        f"""from pathlib import Path

def test_expected():
    Path({str(sentinel)!r}).write_text("expected")

def test_extra():
    Path({str(sentinel)!r}).write_text("extra")
""",
    )
    expected = "server/tests/test_contract.py::test_expected"
    manifest = root / "manifest.json"
    digest = write_attestation_manifest(
        manifest,
        "profile",
        (ScenarioBinding("expected", expected),),
    )

    result = _run(root, manifest, digest, selected)

    assert result.returncode != 0
    assert "collection" in (result.stdout + result.stderr).lower()
    assert not sentinel.exists()


def test_duplicate_collection_aborts_before_execution(tmp_path: Path) -> None:
    sentinel = tmp_path / "executed.txt"
    root, _ = _source(
        tmp_path,
        f"""from pathlib import Path
def test_expected():
    Path({str(sentinel)!r}).write_text("ran")
""",
    )
    node = "server/tests/test_contract.py::test_expected"
    manifest = root / "manifest.json"
    digest = write_attestation_manifest(
        manifest,
        "profile",
        (ScenarioBinding("expected", node),),
    )

    result = _run(
        root,
        manifest,
        digest,
        ("server/tests/test_contract.py", "server/tests/test_contract.py"),
        "--keep-duplicates",
    )

    assert result.returncode != 0
    assert "duplicate" in (result.stdout + result.stderr).lower()
    assert not sentinel.exists()


def test_keyword_override_is_rejected_before_execution(tmp_path: Path) -> None:
    sentinel = tmp_path / "executed.txt"
    root, _ = _source(
        tmp_path,
        f"""from pathlib import Path
def test_expected():
    Path({str(sentinel)!r}).write_text("ran")
""",
    )
    node = "server/tests/test_contract.py::test_expected"
    manifest = root / "manifest.json"
    digest = write_attestation_manifest(
        manifest,
        "profile",
        (ScenarioBinding("expected", node),),
    )

    result = _run(root, manifest, digest, (node,), "-k", "expected")

    assert result.returncode != 0
    assert "selector" in (result.stdout + result.stderr).lower()
    assert not sentinel.exists()


def test_parameterized_policy_leaf_runs_without_collecting_siblings(tmp_path: Path) -> None:
    root, _ = _source(
        tmp_path,
        """import pytest

@pytest.mark.parametrize("value", ["one", "two"])
def test_value(value):
    assert value in {"one", "two"}
""",
    )
    node = "server/tests/test_contract.py::test_value[one]"
    manifest = root / "manifest.json"
    digest = write_attestation_manifest(
        manifest,
        "profile",
        (ScenarioBinding("value-one", node),),
    )

    result = _run(root, manifest, digest, (node,))

    assert result.returncode == 0, result.stdout + result.stderr
    junit = parse_attested_pytest_junit(root / "junit.xml")
    assert junit.scenario_nodes == (("value-one", node),)


def test_setup_failure_retains_attested_identity(tmp_path: Path) -> None:
    root, _ = _source(
        tmp_path,
        """import pytest

@pytest.fixture
def broken_setup():
    raise RuntimeError("setup failed")

def test_contract(broken_setup):
    assert False
""",
    )
    node = "server/tests/test_contract.py::test_contract"
    manifest = root / "manifest.json"
    digest = write_attestation_manifest(
        manifest,
        "profile",
        (ScenarioBinding("setup-contract", node),),
    )

    result = _run(root, manifest, digest, (node,))

    assert result.returncode != 0
    junit = parse_attested_pytest_junit(root / "junit.xml")
    assert junit.failed == 1
    assert junit.scenario_nodes == (("setup-contract", node),)
