from unity_mcp._preflight import run_preflight
run_preflight()

import asyncio
import logging
import os
import sys
import threading
import time
os.environ.setdefault("UNITY_MCP_DISTILL", "1")
log = logging.getLogger("unity_mcp.server")

# SIGTERM graceful shutdown state — populated from lifespan, read by signal handler.
_sigterm_state: dict = {"loop": None, "task": None, "requested": False, "lock_fd": None}


def _handle_sigterm(signum, frame) -> None:
    """SIGTERM handler: release lockfile synchronously, then os._exit(0).

    Extracted to module level so it can be unit-tested without running main().
    The async lifespan finally cannot be used — anyio's stdio_server blocks on
    to_thread.run_sync(readline, cancellable=False), which ignores task
    cancellation while stdin is open.
    """
    if _sigterm_state["requested"]:
        return
    _sigterm_state["requested"] = True
    lock_fd = _sigterm_state.get("lock_fd")
    if lock_fd is not None:
        try:
            release_lock(lock_fd)
        except Exception:
            pass
        _sigterm_state["lock_fd"] = None
    os._exit(0)

# --- Idle watchdog ---
_last_activity: float = time.monotonic()


def _touch_activity() -> None:
    """Update last-activity timestamp. Call before every MCP tool dispatch."""
    global _last_activity
    _last_activity = time.monotonic()


_watchdog_stop: threading.Event = threading.Event()


def _start_idle_watchdog() -> threading.Thread | None:
    """Start daemon thread that calls os._exit(0) after UNITY_MCP_IDLE_TIMEOUT seconds of inactivity.
    Returns None if timeout=0 (disabled)."""
    timeout = int(os.environ.get("UNITY_MCP_IDLE_TIMEOUT", "300"))
    if timeout <= 0:
        return None

    _watchdog_stop.clear()

    def _loop():
        # N2: check stop event for clean exit in tests (no unhandled exception warnings).
        while not _watchdog_stop.is_set():
            time.sleep(30)
            if _watchdog_stop.is_set():
                return
            idle = time.monotonic() - _last_activity
            if idle > timeout:
                from .bridge_heartbeat import _ORIGINAL_PPID
                if os.getppid() == _ORIGINAL_PPID:
                    continue  # parent alive — don't kill; remain as orphan-reaper only
                log.warning("idle watchdog: parent dead + idle=%.0fs >= timeout=%ds, exiting", idle, timeout)
                logging.shutdown()
                os._exit(0)

    t = threading.Thread(target=_loop, daemon=True, name="unity-mcp-idle-watchdog")
    t.start()
    return t


from contextlib import asynccontextmanager
from mcp.server.fastmcp import FastMCP


class _UnstructuredMCP(FastMCP):
    def add_tool(self, fn, name=None, title=None, description=None,
                 annotations=None, icons=None, meta=None,
                 structured_output=None) -> None:
        super().add_tool(fn, name=name, title=title, description=description,
                         annotations=annotations, icons=icons, meta=meta,
                         structured_output=False)


from mcp.server.fastmcp.exceptions import ToolError
from .bridge_result import unwrap_bridge_result
from .connection_slot import ConnectionSlot
from .lockfile import acquire_lock, release_lock, cleanup_stale_locks
from .plugins import load_plugins
from .tools import register_all
from .server_filtering import (
    _strip_deferred_schemas,
    push_catalog as _push_catalog,
    filter_tools as _filter_tools_pure,
    install_list_tools_filter,
    read_unity_port as _read_unity_port,
)
from .server_lifespan import build_middleware, init_budget, wire_circuit_breaker

# Re-export tool functions for test imports
from .tools.scene import (
    compress_hierarchy, get_hierarchy, scene, search_scene,
    fingerprint, get_changes, scene_diff, save_session, load_session,
    screenshot_baseline, screenshot_compare,
)
from .tools.console import get_console, get_compile_errors, recompile
from .tools.screenshot import screenshot
from .tools.testing import run_tests, get_test_results
from .tools.editor_control import editor, checkpoint
from .tools.objects import (
    get_component, inspect, get_components_list, find_objects,
    set_property, create_object, set_active, wire_event, unwire_event,
    delete_object, manage_component, get_object_detail, set_material,
    set_property_delta, set_parent, object_diff, set_sibling_index,
)
from .tools.asset import (
    asset, project_settings, material, prefab, scriptable_object, get_enabled_tools,
)
from .tools.animation import animation, timeline, animator, particle
# Re-exported for tests that import these from `unity_mcp.server` (split from advanced.py: F19).
from .tools.batch import batch, references, validate_references
from .tools.codegen import execute_code, get_schema, auto_fix, smart_build
from .tools.skills import save_skill, use_skill, list_skills, apply_template, save_template, list_templates
from .tools.spatial import validate_layout, get_spatial_context, scan_scene, check_colliders, autofit_collider, spatial_query
from .tools.ui import create_ui, set_rect, menu, shader
from .tools.connection import list_connections, reconnect_unity
from .tools.runtime import invoke_method, set_runtime_property, wait_until, move_to, query_state, test_step, run_playtest, fuzz_playtest
from .tools.watch import watch, get_watches
from .tools.autobatch import setup_objects, set_properties, configure_objects
from .tools.code_intel import compile_preflight
from .tools.animator_intent_tool import animator_intent
from .tools.vfx_intent_tool import vfx_intent
from .tools.ui_intent_tool import ui_intent
from .tools.metrics_tool import get_metrics
from .tools.meta import discover_tools, doctor, resolve_tool_schema, set_llm_config
from .middleware import wrap_send, Middleware

from typing import Optional

# Disabled-tools state lives here so tests can mutate srv._disabled_tools_cache directly.
_disabled_tools_cache: Optional[set] = None
_refresh_tools_lock: Optional[asyncio.Lock] = None


async def _refresh_tools_cache(bridge_) -> None:
    """Fetch disabled tools from Unity and populate cache. Called on connect/reconnect.

    Idempotent: if already refreshing, skip. Failures are silent — stale cache
    is acceptable until next successful reconnect.

    Fix 4: when the disabled set actually changes, notify the live MCP session
    (if one was captured during a prior ListTools call) so the client re-fetches
    ListTools instead of keeping a stale list around after a manual reconnect.
    """
    global _disabled_tools_cache, _refresh_tools_lock
    if _refresh_tools_lock is None:
        _refresh_tools_lock = asyncio.Lock()
    if _refresh_tools_lock.locked():
        return  # another refresh in flight — skip
    if bridge_ is None or not bridge_.connected:
        return
    async with _refresh_tools_lock:
        try:
            result = await bridge_.send("get_disabled_tools", {}, timeout=5.0)
            if result.get("ok"):
                data = result.get("data", "").strip()
                new_cache = set(data.split(",")) if data else set()
                changed = new_cache != _disabled_tools_cache
                _disabled_tools_cache = new_cache
                if changed:
                    from . import server_filtering
                    session = server_filtering.get_active_session()
                    if session is not None:
                        await session.send_tool_list_changed()
        except Exception:
            pass


async def _filter_tools(tools: list, bridge_) -> list:
    """Filter tools by gating then subtract disabled set (from Unity MCPSettings).
    Cache is None → gating-only fallback (no TCP call)."""
    return _filter_tools_pure(tools, _disabled_tools_cache)


from .timeout_categories import (
    DEFAULT_TIMEOUT,
    TIMEOUT_CATEGORIES,
    get_timeout,
)

# Backward-compat alias used by tests that import COMMAND_TIMEOUTS from server.
COMMAND_TIMEOUTS = TIMEOUT_CATEGORIES

slot: Optional[ConnectionSlot] = None
manager: Optional[ConnectionSlot] = None  # backward-compat alias for tests/conftest
_middleware: Optional[Middleware] = None
_wrapped_send = None
_budget_tracker = None
_budget_router = None


async def _send_raw(cmd: str, args: dict, timeout: float = 0) -> str:
    if slot is None:
        raise ToolError("Server not initialized. Restart MCP server (/mcp).")
    bridge = slot.bridge
    if bridge is None:
        raise ToolError("No Unity connection configured. Use reconnect_unity(port).")
    if timeout <= 0:
        timeout = get_timeout(cmd)
    probe = getattr(bridge, "_probe", None)
    try:
        result = await bridge.send(cmd, args, timeout=timeout)
    except asyncio.CancelledError:
        raise ToolError("Operation cancelled. Retry the command.")
    except (ConnectionError, TimeoutError, OSError) as e:
        ue = getattr(e, "unity_error", None)
        if ue is None:
            try:
                from .errors import classify_failure
                probe_busy = probe.has_strong_busy_signal() if probe else False
                rem = probe.estimated_remaining_s() if probe else 0.0
                ue = classify_failure(e, probe_busy, rem)
            except Exception:
                ue = None
        if ue is not None:
            raise ToolError(
                f"[UNITY_UNAVAILABLE] state={ue.unity_state} transient={ue.is_transient} "
                f"retry_after={ue.retry_after_seconds}s | {ue.message}"
            ) from e
        raise ToolError(f"Unity connection lost: {e}. Retry or /mcp to reconnect.") from e
    except Exception as e:
        raise ToolError(f"Unexpected error: {type(e).__name__}: {e}") from e
    text, ok = unwrap_bridge_result(result)
    if not ok:
        raise ToolError(text)
    return text


async def _send(cmd: str, args: dict, timeout: float = 0) -> str:
    _touch_activity()
    if _wrapped_send is not None:
        return await _wrapped_send(cmd, args, timeout=timeout)
    return await _send_raw(cmd, args, timeout)


def _args(**kwargs) -> dict:
    return {k: v for k, v in kwargs.items() if v is not None}


@asynccontextmanager
async def lifespan(app):
    global slot, manager, _middleware, _wrapped_send, _budget_tracker, _budget_router
    # Register loop + task so the SIGTERM handler can cancel us and find the lock.
    _sigterm_state["loop"] = asyncio.get_event_loop()
    _sigterm_state["task"] = asyncio.current_task()
    try:
        unity_port = _read_unity_port()
    except (ValueError, OSError):
        from .constants import DEFAULT_PORT
        unity_port = DEFAULT_PORT
    cleanup_stale_locks(port=unity_port)
    from .server_filtering import cleanup_stale_port_files as _cleanup_ports
    _cleanup_ports()
    lock_fd = acquire_lock(port=unity_port)  # raises on failure — do not swallow
    _sigterm_state["lock_fd"] = lock_fd  # expose for SIGTERM synchronous release

    def _on_port_change(old_port: int, new_port: int):
        nonlocal lock_fd
        # Atomic: acquire new lock BEFORE releasing old — no lockless window.
        try:
            new_fd = acquire_lock(port=new_port)
        except Exception as exc:
            log.warning("Failed to acquire lock for new port %d — keeping old: %s", new_port, exc)
            return
        old_fd, lock_fd = lock_fd, new_fd
        _sigterm_state["lock_fd"] = lock_fd
        try:
            release_lock(old_fd)
        except Exception as exc:
            log.warning("Failed to release old port lock fd=%s: %s", old_fd, exc)

    import threading
    def _bg_update_check():
        try:
            from unity_mcp._update_check import check_for_update, format_update_banner
            new = check_for_update()
            if new:
                log.info(format_update_banner(new))
        except Exception:
            pass
    threading.Thread(target=_bg_update_check, daemon=True).start()

    try:
        from .tools._annotations import retry_safe_cmds
        _retry_safe = await retry_safe_cmds(mcp)
        slot = ConnectionSlot(
            port_discoverer=_read_unity_port,
            on_port_change=_on_port_change,
            is_retry_safe=lambda cmd: cmd in _retry_safe,
        )
        manager = slot  # backward-compat alias
        _middleware = build_middleware(_send_raw)
        _budget_tracker, _budget_router = init_budget(_middleware)
        if _budget_tracker is not None:
            from .tools import budget_tool as _bt
            _bt._tracker = _budget_tracker
        global _wrapped_send
        if _middleware is not None:
            _wrapped_send = wrap_send(_send_raw, _middleware)
        await slot.connect(unity_port)
        from . import editor_log as _editor_log
        _editor_log.init_corroboration(port=unity_port)
        active = slot.bridge
        if active is not None:
            if active.connected:
                await _refresh_tools_cache(active)
                await _push_catalog(active)
            _last_refresh_ts: float = 0.0

            from .tools.sync import _reset_bump_used as _sync_reset_bump

            def _on_reconnect():
                nonlocal _last_refresh_ts
                now = time.monotonic()
                # P9: re-resolve project path on reconnect — may be a different Unity instance.
                # Read slot.port live (not the `unity_port` closed over at startup) — an
                # automatic port-drift reconnect otherwise keeps corroborating the OLD project.
                _editor_log.init_corroboration(port=slot.port)
                if now - _last_refresh_ts < 30.0:
                    return
                _last_refresh_ts = now
                asyncio.ensure_future(_refresh_tools_cache(slot.bridge))
                asyncio.ensure_future(_push_catalog(slot.bridge))
            slot.add_reconnect_callback(_on_reconnect)
            slot.add_reconnect_callback(_sync_reset_bump)
            # gating.reset() is intentionally NOT wired here — automatic heartbeat
            # reconnects (incl. domain-reload of the SAME project) would otherwise
            # wipe discover_tools() unlocks on every recompile. Manual reconnects
            # (explicit user action, possibly a different project) reset gating
            # from tools/connection.py:reconnect_unity() instead.
            if _middleware is not None:
                slot.add_reconnect_callback(_middleware.reset_session)
                wire_circuit_breaker(_middleware, active)
            active.start_heartbeat()
        yield
    finally:
        _wrapped_send = None
        if slot and slot.bridge:
            slot.bridge.stop_heartbeat()
        if _middleware and _middleware.watchdog:
            try:
                await _middleware.watchdog.cancel()
            except Exception:
                pass
        if slot:
            await slot.close()
        if lock_fd is not None:
            release_lock(lock_fd)


mcp = _UnstructuredMCP("UnityMCP", lifespan=lifespan)

register_all(mcp, _send, _args, get_slot=lambda: slot,
             get_middleware=lambda: _middleware,
             refresh_tools_cache=_refresh_tools_cache,
             push_catalog=_push_catalog)
load_plugins(mcp, _send, _args)


from .resources import register as register_resources
register_resources(mcp, _send, _args)


# Install filtering handler — captures schemas + applies gating + disabled-set.
install_list_tools_filter(mcp, lambda: _disabled_tools_cache)


def main():
    global _last_activity
    _last_activity = time.monotonic()
    _start_idle_watchdog()
    import signal
    if hasattr(signal, "SIGPIPE"):
        try:
            signal.signal(signal.SIGPIPE, signal.SIG_IGN)
        except (OSError, ValueError):
            pass

    # SIGTERM -> graceful shutdown.
    # POSIX only: on Windows, TerminateProcess (taskkill) does NOT invoke Python
    # signal handlers, so this path is unreachable on Windows. The Windows path
    # relies on parent-death / stdio-close to trigger lifespan cleanup.
    #
    # The handler releases the lockfile synchronously (the critical operation that
    # stop_server polls for), then calls os._exit(0). The async lifespan finally
    # block cannot be used because anyio's stdio_server uses to_thread.run_sync
    # (without cancellable=True) for readline(), which deadlocks under task
    # cancellation when stdin is still open.
    if hasattr(signal, "SIGTERM"):
        try:
            signal.signal(signal.SIGTERM, _handle_sigterm)
        except (OSError, ValueError):
            pass  # restricted environments

    from unity_mcp.crash_log import log_crash
    transport = os.environ.get("UNITY_MCP_TRANSPORT", "stdio")
    try:
        try:
            if transport == "http":
                port = int(os.environ.get("UNITY_MCP_HTTP_PORT", "8765"))
                mcp.run(transport="streamable-http", host="127.0.0.1", port=port)
            else:
                mcp.run(transport="stdio")
        except BrokenPipeError:
            pass
        except OSError as e:
            import errno
            if e.errno != errno.EPIPE:
                raise
    except (KeyboardInterrupt, SystemExit, asyncio.CancelledError):
        pass  # graceful shutdown paths: Ctrl-C, SystemExit, SIGTERM-cancel
    except BaseException as exc:
        log_crash(exc)
        raise


if __name__ == "__main__":
    main()
