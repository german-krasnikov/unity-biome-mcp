"""Tests for _refresh_resources() in server.py (scenarios 18–19)."""
import asyncio
from unittest.mock import AsyncMock, patch


async def test_refresh_resources_no_connection():
    """_refresh_resources(None) returns early — no crash, catalog unchanged."""
    from unity_mcp import resources
    resources._send = None
    resources._dynamic_uris = set()
    # Import the server function
    from unity_mcp.server import _refresh_resources
    await _refresh_resources(None)
    assert resources._dynamic_uris == set()


async def test_refresh_resources_populates_catalog():
    """_refresh_resources wires through to refresh_dynamic."""
    import asyncio
    from unity_mcp import resources

    class _FRM:
        def __init__(self): self._resources = {}
    class _FMCP:
        def __init__(self): self._resource_manager = _FRM()

    resources._mcp = _FMCP()
    resources._dynamic_uris = set()
    resources._send = AsyncMock(return_value="go\t/Root/Player\tPlayer")
    if resources._refresh_lock is None:
        resources._refresh_lock = asyncio.Lock()

    from unity_mcp.server import _refresh_resources
    await _refresh_resources(None)
    assert "biome://go/Root/Player" in resources._mcp._resource_manager._resources
