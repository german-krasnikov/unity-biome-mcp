"""Moved to unity_mcp.bridge.crash_log. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.crash_log.X")`
and `from unity_mcp.crash_log import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.bridge.crash_log directly."""
import sys

from .bridge import crash_log as _impl

sys.modules[__name__] = _impl
