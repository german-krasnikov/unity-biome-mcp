"""Atomic plugin registration (F3): a plugin's register() call is all-or-nothing.

Snapshots every registry plugin_api.py can mutate before a plugin's register()
runs, and rolls all of them back on any exception (including a duplicate-name
collision, raised via _GuardedMcp instead of silently no-oping). Mirrors the
C# PluginRegistry/CommandRegistry snapshot-and-restore shape. See
Plans/PR-03.md for the full design.
"""
import logging
from dataclasses import dataclass, field

log = logging.getLogger(__name__)

# Tool name -> identity that owns it. Persists across register_plugin_module
# calls so a same-identity reload is recognized as "not a collision".
_command_owner: dict[str, str] = {}

# (identity, error message) for plugins that failed on their most recent
# register_plugin_module call.
_failed: list[tuple[str, str]] = []


class PluginLoadError(RuntimeError):
    """Raised by _GuardedMcp before a duplicate tool name reaches FastMCP.
    Caught by register_plugin_module exactly like any other exception raised
    from module.register()."""


@dataclass
class _RegistrySnapshot:
    tools: dict
    read_cmds: set
    write_cmds: set
    dsl_tools: set
    all_known: set
    themed: dict = field(default_factory=dict)
    categories: dict = field(default_factory=dict)
    features: dict = field(default_factory=dict)
    owners: dict = field(default_factory=dict)


def _capture(mcp) -> _RegistrySnapshot:
    from unity_mcp.budget.registry import FEATURES
    from unity_mcp.middleware import READ_CMDS, WRITE_CMDS
    from unity_mcp.tools import gating
    from unity_mcp.tools.batch import _dsl_tools

    return _RegistrySnapshot(
        tools=dict(mcp._tool_manager._tools),
        read_cmds=set(READ_CMDS),
        write_cmds=set(WRITE_CMDS),
        dsl_tools=set(_dsl_tools),
        all_known=set(gating._ALL_KNOWN),
        themed={k: list(v) for k, v in gating._THEMED_CATEGORIES.items()},
        categories={k: set(v) for k, v in gating.CATEGORIES.items()},
        features=dict(FEATURES),
        owners=dict(_command_owner),
    )


def _restore(mcp, snap: _RegistrySnapshot) -> None:
    """Wholesale restore: clear each live container, repopulate from the
    snapshot. Undoes both "plugin added a new entry" and "plugin overwrote an
    existing value" with the same code path."""
    from unity_mcp.budget.registry import FEATURES
    from unity_mcp.middleware import READ_CMDS, WRITE_CMDS
    from unity_mcp.tools import gating
    from unity_mcp.tools.batch import _dsl_tools

    mcp._tool_manager._tools.clear()
    mcp._tool_manager._tools.update(snap.tools)
    READ_CMDS.clear()
    READ_CMDS.update(snap.read_cmds)
    WRITE_CMDS.clear()
    WRITE_CMDS.update(snap.write_cmds)
    _dsl_tools.clear()
    _dsl_tools.update(snap.dsl_tools)
    gating._ALL_KNOWN.clear()
    gating._ALL_KNOWN.update(snap.all_known)
    gating._THEMED_CATEGORIES.clear()
    gating._THEMED_CATEGORIES.update(snap.themed)
    gating.CATEGORIES = snap.categories
    FEATURES.clear()
    FEATURES.update(snap.features)
    _command_owner.clear()
    _command_owner.update(snap.owners)


class _GuardedMcp:
    """Wraps mcp for exactly one plugin's register() call. .tool(**kwargs)
    raises PluginLoadError before delegating to the real mcp.tool() whenever
    the target name is already owned by a DIFFERENT identity. Same-identity
    re-registration (this exact plugin loading twice) passes through
    unchanged."""

    def __init__(self, mcp, identity: str, owners: dict):
        self._mcp = mcp
        self._identity = identity
        self._owners = owners

    def tool(self, **kwargs):
        def decorator(fn):
            name = kwargs.get("name") or fn.__name__
            owner = self._owners.get(name)
            if owner is not None and owner != self._identity:
                raise PluginLoadError(f"duplicate command '{name}': owned by '{owner}'")
            result = self._mcp.tool(**kwargs)(fn)
            if name not in self._mcp._tool_manager._tools:
                return result
            self._owners[name] = self._identity
            return result
        return decorator

    def __getattr__(self, name):
        return getattr(self._mcp, name)


def register_plugin_module(module, identity: str, mcp, send, args) -> bool:
    """Unified entry point for all 3 discovery sources. `identity` is the
    loader-assigned name (built-in module name / entry_point.name / plugin_dir
    module name) — used as the ownership key and as the failed-plugin label.
    Returns True on success, False on failure (already logged + recorded)."""
    from unity_mcp.plugins import _auto_gate_new_tools

    if not hasattr(module, "register"):
        return False
    snap = _capture(mcp)
    guarded = _GuardedMcp(mcp, identity, _command_owner)
    try:
        module.register(guarded, send, args)
    except Exception as e:
        _restore(mcp, snap)
        _failed.append((identity, str(e)))
        log.warning(f"Plugin {identity} skipped: {e}")
        return False
    _auto_gate_new_tools(mcp, snap.tools.keys())
    log.info(f"Plugin loaded: {identity}")
    return True


def get_failed_plugins() -> list[tuple[str, str]]:
    """(identity, error message) for every plugin that failed on the most
    recent load pass. Mirrors PluginRegistry.GetFailedPlugins() in C#."""
    return _failed
