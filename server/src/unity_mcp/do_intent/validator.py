"""Static plan validation for do() tool."""

from ..decisions.planverify_facts import line_verdict, plan_gate
from ..utils import parse_kv_line as _parse_line
from .catalog import ALLOWED, FORBIDDEN

MAX_LINES = 50


def declared_paths(plan: str, scene_paths: set[str]) -> set[str]:
    """Paths that exist in the scene, plus every path any create_object line of `plan` declares."""
    declared: set[str] = set(scene_paths)
    for line in plan.strip().splitlines():
        line = line.strip()
        if not line:
            continue
        cmd, kv = _parse_line(line)
        if cmd == "create_object":
            name = kv.get("name", "")
            parent = kv.get("parent", "")
            parent_norm = (parent if parent.startswith("/") else f"/{parent}") if parent else ""
            declared.add(f"{parent_norm}/{name}" if parent_norm else f"/{name}")
    return declared


def plan_verdict(plan: str, scene_paths: set[str]) -> tuple[str, str | None]:
    """(tag, message) for `plan` — tag is one of fix.verdict's 7 enum values; message is None iff tag == 'ok'."""
    lines = [line.strip() for line in plan.strip().splitlines() if line.strip()]

    gate = plan_gate(lines=len(lines), max_lines=MAX_LINES, rejected=bool(lines) and lines[0].startswith("REJECT:"))
    if gate == "empty":
        return "empty", "Empty plan"
    if gate == "rejected":
        return "rejected", lines[0]
    if gate == "too_long":
        return "too_long", f"Plan exceeds {MAX_LINES} lines limit ({len(lines)} lines)"

    declared = declared_paths(plan, scene_paths)

    for line in lines:
        cmd, kv = _parse_line(line)
        allowed = cmd in ALLOWED and cmd not in FORBIDDEN
        sig = ALLOWED.get(cmd, {"required": set()})
        missing_required = sum(1 for req_key in sig["required"] if req_key not in kv)
        is_create = cmd == "create_object"
        has_path = "path" in kv
        path_declared = has_path and kv.get("path") in declared

        verdict = line_verdict(
            allowed=allowed,
            missing_required=missing_required,
            is_create=is_create,
            has_path=has_path,
            path_declared=path_declared,
        )
        if verdict == "forbidden":
            return "forbidden", f"Forbidden or unknown command: {cmd}"
        if verdict == "missing_key":
            missing = next(req_key for req_key in sig["required"] if req_key not in kv)
            return "missing_key", f"Command '{cmd}' missing required key: {missing}"
        if verdict == "unknown_path":
            return "unknown_path", f"Path not found in scene: {kv['path']}"

    return "ok", None


def validate_plan(plan: str, scene_paths: set[str]) -> str | None:
    """
    Validate Haiku-generated plan text.
    Returns error string or None if valid.
    """
    _, message = plan_verdict(plan, scene_paths)
    return message
