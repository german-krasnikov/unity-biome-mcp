"""Moved to unity_mcp.reload.sync_spec. This file keeps the old import path
alive as the SAME module object (not a copy), so
`patch("unity_mcp.tools.sync_spec.X")` and `from unity_mcp.tools.sync_spec
import X` both still observe/mutate the real module. Delete once every caller
imports unity_mcp.reload.sync_spec directly."""
import sys

from ..reload import sync_spec as _impl

sys.modules[__name__] = _impl
