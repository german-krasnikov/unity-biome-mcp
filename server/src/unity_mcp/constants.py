"""Shared constants for unity_mcp. Zero imports from unity_mcp modules."""
import os

DEFAULT_PORT: int = 9500

# Single source for the ~120s "how long can a legit Unity domain reload/compile
# take before we stop trusting/waiting" window. Every module that used to
# hardcode its own 120.0 copy (bridge_reload_state.DOMAIN_RELOAD_EXPIRY_S,
# compile_state._DISCONNECT_WINDOW_S, unity_state._STALE_SECONDS,
# tools/sync._DEFAULT_TIMEOUT) now imports this instead — one env var controls
# all of them together.
SESSION_TIMEOUT: float = float(os.environ.get("UNITY_MCP_SESSION_TIMEOUT", "120.0"))

# Exact text CommandRouter.CheckGuards emits (CommandRouter.cs FormatBusyResponse
# call site) when a command is rejected because Unity is mid-compile. Matched
# literally (not a loose substring) so tools/sync.py can tell "our command hit
# the compile guard" apart from any other free-text mention of "compiling".
SYNC_COMPILE_GUARD_TEXT: str = "Unity is compiling. Retry in 5s."
