"""Whitelist of allowed commands and their signatures for do() tool.

ALLOWED is derived from the catalog (dsl/tools.biome ARG lines, via
ToolSet.signature) for every do_allowed-tagged tool — no second
hand-written source of truth for argument requiredness (owner D5/Q5,
SV3-DESIGN.md §3.6).
"""
from ..tools.generated.catalog_tools import tools
from ..tools.tags import tagged

ALLOWED: dict[str, dict] = {name: tools.signature(name) for name in sorted(tagged("do_allowed"))}

FORBIDDEN: set[str] = set(tagged("do_forbidden"))


def build_glossary() -> str:
    """Return human-readable command list for Haiku prompt."""
    lines = []
    for cmd, sig in ALLOWED.items():
        req = " ".join(f"{k}=..." for k in sorted(sig["required"]))
        opt = " ".join(f"[{k}=...]" for k in sorted(sig["optional"]))
        lines.append(f"  {cmd} {req} {opt}".rstrip())
    return "\n".join(lines)
