"""UI authoring + visual asset tools: create_ui, set_rect, menu, shader, lint_ugui."""
from ._common import bind

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import create_ui, lint_ugui, menu, set_rect, shader  # noqa: F401

_send = None
_args = None


def register(mcp, send, args):
    bind(globals(), send, args)
