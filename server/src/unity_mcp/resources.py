"""MCP Resources — live Unity context exposed as resource URIs."""
import asyncio
import time
from collections.abc import Callable

from .console_levels import PROBLEM_LEVELS

_send = None
_mcp = None
_dynamic_uris: set[str] = set()
_cache_ts: float = 0.0
_refresh_lock: asyncio.Lock | None = None

_DISPATCH: dict[str, tuple[str, str]] = {
    "go":  ("inspect",           "path"),
    "cs":  ("asset",             "path"),
    "pfb": ("prefab",            "name"),
    "mat": ("material",          "name"),
    "so":  ("scriptable_object", "name"),
}


async def _safe_send(cmd: str, args: dict) -> str:
    try:
        return await _send(cmd, args)
    except Exception as e:
        return f"[disconnected: {e}]"


def _parse_search_context(data: str) -> list[str]:
    uris = []
    for line in data.splitlines():
        parts = line.split("\t", 2)
        if len(parts) == 3:
            type_prefix, path, _ = parts
            uris.append(f"biome://{type_prefix}/{path.lstrip('/')}")
    return uris[:200]


def _make_reader(type_prefix: str, name: str) -> Callable:
    cmd, arg_key = _DISPATCH[type_prefix]
    async def read() -> str:
        return await _safe_send(cmd, {arg_key: name})
    read.__name__ = f"biome_{type_prefix}_{name}"
    return read


async def refresh_dynamic() -> None:
    global _dynamic_uris, _cache_ts
    if _mcp is None or _send is None:
        return
    if _refresh_lock.locked():
        return
    async with _refresh_lock:
        result = await _safe_send("search_context", {"query": "", "limit": 200})
        if result.startswith("[disconnected"):
            return
        new_uris = set(_parse_search_context(result))
        rdict = _mcp._resource_manager._resources
        for uri in _dynamic_uris - new_uris:
            rdict.pop(uri, None)
        from mcp.server.fastmcp.resources import FunctionResource
        for uri in new_uris - _dynamic_uris:
            rest = uri[len("biome://"):]
            slash = rest.index("/")
            type_prefix, path = rest[:slash], rest[slash + 1:]
            if type_prefix not in _DISPATCH:
                continue
            rdict[uri] = FunctionResource.from_function(_make_reader(type_prefix, path), uri=uri, name=path)
        _dynamic_uris = new_uris
        _cache_ts = time.monotonic()


async def scene_hierarchy() -> str:
    """Current scene hierarchy summary."""
    return await _safe_send("get_hierarchy", {"summary": "true"})


async def console_errors() -> str:
    """Recent console errors."""
    return await _safe_send("get_console", {"count": 20, "level": PROBLEM_LEVELS})


async def editor_state() -> str:
    """Editor state: play mode, scene, selection."""
    return await _safe_send("editor", {"action": "state"})


async def tool_categories() -> str:
    """Available tool categories."""
    from .tools.gating import get_categories
    return "\n".join(f"{k}: {', '.join(sorted(v))}" for k, v in get_categories().items())


def register(mcp, send, args) -> None:
    global _send, _mcp, _refresh_lock
    _send = send
    _mcp = mcp
    if _refresh_lock is None:
        _refresh_lock = asyncio.Lock()
    mcp.resource("biome://scene/hierarchy")(scene_hierarchy)
    mcp.resource("biome://console/errors")(console_errors)
    mcp.resource("biome://editor/state")(editor_state)
    mcp.resource("biome://tools/categories")(tool_categories)
