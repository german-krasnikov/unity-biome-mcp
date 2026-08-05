"""TDD tests for scripts/check_skills_freshness.py — Red phase first."""
from __future__ import annotations

import importlib.util
import pathlib
import sys

# ---------------------------------------------------------------------------
# Import helper — load without installing as a package
# ---------------------------------------------------------------------------
_SCRIPT = pathlib.Path(__file__).parent.parent / "check_skills_freshness.py"


def _load():
    spec = importlib.util.spec_from_file_location("check_skills_freshness", _SCRIPT)
    mod = importlib.util.module_from_spec(spec)
    sys.modules["check_skills_freshness"] = mod
    spec.loader.exec_module(mod)
    return mod


csf = _load()

SkillFile = csf.SkillFile
AgentFile = csf.AgentFile
Finding = csf.Finding
parse_frontmatter = csf.parse_frontmatter
load_tool_specs_from_text = csf.load_tool_specs_from_text
check_missing_frontmatter = csf.check_missing_frontmatter
check_stale_tool_refs = csf.check_stale_tool_refs
check_stale_file_refs = csf.check_stale_file_refs
check_missing_skill_description = csf.check_missing_skill_description
check_agent_skill_mismatch = csf.check_agent_skill_mismatch
check_stale_agent_tools = csf.check_stale_agent_tools
check_orphaned_skills = csf.check_orphaned_skills
run_skills_freshness = csf.run_skills_freshness


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def make_agent(tmp_path, frontmatter_str, body=""):
    p = tmp_path / "agents" / "test_agent.md"
    p.parent.mkdir(parents=True, exist_ok=True)
    text = frontmatter_str + body
    p.write_text(text, encoding="utf-8")
    fm, b = parse_frontmatter(text)
    return AgentFile(p, str(p.relative_to(tmp_path)), fm, b)


def make_skill(tmp_path, body, name="test-skill"):
    p = tmp_path / "skills" / f"{name}.md"
    p.parent.mkdir(parents=True, exist_ok=True)
    text = f"---\nname: {name}\n---\n{body}"
    p.write_text(text, encoding="utf-8")
    fm, b = parse_frontmatter(text)
    return SkillFile(p, str(p.relative_to(tmp_path)), name, fm, b)


TOOL_SPEC_TEXT = """\
_SPECS = {
    'batch': ToolSpec(category='CORE', core=True),
    'get_hierarchy': ToolSpec(category='scene'),
    'run_tests': ToolSpec(category='testing'),
}
"""


# ---------------------------------------------------------------------------
# 1. load_tool_specs_from_text
# ---------------------------------------------------------------------------

def test_load_tool_specs_returns_frozenset():
    result = load_tool_specs_from_text(TOOL_SPEC_TEXT)
    assert isinstance(result, frozenset)
    assert "batch" in result
    assert "get_hierarchy" in result
    assert "run_tests" in result


def test_load_tool_specs_excludes_comments():
    text = """\
# 'commented_tool': ToolSpec(category='x'),
_SPECS = {
    'real_tool': ToolSpec(category='CORE'),
}
"""
    result = load_tool_specs_from_text(text)
    assert "real_tool" in result
    assert "commented_tool" not in result


# ---------------------------------------------------------------------------
# 2. parse_frontmatter
# ---------------------------------------------------------------------------

def test_parse_frontmatter_simple_kv():
    text = "---\nname: my-skill\ndescription: does stuff\n---\nbody here"
    fm, body = parse_frontmatter(text)
    assert fm["name"] == "my-skill"
    assert fm["description"] == "does stuff"
    assert body == "body here"


def test_parse_frontmatter_yaml_list_block():
    text = "---\nname: x\nskills:\n  - alpha\n  - beta\n---\n"
    fm, _ = parse_frontmatter(text)
    assert fm["skills"] == ["alpha", "beta"]


def test_parse_frontmatter_yaml_list_inline():
    text = "---\nname: x\ntools: [Read, Bash]\n---\n"
    fm, _ = parse_frontmatter(text)
    assert fm["tools"] == ["Read", "Bash"]


def test_parse_frontmatter_allowed_tools_hyphenated_key():
    text = "---\nname: x\nallowed-tools:\n  - mcp__unity-biome-mcp__batch\n---\n"
    fm, _ = parse_frontmatter(text)
    assert "allowed-tools" in fm
    assert "mcp__unity-biome-mcp__batch" in fm["allowed-tools"]


def test_parse_frontmatter_quoted_description_with_colon():
    text = '---\nname: x\ndescription: "Use for X: Y"\n---\n'
    fm, _ = parse_frontmatter(text)
    assert fm["description"] == "Use for X: Y"


def test_parse_frontmatter_no_frontmatter_block():
    text = "just plain text, no dashes"
    fm, body = parse_frontmatter(text)
    assert fm == {}
    assert body == text


# ---------------------------------------------------------------------------
# 3. check_stale_agent_tools
# ---------------------------------------------------------------------------

def test_stale_agent_tool_via_allowed_tools_key(tmp_path):
    """B2/B3 regression: allowed-tools key must be checked."""
    text = "---\nname: x\nallowed-tools:\n  - mcp__unity-biome-mcp__nonexistent_tool\n---\n"
    agent = make_agent(tmp_path, text)
    tools = frozenset(["batch", "get_hierarchy"])
    findings = check_stale_agent_tools([agent], tools)
    assert len(findings) == 1
    assert findings[0].severity == "ERROR"
    assert findings[0].check == "stale-agent-tool"
    assert "nonexistent_tool" in findings[0].detail


def test_stale_agent_tool_via_tools_key(tmp_path):
    text = "---\nname: x\ntools:\n  - mcp__unity-biome-mcp__nonexistent_tool\n---\n"
    agent = make_agent(tmp_path, text)
    tools = frozenset(["batch"])
    findings = check_stale_agent_tools([agent], tools)
    assert len(findings) == 1
    assert findings[0].severity == "ERROR"


def test_stale_agent_tool_valid_tool_no_finding(tmp_path):
    text = "---\nname: x\ntools:\n  - mcp__unity-biome-mcp__batch\n---\n"
    agent = make_agent(tmp_path, text)
    tools = frozenset(["batch"])
    findings = check_stale_agent_tools([agent], tools)
    assert findings == []


def test_stale_agent_tool_non_mcp_entry_ignored(tmp_path):
    text = "---\nname: x\ntools:\n  - Read\n  - Bash\n---\n"
    agent = make_agent(tmp_path, text)
    tools = frozenset(["batch"])
    findings = check_stale_agent_tools([agent], tools)
    assert findings == []


# ---------------------------------------------------------------------------
# 4. check_agent_skill_mismatch
# ---------------------------------------------------------------------------

def test_skill_mismatch_missing_skill_produces_error(tmp_path):
    """B1 regression: skill listed in agent but not in skills dirs → ERROR."""
    text = "---\nname: x\nskills:\n  - ghost-skill\n---\n"
    agent = make_agent(tmp_path, text)
    # repo_root is tmp_path — no skills dirs exist
    findings = check_agent_skill_mismatch([agent], tmp_path)
    assert len(findings) == 1
    assert findings[0].severity == "ERROR"
    assert findings[0].check == "agent-skill-mismatch"
    assert "ghost-skill" in findings[0].detail


def test_skill_mismatch_valid_flat_skill_no_finding(tmp_path):
    # Create a flat .md skill
    skills_dir = tmp_path / ".claude" / "skills"
    skills_dir.mkdir(parents=True)
    (skills_dir / "my-skill.md").write_text("---\nname: my-skill\n---\n", encoding="utf-8")

    text = "---\nname: x\nskills:\n  - my-skill\n---\n"
    agent = make_agent(tmp_path, text)
    findings = check_agent_skill_mismatch([agent], tmp_path)
    assert findings == []


def test_skill_mismatch_valid_dir_skill_no_finding(tmp_path):
    # Create a dir-style SKILL.md
    skills_dir = tmp_path / ".claude" / "skills" / "my-dir-skill"
    skills_dir.mkdir(parents=True)
    (skills_dir / "SKILL.md").write_text("---\nname: my-dir-skill\n---\n", encoding="utf-8")

    text = "---\nname: x\nskills:\n  - my-dir-skill\n---\n"
    agent = make_agent(tmp_path, text)
    findings = check_agent_skill_mismatch([agent], tmp_path)
    assert findings == []


def test_skill_mismatch_dir_exists_but_no_skill_md_errors(tmp_path):
    # Dir exists but no SKILL.md inside
    skills_dir = tmp_path / ".claude" / "skills" / "incomplete-skill"
    skills_dir.mkdir(parents=True)
    # No SKILL.md

    text = "---\nname: x\nskills:\n  - incomplete-skill\n---\n"
    agent = make_agent(tmp_path, text)
    findings = check_agent_skill_mismatch([agent], tmp_path)
    assert len(findings) == 1
    assert findings[0].severity == "ERROR"


# ---------------------------------------------------------------------------
# 5. check_stale_tool_refs
# ---------------------------------------------------------------------------

def test_stale_tool_refs_ignores_fenced_code_blocks(tmp_path):
    body = "```python\nget_old_tool()\n```\n"
    skill = make_skill(tmp_path, body)
    tools = frozenset(["batch"])
    findings = check_stale_tool_refs([skill], tools)
    assert findings == []


def test_stale_tool_refs_flags_prose_backtick_mention(tmp_path):
    body = "Use `get_old_tool` to do things.\n"
    skill = make_skill(tmp_path, body)
    tools = frozenset(["batch"])
    findings = check_stale_tool_refs([skill], tools)
    assert len(findings) == 1
    assert findings[0].severity == "WARNING"
    assert findings[0].check == "stale-tool-ref"
    assert "get_old_tool" in findings[0].detail


def test_stale_tool_refs_false_positive_excluded(tmp_path):
    body = "Use `wrap_send` for wrapping.\n"
    skill = make_skill(tmp_path, body)
    tools = frozenset(["batch"])
    findings = check_stale_tool_refs([skill], tools)
    assert findings == []


def test_stale_tool_refs_valid_tool_no_finding(tmp_path):
    body = "Use `batch` for multiple ops.\n"
    skill = make_skill(tmp_path, body)
    tools = frozenset(["batch"])
    findings = check_stale_tool_refs([skill], tools)
    assert findings == []


# ---------------------------------------------------------------------------
# 6. check_stale_file_refs
# ---------------------------------------------------------------------------

def test_stale_file_refs_missing_path_warns(tmp_path):
    body = "See `server/src/missing_module.py` for details.\n"
    skill = make_skill(tmp_path, body)
    findings = check_stale_file_refs([skill], tmp_path, exists_fn=lambda p: False)
    assert len(findings) == 1
    assert findings[0].severity == "WARNING"
    assert findings[0].check == "stale-file-ref"


def test_stale_file_refs_existing_path_no_finding(tmp_path):
    body = "See `server/src/unity_mcp/server.py` for details.\n"
    skill = make_skill(tmp_path, body)
    findings = check_stale_file_refs([skill], tmp_path, exists_fn=lambda p: True)
    assert findings == []


# ---------------------------------------------------------------------------
# 7. End-to-end
# ---------------------------------------------------------------------------

def test_orphaned_skill_warns(tmp_path):
    skill = make_skill(tmp_path, "Use `batch`.", name="lonely-skill")
    agents = [make_agent(tmp_path, "---\nname: a\nskills:\n  - other-skill\n---\n")]
    findings = check_orphaned_skills([skill], agents, "No mention of it here.")
    assert any(f.check == "orphaned-skill" and f.severity == "WARNING" for f in findings)


def test_referenced_skill_no_orphan(tmp_path):
    skill = make_skill(tmp_path, "Use `batch`.", name="used-skill")
    agents = [make_agent(tmp_path, "---\nname: a\nskills:\n  - used-skill\n---\n")]
    findings = check_orphaned_skills([skill], agents, "")
    assert findings == []


def test_skill_in_claude_md_no_orphan(tmp_path):
    skill = make_skill(tmp_path, "body", name="mentioned-skill")
    findings = check_orphaned_skills([skill], [], "Use mentioned-skill for X.")
    assert findings == []


def test_missing_skill_description_warns(tmp_path):
    p = tmp_path / "ClientSkills" / "skills" / "no-desc" / "SKILL.md"
    p.parent.mkdir(parents=True)
    p.write_text("---\nname: no-desc\n---\nbody", encoding="utf-8")
    fm, body = parse_frontmatter(p.read_text(encoding="utf-8"))
    skill = SkillFile(p, "ClientSkills/skills/no-desc/SKILL.md", "no-desc", fm, body)
    findings = check_missing_skill_description([skill])
    assert any(f.check == "missing-skill-description" for f in findings)


def test_skill_with_description_no_finding(tmp_path):
    p = tmp_path / "ClientSkills" / "skills" / "has-desc" / "SKILL.md"
    p.parent.mkdir(parents=True)
    p.write_text("---\nname: has-desc\ndescription: Does things\n---\nbody", encoding="utf-8")
    fm, body = parse_frontmatter(p.read_text(encoding="utf-8"))
    skill = SkillFile(p, "ClientSkills/skills/has-desc/SKILL.md", "has-desc", fm, body)
    findings = check_missing_skill_description([skill])
    assert findings == []


def test_non_client_skill_no_description_check(tmp_path):
    skill = make_skill(tmp_path, "body", name="local-skill")
    findings = check_missing_skill_description([skill])
    assert findings == []


def test_end_to_end_with_fixture(tmp_path):
    """run_skills_freshness on a minimal synthetic repo layout."""
    # Create tool_specs.py
    specs_dir = tmp_path / "server" / "src" / "unity_mcp" / "tools"
    specs_dir.mkdir(parents=True)
    (specs_dir / "tool_specs.py").write_text(TOOL_SPEC_TEXT, encoding="utf-8")

    # CLAUDE.md
    (tmp_path / "CLAUDE.md").write_text("Skills: my-skill, other-skill", encoding="utf-8")

    # A valid skill in .claude/skills/
    claude_skills = tmp_path / ".claude" / "skills"
    claude_skills.mkdir(parents=True)
    (claude_skills / "my-skill.md").write_text(
        "---\nname: my-skill\ndescription: does stuff\n---\nUse `batch` for multi-op.\n",
        encoding="utf-8",
    )

    # A valid agent referencing that skill
    agents_dir = tmp_path / ".claude" / "agents"
    agents_dir.mkdir(parents=True)
    (agents_dir / "my-agent.md").write_text(
        "---\nname: my-agent\nskills:\n  - my-skill\ntools:\n  - mcp__unity-biome-mcp__batch\n---\n",
        encoding="utf-8",
    )

    findings = run_skills_freshness(tmp_path)
    errors = [f for f in findings if f.severity == "ERROR"]
    assert errors == [], f"Unexpected errors: {errors}"
