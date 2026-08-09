"""Shared identities and limits for release package inspection."""

from __future__ import annotations

from dataclasses import dataclass

RELEASE_ARTIFACT_TYPES = (
    "python_wheel",
    "unity_editor_upm",
    "unity_reload_upm",
)
SUPPORTED_ARTIFACT_TYPES = frozenset(RELEASE_ARTIFACT_TYPES)
PUBLIC_STDIO_ARTIFACT_TYPES = ("python_wheel",)
UNITY_UPM_ARTIFACT_TYPES = ("unity_editor_upm", "unity_reload_upm")
UNITY_EDITOR_ARTIFACT_TYPES = RELEASE_ARTIFACT_TYPES
PACKAGE_SOURCE_PATHS = {
    "python_wheel": "server/pyproject.toml",
    "unity_editor_upm": "unity-plugin/package.json",
    "unity_reload_upm": "unity-plugin-reload/package.json",
}
PACKAGE_CONTENT_ROOTS = {
    "python_wheel": ("server/src/unity_mcp", "unity_mcp"),
    "unity_editor_upm": ("unity-plugin", ""),
    "unity_reload_upm": ("unity-plugin-reload", ""),
}
PACKAGE_NAMES = {
    "python_wheel": "unity-biome-mcp",
    "unity_editor_upm": "com.unity-biome-mcp.editor",
    "unity_reload_upm": "com.unity-biome-mcp.reload",
}
UPM_FILENAMES = {
    "unity_editor_upm": "com.unity-biome-mcp.editor-{version}.tgz",
    "unity_reload_upm": "com.unity-biome-mcp.reload-{version}.tgz",
}


class PackageArchiveError(ValueError):
    """Raised when staged package bytes are unsafe or semantically wrong."""


@dataclass(frozen=True, slots=True)
class PackageIdentity:
    package_name: str
    package_version: str
    content_sha256: str
    runtime_contract_sha256: str


@dataclass(frozen=True, slots=True)
class MemberFingerprint:
    path: str
    size: int
    sha256: str
