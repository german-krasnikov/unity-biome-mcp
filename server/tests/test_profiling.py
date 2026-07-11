"""Tests for profiling tools: get_frame_stats, profile, get_memory."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture
def mock_send():
    return AsyncMock(return_value="ok")


@pytest.fixture(autouse=True)
def patch_profiling(mock_send):
    import unity_mcp.tools.profiling as mod
    mod._send = mock_send
    mod._args = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    yield
    mod._send = None
    mod._args = None


# ── get_frame_stats ────────────────────────────────────────────────────────────

class TestGetFrameStats:
    async def test_returns_text(self, mock_send):
        mock_send.return_value = "frame dt=16.2ms fps=61.7\ncpu=14.8ms gpu=11.2ms"
        from unity_mcp.tools.profiling import get_frame_stats
        result = await get_frame_stats()
        mock_send.assert_called_once_with("get_frame_stats", {})
        assert "fps=" in result


# ── profile ────────────────────────────────────────────────────────────────────

class TestProfile:
    async def test_start_burst_sends_duration(self, mock_send):
        from unity_mcp.tools.profiling import profile
        await profile(action="start", duration=3.0, mode="burst")
        call = mock_send.call_args[0]
        assert call[0] == "profile"
        assert call[1]["action"] == "start"
        assert call[1]["duration"] == "3.0"
        assert call[1]["mode"] == "burst"

    async def test_start_manual_no_duration(self, mock_send):
        from unity_mcp.tools.profiling import profile
        await profile(action="start", mode="manual")
        args = mock_send.call_args[0][1]
        assert "duration" not in args

    async def test_stop_no_extra_args(self, mock_send):
        from unity_mcp.tools.profiling import profile
        await profile(action="stop")
        mock_send.assert_called_once_with("profile", {"action": "stop"})

    async def test_compare_sends_both_sessions(self, mock_send):
        from unity_mcp.tools.profiling import profile
        await profile(action="compare", session="p2", compare_with="p1")
        args = mock_send.call_args[0][1]
        assert args["session"] == "p2"
        assert args["compare_with"] == "p1"

    async def test_triggered_sends_threshold(self, mock_send):
        from unity_mcp.tools.profiling import profile
        await profile(action="start", mode="triggered", threshold_ms=33.3)
        args = mock_send.call_args[0][1]
        assert args["threshold_ms"] == "33.3"

    async def test_focus_passed_on_analyze(self, mock_send):
        from unity_mcp.tools.profiling import profile
        await profile(action="analyze", session="p1", focus="gc")
        args = mock_send.call_args[0][1]
        assert args["focus"] == "gc"


# ── get_memory ─────────────────────────────────────────────────────────────────

class TestGetMemory:
    async def test_default_include_omitted(self, mock_send):
        from unity_mcp.tools.profiling import get_memory
        await get_memory()
        args = mock_send.call_args[0][1]
        assert "include" not in args

    async def test_custom_include_passed(self, mock_send):
        from unity_mcp.tools.profiling import get_memory
        await get_memory(include="textures")
        args = mock_send.call_args[0][1]
        assert args["include"] == "textures"


# ── gating ─────────────────────────────────────────────────────────────────────

def test_profiling_category_has_correct_tools():
    from unity_mcp.tools.gating import _THEMED_CATEGORIES
    # Phase 2: PROFILING merged into RUNTIME
    assert "profile" in _THEMED_CATEGORIES["RUNTIME"]
    assert "get_frame_stats" in _THEMED_CATEGORIES["RUNTIME"]
