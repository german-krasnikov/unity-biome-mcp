"""Moved to unity_mcp.bridge.cassette. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.bridge_cassette.X")`
and `from unity_mcp.bridge_cassette import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.bridge.cassette directly."""
import sys

from .bridge import cassette as _impl

sys.modules[__name__] = _impl
