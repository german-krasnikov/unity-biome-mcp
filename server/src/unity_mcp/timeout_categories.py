"""Per-command timeout categories.

Commands are grouped by expected duration. Unknown commands fall back to
DEFAULT_TIMEOUT (30 s). Tool functions can still override by passing an
explicit ``timeout`` to ``_send_raw``.

M8: generated from tools.tool_specs._SPECS (single source of truth shared with
gating.py) instead of a hand-typed literal that could drift from it. Includes
_INTERNAL protocol commands (ping/get_version/export_package/import_package) —
not MCP tools, but still need timeouts.
"""
from .tools.tool_specs import _SPECS, DEFAULT_TIMEOUT

TIMEOUT_CATEGORIES: dict[str, float] = {
    name: spec.timeout_s for name, spec in _SPECS.items() if spec.timeout_s != DEFAULT_TIMEOUT
}


def get_timeout(cmd: str) -> float:
    """Return the timeout for *cmd*, falling back to DEFAULT_TIMEOUT."""
    return TIMEOUT_CATEGORIES.get(cmd, DEFAULT_TIMEOUT)
