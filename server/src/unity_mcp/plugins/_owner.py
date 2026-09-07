"""Scoped plugin identity + API-v1 metadata journal (N1a T3).

A tiny leaf module with no unity_mcp imports of its own. _atomic.py sets/reads
this around a plugin's module.register() call; plugin_api.py's API-v1
functions (register_read_cmds, register_write_cmds, register_tools,
register_dsl_tools, register_features) record every name they touch here.
Keeping this state in its own leaf module — instead of plugin_api.py reaching
into _atomic.py's globals, or vice versa — means neither module needs to
import the other, so no import cycle is introduced.

Registration is serialized and startup-only (no threading), so a single pair
of module-level globals is sufficient.
"""
from collections.abc import Iterable  # noqa: TC003

# Identity of the plugin currently inside its module.register() call.
# None outside of that call (including during host register_all()).
current: str | None = None

# Names declared via an API-v1 call while `current` is set. Cleared by
# begin(); read once by register_plugin_module()'s commit-time validation.
journal: set[str] = set()


def begin(identity: str) -> None:
    """Called by register_plugin_module() before module.register() runs."""
    global current
    current = identity
    journal.clear()


def end() -> None:
    """Called by register_plugin_module() once the journal has been read
    (success or failure). Does not clear the journal — begin() does that on
    the next call, so a caller inspecting `journal` right after end() still
    sees the just-finished plugin's declarations."""
    global current
    current = None


def record(names: Iterable[str]) -> None:
    """Record names touched by an API-v1 call. No-op for host-level calls
    (register_all(), where no plugin is currently registering)."""
    if current is not None:
        journal.update(names)
