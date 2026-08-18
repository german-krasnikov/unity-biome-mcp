"""Portable process-boundary tests for the attested pytest runner."""


import sys
from pathlib import Path
from types import SimpleNamespace

SCRIPTS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPTS))

from attested_conformance_runner import (  # noqa: E402
    _pytest_command,
    _write_pytest_arguments,
)


def test_pytest_selectors_use_argsfile_beyond_windows_command_limit(
    tmp_path: Path,
) -> None:
    arguments = tuple(
        f"/snapshot/server/tests/test_contract_{index}.py::test_{'x' * 900}"
        for index in range(40)
    )
    path = tmp_path / "pytest.args"

    _write_pytest_arguments(path, arguments)

    payload = path.read_text(encoding="utf-8")
    assert len(payload.encode("utf-8")) > 32_767
    assert tuple(payload.splitlines()) == arguments


def test_pytest_child_forces_utf8_mode_for_cross_platform_argsfile(
    tmp_path: Path,
) -> None:
    execution_root = tmp_path / "exact source"
    (execution_root / "server/src").mkdir(parents=True)
    (execution_root / "server/tests").mkdir(parents=True)
    args = SimpleNamespace(timeout=30, verbose=False)
    profile = SimpleNamespace(
        pytest_node_ids=("server/tests/test_contract.py::test_unicode",),
    )

    command = _pytest_command(
        args,
        profile,
        execution_root,
        tmp_path / "manifest.json",
        "0" * 64,
        tmp_path / "junit.xml",
        tmp_path / "pytest-temp",
        tmp_path / "pytest.args",
    )

    assert command[1:4] == ["-I", "-X", "utf8"]
    assert command[4].endswith("pytest_bootstrap.py")
    assert command[-1].startswith("@")
