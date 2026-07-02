"""Tests for timeout_categories module."""

from unittest.mock import AsyncMock, patch

import pytest

from unity_mcp.timeout_categories import (
    DEFAULT_TIMEOUT,
    TIMEOUT_CATEGORIES,
    get_timeout,
)


# --- unit tests ---


def test_ping_timeout_is_5():
    assert get_timeout("ping") == 5.0


def test_unknown_cmd_returns_default():
    assert get_timeout("nonexistent_cmd") == DEFAULT_TIMEOUT


def test_run_playtest_timeout_is_300():
    assert get_timeout("run_playtest") == 300.0


def test_all_categories_are_positive():
    for cmd, t in TIMEOUT_CATEGORIES.items():
        assert t > 0, f"{cmd} has non-positive timeout {t}"


def test_default_timeout_is_positive():
    assert DEFAULT_TIMEOUT > 0


# --- integration: _send_raw uses category timeout ---


@pytest.mark.asyncio
async def test_send_raw_uses_category_timeout():
    """_send_raw with timeout<=0 should resolve timeout via get_timeout."""
    mock_bridge = AsyncMock()
    mock_bridge.send.return_value = {"ok": True, "data": "pong"}
    mock_bridge._probe = None

    mock_slot = type("Slot", (), {"bridge": mock_bridge})()

    with patch("unity_mcp.server.slot", mock_slot):
        from unity_mcp.server import _send_raw

        await _send_raw("ping", {})

    mock_bridge.send.assert_awaited_once_with("ping", {}, timeout=5.0)


@pytest.mark.asyncio
async def test_send_raw_explicit_timeout_overrides_category():
    """Explicit timeout > 0 must bypass the category lookup."""
    mock_bridge = AsyncMock()
    mock_bridge.send.return_value = {"ok": True, "data": "ok"}
    mock_bridge._probe = None

    mock_slot = type("Slot", (), {"bridge": mock_bridge})()

    with patch("unity_mcp.server.slot", mock_slot):
        from unity_mcp.server import _send_raw

        await _send_raw("ping", {}, timeout=99.0)

    mock_bridge.send.assert_awaited_once_with("ping", {}, timeout=99.0)


# --- C1: _send's own default must not shadow _send_raw's category guard ---


async def test_send_uses_category_timeout_not_stale_default(mock_bridge, bridge_response):
    """_send() with no explicit timeout must resolve via get_timeout(cmd), not a
    stale hardcoded 30.0 default that shadows _send_raw's `timeout <= 0` guard.

    execute_code is declared at 60.0 in TIMEOUT_CATEGORIES — distinct from the
    stale default, so this fails on unfixed code (which sends timeout=30.0)."""
    from unity_mcp.server import _send

    bridge_response(data="ok")
    await _send("execute_code", {"code": "1+1"})
    mock_bridge.send.assert_called_once_with(
        "execute_code", {"code": "1+1"}, timeout=60.0
    )
