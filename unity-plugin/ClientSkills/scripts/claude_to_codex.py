#!/usr/bin/env python3
"""
Import project agents and skills from Claude format into Codex format.

Sources:
  .claude/agents/*.md
  .claude/skills/*.md
  .claude/skills/*/SKILL.md

Targets:
  .codex/agents/*.toml
  .agents/skills/*/SKILL.md

The converter keeps instruction bodies intact except for Codex-local path
references, and normalizes the wrapper metadata Codex expects.
"""

from __future__ import annotations

import argparse
import filecmp
import hashlib
import json
import os
import re
import shutil
import sys
import tempfile
try:
    import tomllib
except ModuleNotFoundError:
    tomllib = None  # Python <3.11: skip TOML validation
from dataclasses import dataclass
from pathlib import Path
from typing import Union


CODEX_AGENT_KEYS = {
    "name",
    "description",
    "developer_instructions",
    "nickname_candidates",
    "sandbox_mode",
}
SKILL_REF_NAME = r"[A-Za-z0-9_\-\[\]]+"
SKILL_FILE_RE = re.compile(rf"\.(?:claude|agents)/skills/({SKILL_REF_NAME})(?:\.md|/SKILL\.md)")
CLAUDE_SKILL_DIR_RE = re.compile(rf"\.claude/skills/({SKILL_REF_NAME})/")
SAFE_ID_RE = re.compile(r"^[a-z0-9]+(?:-[a-z0-9]+)*$")
FrontmatterValue = Union[str, list]
IGNORED_RESOURCE_NAMES = {".DS_Store", ".meta"}
IGNORED_RESOURCE_DIRS = {"__pycache__"}
IGNORED_RESOURCE_SUFFIXES = {".pyc"}
MANIFEST_RELATIVE_PATH = Path(".codex") / ".claude-to-codex-manifest.json"
MANAGED_TARGET_PREFIXES = (Path(".codex") / "agents", Path(".agents") / "skills")
SHA256_RE = re.compile(r"^[0-9a-f]{64}$")
LEGACY_GENERATED_BLOBS = {
    ".codex/agents/playmode-tester.toml": frozenset({
        "0df0c022cad1a0d06cc0e9726080a4331c3c99e4",
        "88a4515a4cc138229d3de9804cbf59b24c04fa45",
    }),
    ".codex/agents/unity-editor-developer.toml": frozenset({
        "84b9687dfad788b40f4b8386a4193b7b7dda93ee",
        "c91672efe66b713e7925d982ce60615223d260e0",
    }),
    ".codex/agents/unity-test-reviewer.toml": frozenset({
        "08c48d396f7848be5f8f57aaa1c493c6c6dfb3e7",
    }),
    ".agents/skills/csharp-unity/SKILL.md": frozenset({
        "88a168602fcfb769c77c48bad11f82f9dd0f9088",
        "c29971e791d91962d47fe27c4931255e1e80030f",
    }),
    ".agents/skills/playmode-verification/SKILL.md": frozenset({"af23c23c31456f60f99fc575768c5b113476358d"}),
    ".agents/skills/playtest-dsl/SKILL.md": frozenset({"d92be3d7165f4f43381d78b913f2754675cce4fd"}),
    ".agents/skills/testing-tdd/SKILL.md": frozenset({"ca94dc4664a7194ac51882126c5bda3da9475969"}),
    ".agents/skills/token-optimization/SKILL.md": frozenset({
        "bf5afe20e522ce7d5343277c87525c160739a1e6",
        "6a6de60db65c7259c1efdd9943b6c9b78240f090",
    }),
    ".agents/skills/unity-animation/SKILL.md": frozenset({"201bb68a42dc33526e9158b6fcc1dc882137aae3"}),
    ".agents/skills/unity-animator/SKILL.md": frozenset({"275fe372c937040f6e3d9a12570530132c525746"}),
    ".agents/skills/unity-assets/SKILL.md": frozenset({"0eb1b6b86200e6268043ad2e0a4aa8a37da85845"}),
    ".agents/skills/unity-biome-mcp-reference/SKILL.md": frozenset({"9790dc3505e1e3dfb6aff97dcbd9e8ba32afdfcc"}),
    ".agents/skills/unity-mcp-reference/SKILL.md": frozenset({"9d9c4ac7af1bf0516d6e14a11b777e95c1e6b507"}),
    ".agents/skills/unity-code-intel/SKILL.md": frozenset({
        "926cfb0b96ba5e4d0e571c8fbccc7acc0c1eb8b9",
        "9bcc520314efd1fdc92522e3c49ade4a8d5d731c",
        "4a5dea458dba5231898e506a76092e8193c28bdb",
    }),
    ".agents/skills/unity-components/SKILL.md": frozenset({"f85f73219de69b5b7530db2f3e76bc7b7532ebae"}),
    ".agents/skills/unity-debugging/SKILL.md": frozenset({"115d02ea1fc0e2632fc0ea5746b9b9866e220e19"}),
    ".agents/skills/unity-efficiency/SKILL.md": frozenset({"d93ecaa95615ac01da1598743a4bd78b0d112afa"}),
    ".agents/skills/unity-hierarchy/SKILL.md": frozenset({"c2ceab23471e6e7449614aa74eacb200170eac45"}),
    ".agents/skills/unity-intent/SKILL.md": frozenset({
        "7ddb4fc45cbd208ab20bad706fcfa825c0d5a366",
        "c8d26d3a817326ede91147f071f7b9e063806447",
    }),
    ".agents/skills/unity-particles/SKILL.md": frozenset({"98db93c8ec3fbd8c87643e119bf9b434e16ad9ba"}),
    ".agents/skills/unity-performance/SKILL.md": frozenset({"97c075f8cc5c68a645d89a2f9cf16a13408ca78d"}),
    ".agents/skills/unity-physics/SKILL.md": frozenset({
        "474e4ba58a5a2b312df9acdc72cceeffa0351043",
        "548b28d3daed1b7bfc112695b7247bab69de0564",
    }),
    ".agents/skills/unity-scene-ui/SKILL.md": frozenset({"b52dc3c0fe401ad75422b5ccf77476b6c9bb5bfe"}),
    ".agents/skills/unity-session/SKILL.md": frozenset({
        "b6c529cf02ee0889ff96aac270048f3e9926aff5",
        "3274e7f1365cb8612791fb151d26ddd0605921a3",
    }),
    ".agents/skills/unity-shaders/SKILL.md": frozenset({"a9c864f1551d2642b02f78df303342bcbf266896"}),
    ".agents/skills/unity-testing/SKILL.md": frozenset({
        "af3ab25e11f7d3624ae9d5d16dbdfb6dc3c12057",
        "6edd52030796ea3c8b7cbe4f34cd33006cc15213",
    }),
    ".agents/skills/unity-testing-verification/references/test-authoring.md": frozenset({
        "a06497388792403ae2e34cef65324140ebad446c",
        "f6ed9e999cba3f8aefa653fa2013fc40ce53474a",
    }),
    ".agents/skills/unity-timeline/SKILL.md": frozenset({"fe29a910dd5813089e41c4d96a55c8ee9aea6f2b"}),
    ".agents/skills/unity-ui-authoring/SKILL.md": frozenset({
        "ace0723100751e5eec95e58544dd48a560f6ced8",
    }),
}


@dataclass(frozen=True)
class GeneratedFile:
    path: Path
    content: str


@dataclass(frozen=True)
class SyncPlan:
    desired: dict[Path, bytes]
    changed: tuple[Path, ...]
    unchanged: tuple[Path, ...]
    remove: tuple[Path, ...]


def split_frontmatter(text: str) -> tuple[dict[str, FrontmatterValue], str]:
    lines = text.splitlines(keepends=True)
    if not lines or lines[0].strip() != "---":
        return {}, text

    for index in range(1, len(lines)):
        if lines[index].strip() == "---":
            frontmatter = "".join(lines[1:index])
            body = "".join(lines[index + 1 :])
            return parse_simple_frontmatter(frontmatter), body

    return {}, text


def _unquote(value: str) -> str:
    value = value.strip()
    if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
        return value[1:-1]
    return value


def parse_simple_frontmatter(frontmatter: str) -> dict[str, FrontmatterValue]:
    """Parse dependency-free scalar and string-list YAML frontmatter."""
    data: dict[str, FrontmatterValue] = {}
    active_list: str | None = None

    for raw_line in frontmatter.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#"):
            continue

        if active_list and line.startswith("- "):
            values = data[active_list]
            assert isinstance(values, list)
            values.append(_unquote(line[2:]))
            continue

        active_list = None
        if ":" not in line:
            continue

        key, value = line.split(":", 1)
        key = key.strip()
        value = value.strip()
        if not value:
            data[key] = []
            active_list = key
        elif value.startswith("[") and value.endswith("]"):
            inner = value[1:-1].strip()
            data[key] = [] if not inner else [_unquote(item) for item in inner.split(",")]
        else:
            data[key] = _unquote(value)

    return data


def toml_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def toml_literal(value: str) -> str:
    if "'''" not in value:
        return "'''" + value + "'''"
    return toml_string(value)


def yaml_string(value: str) -> str:
    return json.dumps(value, ensure_ascii=False)


def normalize_codex_paths(body: str) -> str:
    """Map Claude source skill paths to active Codex skill paths in generated files."""

    def replace_skill_file(match: re.Match[str]) -> str:
        skill_name = match.group(1)
        return f".agents/skills/{skill_name}/SKILL.md"

    def replace_skill_dir(match: re.Match[str]) -> str:
        skill_name = match.group(1)
        return f".agents/skills/{skill_name}/"

    body = SKILL_FILE_RE.sub(replace_skill_file, body)
    body = CLAUDE_SKILL_DIR_RE.sub(replace_skill_dir, body)
    return body.replace(".claude/skills/", ".agents/skills/")


def existing_skill_description(skill_dir: Path) -> str | None:
    skill_file = skill_dir / "SKILL.md"
    if not skill_file.exists():
        return None

    frontmatter, _ = split_frontmatter(skill_file.read_text(encoding="utf-8"))
    description = frontmatter.get("description")
    return description if isinstance(description, str) else None


def frontmatter_string(frontmatter: dict[str, FrontmatterValue], key: str) -> str | None:
    value = frontmatter.get(key)
    return value if isinstance(value, str) else None


def frontmatter_list(frontmatter: dict[str, FrontmatterValue], key: str) -> list[str]:
    value = frontmatter.get(key)
    if isinstance(value, list):
        return [item for item in value if item]
    if isinstance(value, str) and value:
        return [value]
    return []


def frontmatter_string_list(frontmatter: dict[str, FrontmatterValue], key: str) -> list[str]:
    values = frontmatter_list(frontmatter, key)
    if len(values) == 1 and "," in values[0]:
        return [item.strip() for item in values[0].split(",") if item.strip()]
    return values


def validate_identifier(identifier: str, source: Path) -> None:
    if not SAFE_ID_RE.fullmatch(identifier):
        raise ValueError(
            f"{source}: invalid identifier {identifier!r}; "
            "use lowercase letters, digits, and single hyphens"
        )


def discover_skill_sources(claude_skills_dir: Path) -> list[tuple[str, Path]]:
    sources: list[tuple[str, Path]] = []
    seen: dict[str, Path] = {}
    if not claude_skills_dir.exists():
        return sources

    for source in sorted(claude_skills_dir.iterdir()):
        if source.is_dir() and (source / "SKILL.md").exists():
            name, source_file = source.name, source / "SKILL.md"
        elif source.is_file() and source.suffix == ".md":
            name, source_file = source.stem, source
        else:
            continue

        validate_identifier(name, source_file)
        if name in seen:
            raise ValueError(f"Duplicate skill id {name!r}: {seen[name]} and {source_file}")
        seen[name] = source_file
        sources.append((name, source_file))

    return sources


def build_agent_files_from(source_dir: Path, target_dir: Path) -> list[GeneratedFile]:
    files: list[GeneratedFile] = []
    seen: dict[str, Path] = {}

    for source in sorted(source_dir.glob("*.md")):
        frontmatter, body = split_frontmatter(source.read_text(encoding="utf-8"))
        name = frontmatter_string(frontmatter, "name") or source.stem
        validate_identifier(name, source)
        if name in seen:
            raise ValueError(f"Duplicate agent id {name!r}: {seen[name]} and {source}")
        seen[name] = source
        description = frontmatter_string(frontmatter, "description") or f"Project custom agent: {name}."
        body = normalize_codex_paths(body)
        required_skills = frontmatter_string_list(frontmatter, "skills")
        for skill_name in required_skills:
            validate_identifier(skill_name, source)
        if required_skills:
            skill_lines = "\n".join(
                f"- `.agents/skills/{skill_name}/SKILL.md`" for skill_name in required_skills
            )
            body = (
                "## Required skills\n\n"
                "Before acting, read and follow these repository skills:\n"
                f"{skill_lines}\n\n"
                f"{body.lstrip()}"
            )
        lines = [
            f"name = {toml_string(name)}",
            f"description = {toml_string(description)}",
        ]
        disallowed_tools = set(frontmatter_string_list(frontmatter, "disallowedTools"))
        if {"Write", "Edit", "NotebookEdit"} & disallowed_tools:
            lines.append('sandbox_mode = "read-only"')
        lines.append(f"developer_instructions = {toml_literal(body)}")
        content = "\n".join(lines) + "\n"
        files.append(GeneratedFile(target_dir / f"{name}.toml", content))

    return files


def build_agent_files(repo_root: Path) -> list[GeneratedFile]:
    return build_agent_files_from(
        repo_root / ".claude" / "agents",
        repo_root / ".codex" / "agents",
    )


def build_skill_files(repo_root: Path) -> list[GeneratedFile]:
    source_dir = repo_root / ".claude" / "skills"
    target_dir = repo_root / ".agents" / "skills"
    files: list[GeneratedFile] = []

    for name, source_file in discover_skill_sources(source_dir):
        frontmatter, body = split_frontmatter(source_file.read_text(encoding="utf-8"))
        description = (
            frontmatter_string(frontmatter, "description")
            or existing_skill_description(target_dir / name)
            or name.replace("-", " ").capitalize()
        )
        body = normalize_codex_paths(body)
        content = (
            "---\n"
            f"name: {name}\n"
            f"description: {yaml_string(description)}\n"
            "---\n\n"
            f"{body.lstrip()}"
        )
        files.append(GeneratedFile(target_dir / name / "SKILL.md", content))

    return files


def is_ignored_resource(path: Path, skill_root: Path) -> bool:
    relative = path.relative_to(skill_root)
    return (
        any(part in IGNORED_RESOURCE_DIRS for part in relative.parts)
        or path.name in IGNORED_RESOURCE_NAMES
        or path.name.endswith(".meta")
        or path.suffix in IGNORED_RESOURCE_SUFFIXES
    )


def discover_skill_resources_from(source_dir: Path, target_dir: Path) -> list[tuple[Path, Path]]:
    resources: list[tuple[Path, Path]] = []

    if not source_dir.exists():
        return resources

    for skill_source in sorted(source_dir.iterdir()):
        if not skill_source.is_dir() or not (skill_source / "SKILL.md").exists():
            continue

        skill_target = target_dir / skill_source.name
        for resource in sorted(skill_source.rglob("*")):
            if resource.is_dir() or resource.name == "SKILL.md":
                continue
            if is_ignored_resource(resource, skill_source):
                continue

            resources.append((resource, skill_target / resource.relative_to(skill_source)))

    return resources


def discover_skill_resources(repo_root: Path) -> list[tuple[Path, Path]]:
    return discover_skill_resources_from(
        repo_root / ".claude" / "skills",
        repo_root / ".agents" / "skills",
    )


def file_hash(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git_blob_hash(path: Path) -> str:
    content = path.read_bytes()
    return hashlib.sha1(f"blob {len(content)}\0".encode() + content).hexdigest()


def content_hash(content: str) -> str:
    return hashlib.sha256(content.encode("utf-8")).hexdigest()


def expected_hashes(repo_root: Path, files: list[GeneratedFile], resources: list[tuple[Path, Path]]) -> dict[str, str]:
    expected = {
        generated.path.relative_to(repo_root).as_posix(): content_hash(generated.content)
        for generated in files
    }
    expected.update(
        {
            target.relative_to(repo_root).as_posix(): file_hash(source)
            for source, target in resources
        }
    )
    return dict(sorted(expected.items()))


def load_manifest(repo_root: Path) -> dict[str, str]:
    path = repo_root / MANIFEST_RELATIVE_PATH
    if not path.exists():
        return {}
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as exc:
        raise ValueError(f"{path}: invalid ownership manifest: {exc}") from exc

    if not isinstance(data, dict) or data.get("version") != 1:
        raise ValueError(f"{path}: unsupported ownership manifest")
    files = data.get("files", {})
    if not isinstance(files, dict):
        raise ValueError(f"{path}: manifest files must be an object")

    validated: dict[str, str] = {}
    for relative_text, digest in files.items():
        if not isinstance(relative_text, str) or not isinstance(digest, str):
            raise ValueError(f"{path}: manifest entries must map paths to SHA-256 strings")
        relative = Path(relative_text)
        if not is_managed_target(relative) or not SHA256_RE.fullmatch(digest):
            raise ValueError(f"{path}: invalid managed entry {relative_text!r}")
        validated[relative.as_posix()] = digest
    return validated


def manifest_bytes(hashes: dict[str, str]) -> bytes:
    payload = {"version": 1, "files": dict(sorted(hashes.items()))}
    return (json.dumps(payload, indent=2) + "\n").encode("utf-8")


def write_manifest(repo_root: Path, hashes: dict[str, str]) -> None:
    path = assert_safe_path(
        repo_root,
        repo_root / MANIFEST_RELATIVE_PATH,
        managed=False,
    )
    _atomic_write(path, manifest_bytes(hashes))


def is_managed_target(relative: Path) -> bool:
    if relative.is_absolute() or ".." in relative.parts:
        return False
    return any(relative == prefix or prefix in relative.parents for prefix in MANAGED_TARGET_PREFIXES)


def desired_files(
    files: list[GeneratedFile],
    resources: list[tuple[Path, Path]],
) -> dict[Path, bytes]:
    desired = {generated.path: generated.content.encode("utf-8") for generated in files}
    for source, target in resources:
        if source.is_symlink():
            raise ValueError(f"Skill resource symlinks are not supported: {source}")
        if target in desired:
            raise ValueError(f"Duplicate generated target: {target}")
        desired[target] = source.read_bytes()
    return desired


def assert_safe_path(repo_root: Path, target: Path, *, managed: bool = True) -> Path:
    root = repo_root.resolve()
    absolute = target if target.is_absolute() else root / target
    try:
        relative = absolute.relative_to(root)
    except ValueError as exc:
        raise ValueError(f"Target escapes repository root: {target}") from exc
    if managed and not is_managed_target(relative):
        raise ValueError(f"Target is outside managed Codex roots: {target}")

    current = root
    for part in relative.parts:
        current = current / part
        if current.is_symlink():
            raise ValueError(f"Symlink is not allowed in managed path: {current}")
    return absolute


def _accepted_legacy_hashes(
    legacy_blobs: dict[str, frozenset[str] | str] | None,
) -> dict[str, frozenset[str]]:
    raw = LEGACY_GENERATED_BLOBS if legacy_blobs is None else legacy_blobs
    return {
        relative: hashes if isinstance(hashes, frozenset) else frozenset({hashes})
        for relative, hashes in raw.items()
    }


def build_sync_plan(
    repo_root: Path,
    files: list[GeneratedFile],
    resources: list[tuple[Path, Path]],
    *,
    prune: bool,
    legacy_blobs: dict[str, frozenset[str] | str] | None = None,
) -> SyncPlan:
    desired = desired_files(files, resources)
    expected = expected_hashes(repo_root, files, resources)
    previous = load_manifest(repo_root)
    legacy = _accepted_legacy_hashes(legacy_blobs)
    changed: list[Path] = []
    unchanged: list[Path] = []
    remove: list[Path] = []
    conflicts: list[str] = []

    for target, content in sorted(desired.items(), key=lambda item: str(item[0])):
        target = assert_safe_path(repo_root, target)
        relative = target.relative_to(repo_root).as_posix()
        if not target.exists():
            changed.append(target)
            continue
        if not target.is_file():
            conflicts.append(f"{target}: target is not a regular file")
            continue
        current = target.read_bytes()
        if current == content:
            unchanged.append(target)
            continue

        owned_hash = previous.get(relative)
        is_owned = owned_hash is not None and hashlib.sha256(current).hexdigest() == owned_hash
        is_legacy = git_blob_hash(target) in legacy.get(relative, frozenset())
        if is_owned or is_legacy:
            changed.append(target)
        else:
            conflicts.append(f"{target}: existing file is not owned by this converter")

    if prune:
        candidates = set(previous) | set(legacy)
        for relative_text in sorted(candidates - set(expected)):
            target = assert_safe_path(repo_root, repo_root / relative_text)
            if not target.exists():
                continue
            if not target.is_file():
                conflicts.append(f"{target}: prune target is not a regular file")
                continue
            previous_hash = previous.get(relative_text)
            is_owned = previous_hash is not None and file_hash(target) == previous_hash
            is_legacy = git_blob_hash(target) in legacy.get(relative_text, frozenset())
            if is_owned or is_legacy:
                remove.append(target)
            else:
                conflicts.append(f"{target}: modified generated file requires review")

    if conflicts:
        raise ValueError("Codex sync preflight failed:\n" + "\n".join(f"- {item}" for item in conflicts))

    return SyncPlan(
        desired=desired,
        changed=tuple(changed),
        unchanged=tuple(unchanged),
        remove=tuple(remove),
    )


def _atomic_write(path: Path, content: bytes) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    descriptor, temporary_name = tempfile.mkstemp(prefix=f".{path.name}.", dir=path.parent)
    temporary = Path(temporary_name)
    try:
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(content)
            stream.flush()
            os.fsync(stream.fileno())
        os.replace(temporary, path)
    finally:
        if temporary.exists():
            temporary.unlink()


def _warn_cleanup_failure(path: Path, error: OSError) -> None:
    """Report best-effort cleanup failures without changing transaction outcome."""
    print(f"WARNING cleanup incomplete for {path}: {error}", file=sys.stderr)


def _ensure_transaction_directories(
    repo_root: Path,
    directory: Path,
    created_directories: list[Path],
) -> None:
    """Create missing parents and journal only directories created by this transaction."""
    root = repo_root.resolve()
    missing: list[Path] = []
    current = directory
    while current != root and not current.exists():
        missing.append(current)
        current = current.parent
    if current != root and not current.exists():
        raise ValueError(f"Destination parent escapes repository root: {directory}")

    for candidate in reversed(missing):
        created_directories.append(candidate)
        try:
            candidate.mkdir()
        except FileExistsError:
            created_directories.pop()
            if not candidate.is_dir() or candidate.is_symlink():
                raise


def _remove_empty_managed_directory(directory: Path) -> None:
    """Remove an empty managed directory, warning when cleanup cannot finish."""
    try:
        if not directory.is_symlink() and not any(directory.iterdir()):
            directory.rmdir()
    except OSError as cleanup_error:
        _warn_cleanup_failure(directory, cleanup_error)


def apply_sync_plan(
    repo_root: Path,
    plan: SyncPlan,
    manifest_hashes: dict[str, str],
) -> None:
    backups: list[tuple[Path, Path | None]] = []
    created_directories: list[Path] = []
    transaction_root = Path(tempfile.mkdtemp(prefix=".claude-to-codex-", dir=repo_root))
    backup_root = transaction_root / "backup"
    preserve_transaction = False

    try:
        for target in plan.changed:
            assert_safe_path(repo_root, target)
            relative = target.relative_to(repo_root)
            backup = backup_root / relative
            if target.exists():
                backup.parent.mkdir(parents=True, exist_ok=True)
                shutil.copy2(target, backup)
                backups.append((target, backup))
            else:
                backups.append((target, None))
            _ensure_transaction_directories(repo_root, target.parent, created_directories)
            assert_safe_path(repo_root, target)
            _atomic_write(target, plan.desired[target])

        for target in plan.remove:
            assert_safe_path(repo_root, target)
            relative = target.relative_to(repo_root)
            backup = backup_root / relative
            backup.parent.mkdir(parents=True, exist_ok=True)
            backups.append((target, backup))
            os.replace(target, backup)

        manifest_path = assert_safe_path(
            repo_root,
            repo_root / MANIFEST_RELATIVE_PATH,
            managed=False,
        )
        manifest_backup = backup_root / MANIFEST_RELATIVE_PATH
        if manifest_path.exists():
            manifest_backup.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(manifest_path, manifest_backup)
            backups.append((manifest_path, manifest_backup))
        else:
            backups.append((manifest_path, None))
        _ensure_transaction_directories(repo_root, manifest_path.parent, created_directories)
        assert_safe_path(repo_root, manifest_path, managed=False)
        _atomic_write(manifest_path, manifest_bytes(manifest_hashes))
    except BaseException as original_error:
        rollback_errors: list[str] = []
        for target, backup in reversed(backups):
            try:
                if backup is None:
                    if target.exists():
                        target.unlink()
                elif backup.exists():
                    target.parent.mkdir(parents=True, exist_ok=True)
                    os.replace(backup, target)
            except BaseException as rollback_error:
                rollback_errors.append(f"{target}: {rollback_error}")
        for directory in reversed(created_directories):
            try:
                if (
                    directory.exists()
                    and not directory.is_symlink()
                    and not any(directory.iterdir())
                ):
                    directory.rmdir()
            except BaseException as rollback_error:
                rollback_errors.append(f"{directory}: {rollback_error}")
        if rollback_errors:
            preserve_transaction = True
            raise RuntimeError(
                "Codex sync failed and rollback was incomplete. "
                f"Recovery files remain in {transaction_root}:\n"
                + "\n".join(rollback_errors)
            ) from original_error
        raise
    finally:
        if transaction_root.exists() and not preserve_transaction:
            try:
                shutil.rmtree(transaction_root)
            except OSError as cleanup_error:
                _warn_cleanup_failure(transaction_root, cleanup_error)

    cleanup_directories: set[Path] = set()
    for target in plan.remove:
        relative = target.relative_to(repo_root)
        managed_root = next(
            repo_root / prefix
            for prefix in MANAGED_TARGET_PREFIXES
            if relative == prefix or prefix in relative.parents
        )
        directory = target.parent
        while directory != managed_root:
            cleanup_directories.add(directory)
            directory = directory.parent
    for directory in sorted(
        cleanup_directories,
        key=lambda path: len(path.parts),
        reverse=True,
    ):
        _remove_empty_managed_directory(directory)


def parse_agent_toml_subset(content: str) -> dict[str, str]:
    """Parse the deterministic TOML subset emitted by this converter."""
    marker = "developer_instructions = "
    if marker not in content:
        raise ValueError("missing developer_instructions value")
    header, instructions = content.split(marker, 1)
    data: dict[str, str] = {}
    for line in header.splitlines():
        if not line:
            continue
        if " = " not in line:
            raise ValueError(f"invalid assignment: {line}")
        key, raw = line.split(" = ", 1)
        if key in data:
            raise ValueError(f"duplicate key: {key}")
        value = json.loads(raw)
        if not isinstance(value, str):
            raise ValueError(f"{key} must be a string")
        data[key] = value

    if instructions.startswith("'''") and instructions.endswith("'''\n"):
        data["developer_instructions"] = instructions[3:-4]
    else:
        value = json.loads(instructions.strip())
        if not isinstance(value, str):
            raise ValueError("developer_instructions must be a string")
        data["developer_instructions"] = value
    return data


def parse_agent_toml(content: str) -> dict[str, object]:
    if tomllib is not None:
        return tomllib.loads(content)
    return parse_agent_toml_subset(content)


def validate_agent_content(label: str, content: str) -> list[str]:
    errors: list[str] = []
    try:
        data = parse_agent_toml(content)
    except Exception as exc:
        return [f"{label}: TOML parse failed: {exc}"]

    keys = set(data)
    extra = sorted(keys - CODEX_AGENT_KEYS)
    missing = sorted({"name", "description", "developer_instructions"} - keys)
    if extra or missing:
        errors.append(f"{label}: extra={extra} missing={missing}")
    return errors


def validate_agents(agent_files: list[Path]) -> list[str]:
    errors: list[str] = []
    for agent_file in sorted(agent_files):
        if not agent_file.exists():
            continue
        errors.extend(
            validate_agent_content(
                str(agent_file),
                agent_file.read_text(encoding="utf-8"),
            )
        )
    return errors


def validate_skill_content(label: str, content: str) -> list[str]:
    frontmatter, _ = split_frontmatter(content)
    keys = set(frontmatter)
    extra = sorted(keys - {"name", "description"})
    missing = sorted({"name", "description"} - keys)
    return [f"{label}: extra={extra} missing={missing}"] if extra or missing else []


def validate_skills(skill_files: list[Path]) -> list[str]:
    errors: list[str] = []
    for skill_file in sorted(skill_files):
        if not skill_file.exists():
            continue
        errors.extend(
            validate_skill_content(
                str(skill_file),
                skill_file.read_text(encoding="utf-8"),
            )
        )

    return errors


def check_generated_state(
    repo_root: Path,
    files: list[GeneratedFile],
    resources: list[tuple[Path, Path]],
) -> list[str]:
    mismatches: list[str] = []
    for generated in files:
        assert_safe_path(repo_root, generated.path)
        if (
            not generated.path.exists()
            or generated.path.read_text(encoding="utf-8") != generated.content
        ):
            mismatches.append(str(generated.path))

    for source, target in resources:
        if not target.exists() or not filecmp.cmp(source, target, shallow=False):
            mismatches.append(str(target))

    expected = expected_hashes(repo_root, files, resources)
    manifest = load_manifest(repo_root)
    for relative_text in manifest:
        if relative_text not in expected and (repo_root / relative_text).exists():
            mismatches.append(str(repo_root / relative_text))
    if manifest != expected:
        mismatches.append(str(repo_root / MANIFEST_RELATIVE_PATH))

    return mismatches


def main() -> int:
    parser = argparse.ArgumentParser(description="Import .claude agents and skills into Codex format.")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd(), help="Repository root. Defaults to cwd.")
    parser.add_argument("--dry-run", action="store_true", help="Show what would change without writing files.")
    parser.add_argument("--check", action="store_true", help="Fail if generated files are not up to date.")
    parser.add_argument("--prune", action="store_true", help="Remove Codex agents/skills absent from .claude sources.")
    args = parser.parse_args()

    repo_root = args.repo_root.resolve()
    try:
        files = build_agent_files(repo_root) + build_skill_files(repo_root)
        resources = discover_skill_resources(repo_root)
        agent_files = [file for file in files if file.path.suffix == ".toml"]
        skill_files = [file for file in files if file.path.name == "SKILL.md"]
        validation_errors = [
            error
            for generated in agent_files
            for error in validate_agent_content(str(generated.path), generated.content)
        ]
        validation_errors.extend(
            error
            for generated in skill_files
            for error in validate_skill_content(str(generated.path), generated.content)
        )
        if validation_errors:
            for error in validation_errors:
                print(f"INVALID {error}")
            return 1

        expected = expected_hashes(repo_root, files, resources)
        if args.check:
            mismatches = check_generated_state(repo_root, files, resources)
            mismatches.extend(
                str(repo_root / relative)
                for relative in LEGACY_GENERATED_BLOBS
                if relative not in expected and (repo_root / relative).exists()
            )
            if mismatches:
                for path in dict.fromkeys(mismatches):
                    print(f"OUTDATED {path}")
                return 1
            print(f"OK up to date: {len(files)} generated files")
            return 0

        plan = build_sync_plan(
            repo_root,
            files,
            resources,
            prune=args.prune,
        )
        manifest_hashes = expected
        if not args.prune:
            manifest_hashes = {**load_manifest(repo_root), **expected}
        if not args.dry_run:
            apply_sync_plan(repo_root, plan, manifest_hashes)

        generated_targets = {generated.path for generated in files}
        resource_changes = [path for path in plan.changed if path not in generated_targets]
        mode = "DRY RUN" if args.dry_run else "UPDATED"
        print(
            f"{mode}: changed={len(plan.changed)} unchanged={len(plan.unchanged)} "
            f"resources={len(resource_changes)} removed={len(plan.remove)} protected=0"
        )
        for path in plan.changed:
            label = "RESOURCE" if path in resource_changes else "CHANGED"
            print(f"{label} {path}")
        for path in plan.remove:
            print(f"REMOVED {path}")
        return 0
    except (OSError, ValueError, RuntimeError) as exc:
        print(f"ERROR {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
