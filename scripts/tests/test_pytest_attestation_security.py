"""Fail-closed interpreter and marker tests for pytest attestation."""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from gauntlet.pytest_attestation import (  # noqa: E402
    ScenarioBinding,
    write_attestation_manifest,
)


def _run(
    root: Path,
    manifest: Path,
    digest: str,
    node: str,
    *,
    environment_overrides: dict[str, str] | None = None,
) -> subprocess.CompletedProcess[str]:
    environment = os.environ.copy()
    environment["PYTHONPATH"] = str(SCRIPTS)
    environment["PYTEST_DISABLE_PLUGIN_AUTOLOAD"] = "1"
    environment.pop("PYTEST_ADDOPTS", None)
    if environment_overrides:
        environment.update(environment_overrides)
    return subprocess.run(
        [
            sys.executable,
            "-m",
            "pytest",
            "-p",
            "gauntlet.pytest_attestation",
            node,
            f"--gauntlet-manifest={manifest}",
            f"--gauntlet-manifest-sha={digest}",
            f"--gauntlet-source-root={root}",
            f"--junitxml={root / 'junit.xml'}",
            "-q",
        ],
        cwd=root,
        env=environment,
        capture_output=True,
        text=True,
        timeout=30,
    )


def _case(tmp_path: Path, body: str) -> tuple[Path, Path, str, str]:
    root = tmp_path / "source"
    test_path = root / "server/tests/test_contract.py"
    test_path.parent.mkdir(parents=True)
    test_path.write_text(body, encoding="utf-8")
    node = "server/tests/test_contract.py::test_expected"
    manifest = root / "manifest.json"
    digest = write_attestation_manifest(
        manifest,
        "profile",
        (ScenarioBinding("expected", node),),
    )
    return root, manifest, digest, node


def test_xfail_marker_is_rejected_before_execution(tmp_path: Path) -> None:
    sentinel = tmp_path / "executed.txt"
    root, manifest, digest, node = _case(
        tmp_path,
        f"""import pytest
from pathlib import Path

@pytest.mark.xfail(reason="must not weaken release evidence")
def test_expected():
    Path({str(sentinel)!r}).write_text("ran")
    assert True
""",
    )

    result = _run(root, manifest, digest, node)

    assert result.returncode != 0
    assert "xfail" in (result.stdout + result.stderr).lower()
    assert not sentinel.exists()


def test_optimized_interpreter_is_rejected_before_execution(tmp_path: Path) -> None:
    sentinel = tmp_path / "executed.txt"
    root, manifest, digest, node = _case(
        tmp_path,
        f"""from pathlib import Path

def test_expected():
    Path({str(sentinel)!r}).write_text("ran")
""",
    )

    result = _run(
        root,
        manifest,
        digest,
        node,
        environment_overrides={"PYTHONOPTIMIZE": "1"},
    )

    assert result.returncode != 0
    assert "optimization" in (result.stdout + result.stderr).lower()
    assert not sentinel.exists()
