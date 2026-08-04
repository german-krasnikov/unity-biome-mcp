"""Static plan validation for do() tool."""

from ..utils import parse_kv_line as _parse_line
from .catalog import ALLOWED, FORBIDDEN

MAX_LINES = 50


def validate_plan(plan: str, scene_paths: set[str]) -> str | None:
    """
    Validate Haiku-generated plan text.
    Returns error string or None if valid.
    """
    lines = [l.strip() for l in plan.strip().splitlines() if l.strip()]

    if not lines:
        return "Empty plan"

    # REJECT prefix from Haiku
    if lines[0].startswith("REJECT:"):
        return lines[0]

    if len(lines) > MAX_LINES:
        return f"Plan exceeds {MAX_LINES} lines limit ({len(lines)} lines)"

    declared_paths: set[str] = set(scene_paths)

    for line in lines:
        cmd, kv = _parse_line(line)

        # Forbidden command check
        if cmd in FORBIDDEN or cmd not in ALLOWED:
            return f"Forbidden or unknown command: {cmd}"

        sig = ALLOWED[cmd]

        # Required keys check
        for req_key in sig["required"]:
            if req_key not in kv:
                return f"Command '{cmd}' missing required key: {req_key}"

        # Path existence check (for commands that reference existing objects)
        if cmd != "create_object" and "path" in kv:
            p = kv["path"]
            if p not in declared_paths:
                return f"Path not found in scene: {p}"

        # Track newly created objects
        if cmd == "create_object":
            name = kv.get("name", "")
            parent = kv.get("parent", "")
            if parent:
                parent_norm = parent if parent.startswith("/") else f"/{parent}"
                declared_paths.add(f"{parent_norm}/{name}")
            else:
                declared_paths.add(f"/{name}")

    return None
