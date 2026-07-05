#!/usr/bin/env python3
"""Sync version across pyproject.toml, package.json, __version__.py, _meta.json, MCPServer.cs.

Usage: python sync_versions.py 0.38.0
"""
import json
import os
import re
import sys
from pathlib import Path

SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+$")


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


def _update_version_py(path: Path, version: str) -> str:
    return f'__version__ = "{version}"\n'


def _update_meta_json(path: Path, version: str) -> str:
    data = json.loads(path.read_text(encoding="utf-8"))
    data["server_version"] = version
    data["plugin_version"] = version
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
    tmp = path.with_suffix(".tmp")
    tmp.write_text(content, encoding="utf-8")
    os.replace(str(tmp), str(path))


def _artifact_paths(root: Path) -> dict:
    return {
        "pyproject.toml": root / "server" / "pyproject.toml",
        "package.json": root / "unity-plugin" / "package.json",
        "__version__.py": root / "server" / "src" / "unity_mcp" / "__version__.py",
        "_meta.json": root / "docs" / "assets" / "_meta.json",
        "MCPServer.cs": root / "unity-plugin" / "Editor" / "MCPServer.cs",
    }


def _read_version(name: str, path: Path) -> str:
    text = path.read_text(encoding="utf-8")
    if name == "_meta.json":
        return json.loads(text).get("server_version", "?")
    patterns = {
        "pyproject.toml": r'^version = "([^"]*)"',
        "package.json": r'"version":\s*"([^"]*)"',
        "__version__.py": r'__version__ = "([^"]*)"',
        "MCPServer.cs": r'internal static string PluginVersion => "([^"]*)"',
    }
    m = re.search(patterns[name], text, re.MULTILINE)
    return m.group(1) if m else "?"


def _check(root: Path) -> None:
    """Verify-only mode: compare version across all 5 artifacts, no writes."""
    paths = _artifact_paths(root)
    for path in paths.values():
        if not path.exists():
            print(f"Missing: {path}", file=sys.stderr)
            sys.exit(1)
    versions = {name: _read_version(name, path) for name, path in paths.items()}
    if len(set(versions.values())) > 1:
        print("version mismatch:", file=sys.stderr)
        for name, v in sorted(versions.items()):
            print(f"  {name}: {v}", file=sys.stderr)
        sys.exit(1)
    print(f"versions in sync: {next(iter(versions.values()))}")
    sys.exit(0)


def main() -> None:
    args = sys.argv[1:]

    if args and args[0] == "--check":
        rest = args[1:]
        if not rest:
            root = Path(__file__).parents[1]
        elif len(rest) == 2 and rest[0] == "--root":
            root = Path(rest[1])
        else:
            print("Usage: sync_versions.py --check [--root <path>]", file=sys.stderr)
            sys.exit(1)
        _check(root)
        return

    if not args or len(args) > 3:
        print("Usage: sync_versions.py <version> [--root <path>]", file=sys.stderr)
        sys.exit(1)

    version = args[0]
    _validate(version)

    if len(args) == 3 and args[1] == "--root":
        root = Path(args[2])
    else:
        root = Path(__file__).parents[1]

    files = {
        "pyproject.toml": (root / "server" / "pyproject.toml", _update_pyproject),
        "package.json": (root / "unity-plugin" / "package.json", _update_package_json),
        "__version__.py": (root / "server" / "src" / "unity_mcp" / "__version__.py", _update_version_py),
        "_meta.json": (root / "docs" / "assets" / "_meta.json", _update_meta_json),
        "MCPServer.cs": (root / "unity-plugin" / "Editor" / "MCPServer.cs", _update_plugin_version_cs),
    }

    # Collect all updates first — fail fast before writing anything
    updates: list[tuple[str, Path, str]] = []
    for name, (path, updater) in files.items():
        if not path.exists():
            print(f"Missing: {path}", file=sys.stderr)
            sys.exit(1)
        try:
            content = updater(path, version)
            updates.append((name, path, content))
        except Exception as e:
            print(f"Failed to prepare {name}: {e}", file=sys.stderr)
            sys.exit(1)

    # All good — write atomically
    for name, path, content in updates:
        try:
            _atomic_write(path, content)
            print(f"Updated {name} → {version}")
        except Exception as e:
            print(f"Failed to write {name}: {e}", file=sys.stderr)
            sys.exit(1)


if __name__ == "__main__":
    main()
