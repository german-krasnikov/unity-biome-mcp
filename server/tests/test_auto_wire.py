"""Tests for auto_wire tool."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture
def mock_send():
    return AsyncMock(return_value="Wired: 1 | Ambiguous: 0 | No match: 0")


@pytest.fixture(autouse=True)
def patch_auto_wire(mock_send, monkeypatch):
    import unity_mcp.tools.auto_wire as mod
    monkeypatch.setattr(mod, "_send", mock_send)
    monkeypatch.setattr(mod, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})


class TestAutoWire:
    async def test_auto_wire_sends_cmd(self, mock_send):
        from unity_mcp.tools.auto_wire import auto_wire
        result = await auto_wire("/Player/UI/HealthBar")
        assert mock_send.call_args[0][0] == "auto_wire"
        assert "Wired: 1" in result

    async def test_auto_wire_dry_run_true(self, mock_send):
        from unity_mcp.tools.auto_wire import auto_wire
        await auto_wire("/Player", dry_run=True)
        args = mock_send.call_args[0][1]
        assert args["dry_run"] == "true"

    async def test_auto_wire_dry_run_default_omitted(self, mock_send):
        """dry_run=False (default) omits key (Pattern A: C# default is false)."""
        from unity_mcp.tools.auto_wire import auto_wire
        await auto_wire("/Player")
        args = mock_send.call_args[0][1]
        assert "dry_run" not in args, (
            f"dry_run should be omitted when False, got {args.get('dry_run')!r}"
        )

    async def test_auto_wire_tool_error(self, mock_send):
        from mcp.server.fastmcp.exceptions import ToolError
        mock_send.side_effect = ToolError("Object not found: /Missing")
        from unity_mcp.tools.auto_wire import auto_wire
        with pytest.raises(ToolError):
            await auto_wire("/Missing")
