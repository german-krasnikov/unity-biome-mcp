"""Shared registration helper for tools/*.py modules."""
import os

from mcp.server.fastmcp.exceptions import ToolError


def _guard_read_only(tool_name: str) -> None:
    """Raise ToolError if UNITY_MCP_READ_ONLY=1 (for direct_only file-writing tools)."""
    if os.environ.get("UNITY_MCP_READ_ONLY", "0") == "1":
        raise ToolError(f"READ_ONLY_BLOCKED: '{tool_name}' file write disabled in read-only mode")


def bind(module_globals: dict, send, args) -> None:
    """Bind the standard _send/_args module globals shared by every tools/*.py
    register(mcp, send, args) implementation. Always binds both names, even in
    modules that don't currently call _args() — uniformity here eliminates the
    3-variant drift class (full-bind / send-only / neither) this helper replaces.
    """
    module_globals["_send"] = send
    module_globals["_args"] = args
