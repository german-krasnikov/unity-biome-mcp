"""PR-02 Track B: Import Linter contracts (server/pyproject.toml [tool.importlinter]).

These tests are the mechanized, whole-codebase version of
test_connection_module_source_has_no_server_import (test_bridge_transport.py) —
they run the real `lint-imports` CLI in-process against this repo's actual
dependency graph, plus a synthetic fixture that proves the mechanism catches a
forbidden import regardless of whether the real repo happens to be clean.
"""
from pathlib import Path

from click.testing import CliRunner
from importlinter.cli import lint_imports_command

CONFIG = Path(__file__).resolve().parent.parent / "pyproject.toml"


def test_repo_contracts_pass():
    """The real repo must satisfy every [tool.importlinter] contract — this is
    the gate that would have caught tools/connection.py's reverse import into
    unity_mcp.server before it shipped."""
    result = CliRunner().invoke(
        lint_imports_command, ["--config", str(CONFIG), "--no-logo"]
    )
    assert result.exit_code == 0, result.output


def test_forbidden_contract_flags_violation(tmp_path, monkeypatch):
    """A `forbidden` contract must actually fail (exit 1, BROKEN) when the
    source module imports the forbidden module — proves the gate is not
    vacuously green regardless of the real repo's current cleanliness."""
    pkg = tmp_path / "mypkg"
    (pkg / "tools").mkdir(parents=True)
    (pkg / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "server.py").write_text("VALUE = 1\n", encoding="utf-8")
    (pkg / "tools" / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "tools" / "bad.py").write_text("from mypkg import server\n", encoding="utf-8")

    fixture_cfg = tmp_path / "pyproject.toml"
    fixture_cfg.write_text(
        "[tool.importlinter]\n"
        'root_package = "mypkg"\n'
        "\n"
        "[[tool.importlinter.contracts]]\n"
        'name = "tools must not import server"\n'
        'type = "forbidden"\n'
        'source_modules = ["mypkg.tools"]\n'
        'forbidden_modules = ["mypkg.server"]\n',
        encoding="utf-8",
    )

    monkeypatch.syspath_prepend(str(tmp_path))
    result = CliRunner().invoke(
        lint_imports_command, ["--config", str(fixture_cfg), "--no-logo"]
    )

    assert result.exit_code == 1, result.output
    assert "BROKEN" in result.output


def test_forbidden_contract_passes_without_violation(tmp_path, monkeypatch):
    """Positive control for test_forbidden_contract_flags_violation's fixture:
    with the bad import removed, the same contract shape must pass. Guards
    against a misconfigured root_package silently matching zero files and
    making the negative sentinel always pass regardless of the real import."""
    pkg = tmp_path / "mypkg"
    (pkg / "tools").mkdir(parents=True)
    (pkg / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "server.py").write_text("VALUE = 1\n", encoding="utf-8")
    (pkg / "tools" / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "tools" / "bad.py").write_text("VALUE = 2\n", encoding="utf-8")

    fixture_cfg = tmp_path / "pyproject.toml"
    fixture_cfg.write_text(
        "[tool.importlinter]\n"
        'root_package = "mypkg"\n'
        "\n"
        "[[tool.importlinter.contracts]]\n"
        'name = "tools must not import server"\n'
        'type = "forbidden"\n'
        'source_modules = ["mypkg.tools"]\n'
        'forbidden_modules = ["mypkg.server"]\n',
        encoding="utf-8",
    )

    monkeypatch.syspath_prepend(str(tmp_path))
    result = CliRunner().invoke(
        lint_imports_command, ["--config", str(fixture_cfg), "--no-logo"]
    )

    assert result.exit_code == 0, result.output
