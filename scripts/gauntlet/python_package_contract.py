"""Canonical, fail-closed Python source-to-wheel install contract."""


import re
from typing import TYPE_CHECKING

from packaging.requirements import InvalidRequirement, Requirement
from packaging.specifiers import InvalidSpecifier, SpecifierSet
from packaging.utils import canonicalize_name

from gauntlet.package_contracts import PackageArchiveError
from gauntlet.python_entry_points import parse_entry_points, source_entry_points
from gauntlet.receipts import content_hash

if TYPE_CHECKING:
    from email.message import Message

_SOURCE_KEYS = frozenset(
    {
        "name",
        "version",
        "description",
        "license",
        "authors",
        "maintainers",
        "classifiers",
        "requires-python",
        "dependencies",
        "optional-dependencies",
        "urls",
        "scripts",
        "gui-scripts",
        "entry-points",
    }
)
_SINGLE_HEADERS = frozenset(
    {
        "Name",
        "Version",
        "Summary",
        "Author",
        "Author-email",
        "Maintainer",
        "Maintainer-email",
        "License",
        "Requires-Python",
    }
)
_MULTI_HEADERS = frozenset(
    {"Project-URL", "Classifier", "Requires-Dist", "Provides-Extra"}
)
_ALLOWED_HEADERS = _SINGLE_HEADERS | _MULTI_HEADERS | {"Metadata-Version"}
_SUPPORTED_METADATA_VERSIONS = frozenset({"2.1", "2.2", "2.3", "2.4"})
_EXTRA_MARKER = re.compile(r"\bextra\s*(==|!=)\s*(['\"])([A-Za-z0-9._-]+)\2")


def source_python_contract(project: dict[str, object]) -> str:
    """Hash every supported wheel metadata value derived from ``[project]``."""
    unsupported = set(project) - _SOURCE_KEYS
    if unsupported:
        raise PackageArchiveError("project contains unsupported Python metadata fields")
    metadata = _source_metadata(project)
    requirements, extras = _source_requirements(project)
    if requirements:
        metadata["Requires-Dist"] = requirements
    if extras:
        metadata["Provides-Extra"] = extras
    return _contract_hash(metadata, source_entry_points(project))


def wheel_python_contract(metadata: Message, entry_points: bytes | None) -> str:
    """Validate and hash the complete supported core-metadata projection."""
    fields = _wheel_metadata(metadata)
    return _contract_hash(fields, parse_entry_points(entry_points))


def _source_metadata(project: dict[str, object]) -> dict[str, list[str]]:
    result: dict[str, list[str]] = {}
    _copy_optional_string(project, result, "name", "Name")
    _copy_optional_string(project, result, "version", "Version")
    _copy_optional_string(project, result, "description", "Summary")
    _copy_optional_string(project, result, "requires-python", "Requires-Python")
    if "requires-python" in project:
        result["Requires-Python"] = [_normalize_specifier(result["Requires-Python"][0])]
    _source_license(project, result)
    _source_people(project, result, "authors", "Author")
    _source_people(project, result, "maintainers", "Maintainer")
    classifiers = _require_string_list(project.get("classifiers", []), "project classifiers")
    if classifiers:
        result["Classifier"] = _unique(classifiers, "project classifiers")
    urls = project.get("urls", {})
    if not isinstance(urls, dict):
        raise PackageArchiveError("project urls table is invalid")
    if urls:
        mapping = _string_mapping(urls, "project urls")
        result["Project-URL"] = [f"{key}, {value}" for key, value in mapping.items()]
    return result


def _source_requirements(project: dict[str, object]) -> tuple[list[str], list[str]]:
    dependencies = _require_string_list(project.get("dependencies", []), "project dependencies")
    requirements = [_normalize_requirement(value) for value in dependencies]
    optional = project.get("optional-dependencies", {})
    if not isinstance(optional, dict):
        raise PackageArchiveError("project optional dependencies are invalid")
    extras = []
    for raw_extra, values in optional.items():
        if not isinstance(raw_extra, str) or not raw_extra:
            raise PackageArchiveError("project optional dependency name is invalid")
        extra = str(canonicalize_name(raw_extra))
        if extra in extras:
            raise PackageArchiveError("project optional dependency extras are duplicated")
        extras.append(extra)
        requirements.extend(
            _normalize_requirement(_add_extra_marker(value, extra))
            for value in _require_string_list(values, "project optional dependencies")
        )
    return _unique(requirements, "project requirements"), sorted(extras)


def _wheel_metadata(metadata: Message) -> dict[str, list[str]]:
    names = [name for name, _ in metadata.items()]
    if any(name not in _ALLOWED_HEADERS for name in names):
        raise PackageArchiveError("python wheel has unsupported METADATA headers")
    versions = metadata.get_all("Metadata-Version", [])
    if len(versions) != 1 or versions[0] not in _SUPPORTED_METADATA_VERSIONS:
        raise PackageArchiveError("python wheel Metadata-Version is invalid")
    payload = metadata.get_payload()
    if not isinstance(payload, str) or payload:
        raise PackageArchiveError("python wheel METADATA description body is unsupported")
    result: dict[str, list[str]] = {}
    for header in sorted(_SINGLE_HEADERS):
        values = metadata.get_all(header, [])
        if len(values) > 1:
            raise PackageArchiveError(f"python wheel has duplicate {header} metadata")
        if values:
            result[header] = values
    for header in sorted(_MULTI_HEADERS):
        values = metadata.get_all(header, [])
        if len(values) != len(set(values)):
            raise PackageArchiveError(f"python wheel has duplicate {header} metadata")
        if values:
            result[header] = values
    if "Requires-Python" in result:
        result["Requires-Python"] = [_normalize_specifier(result["Requires-Python"][0])]
    if "Requires-Dist" in result:
        result["Requires-Dist"] = [
            _normalize_requirement(value) for value in result["Requires-Dist"]
        ]
    if "Provides-Extra" in result:
        canonical = [str(canonicalize_name(value)) for value in result["Provides-Extra"]]
        result["Provides-Extra"] = _unique(canonical, "wheel Provides-Extra")
    return result


def _contract_hash(
    metadata: dict[str, list[str]],
    entry_points: dict[str, dict[str, str]],
) -> str:
    canonical = {
        header: sorted(_unique(values, f"Python package {header}"))
        for header, values in sorted(metadata.items())
    }
    return content_hash(
        {
            "domain": "unity-biome-mcp.python-install-contract.v2",
            "metadata": canonical,
            "description_body": "",
            "entry_points": entry_points,
        }
    )


def _source_license(project: dict[str, object], result: dict[str, list[str]]) -> None:
    if "license" not in project:
        return
    license_value = project["license"]
    if not isinstance(license_value, dict) or set(license_value) != {"text"}:
        raise PackageArchiveError("project license metadata is unsupported")
    text = license_value["text"]
    if not isinstance(text, str) or not text:
        raise PackageArchiveError("project license text is invalid")
    result["License"] = [text]


def _source_people(
    project: dict[str, object],
    result: dict[str, list[str]],
    key: str,
    header: str,
) -> None:
    people = project.get(key, [])
    if not isinstance(people, list):
        raise PackageArchiveError(f"project {key} metadata is invalid")
    names = []
    addresses = []
    for person in people:
        if not isinstance(person, dict) or not person or set(person) - {"name", "email"}:
            raise PackageArchiveError(f"project {key} metadata is invalid")
        name, email = person.get("name"), person.get("email")
        if name is not None and (not isinstance(name, str) or not name):
            raise PackageArchiveError(f"project {key} name is invalid")
        if email is not None and (not isinstance(email, str) or not email):
            raise PackageArchiveError(f"project {key} email is invalid")
        if email:
            addresses.append(f"{name} <{email}>" if name else email)
        elif name:
            names.append(name)
    if names:
        result[header] = [", ".join(names)]
    if addresses:
        result[f"{header}-email"] = [", ".join(addresses)]


def _normalize_requirement(value: str) -> str:
    try:
        requirement = Requirement(value)
    except InvalidRequirement as exc:
        raise PackageArchiveError("Python package requirement is invalid") from exc
    name = canonicalize_name(requirement.name)
    normalized_extras = sorted({str(canonicalize_name(extra)) for extra in requirement.extras})
    extras = f"[{','.join(normalized_extras)}]" if normalized_extras else ""
    target = f" @ {requirement.url}" if requirement.url else str(requirement.specifier)
    marker = str(requirement.marker) if requirement.marker is not None else ""
    marker = _EXTRA_MARKER.sub(_canonical_extra_marker, marker)
    return f"{name}{extras}{target}{f'; {marker}' if marker else ''}"


def _canonical_extra_marker(match: re.Match[str]) -> str:
    return f'extra {match.group(1)} "{canonicalize_name(match.group(3))}"'


def _normalize_specifier(value: str) -> str:
    try:
        return str(SpecifierSet(value))
    except InvalidSpecifier as exc:
        raise PackageArchiveError("Python package Requires-Python is invalid") from exc


def _add_extra_marker(value: str, extra: str) -> str:
    if ";" not in value:
        return f"{value}; extra == '{extra}'"
    requirement, marker = value.split(";", 1)
    return f"{requirement}; ({marker.strip()}) and extra == '{extra}'"


def _copy_optional_string(
    project: dict[str, object],
    result: dict[str, list[str]],
    key: str,
    header: str,
) -> None:
    if key not in project:
        return
    value = project[key]
    if not isinstance(value, str) or not value:
        raise PackageArchiveError(f"project {key} metadata is invalid")
    result[header] = [value]


def _require_string_list(value: object, label: str) -> list[str]:
    if not isinstance(value, list) or any(not isinstance(item, str) or not item for item in value):
        raise PackageArchiveError(f"{label} must be a list of strings")
    return value


def _string_mapping(value: dict[object, object], label: str) -> dict[str, str]:
    if any(
        not isinstance(key, str) or not key or not isinstance(item, str) or not item
        for key, item in value.items()
    ):
        raise PackageArchiveError(f"{label} must map non-empty strings to strings")
    return {str(key): str(item) for key, item in value.items()}


def _unique(values: list[str], label: str) -> list[str]:
    if len(values) != len(set(values)):
        raise PackageArchiveError(f"{label} contains duplicate values")
    return values
