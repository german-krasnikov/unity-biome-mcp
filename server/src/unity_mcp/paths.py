"""Canonical path helpers for ~/.unity-biome-mcp directory layout."""
import sys
from pathlib import Path

_NEW = ".unity-biome-mcp"
_OLD = ".unity-mcp"


def unity_mcp_dir() -> Path:
    return Path.home() / _NEW


def ports_dir() -> Path:
    return unity_mcp_dir() / "ports"


def migrate_data_dir() -> None:
    """One-shot: rename ~/.unity-mcp → ~/.unity-biome-mcp if old exists and new doesn't.

    Edge cases:
    - Both exist: skip (new dir wins, old stays; don't clobber).
    - Old is symlink: skip (os.rename on a symlink moves the link, not the target;
      better to leave it for manual handling).
    - Permission error: print to stderr, continue.
    """
    old = Path.home() / _OLD
    new = unity_mcp_dir()
    if not old.exists() or new.exists():
        return
    if old.is_symlink():
        print(
            f"unity-biome-mcp: skipping migration of {old} (symlink) → {new}",
            file=sys.stderr,
        )
        return
    try:
        old.rename(new)
    except OSError as e:
        print(
            f"unity-biome-mcp: could not migrate {old} → {new}: {e}",
            file=sys.stderr,
        )
