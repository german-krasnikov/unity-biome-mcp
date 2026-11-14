"""Moved to unity_mcp.bridge.lockfile. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.lockfile.X")`
and `from unity_mcp.lockfile import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.bridge.lockfile directly."""
import sys

from .bridge import lockfile as _impl

sys.modules[__name__] = _impl
