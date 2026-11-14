"""Moved to unity_mcp.reload.reload_ladder. This file keeps the old import
path alive as the SAME module object (not a copy), so
`patch("unity_mcp.tools.reload_ladder.X")` and `from
unity_mcp.tools.reload_ladder import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.reload.reload_ladder
directly."""
import sys

from ..reload import reload_ladder as _impl

sys.modules[__name__] = _impl
