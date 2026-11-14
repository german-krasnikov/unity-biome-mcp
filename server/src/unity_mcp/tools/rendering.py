"""Rendering analysis MCP tool.

render_analyze dispatches to RenderAnalyzer.cs on the Unity side.
FrameDebugHelper.cs handles frame_debug action via reflection.
"""
# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import bake, render_analyze  # noqa: F401
