"""Import Linter contracts (server/pyproject.toml [tool.importlinter]).

These tests are the mechanized, whole-codebase version of
test_connection_module_source_has_no_server_import (test_bridge_transport.py) —
they run the real `lint-imports` CLI in-process against this repo's actual
dependency graph, plus a synthetic fixture that proves the mechanism catches a
forbidden import regardless of whether the real repo happens to be clean.
"""
import json
import os
import sys
from pathlib import Path

from click.testing import CliRunner
from importlinter.cli import lint_imports_command

CONFIG = Path(__file__).resolve().parent.parent / "pyproject.toml"


def _write_mypkg_fixture(root: Path, bad_contents: str) -> Path:
    """Write a synthetic `mypkg` package (with a `forbidden` contract that
    bans mypkg.tools -> mypkg.server) under `root`, and return its config
    path. `bad_contents` is the source of mypkg/tools/bad.py — pass an
    import of mypkg.server to trigger the contract, or an unrelated
    statement to keep it clean."""
    pkg = root / "mypkg"
    (pkg / "tools").mkdir(parents=True)
    (pkg / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "server.py").write_text("VALUE = 1\n", encoding="utf-8")
    (pkg / "tools" / "__init__.py").write_text("", encoding="utf-8")
    (pkg / "tools" / "bad.py").write_text(bad_contents, encoding="utf-8")

    fixture_cfg = root / "pyproject.toml"
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
    return fixture_cfg


def _invoke_synthetic_lint(config_path: Path):
    """Run lint-imports against a synthetic `mypkg` fixture.

    `--no-cache` is mandatory here: import-linter's default cache directory
    (`.import_linter_cache`) is relative to CWD and grimp keys its cache
    files purely by root package name + mtime, not by absolute path. Every
    synthetic fixture in this module reuses the "mypkg" package name, so
    without `--no-cache` two fixtures created in different tmp_path
    directories (e.g. the violation and passing variants) would read/write
    the *same* cache files. A stale entry from one fixture can then be
    served to another whose freshly written file happens to report the same
    mtime as the cached one — observed on CI Windows runners, where mtime
    resolution and pytest-xdist worker concurrency make that collision much
    more likely than on macOS/Linux. See
    test_synthetic_cache_poisoning_is_ignored below for a deterministic
    reproduction of that failure mode.
    """
    return CliRunner().invoke(
        lint_imports_command, ["--config", str(config_path), "--no-logo", "--no-cache"]
    )


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
    fixture_cfg = _write_mypkg_fixture(tmp_path, "from mypkg import server\n")

    monkeypatch.syspath_prepend(str(tmp_path))
    result = _invoke_synthetic_lint(fixture_cfg)

    assert result.exit_code == 1, result.output
    assert "BROKEN" in result.output


def test_forbidden_contract_passes_without_violation(tmp_path, monkeypatch):
    """Positive control for test_forbidden_contract_flags_violation's fixture:
    with the bad import removed, the same contract shape must pass. Guards
    against a misconfigured root_package silently matching zero files and
    making the negative sentinel always pass regardless of the real import."""
    fixture_cfg = _write_mypkg_fixture(tmp_path, "VALUE = 2\n")

    monkeypatch.syspath_prepend(str(tmp_path))
    result = _invoke_synthetic_lint(fixture_cfg)

    assert result.exit_code == 0, result.output


def test_synthetic_cache_poisoning_is_ignored(tmp_path, monkeypatch):
    """Deterministic reproduction of the CI Windows flake this module used to
    hit (PR #96): a stale grimp cache entry for the module name
    "mypkg.tools.bad", written by one fixture's real (cache-enabled) lint
    run, must not leak into a different, clean fixture that reuses the same
    module name — even when the clean fixture's file is forced to report the
    exact mtime grimp cached for the stale one. That forced-mtime collision
    stands in for what CI Windows runners can hit naturally, where mtime
    resolution and pytest-xdist worker concurrency make two independently
    created files land on the same cached mtime.

    This pins down _invoke_synthetic_lint's `--no-cache` flag as the actual
    fix rather than a coincidence: swap the `_invoke_synthetic_lint` call
    below for a direct `CliRunner().invoke(lint_imports_command, ["--config",
    str(clean_cfg), "--no-logo"])` (i.e. drop `--no-cache`) and this test
    goes red, reading the poisoned cache and reporting a false BROKEN.
    """
    monkeypatch.chdir(tmp_path)

    # Establish a real (cache-enabled) violating run so the poisoned cache
    # file uses grimp's own hash/format, not a hand-crafted approximation.
    poison_root = tmp_path / "poison"
    poison_cfg = _write_mypkg_fixture(poison_root, "from mypkg import server\n")
    monkeypatch.syspath_prepend(str(poison_root))
    poison_result = CliRunner().invoke(
        lint_imports_command, ["--config", str(poison_cfg), "--no-logo"]
    )
    assert poison_result.exit_code == 1, poison_result.output
    sys.path.remove(str(poison_root))

    meta_file = tmp_path / ".import_linter_cache" / "mypkg.meta.json"
    stale_mtime = json.loads(meta_file.read_text(encoding="utf-8"))["mypkg.tools.bad"]

    # A fresh, clean fixture reusing the same module name "mypkg.tools.bad",
    # forced to collide with the mtime grimp cached for the violating one.
    clean_root = tmp_path / "clean"
    clean_cfg = _write_mypkg_fixture(clean_root, "VALUE = 2\n")
    clean_bad_py = clean_root / "mypkg" / "tools" / "bad.py"
    os.utime(clean_bad_py, (stale_mtime, stale_mtime))
    monkeypatch.syspath_prepend(str(clean_root))

    result = _invoke_synthetic_lint(clean_cfg)

    assert result.exit_code == 0, result.output
