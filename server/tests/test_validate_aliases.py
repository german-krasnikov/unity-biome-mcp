"""Tests for validate/sync/export playtest aliases MCP tools."""
import pytest
from unittest.mock import AsyncMock
from unity_mcp.server import (
    validate_playtest_aliases,
    sync_playtest_aliases_from_defs,
    export_playtest_aliases_to_defs,
)


async def test_validate_playtest_aliases_forwards_to_send(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok: 5 aliases in sync"}
    result = await validate_playtest_aliases(
        defs="Assets/PlaytestDefs/farm_core.defs",
        asset="Assets/Configs/PlaytestConfig.asset",
    )
    mock_bridge.send.assert_called_once_with(
        "validate_playtest_aliases",
        {
            "defs": "Assets/PlaytestDefs/farm_core.defs",
            "asset": "Assets/Configs/PlaytestConfig.asset",
        },
        timeout=30.0,
    )
    assert result == "ok: 5 aliases in sync"


async def test_validate_playtest_aliases_defs_always_in_args(mock_bridge):
    mock_bridge.send.return_value = {"ok": True, "data": "ok: 0 aliases in sync"}
    await validate_playtest_aliases(defs="Assets/PlaytestDefs/farm_core.defs")
    sent_args = mock_bridge.send.call_args[0][1]
    assert "defs" in sent_args
    assert sent_args["defs"] == "Assets/PlaytestDefs/farm_core.defs"


async def test_sync_playtest_aliases_from_defs_forwards_to_send(mock_bridge):
    mock_bridge.send.return_value = {
        "ok": True, "data": "synced: 5 aliases -> Assets/Configs/PlaytestConfig.asset"
    }
    result = await sync_playtest_aliases_from_defs(
        defs="Assets/PlaytestDefs/farm_core.defs",
        asset="Assets/Configs/PlaytestConfig.asset",
    )
    mock_bridge.send.assert_called_once_with(
        "sync_playtest_aliases_from_defs",
        {
            "defs": "Assets/PlaytestDefs/farm_core.defs",
            "asset": "Assets/Configs/PlaytestConfig.asset",
        },
        timeout=30.0,
    )
    assert result == "synced: 5 aliases -> Assets/Configs/PlaytestConfig.asset"


async def test_export_playtest_aliases_to_defs_forwards_to_send(mock_bridge):
    mock_bridge.send.return_value = {
        "ok": True, "data": "exported: 5 aliases -> Assets/PlaytestDefs/farm_core.defs"
    }
    result = await export_playtest_aliases_to_defs(
        asset="Assets/Configs/PlaytestConfig.asset",
        defs="Assets/PlaytestDefs/farm_core.defs",
    )
    mock_bridge.send.assert_called_once_with(
        "export_playtest_aliases_to_defs",
        {
            "asset": "Assets/Configs/PlaytestConfig.asset",
            "defs": "Assets/PlaytestDefs/farm_core.defs",
        },
        timeout=30.0,
    )
    assert result == "exported: 5 aliases -> Assets/PlaytestDefs/farm_core.defs"
