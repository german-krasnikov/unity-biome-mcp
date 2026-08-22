"""Classify MCP commands by domain reload risk + track script writes since last compile."""
import os
import re
from typing import Literal

# --- Classifier ---

_SCRIPT_EXTS = frozenset({
    ".cs", ".asmdef", ".asmref", ".rsp",
    ".dll", ".mdb", ".pdb", ".aar", ".jar",
})

_SCRIPT_CMDS = frozenset({"sync_unity", "recompile", "force_refresh"})


def classify(cmd: str, args: dict | None = None) -> Literal["none", "script"]:
    """'script' if cmd may trigger domain reload; 'none' if safe."""
    if cmd in _SCRIPT_CMDS:
        return "script"
    if cmd in ("asset", "write_text"):
        path = (args or {}).get("path", "")
        if _is_script_path(path):
            return "script"
    return "none"


def classify_batch(commands: str) -> Literal["none", "script"]:
    """Scan batch block for any script-touching op. O(n lines), pure string ops."""
    for line in commands.splitlines():
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        parts = stripped.split(None, 1)
        cmd = parts[0]
        rest = parts[1] if len(parts) > 1 else ""
        if classify(cmd, _parse_kv(rest)) == "script":
            return "script"
    return "none"


def _is_script_path(path: str) -> bool:
    return os.path.splitext(path)[1].lower() in _SCRIPT_EXTS


def _parse_kv(rest: str) -> dict:
    """Minimal key=value parser — quoted and unquoted values; extension check only."""
    result = {}
    for m in re.finditer(r'(\w+)=(?:"([^"]*)"|(\S+))', rest):
        result[m.group(1)] = m.group(2) if m.group(2) is not None else m.group(3)
    return result


# --- Tracker (module-level counter) ---

_script_touch_count: int = 0


def touch() -> None:
    """Increment script-write counter. Call after classify() returns 'script'."""
    global _script_touch_count
    _script_touch_count += 1


def reset() -> None:
    """Reset counter. Call after compile-clean confirmed."""
    global _script_touch_count
    _script_touch_count = 0


# Alias for semantic clarity at sync.py call sites
on_compile_clean = reset


def has_touches() -> bool:
    """True when at least one script write happened since last reset."""
    return _script_touch_count > 0


def current_count() -> int:
    """Current touch count. Diagnostics / tests only."""
    return _script_touch_count
