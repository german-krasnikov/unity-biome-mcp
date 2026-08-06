"""Tests for bootstrap install scripts (syntax + content validation)."""
import os
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

_REPO_ROOT = Path(__file__).parents[2]
SH = str(_REPO_ROOT / "install" / "bootstrap.sh")
PS1 = str(_REPO_ROOT / "install" / "bootstrap.ps1")


def _find_bash() -> str:
    """Return path to bash — Git Bash on Windows via fallback chain, plain 'bash' elsewhere."""
    if sys.platform != "win32":
        return "bash"

    def _try(p: Path) -> str | None:
        return str(p) if p.exists() else None

    # 1. GIT_INSTALL_ROOT env (some CI installers set this)
    root = os.environ.get("GIT_INSTALL_ROOT")
    if root and (r := _try(Path(root) / "bin" / "bash.exe")):
        return r

    # 2. Windows Registry — most reliable, written by every Git installer
    try:
        import winreg
        for hive in (winreg.HKEY_LOCAL_MACHINE, winreg.HKEY_CURRENT_USER):
            try:
                with winreg.OpenKey(hive, r"SOFTWARE\GitForWindows") as key:
                    install_path, _ = winreg.QueryValueEx(key, "InstallPath")
                    if r := _try(Path(install_path) / "bin" / "bash.exe"):
                        return r
            except OSError:  # noqa: PERF203
                pass
    except ImportError:
        pass

    # 3. Derive from git in PATH
    git_exe = shutil.which("git")
    if git_exe:
        p = Path(git_exe)
        # bin/git.exe layout
        if r := _try(p.parent / "bash.exe"):
            return r
        # cmd/git.exe → ../bin/bash.exe (GitHub Actions layout)
        if r := _try(p.parent.parent / "bin" / "bash.exe"):
            return r

    # 4. Standard install locations
    for env_var in ("ProgramFiles", "ProgramFiles(x86)"):
        base = os.environ.get(env_var)
        if base and (r := _try(Path(base) / "Git" / "bin" / "bash.exe")):
            return r
    local = os.environ.get("LOCALAPPDATA")
    if local and (r := _try(Path(local) / "Programs" / "Git" / "bin" / "bash.exe")):
        return r

    # 5. shutil.which — skip WSL bash hiding in System32
    found = shutil.which("bash")
    if found and "System32" not in found:
        return found

    pytest.skip("bash not found on this Windows environment")


def _read(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


# --- bootstrap.sh ---

def test_bootstrap_sh_syntax():
    bash = _find_bash()
    result = subprocess.run(
        [bash, "-n", SH],
        capture_output=True, text=True, encoding="utf-8"
    )
    assert result.returncode == 0, result.stderr


@pytest.mark.skipif(sys.platform == "win32", reason="os.access X_OK unreliable on Windows")
def test_bootstrap_sh_is_executable():
    assert os.access(SH, os.X_OK), f"{SH} must be executable"


def test_bootstrap_sh_has_error_handling():
    assert "set -euo pipefail" in _read(SH)


def test_bootstrap_sh_checks_uv():
    assert "command -v uv" in _read(SH)


def test_bootstrap_sh_installs_uv():
    assert "astral.sh/uv/install.sh" in _read(SH)


def test_bootstrap_sh_clones_repo():
    assert "git clone" in _read(SH)


def test_bootstrap_sh_runs_install_py():
    assert "install.py" in _read(SH)


def test_bootstrap_sh_supports_custom_dir():
    assert "UNITY_MCP_DIR" in _read(SH)


def test_bootstrap_sh_handles_existing_install():
    content = _read(SH)
    assert "git" in content and "pull" in content


def test_bootstrap_sh_macos_quarantine():
    assert "quarantine" in _read(SH)


def test_bootstrap_sh_no_unquoted_variables():
    """Key path variables must be quoted to handle spaces."""
    content = _read(SH)
    # INSTALL_DIR should always appear quoted
    assert '"$INSTALL_DIR"' in content, "INSTALL_DIR must be quoted"


# --- bootstrap.ps1 ---

def test_bootstrap_ps1_syntax():
    if subprocess.run(["which", "pwsh"], capture_output=True, encoding="utf-8").returncode != 0:
        pytest.skip("pwsh not installed")
    result = subprocess.run(
        ["pwsh", "-NoProfile", "-NonInteractive", "-Command",
         f"$null = [System.Management.Automation.Language.Parser]::ParseFile('{PS1}', [ref]$null, [ref]$null); exit 0"],
        capture_output=True, text=True, encoding="utf-8"
    )
    assert result.returncode == 0, result.stderr


def test_bootstrap_ps1_checks_uv():
    assert "Get-Command uv" in _read(PS1)


def test_bootstrap_ps1_installs_uv():
    assert "astral.sh/uv/install.ps1" in _read(PS1)


def test_bootstrap_ps1_clones_repo():
    assert "git clone" in _read(PS1)


def test_bootstrap_ps1_runs_install_py():
    assert "install.py" in _read(PS1)


def test_bootstrap_ps1_supports_custom_dir():
    assert "UNITY_MCP_DIR" in _read(PS1)


def test_bootstrap_ps1_long_paths():
    assert "core.longpaths" in _read(PS1)


def test_bootstrap_ps1_execution_policy_note():
    assert "ExecutionPolicy" in _read(PS1)


def test_bootstrap_ps1_quoted_install_dir():
    """$installDir must be quoted in git commands to handle paths with spaces."""
    content = _read(PS1)
    assert '"$installDir"' in content, "$installDir must be quoted"
