"""Gate 2: Read Operations — verify read-only MCP commands return valid data."""
from __future__ import annotations

import pytest

pytestmark = [pytest.mark.live, pytest.mark.asyncio(loop_scope="session")]


async def test_get_hierarchy_returns_nodes(conformance_worker):
    """get_hierarchy returns non-empty hierarchy text."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_hierarchy", {"depth": 2})
    data = resp.get("data", "")
    assert isinstance(data, str)
    assert len(data) > 0, "hierarchy is empty"


async def test_inspect_main_camera(conformance_worker):
    """inspect returns component info for a known object."""
    worker, bridge = conformance_worker
    resp = await bridge.send("inspect", {"path": "/Main Camera"})
    data = resp.get("data", "")
    assert isinstance(data, str)
    assert len(data) > 0, "inspect returned empty"
    assert "Camera" in data, "Camera component not found on Main Camera"


async def test_get_compile_errors_clean(conformance_worker):
    """No compile errors on a healthy project."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_compile_errors", {})
    data = resp.get("data", "")
    clean = (
        not data
        or data.strip() == ""
        or "no compilation errors" in data.lower()
        or "0 error" in data.lower()
    )
    assert clean, f"compile errors present: {data[:200]}"


async def test_get_console_basic(conformance_worker):
    """get_console returns without error."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_console", {})
    assert resp["ok"], f"get_console failed: {resp}"


async def test_get_console_returns_data(conformance_worker):
    """get_console returns structured console output."""
    worker, bridge = conformance_worker
    resp = await bridge.send("get_console", {})
    assert resp["ok"], f"get_console failed: {resp}"
    # data is always present (may be empty if no log entries)
    assert "data" in resp


async def test_screenshot_succeeds(conformance_worker):
    """screenshot returns a non-empty response."""
    worker, bridge = conformance_worker
    resp = await bridge.send("screenshot", {})
    assert resp["ok"], f"screenshot failed: {resp}"
    data = resp.get("data", {})
    assert data, "screenshot returned empty data"


async def test_search_scene_basic(conformance_worker):
    """search_scene finds Main Camera (present in every Unity scene)."""
    worker, bridge = conformance_worker
    resp = await bridge.send("search_scene", {"query": "Main Camera"})
    assert resp["ok"], f"search_scene failed: {resp}"
    data = resp.get("data", "")
    assert data, "search_scene returned empty for 'Main Camera'"
