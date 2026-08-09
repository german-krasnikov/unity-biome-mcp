"""CLI integration tests for policy-bound exact conformance execution."""

from __future__ import annotations

import subprocess
import sys
from pathlib import Path

import pytest

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))
sys.path.insert(0, str(Path(__file__).resolve().parent))

from gauntlet.junit import parse_attested_pytest_junit  # noqa: E402
from release_source_test_support import prepare_source  # noqa: E402

RUNNER = SCRIPTS / "attested_conformance_runner.py"


def _git(root: Path, *args: str) -> str:
    result = subprocess.run(
        ["git", "-C", str(root), *args],
        check=True,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip()


def _fixture(tmp_path: Path) -> tuple[Path, Path, str, Path]:
    source = prepare_source(
        tmp_path / "source",
        version="1.27.0",
        profile_id="unity-linux",
        scenarios=("identity", "read-envelope"),
    )
    test_path = source.root / "server" / "tests" / "contracts" / "test_release.py"
    test_path.parent.mkdir(parents=True, exist_ok=True)
    test_path.write_text(
        """def test_contract_0():
    assert True

def test_contract_1():
    assert True
""",
        encoding="utf-8",
    )
    _git(source.root, "add", "server/tests/contracts/test_release.py")
    _git(source.root, "commit", "-q", "-m", "add exact tests")
    project = tmp_path / "UnityProject"
    (project / "Assets").mkdir(parents=True)
    return source.root, source.policy_path, _git(source.root, "rev-parse", "HEAD"), project


def test_runner_executes_only_observed_policy_nodes_and_publishes_attested_junit(
    tmp_path: Path,
) -> None:
    root, policy, head, project = _fixture(tmp_path)
    junit = tmp_path / "result" / "junit.xml"

    result = subprocess.run(
        [
            sys.executable,
            str(RUNNER),
            "--source-root",
            str(root),
            "--policy",
            str(policy),
            "--expected-head",
            head,
            "--profile",
            "unity-linux",
            "--port",
            "9500",
            "--project",
            str(project),
            "--junit",
            str(junit),
        ],
        capture_output=True,
        text=True,
        timeout=30,
    )

    assert result.returncode == 0, result.stdout + result.stderr
    parsed = parse_attested_pytest_junit(junit)
    assert parsed.scenario_nodes == (
        ("identity", "server/tests/contracts/test_release.py::test_contract_0"),
        ("read-envelope", "server/tests/contracts/test_release.py::test_contract_1"),
    )
    assert "compatibility-only" in result.stdout


def test_runner_rejects_worker_mismatch_before_pytest(tmp_path: Path) -> None:
    root, policy, head, _ = _fixture(tmp_path)

    result = subprocess.run(
        [
            sys.executable,
            str(RUNNER),
            "--source-root",
            str(root),
            "--policy",
            str(policy),
            "--expected-head",
            head,
            "--profile",
            "unity-linux",
            "--junit",
            str(tmp_path / "junit.xml"),
        ],
        capture_output=True,
        text=True,
        timeout=30,
    )

    assert result.returncode != 0
    assert "worker" in result.stderr.lower()
    assert not (tmp_path / "junit.xml").exists()


@pytest.mark.parametrize("ignored_overlay", [False, True])
def test_runner_excludes_untracked_conftest_that_replaces_a_failing_leaf(
    tmp_path: Path,
    ignored_overlay: bool,
) -> None:
    root, policy, head, project = _fixture(tmp_path)
    test_path = root / "server" / "tests" / "contracts" / "test_release.py"
    test_path.write_text(
        """def test_contract_0():
    assert False

def test_contract_1():
    assert True
""",
        encoding="utf-8",
    )
    if ignored_overlay:
        (root / ".gitignore").write_text(
            "server/tests/contracts/conftest.py\n",
            encoding="utf-8",
        )
        _git(root, "add", ".gitignore")
    _git(root, "add", "server/tests/contracts/test_release.py")
    _git(root, "commit", "-q", "-m", "make reviewed leaf fail")
    head = _git(root, "rev-parse", "HEAD")
    (test_path.parent / "conftest.py").write_text(
        """def pytest_collection_modifyitems(items):
    for item in items:
        item.obj = lambda: None
""",
        encoding="utf-8",
    )
    sentinel = tmp_path / "junit.xml"

    result = subprocess.run(
        [
            sys.executable,
            str(RUNNER),
            "--source-root",
            str(root),
            "--policy",
            str(policy),
            "--expected-head",
            head,
            "--profile",
            "unity-linux",
            "--port",
            "9500",
            "--project",
            str(project),
            "--junit",
            str(sentinel),
        ],
        capture_output=True,
        text=True,
        timeout=30,
    )

    assert result.returncode != 0
    assert "failed" in (result.stdout + result.stderr).lower()
    assert not sentinel.exists()


def test_runner_cannot_execute_policy_leaf_added_only_to_worktree(tmp_path: Path) -> None:
    source = prepare_source(
        tmp_path / "source",
        version="1.27.0",
        profile_id="unity-linux",
        scenarios=("identity",),
    )
    test_path = source.root / "server" / "tests" / "contracts" / "test_release.py"
    _git(source.root, "rm", "-q", "server/tests/contracts/test_release.py")
    _git(source.root, "commit", "-q", "-m", "remove policy leaf")
    head = _git(source.root, "rev-parse", "HEAD")
    test_path.parent.mkdir(parents=True, exist_ok=True)
    test_path.write_text("def test_contract_0():\n    assert True\n", encoding="utf-8")
    project = tmp_path / "UnityProject"
    (project / "Assets").mkdir(parents=True)
    junit = tmp_path / "junit.xml"

    result = subprocess.run(
        [
            sys.executable,
            str(RUNNER),
            "--source-root",
            str(source.root),
            "--policy",
            str(source.policy_path),
            "--expected-head",
            head,
            "--profile",
            "unity-linux",
            "--port",
            "9500",
            "--project",
            str(project),
            "--junit",
            str(junit),
        ],
        capture_output=True,
        text=True,
        timeout=30,
    )

    assert result.returncode != 0
    assert "not tracked" in (result.stdout + result.stderr).lower()
    assert not junit.exists()


def test_runner_does_not_import_pytest_plugin_shadow_from_test_source(
    tmp_path: Path,
) -> None:
    root, policy, _, project = _fixture(tmp_path)
    test_path = root / "server" / "tests" / "contracts" / "test_release.py"
    test_path.write_text(
        """def test_contract_0():
    assert False

def test_contract_1():
    assert True
""",
        encoding="utf-8",
    )
    (root / "pytest_timeout.py").write_text(
        """def pytest_addoption(parser):
    parser.addoption("--timeout")

def pytest_collection_modifyitems(items):
    for item in items:
        item.obj = lambda: None
""",
        encoding="utf-8",
    )
    _git(root, "add", "server/tests/contracts/test_release.py", "pytest_timeout.py")
    _git(root, "commit", "-q", "-m", "add plugin shadow")
    head = _git(root, "rev-parse", "HEAD")
    junit = tmp_path / "junit.xml"

    result = subprocess.run(
        [
            sys.executable,
            str(RUNNER),
            "--source-root",
            str(root),
            "--policy",
            str(policy),
            "--expected-head",
            head,
            "--profile",
            "unity-linux",
            "--port",
            "9500",
            "--project",
            str(project),
            "--junit",
            str(junit),
        ],
        capture_output=True,
        text=True,
        timeout=30,
    )

    assert result.returncode != 0
    assert "failed" in (result.stdout + result.stderr).lower()
    assert not junit.exists()
