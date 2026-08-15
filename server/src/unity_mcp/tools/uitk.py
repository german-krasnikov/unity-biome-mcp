"""UI Toolkit tools: inspect_uitk, lint_uitk. C# handlers added in Session 4."""
from ._annotations import RO as _RO
from ._common import bind

_send = None
_args = None


async def inspect_uitk(path: str | None = None) -> str:
    """Inspect UI Toolkit element tree. Use when diagnosing UIDocument layout or element visibility."""
    return await _send("inspect_uitk", _args(path=path))


async def lint_uitk(root: str | None = None) -> str:
    """Diagnose UI Toolkit problems: missing UIDocument, StyleSheet errors. Use when UITK UI fails to render."""
    return await _send("lint_uitk", _args(root=root))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(inspect_uitk)
    mcp.tool(annotations=_RO)(lint_uitk)
