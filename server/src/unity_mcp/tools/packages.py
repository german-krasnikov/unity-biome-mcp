"""Package Manager MCP tool."""
from ._common import bind

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import package  # noqa: F401

_send = None
_args = None


def register(mcp, send, args):
    bind(globals(), send, args)
