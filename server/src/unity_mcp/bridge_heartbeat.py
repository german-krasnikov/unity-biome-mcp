"""Moved to unity_mcp.bridge.heartbeat. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.bridge_heartbeat.X")`
and `from unity_mcp.bridge_heartbeat import X` both still observe/mutate the
real module. Delete once every caller imports unity_mcp.bridge.heartbeat
directly."""
import sys

from .bridge import heartbeat as _impl

sys.modules[__name__] = _impl
