"""Discover MCP server command and Unity port."""
import pathlib
import re
import shutil
import sys
from typing import Optional

from unity_mcp.paths import ports_dir as _ports_dir_canonical, iter_port_files as _iter_port_files_canonical
from unity_mcp.constants import DEFAULT_PORT


def _which(name: str) -> Optional[str]:
    """Wrapper around shutil.which for monkeypatching in tests."""
    return shutil.which(name)


def _ports_dir() -> pathlib.Path:
    """Return ~/.unity-biome-mcp/ports directory. Seam for testing."""
    return _ports_dir_canonical()


def _iter_port_files(pattern: str):
    """Iterate port files from primary + legacy dir. Seam for testing."""
    return _iter_port_files_canonical(pattern, _ports_dir())


def find_server_dir() -> Optional[pathlib.Path]:
    """Attempt to find local server installation directory.

    Returns None for uvx-managed installs (path not reliably discoverable).
    """
    return None  # uvx manages this; not reliably discoverable


def find_python() -> str:
    """Return the python/uvx executable for running the MCP server. Venv-first."""
    server_dir = pathlib.Path(__file__).parent.parent.parent.parent  # → server/
    venv_py = server_dir / ".venv" / ("Scripts" if sys.platform == "win32" else "bin") / "python"
    if venv_py.exists():
        return str(venv_py)
    if _which("uvx"):
        return "uvx"
    return sys.executable


GIT_INSTALL_URL = "git+https://github.com/german-krasnikov/unity-biome-mcp.git#subdirectory=server"

_REPO_BASE = "git+https://github.com/german-krasnikov/unity-biome-mcp.git"
_SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+$")


def server_git_url(ref: str | None = None) -> str:
    """Return uvx --from URL with optional @vX.Y.Z pin.

    ref=None → HEAD (default branch, GIT_INSTALL_URL unchanged).
    ref accepts 'X.Y.Z' or 'vX.Y.Z'. Raises ValueError on malformed ref.
    """
    if ref is None:
        return GIT_INSTALL_URL
    clean = ref.lstrip("v")
    if not _SEMVER_RE.match(clean):
        raise ValueError(f"Invalid version ref: {ref!r} — expected X.Y.Z")
    return f"{_REPO_BASE}@v{clean}#subdirectory=server"


def find_server_command() -> list[str]:
    """Return best command to start MCP server. Priority: venv > uvx > sys.executable."""
    exe = find_python()
    if exe == "uvx":
        return ["uvx", "--quiet", "--from", GIT_INSTALL_URL, "unity-biome-mcp"]
    return [exe, "-m", "unity_mcp.server"]


def find_port() -> int:
    """Discover Unity Biome MCP port from port files (new + legacy dir). Default 9500."""
    for port_file in _iter_port_files("*.port"):
        try:
            return int(port_file.read_text(encoding="utf-8").split("\n")[0])
        except (ValueError, OSError):
            continue
    return DEFAULT_PORT


def build_server_entry(port: int = 0) -> dict:
    """Build MCP server entry dict for config files."""
    cmd = find_server_command()
    entry: dict = {"command": cmd[0], "args": cmd[1:]}
    if port:
        entry["env"] = {"UNITY_MCP_PORT": str(port)}
    return entry
