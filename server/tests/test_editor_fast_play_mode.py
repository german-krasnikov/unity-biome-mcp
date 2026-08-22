"""Tests for editor() enable param — fast_play_mode / mutation_mode actions."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture(autouse=True)
def _patch(monkeypatch):
    import unity_mcp.tools.editor_control as mod
    send = AsyncMock(return_value="fast_play_mode:True")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


@pytest.fixture
def mod():
    import unity_mcp.tools.editor_control as m
    return m


async def test_fast_play_mode_enable_forwards_args(mod, _patch):
    """enable='true' must be present in args sent to bridge."""
    await mod.editor(action="fast_play_mode", enable="true")
    call_args = _patch.call_args
    assert call_args[0][0] == "editor"
    assert call_args[0][1].get("action") == "fast_play_mode"
    assert call_args[0][1].get("enable") == "true"


async def test_fast_play_mode_none_enable_omitted(mod, _patch):
    """enable=None (default) must NOT appear in args — Pattern A omission."""
    await mod.editor(action="fast_play_mode")
    call_args = _patch.call_args
    assert "enable" not in call_args[0][1]
