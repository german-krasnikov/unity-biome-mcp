"""Tool filtering pipeline: gating, schema stripping, port discovery.

Pure, stateless helpers. State (_disabled_tools_cache, _refresh_tools_lock)
lives in server.py so tests can mutate srv._disabled_tools_cache directly.
"""
import contextlib
import logging
import os
import socket
import weakref
from pathlib import Path  # noqa: F401  # re-exported; tests mock unity_mcp.server_filtering.Path
from typing import TYPE_CHECKING

from .constants import DEFAULT_PORT
from .lockfile import is_pid_alive as _is_pid_alive
from .paths import iter_port_files as _iter_port_files
from .paths import ports_dir as _ports_dir
from .tools.gating import _CORE_TOOLS, filter_by_tier, get_catalog
from .tools.schema_registry import STUB_SCHEMA
from .tools.schema_registry import _registry as _schema_registry

if TYPE_CHECKING:
    import asyncio

# Core tools keep full schemas; all others get stub schema on ListTools.
_SCHEMA_KEEP_FULL_EXTRA: frozenset[str] = frozenset({
    "run_playtest", "run_playtest_suite", "run_tests", "run_tests_wait",
    "resolve_tool_schema", "discover_tools",
    # MCP091-004 / MCP091-012: required-param TIER1 direct_only tools
    "get_console_since", "scene", "await_compile", "console_mark", "screenshot",
    # MCP091-PARTIAL: additional TIER1 tools with required params
    "compile_preflight", "search_scene", "set_active", "set_parent", "validate_references",
    # MCP091: sync_unity missing — must expose schema so resolve/bump params are visible
    "sync_unity",
    # B3: MEDIA tools with required params (path, action) must not receive stub schema
    "timeline", "animation", "animator",
    # B4: SYSTEM tool with label param; sub-agents need full schema to construct calls
    "checkpoint",
    # Step 1: intent tools promoted to tier1 — must expose full schema
    "ui_intent", "vfx_intent", "uitk_intent",
})
_SCHEMA_KEEP_FULL: frozenset[str] = _CORE_TOOLS | _SCHEMA_KEEP_FULL_EXTRA

# Non-core tool descriptions are truncated to this length in the initial
# ListTools response; full text remains available via resolve_tool_schema.
_SHORT_DESC_MAX_LEN = 120


def _short_description(desc: str) -> str:
    """First sentence or hard-truncate at _SHORT_DESC_MAX_LEN."""
    if not desc or len(desc) <= _SHORT_DESC_MAX_LEN:
        return desc
    dot = desc.find('. ')
    if 0 < dot < _SHORT_DESC_MAX_LEN:
        return desc[:dot + 1]
    return desc[:_SHORT_DESC_MAX_LEN] + '…'


def _apply_gating(tools: list) -> list:
    if os.environ.get("UNITY_MCP_NO_GATING"):
        return tools
    return filter_by_tier(tools)


def _strip_deferred_schemas(tools: list) -> list:
    """Replace inputSchema + shorten description of non-core tools, unless
    UNITY_MCP_FULL_SCHEMAS=1.

    Safe: these are ListTools *response* objects, separate from mcp._tool_manager._tools
    which holds the callable fn. FastMCP dispatches via _tool_manager (validate_input=False
    path), so stripping inputSchema here cannot block tool execution.

    Full description is captured into _schema_registry BEFORE this runs (see
    install_list_tools_filter), so resolve_tool_schema still serves the untruncated text.
    """
    if os.environ.get("UNITY_MCP_FULL_SCHEMAS", "0") == "1":
        return tools
    for t in tools:
        if t.name not in _SCHEMA_KEEP_FULL:
            t.inputSchema = STUB_SCHEMA
            t.description = _short_description(t.description)
    return tools


_push_catalog_lock: "asyncio.Lock | None" = None


async def push_catalog(bridge_) -> None:
    """Push the Python-authoritative tool catalog to Unity on connect/reconnect.

    Silent on failure — Unity can still operate with stale/no catalog.
    Skip-if-locked: prevents parallel invocations from piling up.
    """
    import asyncio
    global _push_catalog_lock
    if _push_catalog_lock is None:
        _push_catalog_lock = asyncio.Lock()
    if _push_catalog_lock.locked():
        return
    if bridge_ is None or not bridge_.connected:
        return
    async with _push_catalog_lock:
        try:
            categories = get_catalog()["categories"]
            catalog_str = "\n".join(
                f"{cat}:{','.join(tools)}"
                for cat, tools in categories.items()
                if tools
            )
            await bridge_.send("set_tool_catalog", {"catalog": catalog_str}, timeout=5.0)
        except Exception:
            pass


def filter_tools(tools: list, disabled: set | None) -> list:
    """Pure filter: gating → disabled-set subtraction → schema strip.
    disabled=None → gating-only fallback.
    """
    result = _apply_gating(tools)
    if disabled is not None:
        result = [t for t in result if t.name not in disabled or t.name in _CORE_TOOLS]
    return _strip_deferred_schemas(result)



def cleanup_stale_port_files() -> int:
    """Delete *.reload-port files whose PID is no longer alive. Returns count cleaned."""
    ports_dir = _ports_dir()
    if not ports_dir.exists():
        return 0
    cleaned = 0
    for f in ports_dir.glob("*.reload-port"):
        try:
            pid = int(f.stem)
        except ValueError:
            continue
        if not _is_pid_alive(pid):
            try:
                f.unlink()
                cleaned += 1
            except OSError:
                pass
    return cleaned


def _tcp_probe(port: int, timeout: float = 0.2) -> bool:
    """Return True if TCP port accepts connections."""
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=timeout):
            return True
    except OSError:
        return False


def _is_path_prefix(cwd: str, prefix: str) -> bool:
    """Return True if prefix is a parent of (or equal to) cwd.

    Uses normcase + normpath to handle Windows forward/backslash mismatch:
    Unity port files use '/' (Application.dataPath), os.getcwd() uses '\\'.
    On POSIX normcase is a no-op.
    """
    cwd_n = os.path.normcase(os.path.normpath(cwd))
    pre_n = os.path.normcase(os.path.normpath(prefix))
    sep = os.path.normcase(os.sep)
    return cwd_n == pre_n or cwd_n.startswith(pre_n + sep)


def read_unity_port(skip_probe: bool = False) -> int | None:
    """Discover Unity Biome MCP port from discovery files, env var, or default 9500.

    Returns None when skip_probe=True and no live candidates found (no silent 9500 drift).
    Returns 9500 when skip_probe=False and no candidates (cold-start backward compat).
    Priority: env var → CWD project match → newest mtime → 9500.
    skip_probe: if True, skip TCP connectivity check (useful during reconnect
                when port may be transiently down due to domain reload).
    """
    if os.environ.get("UNITY_MCP_PORT"):
        try:
            return int(os.environ["UNITY_MCP_PORT"])
        except ValueError:
            pass
    glob_pattern = "*.port"
    candidates = []
    for f in _iter_port_files(glob_pattern, _ports_dir()):
        try:
            lines = f.read_text(encoding="utf-8", errors="replace").strip().split("\n")
            port = int(lines[0])
            pid = int(f.stem)
            if not _is_pid_alive(pid):
                with contextlib.suppress(OSError): f.unlink()
                continue
            project_path = lines[1] if len(lines) > 1 else ""
            project = lines[2] if len(lines) > 2 else "?"
            candidates.append((f.stat().st_mtime, port, project, pid, project_path))
        except (ValueError, OSError):
            with contextlib.suppress(OSError): f.unlink()

    if not candidates:
        # B3: skip_probe reconnect → no live targets → None (caller preserves self._port).
        # Cold-start (skip_probe=False) → fallback 9500 for backward compat.
        return None if skip_probe else DEFAULT_PORT

    # CWD-based selection: prefer project whose path is a prefix of cwd.
    # Waterfall: UNITY_MCP_PROJECT_DIR > CLAUDE_PROJECT_DIR > CWD.
    # If multiple match (nested), prefer longest path.
    cwd = (os.environ.get("UNITY_MCP_PROJECT_DIR")
           or os.environ.get("CLAUDE_PROJECT_DIR")
           or os.getcwd())
    cwd_matches = [
        (len(os.path.normpath(pp)), mtime, port, proj, pid)
        for mtime, port, proj, pid, pp in candidates
        if pp and _is_path_prefix(cwd, pp)
    ]
    if cwd_matches:
        cwd_matches.sort(reverse=True)  # longest path first, then newest mtime
        _, _, port, project, pid = cwd_matches[0]
    else:
        candidates.sort(reverse=True)  # newest mtime first
        _, port, project, pid, _ = candidates[0]

    logging.getLogger("unity_mcp").info(
        "Auto-discovered Unity '%s' on port %d (pid %d)", project, port, pid)
    return port


async def discover_port_with_retry(
    attempts: int = 3,
    initial_delay: float = 0.5,
    max_delay: float = 2.0,
) -> int:
    """Retry read_unity_port() with backoff until a port file appears.

    Handles the startup race where Unity hasn't written its port file yet.
    Returns DEFAULT_PORT after all attempts if no file appears.

    Uses read_unity_port(skip_probe=True): returns None when no candidates
    (reliable "not ready" sentinel), vs DEFAULT_PORT which is ambiguous.
    If UNITY_MCP_PORT is set, returns immediately without retry.
    """
    import asyncio

    if os.environ.get("UNITY_MCP_PORT"):
        return read_unity_port()  # env var wins; no retry needed

    port = read_unity_port(skip_probe=True)
    if port is not None:
        return port  # port file found (even 9500 is a real registration)

    delay = initial_delay
    for _ in range(attempts - 1):
        await asyncio.sleep(delay)
        port = read_unity_port(skip_probe=True)
        if port is not None:
            return port
        delay = min(delay * 2.0, max_delay)

    return DEFAULT_PORT


# Fix 4: last live ServerSession seen while handling a ListTools request. Lets
# reconnect_unity()/_refresh_tools_cache() push a tool_list_changed notification
# to the client outside of a request context (e.g. from a manual reconnect).
_active_session = None
_labeled_sessions: weakref.WeakSet = weakref.WeakSet()


def get_active_session():
    """Return the most recently captured ServerSession, or None if none seen yet."""
    return _active_session


def install_list_tools_filter(mcp_server, get_disabled_cache_fn, get_slot=None):
    """Patch mcp._mcp_server.request_handlers to inject filtering + schema capture."""
    import mcp.types as mcp_types

    original_handler = mcp_server._mcp_server.request_handlers[mcp_types.ListToolsRequest]

    async def _filtered_tools_handler(req):
        global _active_session
        session = None
        with contextlib.suppress(LookupError):
            session = mcp_server._mcp_server.request_context.session
            _active_session = session
        if session is not None:
            _schedule_client_label(session, get_slot)
        result = await original_handler(req)
        # Capture full schemas into registry BEFORE stripping
        for t in result.root.tools:
            schema = getattr(t, "inputSchema", None) or {}
            desc = getattr(t, "description", "") or ""
            ann = getattr(t, "annotations", None)
            _schema_registry.capture(t.name, schema, desc, annotations=ann)
        result.root.tools = filter_tools(result.root.tools, get_disabled_cache_fn())
        return result

    mcp_server._mcp_server.request_handlers[mcp_types.ListToolsRequest] = _filtered_tools_handler


_DEFAULT_CLIENT_LABEL = "Claude Code"


def _schedule_client_label(session, get_slot) -> None:
    """Send the client label once from a request carrying valid session context."""
    if get_slot is None or session in _labeled_sessions:
        return
    client_params = session.client_params
    name = client_params.clientInfo.name if client_params else None
    if not name or name == _DEFAULT_CLIENT_LABEL:
        return
    bridge = get_slot()
    if bridge is None:
        return

    import asyncio

    logger = logging.getLogger("unity_mcp")
    _labeled_sessions.add(session)

    async def _send() -> None:
        try:
            await bridge.send("set_client_label", {"label": name}, timeout=3.0)
        except Exception as e:
            _labeled_sessions.discard(session)
            logger.debug("set_client_label failed for %r: %s", name, e)

    asyncio.create_task(_send())
