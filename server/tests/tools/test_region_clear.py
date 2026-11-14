"""TDD tests for region_clear tool (Item 40)."""
import pytest
from mcp.server.mcpserver.exceptions import ToolError
from unity_mcp.tools.spatial import region_clear

_TRIANGLE = "0,0;10,0;5,10"


# ── Scenario 1: dry_run=True (default) passes dry_run=true to bridge ────────

async def test_region_clear_dry_run_default(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "DRY: 3 objects would be deleted"}
    await region_clear(vertices=_TRIANGLE)
    cmd, args = mock_bridge.send.call_args[0]
    assert cmd == "region_clear"
    assert args["dry_run"] == "true"
    assert args["vertices"] == _TRIANGLE


# ── Scenario 2: dry_run=False passes dry_run=false ──────────────────────────

async def test_region_clear_dry_run_false(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "DELETED: 3 objects"}
    await region_clear(vertices=_TRIANGLE, dry_run=False)
    _, args = mock_bridge.send.call_args[0]
    assert args["dry_run"] == "false"


# ── Scenario 3: filter forwarded when given ──────────────────────────────────

async def test_region_clear_filter_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "DRY: 1 objects would be deleted"}
    await region_clear(vertices=_TRIANGLE, filter="Enemy")
    _, args = mock_bridge.send.call_args[0]
    assert args["filter"] == "Enemy"


# ── Scenario 4: filter=None omitted from args ────────────────────────────────

async def test_region_clear_filter_none_omitted(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "DRY: 0 objects"}
    await region_clear(vertices=_TRIANGLE)
    _, args = mock_bridge.send.call_args[0]
    assert "filter" not in args


# ── Scenario 5: cap forwarded as string ─────────────────────────────────────

async def test_region_clear_cap_forwarded(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "DRY: 5 objects"}
    await region_clear(vertices=_TRIANGLE, cap=10)
    _, args = mock_bridge.send.call_args[0]
    assert args["cap"] == "10"


# ── Scenario 6: invalid vertices raises ToolError ───────────────────────────

async def test_region_clear_invalid_vertices_raises():
    with pytest.raises(ToolError):
        await region_clear(vertices="0,0;10,0")  # only 2 vertices


# ── Scenario 7: empty vertices raises ToolError ─────────────────────────────

async def test_region_clear_empty_vertices_raises():
    with pytest.raises(ToolError, match="vertices required"):
        await region_clear(vertices="")
