from unity_mcp._preflight import run_preflight

run_preflight()

import asyncio
import contextlib
import logging
import os
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
        with contextlib.suppress(Exception):
            release_lock(lock_fd)
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

    t = threading.Thread(target=_loop, daemon=True, name="unity-biome-mcp-idle-watchdog")
    t.start()
    return t


from contextlib import asynccontextmanager, suppress

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
from .lockfile import acquire_lock, cleanup_stale_locks, release_lock
from .middleware import Middleware, wrap_send
from .plugins import load_plugins
from .server_filtering import (
    _strip_deferred_schemas,  # noqa: F401  # re-exported; tests import from unity_mcp.server
    install_initialized_hook,
    install_list_tools_filter,
)
from .server_filtering import (
    discover_port_with_retry as _discover_port_with_retry,
)
from .server_filtering import (
    filter_tools as _filter_tools_pure,
)
from .server_filtering import (
    push_catalog as _push_catalog,
)
from .server_filtering import (
    read_unity_port as _read_unity_port,
)
from .server_lifespan import build_middleware, init_budget, wire_circuit_breaker
from .tools import register_all
from .tools.animation import animation, animator, particle, timeline  # noqa: F401
from .tools.animator_intent_tool import animator_intent  # noqa: F401
from .tools.asset import asset, get_enabled_tools, material, prefab, project_settings, scriptable_object  # noqa: F401
from .tools.autobatch import configure_objects, set_properties, setup_objects  # noqa: F401
from .tools.batch import batch, references, validate_references  # noqa: F401
from .tools.code_intel import compile_preflight  # noqa: F401
from .tools.codegen import auto_fix, execute_code, get_schema, smart_build  # noqa: F401
from .tools.connection import list_connections, reconnect_unity  # noqa: F401
from .tools.console import get_compile_errors, get_console, recompile  # noqa: F401
from .tools.editor_control import checkpoint, editor  # noqa: F401
from .tools.meta import discover_tools, doctor, resolve_tool_schema, set_llm_config  # noqa: F401
from .tools.metrics_tool import get_metrics  # noqa: F401
from .tools.objects import (  # noqa: F401
    create_object,
    delete_object,
    find_objects,
    get_component,
    get_components_list,
    get_object_detail,
    inspect,
    manage_component,
    object_diff,
    set_active,
    set_material,
    set_parent,
    set_property,
    set_property_delta,
    set_sibling_index,
    unwire_event,
    wire_event,
)
from .tools.runtime import (  # noqa: F401
    export_playtest_aliases_to_defs,
    invoke_method,
    lint_playtest,
    lint_playtest_suite,
    move_to,
    query_state,
    run_playtest,
    run_playtest_suite,
    sync_playtest_aliases_from_defs,
    test_step,
    validate_playtest_aliases,
    wait_until,
)

# Re-exported for tests that import these from `unity_mcp.server` (split from advanced.py: F19).
# Re-export tool functions for test imports
from .tools.scene import (  # noqa: F401
    compress_hierarchy,
    fingerprint,
    get_changes,
    get_hierarchy,
    load_session,
    save_session,
    scene,
    scene_diff,
    screenshot_baseline,
    screenshot_compare,
    search_scene,
)
from .tools.screenshot import screenshot  # noqa: F401
from .tools.skills import (  # noqa: F401
    apply_template,
    list_skills,
    list_templates,
    save_skill,
    save_template,
    use_skill,
)
from .tools.spatial import (  # noqa: F401
    autofit_collider,
    check_colliders,
    get_spatial_context,
    scan_scene,
    spatial_query,
    validate_layout,
)
from .tools.testing import get_test_results, run_tests  # noqa: F401
from .tools.ui import create_ui, menu, set_rect, shader  # noqa: F401
from .tools.ui_intent_tool import ui_intent  # noqa: F401
from .tools.vfx_intent_tool import vfx_intent  # noqa: F401
from .tools.watch import get_watches, watch  # noqa: F401

# Disabled-tools state lives here so tests can mutate srv._disabled_tools_cache directly.
_disabled_tools_cache: set | None = None
_refresh_tools_lock: asyncio.Lock | None = None


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


async def _refresh_resources(bridge_) -> None:
    from .resources import refresh_dynamic
    await refresh_dynamic()


async def _warm_alias_cache(bridge_) -> None:
    """Seed _alias_cache from Unity alias table on connect/reconnect. Non-fatal."""
    if bridge_ is None or _middleware is None:
        return
    try:
        resp = await bridge_.send("get_aliases", {})
        if resp and resp.get("ok"):
            from .middleware_alias import parse_aliases_from_get_aliases
            _middleware._alias_cache = parse_aliases_from_get_aliases(resp.get("data", ""))
    except Exception:
        pass


async def _warm_cmd_flags(bridge_) -> None:
    """Augment WRITE_CMDS/_RUNTIME_ONLY_CMDS from C# capabilities. Non-fatal."""
    try:
        resp = await bridge_.send("get_capabilities", {}, timeout=5.0)
    except Exception:
        return  # TCP down — hardcoded baseline stays active
    data = resp.get("data", "") if resp else ""
    from .middleware_types import _RUNTIME_ONLY_CMDS, WRITE_CMDS
    for line in data.splitlines():
        if line.startswith("mutating_cmds:"):
            WRITE_CMDS.update(c for c in line[14:].split(",") if c)
        elif line.startswith("runtime_cmds:"):
            _RUNTIME_ONLY_CMDS.update(c for c in line[13:].split(",") if c)


async def _filter_tools(tools: list, bridge_) -> list:
    """Filter tools by gating then subtract disabled set (from Unity MCPSettings).
    Cache is None → gating-only fallback (no TCP call)."""
    return _filter_tools_pure(tools, _disabled_tools_cache)


from .timeout_categories import (
    TIMEOUT_CATEGORIES,
    get_timeout,
)

# Backward-compat alias used by tests that import COMMAND_TIMEOUTS from server.
COMMAND_TIMEOUTS = TIMEOUT_CATEGORIES

slot: ConnectionSlot | None = None
manager: ConnectionSlot | None = None  # backward-compat alias for tests/conftest
_middleware: Middleware | None = None
_wrapped_send = None
_budget_tracker = None
_budget_router = None


def _stdio_alive() -> bool:
    """Return False when the stdio transport pipe is broken (server restart needed).

    Non-stdio transports (http, sse) are unaffected — always returns True for them.
    """
    import sys
    if os.environ.get("UNITY_MCP_TRANSPORT", "stdio") != "stdio":
        return True
    try:
        sys.stdout.buffer.flush()
        return True
    except (BrokenPipeError, OSError):
        return False


async def _send_raw(cmd: str, args: dict, timeout: float = 0) -> str:
    if slot is None:
        raise ToolError("Server not initialized. Restart MCP server (/mcp).")
    from .tools.tool_specs import _SPECS
    spec = _SPECS.get(cmd)
    if spec is not None and spec.direct_only:
        raise ToolError(
            f"'{cmd}' is a Python-only control-plane tool and cannot be sent to Unity. "
            "Call it as a typed MCP tool."
        )
    if not _stdio_alive():
        raise ToolError(
            "[TRANSPORT_DEAD] stdio transport closed — restart the MCP server (/mcp)"
        )
    bridge = slot.bridge
    if bridge is None:
        raise ToolError("No Unity connection configured. Use reconnect_unity(port).")
    if timeout <= 0:
        timeout = get_timeout(cmd)
    probe = getattr(bridge, "_probe", None)
    try:
        result = await bridge.send(cmd, args, timeout=timeout)
    except asyncio.CancelledError:
        raise ToolError("Operation cancelled. Retry the command.") from None
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
    unity_port = await _discover_port_with_retry()
    cleanup_stale_locks(port=unity_port)
    from .lockfile import cleanup_stale_port_files as _cleanup_ports
    _cleanup_ports(tcp_probe=True)
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
                await _warm_alias_cache(active)
                await _warm_cmd_flags(active)
                await _push_catalog(active)
                await _refresh_resources(active)
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
                asyncio.ensure_future(_warm_alias_cache(slot.bridge))
                asyncio.ensure_future(_warm_cmd_flags(slot.bridge))
                asyncio.ensure_future(_push_catalog(slot.bridge))
                asyncio.ensure_future(_refresh_resources(slot.bridge))
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
            with suppress(Exception):
                await _middleware.watchdog.cancel()
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
# Install initialized hook — sends client name to Unity on MCP handshake.
install_initialized_hook(mcp, lambda: slot.bridge if slot else None)


def main():
    from .paths import migrate_data_dir
    migrate_data_dir()
    global _last_activity
    _last_activity = time.monotonic()
    _start_idle_watchdog()
    import signal
    if hasattr(signal, "SIGPIPE"):
        with suppress(OSError, ValueError):
            signal.signal(signal.SIGPIPE, signal.SIG_IGN)

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
        with suppress(OSError, ValueError):  # restricted environments
            signal.signal(signal.SIGTERM, _handle_sigterm)

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
