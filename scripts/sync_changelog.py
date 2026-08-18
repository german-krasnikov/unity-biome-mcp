#!/usr/bin/env python3
"""Synchronize the Unity package changelog from the canonical root changelog."""


import argparse
import os
import shutil
import sys
import tempfile
from pathlib import Path


def changelog_paths(repo_root: Path) -> tuple[Path, Path]:
    return repo_root / "CHANGELOG.md", repo_root / "unity-plugin" / "CHANGELOG.md"


def changelogs_match(repo_root: Path) -> bool:
    source, mirror = changelog_paths(repo_root)
    return source.read_bytes() == mirror.read_bytes()


def sync_changelog(repo_root: Path) -> None:
    source, mirror = changelog_paths(repo_root)
    content = source.read_bytes()
    mirror.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(
        prefix=f".{mirror.name}.",
        dir=mirror.parent,
    )
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        shutil.copymode(source, temporary)
        os.replace(temporary, mirror)
    finally:
        if temporary.exists():
            temporary.unlink()


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Synchronize unity-plugin/CHANGELOG.md from CHANGELOG.md."
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Verify the mirror without writing.",
    )
    parser.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[1],
        help=argparse.SUPPRESS,
    )
    args = parser.parse_args()

    source, mirror = changelog_paths(args.repo_root)
    try:
        if args.check:
            if not changelogs_match(args.repo_root):
                print(
                    f"changelog mismatch: {mirror} must match {source}",
                    file=sys.stderr,
                )
                return 1
            print("changelogs in sync")
            return 0
        sync_changelog(args.repo_root)
        print(f"updated {mirror} from {source}")
        return 0
    except OSError as error:
        print(f"changelog sync failed: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
