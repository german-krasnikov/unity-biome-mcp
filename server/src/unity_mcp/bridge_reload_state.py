"""Moved to unity_mcp.bridge.reload_state. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.bridge_reload_state.X")`
and `from unity_mcp.bridge_reload_state import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.bridge.reload_state directly."""
import sys

from .bridge import reload_state as _impl

sys.modules[__name__] = _impl
