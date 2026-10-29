"""auto_wire — fill null ObjectReference fields by name/type matching."""
from ._common import bind
from .generated.catalog_tools import tools

_send = None
_args = None


async def auto_wire(path: str, dry_run: bool = False) -> str:
    """Fill null ObjectReference fields on a GameObject by matching field name or type to scene objects. Mutates scene when dry_run=False. No confirmation required.
    dry_run=true previews without applying. Returns wired/ambiguous/no-match summary."""
    return await _send("auto_wire", _args(path=path, dry_run="true" if dry_run else None))


def register(mcp, send, args):
    bind(globals(), send, args)
    tools.register(mcp, auto_wire)
