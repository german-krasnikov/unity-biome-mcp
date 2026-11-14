"""Moved to unity_mcp.bridge.retry. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.bridge_retry.X")`
and `from unity_mcp.bridge_retry import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.bridge.retry directly."""
import sys

from .bridge import retry as _impl

sys.modules[__name__] = _impl
