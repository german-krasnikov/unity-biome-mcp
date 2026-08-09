"""Portable, collision-free archive path contracts."""

from __future__ import annotations

import unicodedata
from pathlib import PurePosixPath
from typing import TYPE_CHECKING

from gauntlet.package_contracts import PackageArchiveError

if TYPE_CHECKING:
    from collections.abc import Iterable

_WINDOWS_FORBIDDEN = frozenset('<>:"\\|?*')
_WINDOWS_RESERVED = frozenset(
    {"CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$"}
    | {f"COM{index}" for index in range(1, 10)}
    | {f"LPT{index}" for index in range(1, 10)}
    | {f"COM{index}" for index in "¹²³"}
    | {f"LPT{index}" for index in "¹²³"}
)


def validate_member_tree(
    entries: Iterable[tuple[str, bool]],
    *,
    label: str,
) -> None:
    """Reject paths that extract ambiguously on supported filesystems."""
    canonical: dict[str, bool] = {}
    files: set[str] = set()
    for raw_path, is_directory in entries:
        path = _normalized_path(raw_path, is_directory, label)
        collision_key = path.casefold()
        if collision_key in canonical:
            raise PackageArchiveError(f"{label} has a canonical path collision")
        canonical[collision_key] = is_directory
        if not is_directory:
            files.add(collision_key)
    for path in files:
        parts = path.split("/")
        for index in range(1, len(parts)):
            if "/".join(parts[:index]) in files:
                raise PackageArchiveError(f"{label} has a file/descendant ancestor collision")


def validate_logical_paths(paths: Iterable[str], *, label: str) -> None:
    validate_member_tree(((path, False) for path in paths), label=label)


def _normalized_path(raw_path: str, is_directory: bool, label: str) -> str:
    value = raw_path[:-1] if is_directory and raw_path.endswith("/") else raw_path
    path = PurePosixPath(value)
    if (
        not value
        or path.is_absolute()
        or path.as_posix() != value
        or "\\" in value
        or any(part in {"", ".", ".."} for part in path.parts)
    ):
        raise PackageArchiveError(f"{label} contains an unsafe member path")
    if unicodedata.normalize("NFC", value) != value:
        raise PackageArchiveError(f"{label} contains a non-canonical member path")
    if len(value.encode("utf-8")) > 4096:
        raise PackageArchiveError(f"{label} member path exceeds the portable length limit")
    for segment in path.parts:
        _validate_portable_segment(segment, label)
    return value


def _validate_portable_segment(segment: str, label: str) -> None:
    if (
        len(segment.encode("utf-8")) > 255
        or segment.endswith((" ", "."))
        or any(character in _WINDOWS_FORBIDDEN for character in segment)
        or any(ord(character) < 32 or ord(character) == 127 for character in segment)
        or segment.split(".", 1)[0].upper() in _WINDOWS_RESERVED
    ):
        raise PackageArchiveError(f"{label} contains a non-portable member path")
