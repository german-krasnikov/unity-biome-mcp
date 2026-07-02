"""Persistent reusable-code library: learned skills + scene templates."""
import os
import re
import json
import time
from ._annotations import RO as _RO, RW as _RW, RW_IDEM as _RW_IDEM
from ._common import bind

_send = None
_args = None


def _skills_dir():
    return os.path.join(os.getcwd(), ".claude", "skills", "learned")


def _safe_name(name: str) -> str:
    if "/" in name or "\\" in name or ".." in name:
        raise ValueError(f"Invalid name: '{name}'")
    return name


def _detect_kind(code: str) -> str:
    return "csharp" if any(kw in code for kw in ("var ", "new ", "GameObject", "//", ";", "using ")) else "batch"


async def save_skill(name: str, description: str, code: str) -> str:
    """Save a learned skill (C# code or batch commands) for reuse across sessions.
    name: skill identifier. description: what it does. code: C# or batch commands."""
    name = _safe_name(name)
    os.makedirs(_skills_dir(), exist_ok=True)
    skill = {"name": name, "description": description, "code": code,
             "kind": _detect_kind(code),
             "created": time.strftime("%Y-%m-%d %H:%M"), "used_count": 0}
    with open(os.path.join(_skills_dir(), f"{name}.json"), "w", encoding="utf-8") as f:
        json.dump(skill, f, ensure_ascii=False)
    return f"Skill saved: {name} — {description}"


async def use_skill(name: str, params: str | None = None) -> str:
    """Execute a previously saved skill. params: comma-separated key=value for substitution."""
    name = _safe_name(name)
    path = os.path.join(_skills_dir(), f"{name}.json")
    if not os.path.exists(path):
        return await list_skills()
    with open(path, encoding="utf-8") as f:
        skill = json.load(f)
    code = skill["code"]
    if params:
        for pair in params.split(","):
            pair = pair.strip()
            if "=" in pair:
                k, v = pair.split("=", 1)
                code = code.replace(f"${{{k.strip()}}}", v.strip())
    skill["used_count"] = skill.get("used_count", 0) + 1
    skill["last_used"] = time.strftime("%Y-%m-%d %H:%M")
    with open(path, "w", encoding="utf-8") as f:
        json.dump(skill, f, ensure_ascii=False)
    if skill.get("kind", _detect_kind(code)) == "csharp":
        return await _send("execute_code", {"code": code, "undo_label": f"skill:{name}"})
    return await _send("batch", {"commands": code})


async def list_skills() -> str:
    """List all saved skills with descriptions and usage counts."""
    if not os.path.exists(_skills_dir()):
        return "No skills saved yet. Use save_skill to create one."
    skills = []
    for fname in sorted(os.listdir(_skills_dir())):
        if not fname.endswith(".json"):
            continue
        with open(os.path.join(_skills_dir(), fname), encoding="utf-8") as fh:
            s = json.load(fh)
        skills.append(f"{s['name']} [{s.get('kind', '?')}]: {s['description']} (used {s.get('used_count', 0)}x)")
    return "\n".join(skills) if skills else "No skills saved yet. Use save_skill to create one."


async def apply_template(name: str, params: str | None = None) -> str:
    """Apply a scene template (.cs file from .claude/templates/).
    params: comma-separated key=value pairs for ${key} replacement.
    Example: apply_template('level_setup', 'player_pos=(0,0,0),count=3')"""
    name = _safe_name(name)
    template_dir = os.path.join(os.getcwd(), ".claude", "templates")
    path = os.path.join(template_dir, f"{name}.cs")
    if not os.path.exists(path):
        if os.path.exists(template_dir):
            available = [f[:-3] for f in os.listdir(template_dir) if f.endswith(".cs")]
            return f"Template '{name}' not found. Available: {', '.join(available) or 'none'}"
        return f"No templates directory. Create .claude/templates/{name}.cs"
    with open(path, encoding="utf-8") as f:
        code = f.read()
    if params:
        # Split on commas not inside parentheses
        for pair in re.split(r",(?![^(]*\))", params):
            pair = pair.strip()
            if "=" in pair:
                key, value = pair.split("=", 1)
                code = code.replace(f"${{{key.strip()}}}", value.strip())
    return await _send("execute_code", {"code": code, "undo_label": f"template:{name}"})


async def save_template(name: str, code: str) -> str:
    """Save C# code as a reusable scene template in .claude/templates/."""
    name = _safe_name(name)
    template_dir = os.path.join(os.getcwd(), ".claude", "templates")
    os.makedirs(template_dir, exist_ok=True)
    path = os.path.join(template_dir, f"{name}.cs")
    with open(path, "w", encoding="utf-8") as f:
        f.write(code)
    return f"Template saved: {path}"


async def list_templates() -> str:
    """List available scene templates in .claude/templates/."""
    template_dir = os.path.join(os.getcwd(), ".claude", "templates")
    if not os.path.exists(template_dir):
        return "No templates. Use save_template to create one."
    templates = [f[:-3] for f in os.listdir(template_dir) if f.endswith(".cs")]
    return "\n".join(sorted(templates)) if templates else "No templates yet."


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(save_skill)
    mcp.tool(annotations=_RW)(use_skill)
    mcp.tool(annotations=_RO)(list_skills)
    mcp.tool(annotations=_RW)(apply_template)
    mcp.tool(annotations=_RW_IDEM)(save_template)
    mcp.tool(annotations=_RO)(list_templates)
