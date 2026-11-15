"""Moved to unity_mcp.reload.sync. This file keeps the old import path alive
as the SAME module object (not a copy), so `patch("unity_mcp.tools.sync.X")`
and `from unity_mcp.tools.sync import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.reload.sync directly."""
import sys

from ..reload import sync as _impl

sys.modules[__name__] = _impl
