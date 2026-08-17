"""TDD tests for D4: discover_tools ctx=None notification fallback."""
import pytest
from unittest.mock import AsyncMock, MagicMock, patch


async def _call_discover(category, enable, ctx, active_session=None):
    """Helper to call discover_tools with patched internals."""
    from unity_mcp.tools import meta as meta_mod

    sent_notification = []

    async def fake_discover_impl(cat, en, inc_legacy, structured):
        return f"ok: {cat}"

    mock_session = MagicMock()
    mock_session.send_tool_list_changed = AsyncMock()

    with patch("unity_mcp.tools.meta._discover_tools_impl", fake_discover_impl):
        with patch("unity_mcp.tools.meta.get_active_session",
                   return_value=mock_session if active_session else None,
                   create=True):
            await meta_mod.discover_tools(category=category, enable=enable, ctx=ctx)

    return mock_session.send_tool_list_changed


# ── ctx provided ──────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_discover_ctx_provided_sends_notification():
    """When ctx is provided and enable+category set, uses ctx.session."""
    mock_session = MagicMock()
    mock_session.send_tool_list_changed = AsyncMock()
    mock_ctx = MagicMock()
    mock_ctx.session = mock_session

    async def fake_impl(cat, en, inc, st):
        return "ok"

    with patch("unity_mcp.tools.meta._discover_tools_impl", fake_impl):
        from unity_mcp.tools import meta as meta_mod
        await meta_mod.discover_tools(category="SCENE", enable=True, ctx=mock_ctx)

    mock_session.send_tool_list_changed.assert_called_once()


# ── ctx=None with active session ──────────────────────────────────────────────

@pytest.mark.asyncio
async def test_discover_ctx_none_uses_active_session():
    """When ctx=None but active session exists, sends notification via active session."""
    mock_session = MagicMock()
    mock_session.send_tool_list_changed = AsyncMock()

    async def fake_impl(cat, en, inc, st):
        return "ok"

    from unity_mcp.tools import meta as meta_mod
    with patch("unity_mcp.tools.meta._discover_tools_impl", fake_impl):
        with patch("unity_mcp.server_filtering.get_active_session", return_value=mock_session):
            await meta_mod.discover_tools(category="SCENE", enable=True, ctx=None)

    mock_session.send_tool_list_changed.assert_called_once()


# ── ctx=None, no active session ───────────────────────────────────────────────

@pytest.mark.asyncio
async def test_discover_ctx_none_no_session_no_error():
    """When ctx=None and no active session, no error raised, no notification sent."""
    async def fake_impl(cat, en, inc, st):
        return "ok"

    from unity_mcp.tools import meta as meta_mod
    with patch("unity_mcp.tools.meta._discover_tools_impl", fake_impl):
        with patch("unity_mcp.server_filtering.get_active_session", return_value=None):
            result = await meta_mod.discover_tools(category="SCENE", enable=True, ctx=None)

    assert result == "ok"


# ── enable=False ──────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_discover_enable_false_no_notification():
    """enable=False — no notification even with ctx."""
    mock_session = MagicMock()
    mock_session.send_tool_list_changed = AsyncMock()
    mock_ctx = MagicMock()
    mock_ctx.session = mock_session

    async def fake_impl(cat, en, inc, st):
        return "ok"

    from unity_mcp.tools import meta as meta_mod
    with patch("unity_mcp.tools.meta._discover_tools_impl", fake_impl):
        await meta_mod.discover_tools(category="SCENE", enable=False, ctx=mock_ctx)

    mock_session.send_tool_list_changed.assert_not_called()


# ── category=None ─────────────────────────────────────────────────────────────

@pytest.mark.asyncio
async def test_discover_category_none_no_notification():
    """category=None — no notification (nothing to enable)."""
    mock_session = MagicMock()
    mock_session.send_tool_list_changed = AsyncMock()
    mock_ctx = MagicMock()
    mock_ctx.session = mock_session

    async def fake_impl(cat, en, inc, st):
        return "ok"

    from unity_mcp.tools import meta as meta_mod
    with patch("unity_mcp.tools.meta._discover_tools_impl", fake_impl):
        await meta_mod.discover_tools(category=None, enable=True, ctx=mock_ctx)

    mock_session.send_tool_list_changed.assert_not_called()
