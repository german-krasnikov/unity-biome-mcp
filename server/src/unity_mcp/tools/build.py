"""Build pipeline MCP tool."""
# Wrappers the tool catalog (dsl/tools.biome) generates; server.py and the tests import them from here.
from .generated.catalog_tools import build  # noqa: F401
