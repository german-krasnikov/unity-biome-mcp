"""Build pipeline MCP tool."""
from ._common import bind
from ._annotations import RW as _RW

_send = None
_args = None


async def build(
    action: str,
    target: str | None = None,
    scenes: str | None = None,
    path: str | None = None,
    dev: bool = False,
) -> str:
    """Build player. action: build.
    target: StandaloneWindows64|StandaloneOSX|Android|iOS|WebGL (default: active).
    scenes: comma-sep asset paths (default: Build Settings list).
    path: output path (default: Builds/<target>).
    dev: development build flag."""
    return await _send("build", _args(
        action=action,
        target=target,
        scenes=scenes,
        path=path,
        dev="true" if dev else None,
    ))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RW)(build)
