"""Tests for SceneBrief integration in middleware wrap_send."""
from unittest.mock import AsyncMock, MagicMock, patch
from unity_mcp.middleware import Middleware, wrap_send


async def test_scene_brief_injected_on_first_non_meta_call():
    """Brief prepended to first result when available."""
    mw = Middleware()

    brief_mock = MagicMock()
    brief_mock._injected = False
    brief_mock.brief = "5 objects, stopped"
    brief_mock.should_inject = MagicMock(return_value=True)
    brief_mock.ensure = AsyncMock(return_value="5 objects, stopped")
    brief_mock.mark_injected = MagicMock()
    mw.scene_brief = brief_mock

    send_fn = AsyncMock(return_value="hierarchy_data")
    wrapped = wrap_send(send_fn, mw)
    result = await wrapped("get_hierarchy", {})

    assert "SCENE CONTEXT" in result
    assert "5 objects, stopped" in result
    assert "hierarchy_data" in result
    brief_mock.mark_injected.assert_called_once()


async def test_scene_brief_not_injected_on_second_call():
    """No injection after mark_injected() was called."""
    mw = Middleware()

    brief_mock = MagicMock()
    brief_mock._injected = True  # already injected — skip
    brief_mock.should_inject = MagicMock(return_value=False)
    brief_mock.ensure = AsyncMock(return_value="5 objects, stopped")
    mw.scene_brief = brief_mock

    send_fn = AsyncMock(return_value="result")
    wrapped = wrap_send(send_fn, mw)
    result = await wrapped("get_hierarchy", {})

    assert "SCENE CONTEXT" not in result


async def test_scene_brief_none_no_injection():
    """No scene_brief field set — no injection."""
    mw = Middleware()
    assert mw.scene_brief is None

    send_fn = AsyncMock(return_value="result")
    wrapped = wrap_send(send_fn, mw)
    result = await wrapped("get_hierarchy", {})
    assert "SCENE CONTEXT" not in result
