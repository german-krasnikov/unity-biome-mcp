"""Performance and diagnostics tools — Play Mode and editor. get_memory lives in profiling.py."""
from ._common import bind

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import debug_animator, debug_physics  # noqa: F401

_send = None


def register(mcp, send, args):
    bind(globals(), send, args)
