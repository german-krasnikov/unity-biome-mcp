"""Rendering analysis MCP tool.

render_analyze dispatches to RenderAnalyzer.cs on the Unity side.
FrameDebugHelper.cs handles frame_debug action via reflection.
"""
from ._common import bind

# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import bake, render_analyze  # noqa: F401

_send = None
_args = None


def register(mcp, send, args):
    bind(globals(), send, args)
