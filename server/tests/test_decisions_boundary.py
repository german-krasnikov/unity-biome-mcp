"""The negative control of the leaf contract of unity_mcp.decisions (step 7.2, P9).

server/pyproject.toml says that unity_mcp.decisions imports nothing of unity_mcp outside itself. A contract that was misspelled, or
whose wildcard matched nothing, would stay green for ever. So the contract is read from the real config, its module names are moved
to a synthetic package, and the synthetic package breaks it in the two ways a decision module could: an import of another module
of the package, and a chain through a module of its own generated folder.
"""
import json
import tomllib
from pathlib import Path

from click.testing import CliRunner
from importlinter.cli import lint_imports_command

CONFIG = Path(__file__).resolve().parent.parent / "pyproject.toml"
NAME = "decisions stay a leaf (nothing of unity_mcp outside the package)"
ROOT = "leafpkg"  # not "mypkg": test_import_boundaries.py has fixtures of that name


def _real_contract() -> dict:
    contracts = tomllib.loads(CONFIG.read_text(encoding="utf-8"))["tool"]["importlinter"]["contracts"]
    (contract,) = [c for c in contracts if c["name"] == NAME]
    return contract


def _toml(contract: dict) -> str:
    """The contract of the real config for the synthetic package: unity_mcp -> leafpkg in every string and list."""
    moved = {key: json.dumps(value).replace("unity_mcp", ROOT) for key, value in contract.items()}
    return (f'[tool.importlinter]\nroot_package = "{ROOT}"\n\n[[tool.importlinter.contracts]]\n'
            + "".join(f"{key} = {value}\n" for key, value in moved.items()))


def _lint(tmp_path: Path, monkeypatch, files: dict[str, str]):
    """Write the synthetic package (`files`: path below it -> source) and its config; run the real lint-imports command on it."""
    for name, source in {"__init__.py": "", "server.py": "VALUE = 1\n", "decisions/__init__.py": "",
                         "decisions/generated/__init__.py": "", **files}.items():
        path = tmp_path / ROOT / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(source, encoding="utf-8")
    config = tmp_path / "pyproject.toml"
    config.write_text(_toml(_real_contract()), encoding="utf-8")
    monkeypatch.syspath_prepend(str(tmp_path))
    return CliRunner().invoke(lint_imports_command, ["--config", str(config), "--no-logo", "--no-cache"])


def test_the_contract_is_in_the_config_once_and_forbids_the_package_from_importing_the_rest():
    contract = _real_contract()
    assert contract["type"] == "forbidden"
    assert contract["source_modules"] == ["unity_mcp.decisions"]
    assert contract["forbidden_modules"] == ["unity_mcp.*"]


def test_a_decision_module_may_import_the_standard_library_and_its_own_package(tmp_path, monkeypatch):
    result = _lint(tmp_path, monkeypatch, {
        "decisions/generated/m_logic.py": "import math\n",
        "decisions/facts.py": f"import json\nfrom {ROOT}.decisions.generated import m_logic\n",
    })
    assert result.exit_code == 0, result.output


def test_a_decision_module_that_imports_another_module_of_the_package_breaks_the_contract(tmp_path, monkeypatch):
    result = _lint(tmp_path, monkeypatch, {"decisions/facts.py": f"from {ROOT} import server\n"})
    assert result.exit_code == 1, result.output
    assert "BROKEN" in result.output


def test_a_chain_through_a_generated_module_breaks_the_contract(tmp_path, monkeypatch):
    result = _lint(tmp_path, monkeypatch, {
        "decisions/generated/m_logic.py": f"from {ROOT} import server\n",
        "decisions/facts.py": f"from {ROOT}.decisions.generated import m_logic\n",
    })
    assert result.exit_code == 1, result.output
    assert "BROKEN" in result.output
