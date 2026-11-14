"""Moved to unity_mcp.bridge.connection_slot. This file keeps the old import
path alive as the SAME module object (not a copy), so
`patch("unity_mcp.connection_slot.X")` and `from unity_mcp.connection_slot
import X` both still observe/mutate the real module. Delete once every
caller imports unity_mcp.bridge.connection_slot directly."""
import sys

from .bridge import connection_slot as _impl

sys.modules[__name__] = _impl
