"""Tests for MCP resources (Part B)."""
from unittest.mock import AsyncMock

from unity_mcp.console_levels import PROBLEM_LEVELS


async def test_hierarchy_resource():
    from unity_mcp import resources
    resources._send = AsyncMock(return_value="Root\n  Player")
    result = await resources.scene_hierarchy()
    resources._send.assert_called_once_with("get_hierarchy", {"summary": "true"})
    assert "Root" in result


async def test_console_errors_resource():
    from unity_mcp import resources
    resources._send = AsyncMock(return_value="[Error] NullRef")
    result = await resources.console_errors()
    resources._send.assert_called_once_with("get_console", {"count": 20, "level": PROBLEM_LEVELS})
    assert "Error" in result


async def test_editor_state_resource():
    from unity_mcp import resources
    resources._send = AsyncMock(return_value="playing: false")
    result = await resources.editor_state()
    resources._send.assert_called_once_with("editor", {"action": "state"})
    assert "playing" in result


async def test_tool_categories_no_bridge():
    """Pure Python — no bridge needed."""
    from unity_mcp import resources
    result = await resources.tool_categories()
    assert "animation" in result
    assert "runtime" in result


def test_resources_registered():
    """register() calls mcp.resource for each URI."""
    from unittest.mock import MagicMock, AsyncMock
    from unity_mcp import resources

    mcp = MagicMock()
    registered_uris = []

    def fake_resource(uri):
        registered_uris.append(uri)
        return lambda fn: fn  # decorator returns fn unchanged

    mcp.resource = fake_resource
    send = AsyncMock()
    resources.register(mcp, send, lambda **kw: kw)

    assert "biome://scene/hierarchy" in registered_uris
    assert "biome://console/errors" in registered_uris
    assert "biome://editor/state" in registered_uris
    assert "biome://tools/categories" in registered_uris


# ---------------------------------------------------------------------------
# PY2.test.5: _safe_send exception-swallowing returns '[disconnected: ...]'
# ---------------------------------------------------------------------------

async def test_safe_send_returns_disconnected_on_exception():
    """_send raising RuntimeError → scene_hierarchy() returns '[disconnected: ...]'."""
    from unity_mcp import resources
    resources._send = AsyncMock(side_effect=RuntimeError("gone"))
    result = await resources.scene_hierarchy()
    assert result.startswith("[disconnected:"), f"Expected '[disconnected:', got: {result!r}"


# ---------------------------------------------------------------------------
# Phase 2: Dynamic Resources (scenarios 1–17)
# ---------------------------------------------------------------------------

class _FakeResourceManager:
    def __init__(self): self._resources = {}

class _FakeMCP:
    def __init__(self): self._resource_manager = _FakeResourceManager()


def _setup_dynamic(mock_send=None):
    """Helper: wire resources module for dynamic tests."""
    import asyncio
    from unity_mcp import resources
    resources._mcp = _FakeMCP()
    resources._send = mock_send
    if resources._refresh_lock is None:
        resources._refresh_lock = asyncio.Lock()
    resources._dynamic_uris = set()
    return resources


# --- parse_search_context ---

def test_parse_search_context_go_line():
    from unity_mcp.resources import _parse_search_context
    assert _parse_search_context("go\t/Root/Player\tPlayer\n") == ["biome://go/Root/Player"]


def test_parse_search_context_empty():
    from unity_mcp.resources import _parse_search_context
    assert _parse_search_context("") == []


def test_parse_search_context_cap():
    from unity_mcp.resources import _parse_search_context
    lines = "\n".join(f"go\t/Root/Go{i}\tGo{i}" for i in range(300))
    result = _parse_search_context(lines)
    assert len(result) <= 200


def test_parse_search_context_ignores_malformed():
    from unity_mcp.resources import _parse_search_context
    data = "no_tabs_here\ngo\t/Root/Player\tPlayer\njust\ttwo"
    result = _parse_search_context(data)
    assert result == ["biome://go/Root/Player"]


# --- refresh_dynamic registration ---

async def test_dynamic_resources_registered_on_connect():
    r = _setup_dynamic(AsyncMock(return_value="go\t/Root/Player\tPlayer"))
    await r.refresh_dynamic()
    assert "biome://go/Root/Player" in r._mcp._resource_manager._resources


async def test_refresh_removes_stale_uris():
    r = _setup_dynamic(AsyncMock(return_value="go\t/Root/Enemy\tEnemy"))
    r._dynamic_uris = {"biome://go/Root/Player"}
    r._mcp._resource_manager._resources["biome://go/Root/Player"] = "old"
    await r.refresh_dynamic()
    assert "biome://go/Root/Player" not in r._mcp._resource_manager._resources
    assert "biome://go/Root/Enemy" in r._mcp._resource_manager._resources


async def test_refresh_skips_unknown_type_prefix():
    r = _setup_dynamic(AsyncMock(return_value="XX\t/Foo\tFoo"))
    await r.refresh_dynamic()
    keys = list(r._mcp._resource_manager._resources.keys())
    assert not any("XX/" in k for k in keys)


# --- dispatch ---

async def test_resource_read_dispatches_go_to_inspect():
    r = _setup_dynamic(AsyncMock(return_value="go\t/Root/Player\tPlayer"))
    await r.refresh_dynamic()
    resource = r._mcp._resource_manager._resources["biome://go/Root/Player"]
    r._send = AsyncMock(return_value="data")
    await resource.read()
    r._send.assert_called_once_with("inspect", {"path": "Root/Player"})


async def test_resource_read_dispatches_cs_to_asset():
    r = _setup_dynamic(AsyncMock(return_value="cs\tAssets/Scripts/Player.cs\tPlayer"))
    await r.refresh_dynamic()
    resource = r._mcp._resource_manager._resources["biome://cs/Assets/Scripts/Player.cs"]
    r._send = AsyncMock(return_value="data")
    await resource.read()
    r._send.assert_called_once_with("asset", {"path": "Assets/Scripts/Player.cs"})


async def test_resource_read_dispatches_pfb_to_prefab():
    r = _setup_dynamic(AsyncMock(return_value="pfb\tPlayer\tPlayer"))
    await r.refresh_dynamic()
    resource = r._mcp._resource_manager._resources["biome://pfb/Player"]
    r._send = AsyncMock(return_value="data")
    await resource.read()
    r._send.assert_called_once_with("prefab", {"name": "Player"})


async def test_resource_read_dispatches_mat_to_material():
    r = _setup_dynamic(AsyncMock(return_value="mat\tM_Player\tM_Player"))
    await r.refresh_dynamic()
    resource = r._mcp._resource_manager._resources["biome://mat/M_Player"]
    r._send = AsyncMock(return_value="data")
    await resource.read()
    r._send.assert_called_once_with("material", {"name": "M_Player"})


async def test_resource_read_dispatches_so_to_scriptable_object():
    r = _setup_dynamic(AsyncMock(return_value="so\tGameConfig\tGameConfig"))
    await r.refresh_dynamic()
    resource = r._mcp._resource_manager._resources["biome://so/GameConfig"]
    r._send = AsyncMock(return_value="data")
    await resource.read()
    r._send.assert_called_once_with("scriptable_object", {"name": "GameConfig"})


# --- error guards ---

async def test_safe_send_fallback_on_disconnect_during_refresh():
    r = _setup_dynamic(AsyncMock(side_effect=RuntimeError("gone")))
    prev = set(r._dynamic_uris)
    await r.refresh_dynamic()
    assert r._dynamic_uris == prev


async def test_refresh_skips_if_disconnected_response():
    r = _setup_dynamic(AsyncMock(return_value="[disconnected: timeout]"))
    prev = set(r._dynamic_uris)
    await r.refresh_dynamic()
    assert r._dynamic_uris == prev


async def test_refresh_skips_if_lock_held():
    r = _setup_dynamic(AsyncMock(return_value="go\t/Root/Player\tPlayer"))
    await r._refresh_lock.acquire()
    try:
        await r.refresh_dynamic()
        r._send.assert_not_called()
    finally:
        r._refresh_lock.release()


async def test_static_resources_still_registered_after_refresh():
    r = _setup_dynamic(AsyncMock(return_value="go\t/Root/Player\tPlayer"))
    static = {
        "biome://scene/hierarchy": "h",
        "biome://console/errors": "e",
        "biome://editor/state": "s",
        "biome://tools/categories": "c",
    }
    r._mcp._resource_manager._resources.update(static)
    await r.refresh_dynamic()
    for uri in static:
        assert uri in r._mcp._resource_manager._resources


async def test_cache_ts_updated_after_successful_refresh():
    r = _setup_dynamic(AsyncMock(return_value="go\t/Root/Player\tPlayer"))
    before = r._cache_ts
    await r.refresh_dynamic()
    assert r._cache_ts > before


def test_register_stores_mcp_reference():
    from unittest.mock import MagicMock
    from unity_mcp import resources
    mcp = MagicMock()
    mcp.resource = lambda uri: (lambda fn: fn)
    resources.register(mcp, AsyncMock(), lambda **kw: kw)
    assert resources._mcp is mcp
    assert resources._refresh_lock is not None


def test_function_resource_api_canary():
    """Canary: fails if mcp SDK removes FunctionResource.from_function (private API we depend on)."""
    from mcp.server.fastmcp.resources import FunctionResource
    assert hasattr(FunctionResource, "from_function")
