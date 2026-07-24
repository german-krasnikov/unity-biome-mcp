"""Canonical path helpers for ~/.unity-biome-mcp directory layout."""
import sys
from pathlib import Path
from typing import Iterator

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


def iter_port_files(pattern: str, primary_dir: Path | None = None) -> Iterator[Path]:
    """Yield port files from primary + legacy ~/.unity-mcp/ports. Dedup by filename.

    primary_dir defaults to ports_dir(). New dir wins on duplicate filenames.
    """
    seen: set[str] = set()
    for d in (primary_dir if primary_dir is not None else ports_dir(), Path.home() / _OLD / "ports"):
        if not d.exists():
            continue
        for f in d.glob(pattern):
            if f.name not in seen:
                seen.add(f.name)
                yield f
