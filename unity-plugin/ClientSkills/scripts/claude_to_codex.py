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
import json
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


CODEX_AGENT_KEYS = {"name", "description", "developer_instructions", "nickname_candidates"}
SKILL_REF_NAME = r"[A-Za-z0-9_\-\[\]]+"
SKILL_FILE_RE = re.compile(rf"\.(?:claude|agents)/skills/({SKILL_REF_NAME})(?:\.md|/SKILL\.md)")
CLAUDE_SKILL_DIR_RE = re.compile(rf"\.claude/skills/({SKILL_REF_NAME})/")


@dataclass(frozen=True)
class GeneratedFile:
    path: Path
    content: str


def split_frontmatter(text: str) -> tuple[dict[str, str], str]:
    lines = text.splitlines(keepends=True)
    if not lines or lines[0].strip() != "---":
        return {}, text

    for index in range(1, len(lines)):
        if lines[index].strip() == "---":
            frontmatter = "".join(lines[1:index])
            body = "".join(lines[index + 1 :])
            return parse_simple_frontmatter(frontmatter), body

    return {}, text


def parse_simple_frontmatter(frontmatter: str) -> dict[str, str]:
    """Parse the simple single-line YAML frontmatter used by the project."""
    data: dict[str, str] = {}
    for raw_line in frontmatter.splitlines():
        line = raw_line.strip()
        if not line or line.startswith("#") or ":" not in line:
            continue

        key, value = line.split(":", 1)
        value = value.strip()
        if len(value) >= 2 and value[0] == value[-1] and value[0] in {"'", '"'}:
            value = value[1:-1]
        data[key.strip()] = value

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

    frontmatter, _ = split_frontmatter(skill_file.read_text())
    return frontmatter.get("description")


def discover_skill_sources(claude_skills_dir: Path) -> list[tuple[str, Path]]:
    sources: list[tuple[str, Path]] = []
    if not claude_skills_dir.exists():
        return sources

    for source in sorted(claude_skills_dir.iterdir()):
        if source.is_dir() and (source / "SKILL.md").exists():
            sources.append((source.name, source / "SKILL.md"))
        elif source.is_file() and source.suffix == ".md":
            sources.append((source.stem, source))

    return sources


def build_agent_files(repo_root: Path) -> list[GeneratedFile]:
    source_dir = repo_root / ".claude" / "agents"
    target_dir = repo_root / ".codex" / "agents"
    files: list[GeneratedFile] = []

    for source in sorted(source_dir.glob("*.md")):
        frontmatter, body = split_frontmatter(source.read_text())
        name = frontmatter.get("name") or source.stem
        description = frontmatter.get("description") or f"Project custom agent: {name}."
        body = normalize_codex_paths(body)
        content = (
            f"name = {toml_string(name)}\n"
            f"description = {toml_string(description)}\n"
            f"developer_instructions = {toml_literal(body)}\n"
        )
        files.append(GeneratedFile(target_dir / f"{name}.toml", content))

    return files


def build_skill_files(repo_root: Path) -> list[GeneratedFile]:
    source_dir = repo_root / ".claude" / "skills"
    target_dir = repo_root / ".agents" / "skills"
    files: list[GeneratedFile] = []

    for name, source_file in discover_skill_sources(source_dir):
        frontmatter, body = split_frontmatter(source_file.read_text())
        description = (
            frontmatter.get("description")
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


def copy_skill_resources(repo_root: Path, dry_run: bool) -> list[str]:
    source_dir = repo_root / ".claude" / "skills"
    target_dir = repo_root / ".agents" / "skills"
    copied: list[str] = []

    for skill_source in sorted(source_dir.iterdir()):
        if not skill_source.is_dir():
            continue

        skill_target = target_dir / skill_source.name
        for resource in sorted(skill_source.rglob("*")):
            if resource.is_dir() or resource.name == "SKILL.md":
                continue
            if resource.name == ".DS_Store":
                continue

            relative = resource.relative_to(skill_source)
            target = skill_target / relative
            if target.exists() and filecmp.cmp(resource, target, shallow=False):
                continue

            copied.append(str(target))
            if dry_run:
                continue

            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(resource, target)

    return copied


def write_generated_files(files: list[GeneratedFile], dry_run: bool) -> tuple[list[str], list[str]]:
    changed: list[str] = []
    unchanged: list[str] = []

    for generated in files:
        old_content = generated.path.read_text() if generated.path.exists() else None
        if old_content == generated.content:
            unchanged.append(str(generated.path))
            continue

        changed.append(str(generated.path))
        if dry_run:
            continue

        generated.path.parent.mkdir(parents=True, exist_ok=True)
        generated.path.write_text(generated.content)

    return changed, unchanged


def prune_missing(repo_root: Path, files: list[GeneratedFile], dry_run: bool) -> list[str]:
    expected = {file.path.resolve() for file in files}
    removed: list[str] = []

    for agent in sorted((repo_root / ".codex" / "agents").glob("*.toml")):
        if agent.resolve() in expected:
            continue
        removed.append(str(agent))
        if not dry_run:
            agent.unlink()

    expected_skill_names = {file.path.parent.name for file in files if ".agents/skills" in file.path.as_posix()}
    skills_dir = repo_root / ".agents" / "skills"
    for skill_dir in sorted(path for path in skills_dir.iterdir() if path.is_dir()):
        if skill_dir.name in expected_skill_names:
            continue
        removed.append(str(skill_dir))
        if not dry_run:
            shutil.rmtree(skill_dir)

    return removed


def validate_agents(repo_root: Path) -> list[str]:
    if tomllib is None:
        return []
    errors: list[str] = []
    for agent_file in sorted((repo_root / ".codex" / "agents").glob("*.toml")):
        try:
            data = tomllib.loads(agent_file.read_text())
        except Exception as exc:
            errors.append(f"{agent_file}: TOML parse failed: {exc}")
            continue

        keys = set(data)
        extra = sorted(keys - CODEX_AGENT_KEYS)
        missing = sorted({"name", "description", "developer_instructions"} - keys)
        if extra or missing:
            errors.append(f"{agent_file}: extra={extra} missing={missing}")

    return errors


def validate_skills(repo_root: Path) -> list[str]:
    errors: list[str] = []
    for skill_file in sorted((repo_root / ".agents" / "skills").glob("*/SKILL.md")):
        frontmatter, _ = split_frontmatter(skill_file.read_text())
        keys = set(frontmatter)
        extra = sorted(keys - {"name", "description"})
        missing = sorted({"name", "description"} - keys)
        if extra or missing:
            errors.append(f"{skill_file}: extra={extra} missing={missing}")

    return errors


def check_generated_state(repo_root: Path, files: list[GeneratedFile]) -> list[str]:
    mismatches: list[str] = []
    with tempfile.TemporaryDirectory() as tmp:
        temp_root = Path(tmp)
        for generated in files:
            relative = generated.path.relative_to(repo_root)
            temp_path = temp_root / relative
            temp_path.parent.mkdir(parents=True, exist_ok=True)
            temp_path.write_text(generated.content)
            if not generated.path.exists() or not filecmp.cmp(generated.path, temp_path, shallow=False):
                mismatches.append(str(generated.path))
    return mismatches


def main() -> int:
    parser = argparse.ArgumentParser(description="Import .claude agents and skills into Codex format.")
    parser.add_argument("--repo-root", type=Path, default=Path.cwd(), help="Repository root. Defaults to cwd.")
    parser.add_argument("--dry-run", action="store_true", help="Show what would change without writing files.")
    parser.add_argument("--check", action="store_true", help="Fail if generated files are not up to date.")
    parser.add_argument("--prune", action="store_true", help="Remove Codex agents/skills absent from .claude sources.")
    args = parser.parse_args()

    repo_root = args.repo_root.resolve()
    files = build_agent_files(repo_root) + build_skill_files(repo_root)

    if args.check:
        mismatches = check_generated_state(repo_root, files)
        validation_errors = validate_agents(repo_root) + validate_skills(repo_root)
        if mismatches or validation_errors:
            for path in mismatches:
                print(f"OUTDATED {path}")
            for error in validation_errors:
                print(f"INVALID {error}")
            return 1
        print(f"OK up to date: {len(files)} generated files")
        return 0

    changed, unchanged = write_generated_files(files, args.dry_run)
    copied = copy_skill_resources(repo_root, args.dry_run)
    removed = prune_missing(repo_root, files, args.dry_run) if args.prune else []

    validation_errors: list[str] = []
    if not args.dry_run:
        validation_errors = validate_agents(repo_root) + validate_skills(repo_root)

    mode = "DRY RUN" if args.dry_run else "UPDATED"
    print(f"{mode}: changed={len(changed)} unchanged={len(unchanged)} resources={len(copied)} removed={len(removed)}")
    for path in changed:
        print(f"CHANGED {path}")
    for path in copied:
        print(f"RESOURCE {path}")
    for path in removed:
        print(f"REMOVED {path}")

    if validation_errors:
        for error in validation_errors:
            print(f"INVALID {error}")
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
