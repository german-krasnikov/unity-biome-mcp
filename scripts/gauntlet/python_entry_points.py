"""Strict source and wheel entry-point projections."""


import configparser
import io

from gauntlet.package_contracts import PackageArchiveError


def source_entry_points(project: dict[str, object]) -> dict[str, dict[str, str]]:
    groups: dict[str, dict[str, str]] = {}
    for source_key, group in (("scripts", "console_scripts"), ("gui-scripts", "gui_scripts")):
        values = project.get(source_key, {})
        if not isinstance(values, dict):
            raise PackageArchiveError(f"project {source_key} table is invalid")
        if values:
            groups[group] = _string_mapping(values, f"project {source_key}")
    generic = project.get("entry-points", {})
    if not isinstance(generic, dict):
        raise PackageArchiveError("project entry-points table is invalid")
    for group, values in generic.items():
        if not isinstance(group, str) or not group or not isinstance(values, dict):
            raise PackageArchiveError("project entry-point group is invalid")
        if group in groups:
            raise PackageArchiveError("project entry-point group is duplicated")
        groups[group] = _string_mapping(values, "project entry points")
    return {group: dict(sorted(values.items())) for group, values in sorted(groups.items())}


def parse_entry_points(payload: bytes | None) -> dict[str, dict[str, str]]:
    if payload is None:
        return {}
    try:
        text = payload.decode("utf-8")
        parser = configparser.ConfigParser(interpolation=None, strict=True, delimiters=("=",))
        parser.optionxform = str
        parser.read_file(io.StringIO(text))
    except (UnicodeDecodeError, configparser.Error) as exc:
        raise PackageArchiveError("python wheel entry_points.txt is malformed") from exc
    if parser.defaults() or not parser.sections():
        raise PackageArchiveError("python wheel entry_points.txt has no entry-point groups")
    groups = {
        section: _string_mapping(dict(parser.items(section)), "wheel entry points")
        for section in sorted(parser.sections())
    }
    if any(not values for values in groups.values()):
        raise PackageArchiveError("python wheel entry_points.txt has an empty group")
    if payload != _canonical_payload(groups):
        raise PackageArchiveError("python wheel entry_points.txt bytes are not canonical")
    return groups


def _canonical_payload(groups: dict[str, dict[str, str]]) -> bytes:
    lines = []
    for group, values in sorted(groups.items()):
        lines.append(f"[{group}]")
        lines.extend(f"{key} = {value}" for key, value in sorted(values.items()))
    return ("\n".join(lines) + "\n").encode("utf-8")


def _string_mapping(value: dict[object, object], label: str) -> dict[str, str]:
    if any(
        not isinstance(key, str) or not key or not isinstance(item, str) or not item
        for key, item in value.items()
    ):
        raise PackageArchiveError(f"{label} must map non-empty strings to strings")
    return {str(key): str(item) for key, item in value.items()}
