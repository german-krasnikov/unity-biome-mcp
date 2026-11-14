"""Moved to unity_mcp.bridge.socket_io. This file keeps the old import path
alive as the SAME module object (not a copy), so `patch("unity_mcp.bridge_socket.X")`
and `from unity_mcp.bridge_socket import X` both still observe/mutate the real
module. Delete once every caller imports unity_mcp.bridge.socket_io directly."""
import sys

from .bridge import socket_io as _impl

sys.modules[__name__] = _impl
