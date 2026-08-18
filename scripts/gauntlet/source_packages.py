"""Parse package identities from the exact observed release source tree."""


import tomllib
from collections.abc import Mapping  # noqa: TC003
from dataclasses import dataclass

from gauntlet.json_io import JsonFileError, parse_json_object
from gauntlet.package_contracts import (
    PACKAGE_NAMES,
    PACKAGE_SOURCE_PATHS,
    PackageArchiveError,
)
from gauntlet.package_versions import is_strict_semver
from gauntlet.python_package_contract import source_python_contract
from gauntlet.receipts import content_hash


class SourcePackageError(ValueError):
    """Raised when observed package source identity is contradictory."""


@dataclass(frozen=True, slots=True)
class SourcePackageIdentity:
    package_name: str
    package_version: str
    runtime_contract_sha256: str


def parse_source_package_identities(
    payloads: Mapping[str, bytes],
) -> dict[str, SourcePackageIdentity]:
    expected_paths = set(PACKAGE_SOURCE_PATHS.values())
    if not expected_paths.issubset(payloads):
        raise SourcePackageError("observed source is missing package identity files")
    return {
        "python_wheel": _parse_python(payloads[PACKAGE_SOURCE_PATHS["python_wheel"]]),
        "unity_editor_upm": _parse_upm(
            payloads[PACKAGE_SOURCE_PATHS["unity_editor_upm"]],
            "unity_editor_upm",
        ),
        "unity_reload_upm": _parse_upm(
            payloads[PACKAGE_SOURCE_PATHS["unity_reload_upm"]],
            "unity_reload_upm",
        ),
    }


def _parse_python(payload: bytes) -> SourcePackageIdentity:
    try:
        data = tomllib.loads(payload.decode("utf-8"))
    except (UnicodeDecodeError, tomllib.TOMLDecodeError) as exc:
        raise SourcePackageError("server pyproject identity is malformed") from exc
    project = data.get("project")
    if not isinstance(project, dict):
        raise SourcePackageError("server pyproject has no project identity")
    identity = _validated_identity("python_wheel", project.get("name"), project.get("version"))
    try:
        runtime_contract = source_python_contract(project)
    except PackageArchiveError as exc:
        raise SourcePackageError(f"server pyproject package contract is invalid: {exc}") from exc
    return SourcePackageIdentity(identity.package_name, identity.package_version, runtime_contract)


def _parse_upm(payload: bytes, artifact_type: str) -> SourcePackageIdentity:
    try:
        data = parse_json_object(payload, source=PACKAGE_SOURCE_PATHS[artifact_type])
    except JsonFileError as exc:
        raise SourcePackageError(str(exc)) from exc
    identity = _validated_identity(artifact_type, data.get("name"), data.get("version"))
    return SourcePackageIdentity(
        identity.package_name,
        identity.package_version,
        content_hash(data),
    )


def _validated_identity(
    artifact_type: str,
    name: object,
    version: object,
) -> SourcePackageIdentity:
    if name != PACKAGE_NAMES[artifact_type]:
        raise SourcePackageError(f"{artifact_type} source package name is invalid")
    if not is_strict_semver(version):
        raise SourcePackageError(f"{artifact_type} source package version is invalid")
    return SourcePackageIdentity(name, version, "")
