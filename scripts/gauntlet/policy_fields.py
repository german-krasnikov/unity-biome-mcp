"""Primitive and cross-field validators for release policy JSON."""


import re
from pathlib import PurePosixPath

from gauntlet.package_contracts import (
    PUBLIC_STDIO_ARTIFACT_TYPES,
    UNITY_EDITOR_ARTIFACT_TYPES,
)

_ID_PATTERN = re.compile(r"^[a-z0-9][a-z0-9._-]*$")


class PolicyError(ValueError):
    """Raised when a release policy is ambiguous or incomplete."""


def require_exact_keys(value: dict[str, object], expected: set[str], label: str) -> None:
    if set(value) != expected:
        missing = sorted(expected - set(value))
        extra = sorted(set(value) - expected)
        raise PolicyError(f"{label} mismatch: missing={missing}, extra={extra}")


def require_text(value: object, label: str) -> str:
    if not isinstance(value, str) or not value:
        raise PolicyError(f"{label} must be a non-empty string")
    return value


def require_digest(value: object, label: str) -> str:
    text = require_text(value, label).lower()
    if len(text) != 64 or any(character not in "0123456789abcdef" for character in text):
        raise PolicyError(f"{label} must contain 64 hexadecimal characters")
    return text


def require_repo_path(value: object, label: str) -> str:
    text = require_text(value, label)
    path = PurePosixPath(text)
    if (
        path.is_absolute()
        or path.as_posix() != text
        or "\\" in text
        or any(part in {"", ".", ".."} for part in path.parts)
    ):
        raise PolicyError(f"{label} must be a normalized repository-relative path")
    return path.as_posix()


def require_id(value: object, label: str) -> str:
    text = require_text(value, label)
    if not _ID_PATTERN.fullmatch(text):
        raise PolicyError(f"{label} contains unsupported characters")
    return text


def require_unique_ids(
    value: object,
    label: str,
    *,
    allow_empty: bool = False,
) -> tuple[str, ...]:
    if not isinstance(value, list) or (not value and not allow_empty):
        raise PolicyError(f"{label} must be a non-empty list")
    identifiers = tuple(sorted(require_id(item, label) for item in value))
    if len(set(identifiers)) != len(identifiers):
        raise PolicyError(f"{label} contains a duplicate")
    return identifiers


def require_scenario_ids(value: object) -> tuple[str, ...]:
    if not isinstance(value, list) or not value:
        raise PolicyError("scenario ids must be a non-empty list")
    scenarios: list[str] = []
    for item in value:
        text = require_text(item, "scenario id")
        if len(text) > 512 or text != text.strip() or not text.isprintable():
            raise PolicyError("scenario id contains unsupported characters")
        scenarios.append(text)
    if len(set(scenarios)) != len(scenarios):
        raise PolicyError("scenario ids contain a duplicate")
    return tuple(sorted(scenarios))


def require_pytest_node_id(value: object) -> str:
    text = require_text(value, "pytest node id")
    if len(text) > 1024 or text != text.strip() or not text.isprintable():
        raise PolicyError("pytest node id contains unsupported characters")
    try:
        file_part, selector = text.split("::", 1)
    except ValueError as exc:
        raise PolicyError("pytest node id must contain a test selector") from exc
    normalized_file = require_repo_path(file_part, "pytest node path")
    if not normalized_file.startswith("server/tests/") or not normalized_file.endswith(".py"):
        raise PolicyError("pytest node path must select a file below server/tests")
    if not selector or selector.startswith("-") or any(character.isspace() for character in selector):
        raise PolicyError("pytest node selector contains unsupported characters")
    return f"{normalized_file}::{selector}"


def require_non_negative_int(value: object, label: str) -> int:
    if isinstance(value, bool) or not isinstance(value, int) or value < 0:
        raise PolicyError(f"{label} must be a non-negative integer")
    return value


def require_positive_int(value: object, label: str) -> int:
    integer = require_non_negative_int(value, label)
    if integer == 0:
        raise PolicyError(f"{label} must be greater than zero")
    return integer


def validate_driver_contract(
    driver: str,
    unity_version: str | None,
    plugin_scope: str,
    workers: int,
    consumed_artifacts: tuple[str, ...],
) -> None:
    if driver == "public_stdio":
        if unity_version is not None or plugin_scope != "none" or workers != 0:
            raise PolicyError("public stdio profile cannot declare Unity workers or plugins")
        if consumed_artifacts != PUBLIC_STDIO_ARTIFACT_TYPES:
            raise PolicyError("public stdio profile must consume only the Python wheel")
        return
    if unity_version is None or plugin_scope != "exact" or workers < 1:
        raise PolicyError("Unity Editor profile requires exact plugin and at least one worker")
    if consumed_artifacts != UNITY_EDITOR_ARTIFACT_TYPES:
        raise PolicyError("Unity Editor profile must consume wheel and both UPM artifacts")
