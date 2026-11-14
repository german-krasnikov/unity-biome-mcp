"""scripts/dsl.sh `check`: the TOOLCHAIN pin guard must fail the build when the
pinned commit does not match the biome-dsl checkout's actual HEAD. Before this
guard, scripts/dsl.sh relied on the Node side printing a non-fatal
TOOLCHAIN_PIN_STALE warning and exiting 0 -- the bug this guards against
(dsl/biome.project pinned a sha that does not exist in the checkout at all).

Uses a throwaway copy of the real dsl.sh plus a fake biome-dsl checkout (a
local git repo) under tmp_path -- no network, no Node. PATH is narrowed to a
shim holding only `dirname`, `grep` and `git` (what the guard itself needs),
so a call that reaches past the guard to `node ...` fails deterministically
with "command not found" regardless of what is installed on the host. That
failure is the proof the guard let the call through rather than blocking it
for the pin: it is how a matching-pin run is told apart from a
mismatching-pin run without actually running Node.
"""
import os
import shutil
import subprocess
from pathlib import Path

import pytest

REPO_ROOT = Path(__file__).resolve().parents[2]
SCRIPT = REPO_ROOT / "scripts" / "dsl.sh"
TOOLCHAIN_SHA_LENGTH = 40

pytestmark = pytest.mark.skipif(
    os.name != "posix",
    reason="dsl.sh is a POSIX shell script; Windows `bash` is the WSL launcher",
)


def _git(cwd: Path, *args: str) -> subprocess.CompletedProcess:
    result = subprocess.run(
        ["git", "-c", "commit.gpgsign=false", *args],
        cwd=cwd, capture_output=True, text=True, encoding="utf-8", check=False,
    )
    assert result.returncode == 0, f"git {args} failed: {result.stderr}"
    return result


def _make_toolchain_checkout(path: Path) -> str:
    """A throwaway git repo standing in for BIOME_DSL_ROOT; returns its HEAD sha."""
    path.mkdir()
    _git(path, "init", "--quiet", "--initial-branch=main")
    _git(path, "-c", "user.name=t", "-c", "user.email=t@t", "commit", "--quiet", "--allow-empty", "-m", "x")
    return _git(path, "rev-parse", "HEAD").stdout.strip()


def _shim_path_without_node(shim_dir: Path) -> str:
    """A PATH with only the external tools the guard needs -- no `node` -- so a call that
    reaches it fails with "command not found" no matter what the host has installed."""
    shim_dir.mkdir()
    for tool in ("dirname", "grep", "git"):
        real = shutil.which(tool)
        assert real, f"{tool} not found on the test host PATH"
        (shim_dir / tool).symlink_to(real)
    return str(shim_dir)


def _make_work_tree(root: Path, toolchain_pin: str) -> Path:
    """A copy of the real scripts/dsl.sh plus a minimal dsl/biome.project pinning `toolchain_pin`."""
    (root / "scripts").mkdir(parents=True)
    shutil.copy(SCRIPT, root / "scripts" / "dsl.sh")
    (root / "scripts" / "dsl.sh").chmod(0o755)
    (root / "dsl").mkdir()
    (root / "dsl" / "biome.project").write_text(f"# fixture\nTOOLCHAIN {toolchain_pin}\n", encoding="utf-8")
    return root


def _run_check(work: Path, biome_dsl_root: Path, path_env: str) -> subprocess.CompletedProcess:
    bash = shutil.which("bash")
    assert bash, "bash not found on the test host PATH"
    env = {"BIOME_DSL_ROOT": str(biome_dsl_root), "PATH": path_env}
    return subprocess.run(
        [bash, str(work / "scripts" / "dsl.sh"), "check"],
        cwd=work, env=env, capture_output=True, text=True, encoding="utf-8", check=False,
    )


def test_check_fails_at_the_guard_when_the_pin_does_not_match_the_checkout_head(tmp_path):
    toolchain_root = tmp_path / "toolchain"
    _make_toolchain_checkout(toolchain_root)
    mismatched_pin = "f" * TOOLCHAIN_SHA_LENGTH
    work = _make_work_tree(tmp_path / "work", mismatched_pin)
    shim = _shim_path_without_node(tmp_path / "shim")

    result = _run_check(work, toolchain_root, shim)

    assert result.returncode != 0
    assert "TOOLCHAIN_PIN_MISMATCH" in result.stderr, result.stderr
    assert "command not found" not in result.stderr, (
        "guard did not stop the script before the Node call:\n" + result.stderr
    )


@pytest.mark.parametrize(
    "biome_project_body",
    [
        pytest.param("# fixture\nCONSTRUCTS constructs\n", id="no_toolchain_line"),
        pytest.param(
            f"# fixture\nTOOLCHAIN {'a' * TOOLCHAIN_SHA_LENGTH}\nTOOLCHAIN {'b' * TOOLCHAIN_SHA_LENGTH}\n",
            id="two_toolchain_lines",
        ),
    ],
)
def test_check_fails_with_a_clear_diagnostic_when_the_project_has_zero_or_two_toolchain_lines(tmp_path, biome_project_body):
    toolchain_root = tmp_path / "toolchain"
    _make_toolchain_checkout(toolchain_root)
    work = _make_work_tree(tmp_path / "work", "f" * TOOLCHAIN_SHA_LENGTH)
    (work / "dsl" / "biome.project").write_text(biome_project_body, encoding="utf-8")
    shim = _shim_path_without_node(tmp_path / "shim")

    result = _run_check(work, toolchain_root, shim)

    assert result.returncode != 0
    assert "TOOLCHAIN_PIN_MISSING" in result.stderr, result.stderr
    assert "TOOLCHAIN_PIN_MISMATCH" not in result.stderr, result.stderr
    assert "command not found" not in result.stderr, (
        "guard did not stop the script before the Node call:\n" + result.stderr
    )


def test_check_fails_with_a_clear_diagnostic_when_biome_dsl_root_is_not_a_git_checkout(tmp_path):
    not_a_checkout = tmp_path / "toolchain"
    not_a_checkout.mkdir()
    work = _make_work_tree(tmp_path / "work", "f" * TOOLCHAIN_SHA_LENGTH)
    shim = _shim_path_without_node(tmp_path / "shim")

    result = _run_check(work, not_a_checkout, shim)

    assert result.returncode != 0
    assert "TOOLCHAIN_ROOT_NOT_GIT" in result.stderr, result.stderr
    assert "TOOLCHAIN_PIN_MISMATCH" not in result.stderr, result.stderr


def test_check_passes_the_guard_and_reaches_node_when_the_pin_matches_the_checkout_head(tmp_path):
    toolchain_root = tmp_path / "toolchain"
    toolchain_head = _make_toolchain_checkout(toolchain_root)
    work = _make_work_tree(tmp_path / "work", toolchain_head)
    shim = _shim_path_without_node(tmp_path / "shim")

    result = _run_check(work, toolchain_root, shim)

    assert result.returncode != 0
    assert "TOOLCHAIN_PIN_MISMATCH" not in result.stderr, (
        "guard blocked a matching pin:\n" + result.stderr
    )
    assert "command not found" in result.stderr, result.stderr
