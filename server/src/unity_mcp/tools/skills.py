"""Persistent reusable-code library: learned skills + scene templates."""
import asyncio
import json
import os
import re
import time
from pathlib import Path

from ._common import _guard_read_only, bind
from .generated.catalog_tools import (  # noqa: F401
    apply_template,
    list_skills,
    list_templates,
    save_skill,
    save_template,
    tools,
    use_skill,
)

_send = None
_args = None


def _skills_dir():
    return os.path.join(os.getcwd(), ".claude", "skills", "learned")


def _read_json(path: str) -> dict:
    return json.loads(Path(path).read_text(encoding="utf-8"))


def _write_json(path: str, data: dict) -> None:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    tmp = Path(f"{path}.{os.getpid()}.{time.time_ns()}.tmp")
    try:
        with tmp.open("w", encoding="utf-8") as f:
            f.write(json.dumps(data, ensure_ascii=False))
            f.flush()
            os.fsync(f.fileno())
        tmp.replace(p)
    except OSError:
        tmp.unlink(missing_ok=True)
        raise


def _read_text(path: str) -> str:
    return Path(path).read_text(encoding="utf-8")


def _write_text(path: str, data: str) -> None:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    tmp = Path(f"{path}.{os.getpid()}.{time.time_ns()}.tmp")
    try:
        with tmp.open("w", encoding="utf-8") as f:
            f.write(data)
            f.flush()
            os.fsync(f.fileno())
        tmp.replace(p)
    except OSError:
        tmp.unlink(missing_ok=True)
        raise


def _safe_name(name: str) -> str:
    if "/" in name or "\\" in name or ".." in name:
        raise ValueError(f"Invalid name: '{name}'")
    return name


def _detect_kind(code: str) -> str:
    return "csharp" if any(kw in code for kw in ("var ", "new ", "GameObject", "//", ";", "using ")) else "batch"


async def _save_skill_body(name: str, description: str, code: str) -> str:
    """Save a learned skill (C# code or batch commands) for reuse across sessions.
    name: skill identifier. description: what it does. code: C# or batch commands."""
    _guard_read_only("save_skill")
    name = _safe_name(name)
    path = os.path.join(_skills_dir(), f"{name}.json")
    skill = {"name": name, "description": description, "code": code,
             "kind": _detect_kind(code),
             "created": time.strftime("%Y-%m-%d %H:%M"), "used_count": 0}
    await asyncio.to_thread(_write_json, path, skill)
    return f"Skill saved: {name} — {description}"


async def _use_skill_body(name: str, params: str | None = None) -> str:
    """Execute a previously saved skill. params: comma-separated key=value for substitution."""
    name = _safe_name(name)
    path = os.path.join(_skills_dir(), f"{name}.json")
    if not os.path.exists(path):
        return await list_skills()  # module-level name, now the generated wrapper re-exported above
    skill = await asyncio.to_thread(_read_json, path)
    code = skill["code"]
    if params:
        for pair in re.split(r",(?![^(]*\))", params):
            pair = pair.strip()
            if "=" in pair:
                k, v = pair.split("=", 1)
                code = code.replace(f"${{{k.strip()}}}", v.strip())
    skill["used_count"] = skill.get("used_count", 0) + 1
    skill["last_used"] = time.strftime("%Y-%m-%d %H:%M")
    await asyncio.to_thread(_write_json, path, skill)
    if skill.get("kind", _detect_kind(code)) == "csharp":
        return await _send("execute_code", {"code": code, "undo_label": f"skill:{name}"})
    return await _send("batch", {"commands": code})


async def _list_skills_body() -> str:
    """List all saved skills with descriptions and usage counts."""
    if not os.path.exists(_skills_dir()):
        return "No skills saved yet. Use save_skill to create one."
    skills = []
    for fname in sorted(os.listdir(_skills_dir())):
        if not fname.endswith(".json"):
            continue
        s = await asyncio.to_thread(_read_json, os.path.join(_skills_dir(), fname))
        skills.append(f"{s['name']} [{s.get('kind', '?')}]: {s['description']} (used {s.get('used_count', 0)}x)")
    return "\n".join(skills) if skills else "No skills saved yet. Use save_skill to create one."


async def _apply_template_body(name: str, params: str | None = None) -> str:
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
    code = await asyncio.to_thread(_read_text, path)
    if params:
        # Split on commas not inside parentheses
        for pair in re.split(r",(?![^(]*\))", params):
            pair = pair.strip()
            if "=" in pair:
                key, value = pair.split("=", 1)
                code = code.replace(f"${{{key.strip()}}}", value.strip())
    return await _send("execute_code", {"code": code, "undo_label": f"template:{name}"})


async def _save_template_body(name: str, code: str) -> str:
    """Save C# code as a reusable scene template in .claude/templates/."""
    _guard_read_only("save_template")
    name = _safe_name(name)
    template_dir = os.path.join(os.getcwd(), ".claude", "templates")
    path = os.path.join(template_dir, f"{name}.cs")
    await asyncio.to_thread(_write_text, path, code)
    return f"Template saved: {path}"


async def _list_templates_body() -> str:
    """List available scene templates in .claude/templates/."""
    template_dir = os.path.join(os.getcwd(), ".claude", "templates")
    if not os.path.exists(template_dir):
        return "No templates. Use save_template to create one."
    templates = [f[:-3] for f in os.listdir(template_dir) if f.endswith(".cs")]
    return "\n".join(sorted(templates)) if templates else "No templates yet."


def register(mcp, send, args):
    bind(globals(), send, args)
