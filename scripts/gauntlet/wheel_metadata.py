"""Strict WHEEL metadata and filename-tag coherence checks."""

from __future__ import annotations

from email.parser import BytesParser
from itertools import product

from packaging.version import InvalidVersion, Version

from gauntlet.package_contracts import PackageArchiveError

_FIELDS = frozenset({"Wheel-Version", "Generator", "Root-Is-Purelib", "Tag"})


def validate_wheel_metadata(payload: bytes, filename: str) -> None:
    try:
        payload.decode("utf-8")
    except UnicodeDecodeError as exc:
        raise PackageArchiveError("python wheel WHEEL metadata is not UTF-8") from exc
    metadata = BytesParser().parsebytes(payload)
    names = [name for name, _ in metadata.items()]
    if any(name not in _FIELDS for name in names):
        raise PackageArchiveError("python wheel WHEEL contains an unsupported field")
    if metadata.get_payload():
        raise PackageArchiveError("python wheel WHEEL contains an unsupported body")
    versions = metadata.get_all("Wheel-Version", [])
    generators = metadata.get_all("Generator", [])
    purelib = metadata.get_all("Root-Is-Purelib", [])
    declared_tags = metadata.get_all("Tag", [])
    if versions != ["1.0"]:
        raise PackageArchiveError("python wheel WHEEL version is unsupported")
    _validate_generator(generators)
    if purelib not in (["true"], ["false"]):
        raise PackageArchiveError("python wheel WHEEL purelib flag is invalid")
    if not declared_tags or len(declared_tags) != len(set(declared_tags)):
        raise PackageArchiveError("python wheel WHEEL tags are missing or duplicated")
    expected_tags = _filename_tags(filename)
    if set(declared_tags) != expected_tags:
        raise PackageArchiveError("python wheel WHEEL tags contradict its filename")
    expected_purelib = all(tag.split("-")[1:] == ["none", "any"] for tag in expected_tags)
    if (purelib == ["true"]) != expected_purelib:
        raise PackageArchiveError("python wheel WHEEL purelib flag contradicts its tags")


def _validate_generator(values: list[str]) -> None:
    if len(values) > 1:
        raise PackageArchiveError("python wheel WHEEL Generator is duplicated")
    if not values:
        return
    parts = values[0].split(" ", 1)
    if len(parts) != 2 or parts[0] != "hatchling":
        raise PackageArchiveError("python wheel WHEEL Generator is unsupported")
    try:
        version = Version(parts[1])
    except InvalidVersion as exc:
        raise PackageArchiveError("python wheel WHEEL Generator version is invalid") from exc
    if str(version) != parts[1] or version.is_prerelease or version.is_devrelease:
        raise PackageArchiveError("python wheel WHEEL Generator version is not canonical")


def _filename_tags(filename: str) -> set[str]:
    stem = filename.removesuffix(".whl")
    parts = stem.split("-")
    if len(parts) not in {5, 6}:
        raise PackageArchiveError("python wheel filename has an invalid tag layout")
    python_tags, abi_tags, platform_tags = parts[-3:]
    components = (python_tags.split("."), abi_tags.split("."), platform_tags.split("."))
    if any(not values or any(not value for value in values) for values in components):
        raise PackageArchiveError("python wheel filename has empty tag components")
    return {"-".join(values) for values in product(*components)}
