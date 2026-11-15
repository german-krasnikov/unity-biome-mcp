"""Moved to unity_mcp.bridge.unity_state. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.unity_state.X")`
and `from unity_mcp.unity_state import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.bridge.unity_state directly."""
import sys

from .bridge import unity_state as _impl

sys.modules[__name__] = _impl
