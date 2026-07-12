"""Tests for scene_health tool — F4 Scene Health Audit."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture
def mock_send():
    return AsyncMock(return_value="OK: no issues")


@pytest.fixture(autouse=True)
def patch_scene_health(mock_send, monkeypatch):
    import unity_mcp.tools.scene_health as mod
    monkeypatch.setattr(mod, "_send", mock_send)
    monkeypatch.setattr(mod, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})


class TestSceneHealth:
    async def test_default_sends_all(self, mock_send):
        from unity_mcp.tools.scene_health import scene_health
        await scene_health()
        mock_send.assert_called_once_with("scene_health", {"focus": "all"})

    async def test_focus_missing_passes_through(self, mock_send):
        from unity_mcp.tools.scene_health import scene_health
        await scene_health(focus="missing")
        args = mock_send.call_args[0][1]
        assert args["focus"] == "missing"

    async def test_focus_hierarchy(self, mock_send):
        from unity_mcp.tools.scene_health import scene_health
        await scene_health(focus="hierarchy")
        args = mock_send.call_args[0][1]
        assert args["focus"] == "hierarchy"

    async def test_returns_send_result(self, mock_send):
        mock_send.return_value = "CRITICAL: /Player — MissingScript (x2)"
        from unity_mcp.tools.scene_health import scene_health
        result = await scene_health()
        assert result == "CRITICAL: /Player — MissingScript (x2)"
