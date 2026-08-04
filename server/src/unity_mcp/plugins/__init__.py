import logging
import os
import pkgutil
import sys
from importlib import import_module

log = logging.getLogger(__name__)

_SKIP = [p for p in os.environ.get("UNITY_MCP_SKIP_PLUGINS", "").split(",") if p]


def load_plugins(mcp, send_fn, args_fn):
    """Load plugins from 3 sources: built-in, entry_points, UNITY_MCP_PLUGIN_DIRS."""
    # 1. Built-in plugins (this package)
    for _finder, name, _ispkg in pkgutil.iter_modules(__path__):
        if name.startswith("_"):
            continue
        if _should_skip(name):
            continue
        _load_module(f"{__name__}.{name}", name, mcp, send_fn, args_fn)

    # 2. entry_points (pip-installed plugins)
    _load_entry_points(mcp, send_fn, args_fn)

    # 3. UNITY_MCP_PLUGIN_DIRS (local dev plugins)
    _load_plugin_dirs(mcp, send_fn, args_fn)


def _should_skip(name):
    if any(name.startswith(p) for p in _SKIP):
        log.info(f"Plugin skipped (UNITY_MCP_SKIP_PLUGINS): {name}")
        return True
    return False


def _auto_gate_new_tools(mcp, before: set) -> None:
    """Any tool name added to mcp's registry during a plugin's register() call
    that the plugin did NOT self-declare via register_tools() gets auto-enrolled
    into the hidden 'plugins' category (Issue 26 — plugin tools default OFF Tier1)."""
    from unity_mcp.tools import gating
    after = set(mcp._tool_manager._tools.keys())
    new_unclaimed = (after - before) - gating._ALL_KNOWN
    if new_unclaimed:
        gating.register_tools("plugins", new_unclaimed)
        log.info(
            f"Plugin tools auto-gated (hidden by default, "
            f"see discover_tools(category='plugins')): {sorted(new_unclaimed)}"
        )


def _load_module(fqn, name, mcp, send_fn, args_fn):
    try:
        module = import_module(fqn)
        if hasattr(module, 'register'):
            before = set(mcp._tool_manager._tools.keys())
            module.register(mcp, send_fn, args_fn)
            _auto_gate_new_tools(mcp, before)
            log.info(f"Plugin loaded: {name}")
    except Exception as e:
        log.warning(f"Plugin {name} skipped: {e}")


def _check_api_version(module, name):
    from unity_mcp.plugin_api import API_VERSION
    v = getattr(module, 'REQUIRED_API_VERSION', None)
    if v is not None and v > API_VERSION:
        log.warning(f"Plugin {name} requires API v{v}, server has v{API_VERSION} — skipped")
        return False
    return True


def _load_entry_points(mcp, send_fn, args_fn):
    try:
        from importlib.metadata import entry_points
        eps = entry_points(group="unity_mcp.plugins")
        for ep in eps:
            if _should_skip(ep.name):
                continue
            try:
                plugin = ep.load()
                if not _check_api_version(plugin, ep.name):
                    continue
                if hasattr(plugin, 'register'):
                    before = set(mcp._tool_manager._tools.keys())
                    plugin.register(mcp, send_fn, args_fn)
                    _auto_gate_new_tools(mcp, before)
                    log.info(f"Plugin loaded (entry_point): {ep.name}")
            except Exception as e:
                log.warning(f"Plugin {ep.name} (entry_point) skipped: {e}")
    except Exception as e:
        log.debug(f"entry_points discovery skipped: {e}")


def _load_plugin_dirs(mcp, send_fn, args_fn):
    dirs = os.environ.get("UNITY_MCP_PLUGIN_DIRS", "")
    if not dirs:
        return
    for d in dirs.split(os.pathsep):
        d = d.strip()
        if not d or not os.path.isdir(d):
            continue
        if d not in sys.path:
            sys.path.append(d)
        for _finder, name, _ispkg in pkgutil.iter_modules([d]):
            if name.startswith("_") or _should_skip(name):
                continue
            try:
                module = import_module(name)
                if not _check_api_version(module, name):
                    continue
                if hasattr(module, 'register'):
                    before = set(mcp._tool_manager._tools.keys())
                    module.register(mcp, send_fn, args_fn)
                    _auto_gate_new_tools(mcp, before)
                    log.info(f"Plugin loaded (plugin_dirs): {name}")
            except Exception as e:
                log.warning(f"Plugin {name} (plugin_dirs) skipped: {e}")
