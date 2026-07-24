"""Unity Biome MCP Server - Control Unity Editor from Claude Code."""

from importlib.metadata import version, PackageNotFoundError
try:
    __version__ = version("unity-biome-mcp")
except PackageNotFoundError:
    __version__ = "0.0.0-dev"
