"""auto_wire — fill null ObjectReference fields by name/type matching."""
from ._annotations import RW as _RW
from ._common import bind

_send = None
_args = None


async def auto_wire(path: str, dry_run: bool = False) -> str:
    """Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects.
    dry_run=true previews without applying. Returns wired/ambiguous/no-match summary."""
    return await _send("auto_wire", _args(path=path, dry_run=str(dry_run).lower()))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(auto_wire)
