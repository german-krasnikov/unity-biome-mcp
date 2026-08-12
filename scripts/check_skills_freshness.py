"""Skills freshness checker — scans skills and agents for stale references.

stdlib only. Run via: python scripts/check_skills_freshness.py [--strict]
"""
from __future__ import annotations

import argparse
import re
import sys
from dataclasses import dataclass
from pathlib import Path

# ---------------------------------------------------------------------------
# Data classes
# ---------------------------------------------------------------------------

@dataclass
class SkillFile:
    path: Path
    rel: str       # relative to repo root
    name: str      # stem for flat .md, parent dir name for dir-skill
    frontmatter: dict
    body: str


@dataclass
class AgentFile:
    path: Path
    rel: str
    frontmatter: dict
    body: str


@dataclass(frozen=True)
class Finding:
    severity: str   # "ERROR" | "WARNING"
    rel_path: str
    check: str
    detail: str


# ---------------------------------------------------------------------------
# Tool specs loader
# ---------------------------------------------------------------------------

_SPEC_ENTRY_RE = re.compile(r"^\s+'([a-z][a-z_0-9]+)':\s+ToolSpec", re.MULTILINE)


def load_tool_specs_from_text(text: str) -> frozenset[str]:
    return frozenset(_SPEC_ENTRY_RE.findall(text))


def load_tool_specs(repo_root: Path) -> frozenset[str]:
    text = (repo_root / "server/src/unity_mcp/tools/tool_specs.py").read_text(encoding="utf-8")
    return load_tool_specs_from_text(text)


# ---------------------------------------------------------------------------
# Frontmatter parser
# ---------------------------------------------------------------------------

def parse_frontmatter(text: str) -> tuple[dict[str, object], str]:
    m = re.match(r"^---\n(.*?)\n---\n(.*)", text, re.DOTALL)
    if not m:
        return {}, text
    fm_text, body = m.group(1), m.group(2)
    result: dict[str, object] = {}
    current_list_key: str | None = None
    for line in fm_text.splitlines():
        if re.match(r"^  - (.+)$", line):
            if current_list_key is not None:
                result[current_list_key].append(line.strip()[2:])
        elif m2 := re.match(r"^([\w-]+):\s*(.*)$", line):
            current_list_key = None
            key, val = m2.group(1), m2.group(2).strip().strip('"')
            if val.startswith("[") and val.endswith("]"):
                result[key] = [v.strip().strip('"') for v in val[1:-1].split(",") if v.strip()]
            elif val == "":
                result[key] = []
                current_list_key = key
            else:
                result[key] = val
    return result, body


# ---------------------------------------------------------------------------
# Discovery
# ---------------------------------------------------------------------------

def _read_skill(path: Path, repo_root: Path, name: str) -> SkillFile:
    text = path.read_text(encoding="utf-8", errors="replace")
    fm, body = parse_frontmatter(text)
    return SkillFile(path, str(path.relative_to(repo_root)), name, fm, body)


def discover_skills(repo_root: Path) -> list[SkillFile]:
    skills: list[SkillFile] = []

    def _scan_flat(skills_dir: Path) -> None:
        if not skills_dir.exists():
            return
        skills.extend(_read_skill(p, repo_root, p.stem) for p in sorted(skills_dir.glob("*.md")))

    def _scan_dir_skills(skills_dir: Path) -> None:
        if not skills_dir.exists():
            return
        for skill_dir in sorted(skills_dir.iterdir()):
            if skill_dir.is_dir():
                skill_md = skill_dir / "SKILL.md"
                if skill_md.exists():
                    skills.append(_read_skill(skill_md, repo_root, skill_dir.name))

    # .claude/skills: flat .md + dir/SKILL.md
    _scan_flat(repo_root / ".claude" / "skills")
    _scan_dir_skills(repo_root / ".claude" / "skills")

    # ClientSkills: dir/SKILL.md only
    _scan_dir_skills(repo_root / "unity-plugin" / "ClientSkills" / "skills")

    return skills


def discover_agents(repo_root: Path) -> list[AgentFile]:
    agents: list[AgentFile] = []

    def _scan(agents_dir: Path) -> None:
        if not agents_dir.exists():
            return
        for p in sorted(agents_dir.glob("*.md")):
            text = p.read_text(encoding="utf-8", errors="replace")
            fm, body = parse_frontmatter(text)
            agents.append(AgentFile(p, str(p.relative_to(repo_root)), fm, body))

    _scan(repo_root / ".claude" / "agents")
    _scan(repo_root / "unity-plugin" / "ClientSkills" / "agents")
    return agents


# ---------------------------------------------------------------------------
# Checks
# ---------------------------------------------------------------------------

def check_missing_frontmatter(files: list[SkillFile | AgentFile]) -> list[Finding]:
    return [
        Finding(
            severity="ERROR",
            rel_path=f.rel,
            check="missing-frontmatter",
            detail="No frontmatter block or missing 'name' key",
        )
        for f in files
        if not f.frontmatter or "name" not in f.frontmatter
    ]


_FALSE_POSITIVE_TOOLS = frozenset({
    "send", "wrap_send", "frame_read", "frame_write", "args", "bridge_send",
    "tool_specs", "open_connection", "configure_await", "bridge_result",
    "read_text", "parse_frontmatter", "run_skills", "check_unity", "tool_name",
    "skill_name", "plugin_api", "middleware_py",
})

_FENCED_CODE_RE = re.compile(r"```.*?```", re.DOTALL)
_BACKTICK_WORD_RE = re.compile(r"`([a-z][a-z_0-9]+)`")


def check_stale_tool_refs(skills: list[SkillFile], tools: frozenset[str]) -> list[Finding]:
    findings = []
    for skill in skills:
        prose = _FENCED_CODE_RE.sub("", skill.body)
        for m in _BACKTICK_WORD_RE.finditer(prose):
            word = m.group(1)
            if "_" not in word:
                continue
            if word in _FALSE_POSITIVE_TOOLS:
                continue
            if word in tools:
                continue
            findings.append(Finding(
                severity="WARNING",
                rel_path=skill.rel,
                check="stale-tool-ref",
                detail=f"`{word}` not in tool_specs",
            ))
    return findings


_PATH_PREFIXES = ("server/", "unity-plugin/", ".claude/", "scripts/", "docs/", "install/")
_BACKTICK_PATH_RE = re.compile(r"`([^\s`]+)`")


def check_stale_file_refs(
    skills: list[SkillFile],
    repo_root: Path,
    exists_fn=None,
) -> list[Finding]:
    if exists_fn is None:
        exists_fn = lambda p: (repo_root / p).exists()  # noqa: E731
    findings = []
    for skill in skills:
        for m in _BACKTICK_PATH_RE.finditer(skill.body):
            candidate = m.group(1)
            if not any(candidate.startswith(pfx) for pfx in _PATH_PREFIXES):
                continue
            if not exists_fn(candidate):
                findings.append(Finding(
                    severity="WARNING",
                    rel_path=skill.rel,
                    check="stale-file-ref",
                    detail=f"`{candidate}` not found",
                ))
    return findings


def check_missing_skill_description(skills: list[SkillFile]) -> list[Finding]:
    findings = []
    for skill in skills:
        # Only ClientSkills skills
        if "ClientSkills" not in str(skill.path):
            continue
        if not skill.frontmatter.get("description"):
            findings.append(Finding(
                severity="WARNING",
                rel_path=skill.rel,
                check="missing-skill-description",
                detail="ClientSkills skill has no description in frontmatter",
            ))
    return findings


def _skill_exists(name: str, repo_root: Path) -> bool:
    """Check if skill exists as flat .md or dir/SKILL.md in .claude or ClientSkills."""
    for base in (
        repo_root / ".claude" / "skills",
        repo_root / "unity-plugin" / "ClientSkills" / "skills",
    ):
        if (base / f"{name}.md").exists():
            return True
        skill_dir = base / name
        if skill_dir.is_dir() and (skill_dir / "SKILL.md").exists():
            return True
    return False


def check_agent_skill_mismatch(agents: list[AgentFile], repo_root: Path) -> list[Finding]:
    return [
        Finding(
            severity="ERROR",
            rel_path=agent.rel,
            check="agent-skill-mismatch",
            detail=f"Skill '{skill_name}' not found",
        )
        for agent in agents
        if isinstance(agent.frontmatter.get("skills", []), list)
        for skill_name in agent.frontmatter.get("skills", [])
        if not _skill_exists(skill_name, repo_root)
    ]


def check_stale_agent_tools(agents: list[AgentFile], tools: frozenset[str]) -> list[Finding]:
    findings = []
    for agent in agents:
        entries: list[str] = []
        for key in ("tools", "allowed-tools"):
            val = agent.frontmatter.get(key, [])
            if isinstance(val, list):
                entries.extend(val)
        for entry in entries:
            if "mcp__unity-biome-mcp__" not in entry:
                continue
            tool_name = entry.rsplit("__", 1)[-1]
            if tool_name not in tools:
                findings.append(Finding(
                    severity="ERROR",
                    rel_path=agent.rel,
                    check="stale-agent-tool",
                    detail=f"Tool '{tool_name}' (from '{entry}') not in tool_specs",
                ))
    return findings


_PROCESS_START_CALL_RE = re.compile(r"Process\.Start\s*\(")


def _strip_line_comments(text: str) -> str:
    """Remove // to end-of-line comments from C# source (non-destructive; multi-line comments ignored)."""
    return "\n".join(line[: line.find("//")] if "//" in line else line for line in text.splitlines())


def check_batch_seam_in_tests(root: Path) -> list[Finding]:
    """Warn on C# test files that call ShouldWarm() without setting IsBatchModeGetter.

    ShouldWarm() returns false on CI (-batchmode), so any test asserting it returns true
    must set RelayWarmup.IsBatchModeGetter = () => false in [SetUp] to be environment-independent.
    """
    findings = []
    for f in root.glob("unity-plugin/Editor/Chat/Tests/**/*.cs"):
        text = f.read_text(encoding="utf-8")
        if "ShouldWarm()" not in text:
            continue
        if "IsBatchModeGetter" in text:
            continue
        findings.append(Finding(
            severity="WARNING",
            rel_path=str(f.relative_to(root)),
            check="batch-seam-missing",
            detail=(
                "calls ShouldWarm() without IsBatchModeGetter seam — "
                "add `RelayWarmup.IsBatchModeGetter = () => false;` in [SetUp] so the test "
                "passes in CI -batchmode (Application.isBatchMode is true there)"
            ),
        ))
    return findings


def check_unix_tools_in_tests(root: Path) -> list[Finding]:
    """Warn on C# test files that call Process.Start() without a Windows-skip guard.

    Process.Start() with a Unix executable path fails on Windows CI.
    Guard with [UnityMCP.Editor.Testing.SkipOnWindows] or [Platform(Exclude="Win")],
    or replace with a cross-platform implementation.
    """
    findings = []
    for f in root.glob("unity-plugin/Editor/Chat/Tests/**/*.cs"):
        text = f.read_text(encoding="utf-8")
        code = _strip_line_comments(text)
        if not _PROCESS_START_CALL_RE.search(code):
            continue
        if "SkipOnWindows" in text or "[Platform" in text:
            continue
        findings.append(Finding(
            severity="WARNING",
            rel_path=str(f.relative_to(root)),
            check="unguarded-unix-tool",
            detail=(
                "calls Process.Start() without SkipOnWindows or [Platform] guard — "
                "add [UnityMCP.Editor.Testing.SkipOnWindows(...)] to the class or method, "
                "or replace with a cross-platform approach (e.g. File.SetAttributes)"
            ),
        ))
    return findings


def check_orphaned_skills(
    dot_claude_skills: list[SkillFile],
    agents: list[AgentFile],
    claude_md: str,
) -> list[Finding]:
    # Collect all skill references from agents and CLAUDE.md
    referenced: set[str] = set()
    for agent in agents:
        for key in ("skills",):
            val = agent.frontmatter.get(key, [])
            if isinstance(val, list):
                referenced.update(val)
    # Also scan CLAUDE.md body for skill names
    for skill in dot_claude_skills:
        if skill.name in claude_md:
            referenced.add(skill.name)

    return [
        Finding(
            severity="WARNING",
            rel_path=skill.rel,
            check="orphaned-skill",
            detail=f"Skill '{skill.name}' not referenced by any agent or CLAUDE.md",
        )
        for skill in dot_claude_skills
        if skill.name not in referenced
    ]


# ---------------------------------------------------------------------------
# Public entry point
# ---------------------------------------------------------------------------

def run_skills_freshness(repo_root: Path) -> list[Finding]:
    tools = load_tool_specs(repo_root)
    skills = discover_skills(repo_root)
    agents = discover_agents(repo_root)
    claude_md = (repo_root / "CLAUDE.md").read_text(encoding="utf-8", errors="replace")
    dot_claude_skills = [s for s in skills if ".claude/skills" in str(s.path)]
    return (
        check_missing_frontmatter(skills + agents)
        + check_stale_tool_refs(skills, tools)
        + check_stale_file_refs(skills, repo_root)
        + check_missing_skill_description(skills)
        + check_agent_skill_mismatch(agents, repo_root)
        + check_stale_agent_tools(agents, tools)
        + check_orphaned_skills(dot_claude_skills, agents, claude_md)
        + check_batch_seam_in_tests(repo_root)
        + check_unix_tools_in_tests(repo_root)
    )


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def format_report(findings: list[Finding]) -> str:
    errors = [f for f in findings if f.severity == "ERROR"]
    warnings = [f for f in findings if f.severity == "WARNING"]
    lines = ["Skills Freshness Check", ""]
    if errors:
        lines.append(f"ERRORS ({len(errors)})")
        lines.extend(f"  {f.rel_path} | {f.check} | {f.detail}" for f in errors)
    if warnings:
        lines.append(f"\nWARNINGS ({len(warnings)})")
        lines.extend(f"  {f.rel_path} | {f.check} | {f.detail}" for f in warnings)
    if not errors and not warnings:
        lines.append("All checks passed.")
    return "\n".join(lines)


def main() -> None:
    parser = argparse.ArgumentParser(description="Check skills freshness")
    parser.add_argument("--strict", action="store_true", help="Exit 1 on any ERROR")
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).parent.parent)
    args = parser.parse_args()
    findings = run_skills_freshness(args.repo_root)
    print(format_report(findings))
    if args.strict and any(f.severity == "ERROR" for f in findings):
        sys.exit(1)


if __name__ == "__main__":
    main()
