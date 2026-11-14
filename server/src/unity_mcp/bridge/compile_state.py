"""Moved to unity_mcp.reload.compile_state. This file keeps the old import
path alive as the SAME module object (not a copy), so
`patch("unity_mcp.bridge.compile_state.X")` and `from
unity_mcp.bridge.compile_state import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.reload.compile_state
directly."""
import sys

from ..reload import compile_state as _impl

sys.modules[__name__] = _impl
