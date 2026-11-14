"""Moved to unity_mcp.bridge.phantom_cleaner. This file keeps the old import
path alive as the SAME module object (not a copy), so
`patch("unity_mcp.phantom_cleaner.X")` and `from unity_mcp.phantom_cleaner
import X` both still observe/mutate the real module. Delete once every caller
imports unity_mcp.bridge.phantom_cleaner directly."""
import sys

from .bridge import phantom_cleaner as _impl

sys.modules[__name__] = _impl
