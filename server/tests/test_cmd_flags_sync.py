"""C1+C2: _warm_cmd_flags augments WRITE_CMDS and _RUNTIME_ONLY_CMDS from capabilities text."""
import pytest
from unittest.mock import AsyncMock


def _bridge(data_text: str):
    b = AsyncMock()
    b.send = AsyncMock(return_value={"ok": True, "data": data_text})
    return b


def _bridge_down():
    b = AsyncMock()
    b.send = AsyncMock(side_effect=ConnectionError("no unity"))
    return b


@pytest.mark.asyncio
async def test_warm_cmd_flags_adds_mutating(monkeypatch):
    from unity_mcp import middleware_types as mt
    from unity_mcp.server import _warm_cmd_flags

    monkeypatch.setattr(mt, "WRITE_CMDS", set(mt.WRITE_CMDS))
    bridge = _bridge("mutating_cmds:auto_wire,autofit_collider,references")
    await _warm_cmd_flags(bridge)

    assert "auto_wire" in mt.WRITE_CMDS
    assert "autofit_collider" in mt.WRITE_CMDS
    assert "references" in mt.WRITE_CMDS


@pytest.mark.asyncio
async def test_warm_cmd_flags_adds_runtime(monkeypatch):
    from unity_mcp import middleware_types as mt
    from unity_mcp.server import _warm_cmd_flags

    monkeypatch.setattr(mt, "_RUNTIME_ONLY_CMDS", set(mt._RUNTIME_ONLY_CMDS))
    bridge = _bridge("runtime_cmds:new_future_cmd")
    await _warm_cmd_flags(bridge)

    assert "new_future_cmd" in mt._RUNTIME_ONLY_CMDS


@pytest.mark.asyncio
async def test_warm_cmd_flags_tcp_down_keeps_baseline():
    from unity_mcp import middleware_types as mt
    from unity_mcp.server import _warm_cmd_flags

    await _warm_cmd_flags(_bridge_down())  # must not raise
    assert "invoke_method" in mt._RUNTIME_ONLY_CMDS  # hardcoded baseline intact


