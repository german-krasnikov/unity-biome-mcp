"""T16: get_changeset MCP tool — returns current ChangeSet as token-efficient text."""
from __future__ import annotations


async def get_changeset() -> str:
    """Return the current ChangeSet: accumulated mutations this session.
    Use after any mutation sequence to review what changed."""
    from ..changeset_coordinator import get_coordinator

    coord = get_coordinator()
    cs = coord.get_current() if coord else None
    if cs is None:
        return "no_changeset"

    nc = sum(1 for op in cs.operations if op.kind == "create")
    nm = sum(1 for op in cs.operations if op.kind == "modify")
    nd = sum(1 for op in cs.operations if op.kind == "delete")

    lines = [
        f"cs:{cs.changeset_id[:8]} status:{cs.status} "
        f"ops:{len(cs.operations)} nc:{nc} nm:{nm} nd:{nd}"
    ]
    for op in cs.operations:
        parts = [op.kind, op.target_type, op.target_path]
        if op.prop:
            parts.append(op.prop)
        if op.before_ref:
            parts.append(f"bh:{op.before_ref.hash16}")
        if op.after_ref:
            parts.append(f"ah:{op.after_ref.hash16}")
        parts.append(f"rev:{str(op.reversible).lower()}")
        lines.append(" ".join(parts))
    return "\n".join(lines)


def register(mcp, send, args):
    from ._annotations import RO as _RO
    mcp.tool(annotations=_RO)(get_changeset)
