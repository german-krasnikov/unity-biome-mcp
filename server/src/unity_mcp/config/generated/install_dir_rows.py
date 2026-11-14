# Generated from dsl/data/installdir.data-contract, dsl/data/installdir.core-data
# source-sha256=9a45422d93fef0eba4c7e29a699ff8ad8ff6cff7bc2bd5572efeec7eb1a8a921
# Edit the source, then run biome generate.

from dataclasses import dataclass


@dataclass(frozen=True, slots=True, kw_only=True)
class InstallDir:
    key: str
    dir: str
    dirMac: str | None = None
    dirWin: str | None = None


INSTALL_DIR_ROWS: tuple[InstallDir, ...] = (
    InstallDir(key="claude-code", dir="~/.claude"),
    InstallDir(
        key="claude-desktop",
        dir="~/.config/Claude",
        dirMac="~/Library/Application Support/Claude",
        dirWin="{appdata}/Claude",
    ),
    InstallDir(key="cursor", dir="~/.cursor"),
    InstallDir(key="windsurf", dir="~/.codeium", dirWin="{appdata}/Codeium/windsurf"),
    InstallDir(
        key="vscode",
        dir="~/.config/Code",
        dirMac="~/Library/Application Support/Code",
        dirWin="{appdata}/Code",
    ),
    InstallDir(key="codex", dir="~/.codex"),
)
