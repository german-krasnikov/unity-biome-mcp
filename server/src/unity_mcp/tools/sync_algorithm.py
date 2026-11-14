"""Moved to unity_mcp.reload.sync_algorithm. This file keeps the old import
path alive as the SAME module object (not a copy), so
`patch("unity_mcp.tools.sync_algorithm.X")` and `from
unity_mcp.tools.sync_algorithm import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.reload.sync_algorithm
directly."""
import sys

from ..reload import sync_algorithm as _impl

sys.modules[__name__] = _impl
