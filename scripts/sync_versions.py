#!/usr/bin/env python3
"""Sync generated version copies from the canonical server/pyproject.toml.

Usage:
    python sync_versions.py 1.4.0  # bump canonical version and sync copies
    python sync_versions.py --sync  # sync copies from the canonical version
    python sync_versions.py --check # verify without writing
"""
import json
import os
import re
import sys
from pathlib import Path

SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+$")
CANONICAL_ARTIFACT = "pyproject.toml"


def _validate(version: str) -> None:
    if not version or not SEMVER_RE.match(version):
        print(f"Invalid semver: {version!r}", file=sys.stderr)
        sys.exit(1)


def _update_pyproject(path: Path, version: str) -> str:
    text = path.read_text(encoding="utf-8")
    new_text, count = re.subn(r'^version = "[^"]*"', f'version = "{version}"', text, count=1, flags=re.MULTILINE)
    if count == 0:
        raise ValueError(f"Pattern not found in {path}")
    return new_text


def _update_package_json(path: Path, version: str) -> str:
    text = path.read_text(encoding="utf-8")
    new_text, count = re.subn(r'"version":\s*"[^"]*"', f'"version": "{version}"', text, count=1)
    if count == 0:
        raise ValueError(f"Pattern not found in {path}")
    return new_text


def _update_uv_lock(path: Path, version: str) -> str:
    text = path.read_text(encoding="utf-8")
    new_text, count = re.subn(
        r'(\[\[package\]\]\nname = "unity-biome-mcp"\nversion = ")[^"]*(")',
        rf"\g<1>{version}\g<2>",
        text,
        count=1,
    )
    if count == 0:
        raise ValueError(f"unity-biome-mcp package pattern not found in {path}")
    return new_text


def _update_version_py(path: Path, version: str) -> str:
    return f'__version__ = "{version}"\n'


def _update_meta_json(path: Path, version: str) -> str:
    data = json.loads(path.read_text(encoding="utf-8"))
    data["server_version"] = version
    data["plugin_version"] = version
    return json.dumps(data, indent=2, ensure_ascii=False) + "\n"


def _update_release_policy(path: Path, version: str) -> str:
    data = json.loads(path.read_text(encoding="utf-8"))
    data["activation_product_version"] = version
    return json.dumps(data, indent=2, ensure_ascii=False) + "\n"


def _update_plugin_version_cs(path: Path, version: str) -> str:
    text = path.read_text(encoding="utf-8")
    new_text, count = re.subn(
        r'(internal static string PluginVersion => ")[^"]*(")',
        rf'\g<1>{version}\g<2>',
        text, count=1,
    )
    if count == 0:
        raise ValueError(f"PluginVersion pattern not found in {path}")
    return new_text


def _atomic_write(path: Path, content: str) -> None:
    _atomic_write_bytes(path, content.encode("utf-8"))


def _atomic_write_bytes(path: Path, content: bytes) -> None:
    tmp = path.with_suffix(".tmp")
    try:
        tmp.write_bytes(content)
        os.replace(str(tmp), str(path))
    finally:
        if tmp.exists():
            tmp.unlink()


def _artifact_paths(root: Path) -> dict:
    return {
        "pyproject.toml": root / "server" / "pyproject.toml",
        "uv.lock": root / "server" / "uv.lock",
        "package.json": root / "unity-plugin" / "package.json",
        "__version__.py": root / "server" / "src" / "unity_mcp" / "__version__.py",
        "_meta.json": root / "docs" / "assets" / "_meta.json",
        "MCPServer.cs": root / "unity-plugin" / "Editor" / "MCPServer.cs",
        "release-policy.json": root / "scripts" / "gauntlet" / "release-policy.json",
    }


def _read_version(name: str, path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    patterns = {
        "pyproject.toml": r'^version = "([^"]*)"',
        "uv.lock": (
            r'\[\[package\]\]\nname = "unity-biome-mcp"\nversion = "([^"]*)"'
        ),
        "package.json": r'"version":\s*"([^"]*)"',
        "__version__.py": r'__version__ = "([^"]*)"',
        "MCPServer.cs": r'internal static string PluginVersion => "([^"]*)"',
        "release-policy.json": r'"activation_product_version":\s*"([^"]*)"',
    }
    m = re.search(patterns[name], text, re.MULTILINE)
    return m.group(1) if m else "?"


def _read_all_versions(paths: dict[str, Path]) -> dict[str, str]:
    versions = {
        name: _read_version(name, path)
        for name, path in paths.items()
        if name != "_meta.json"
    }
    meta = json.loads(paths["_meta.json"].read_text(encoding="utf-8"))
    versions["_meta.json.server_version"] = meta.get("server_version", "?")
    versions["_meta.json.plugin_version"] = meta.get("plugin_version", "?")
    return versions


def _check(root: Path) -> None:
    """Verify every generated copy against the canonical version, without writes."""
    paths = _artifact_paths(root)
    for path in paths.values():
        if not path.exists():
            print(f"Missing: {path}", file=sys.stderr)
            sys.exit(1)
    versions = _read_all_versions(paths)
    canonical = versions[CANONICAL_ARTIFACT]
    invalid = {
        name: version
        for name, version in versions.items()
        if not SEMVER_RE.fullmatch(version)
    }
    if invalid or any(version != canonical for version in versions.values()):
        print("version mismatch:", file=sys.stderr)
        for name, v in sorted(versions.items()):
            print(f"  {name}: {v}", file=sys.stderr)
        sys.exit(1)
    print(f"versions in sync: {canonical}")
    sys.exit(0)


def _sync(root: Path, version: str, *, update_canonical: bool) -> None:
    files = {
        "uv.lock": (root / "server" / "uv.lock", _update_uv_lock),
        "package.json": (root / "unity-plugin" / "package.json", _update_package_json),
        "__version__.py": (root / "server" / "src" / "unity_mcp" / "__version__.py", _update_version_py),
        "_meta.json": (root / "docs" / "assets" / "_meta.json", _update_meta_json),
        "MCPServer.cs": (root / "unity-plugin" / "Editor" / "MCPServer.cs", _update_plugin_version_cs),
        "release-policy.json": (root / "scripts" / "gauntlet" / "release-policy.json", _update_release_policy),
        # Canonical source is replaced last so a failed bump never advertises a
        # version whose generated copies were not written.
        "pyproject.toml": (root / "server" / "pyproject.toml", _update_pyproject),
    }
    if not update_canonical:
        files.pop(CANONICAL_ARTIFACT)

    # Collect all updates first — fail fast before writing anything
    updates: list[tuple[str, Path, str, bytes]] = []
    for name, (path, updater) in files.items():
        if not path.exists():
            print(f"Missing: {path}", file=sys.stderr)
            sys.exit(1)
        try:
            content = updater(path, version)
            updates.append((name, path, content, path.read_bytes()))
        except Exception as e:
            print(f"Failed to prepare {name}: {e}", file=sys.stderr)
            sys.exit(1)

    # Replace the complete set or restore every file byte-for-byte.
    written: list[tuple[str, Path, bytes]] = []
    try:
        for name, path, content, original in updates:
            _atomic_write(path, content)
            written.append((name, path, original))
    except Exception as error:
        rollback_errors = []
        for written_name, written_path, original in reversed(written):
            try:
                _atomic_write_bytes(written_path, original)
            except Exception as rollback_error:  # noqa: PERF203
                rollback_errors.append(
                    f"{written_name}: {rollback_error}"
                )
        print(f"Failed to write {name}: {error}", file=sys.stderr)
        if rollback_errors:
            print(
                "Rollback failed: " + "; ".join(rollback_errors),
                file=sys.stderr,
            )
        sys.exit(1)

    for name, _, _, _ in updates:
        print(f"Updated {name} → {version}")


def _parse_root(args: list[str], usage: str) -> Path:
    if not args:
        return Path(__file__).parents[1]
    if len(args) == 2 and args[0] == "--root":
        return Path(args[1])
    print(usage, file=sys.stderr)
    sys.exit(1)


def main() -> None:
    args = sys.argv[1:]

    if args and args[0] in {"--check", "--sync"}:
        mode = args[0]
        usage = f"Usage: sync_versions.py {mode} [--root <path>]"
        root = _parse_root(args[1:], usage)
        if mode == "--check":
            _check(root)
            return

        canonical_path = _artifact_paths(root)[CANONICAL_ARTIFACT]
        if not canonical_path.exists():
            print(f"Missing: {canonical_path}", file=sys.stderr)
            sys.exit(1)
        version = _read_version(CANONICAL_ARTIFACT, canonical_path)
        _validate(version)
        _sync(root, version, update_canonical=False)
        return

    if not args or len(args) > 3:
        print("Usage: sync_versions.py <version> [--root <path>]", file=sys.stderr)
        sys.exit(1)

    version = args[0]
    _validate(version)
    root = _parse_root(args[1:], "Usage: sync_versions.py <version> [--root <path>]")
    _sync(root, version, update_canonical=True)


if __name__ == "__main__":
    main()
