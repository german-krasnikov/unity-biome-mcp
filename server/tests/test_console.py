"""TDD: console.py — #10 mark_id parsing + keyword/count_only forwarding."""
from unittest.mock import AsyncMock, MagicMock, patch


def _freeze_time(monkeypatch, now: float):
    from unity_mcp.tools import console
    mock_time = MagicMock()
    mock_time.time.return_value = now
    monkeypatch.setattr(console, "_time", mock_time)


async def test_mark_id_standard_format(monkeypatch):
    """'mark:1234.5' → ts=1234.5, since_s = now - 1234.5."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="logs")
    monkeypatch.setattr(console, "_send", mock_send)
    monkeypatch.setattr(console, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    _freeze_time(monkeypatch, 10000.0)

    await console.get_console_since("mark:1234.5")
    call_args = mock_send.call_args[0][1]
    assert abs(call_args["since"] - (10000.0 - 1234.5)) < 0.01


async def test_mark_id_with_label(monkeypatch):
    """'mark:1234.5:my label' → ts=1234.5."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="logs")
    monkeypatch.setattr(console, "_send", mock_send)
    monkeypatch.setattr(console, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    _freeze_time(monkeypatch, 10000.0)

    await console.get_console_since("mark:1234.5:my label")
    call_args = mock_send.call_args[0][1]
    assert abs(call_args["since"] - (10000.0 - 1234.5)) < 0.01


async def test_mark_id_with_colons_in_label(monkeypatch):
    """'mark:1234.5:label:with:colons' → ts=1234.5 (only split on first colon after 'mark:')."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="logs")
    monkeypatch.setattr(console, "_send", mock_send)
    monkeypatch.setattr(console, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    _freeze_time(monkeypatch, 10000.0)

    await console.get_console_since("mark:1234.5:label:with:colons")
    call_args = mock_send.call_args[0][1]
    assert abs(call_args["since"] - (10000.0 - 1234.5)) < 0.01


async def test_mark_id_bare_float(monkeypatch):
    """Bare float string '9999.0' → ts=9999.0 (no 'mark:' prefix needed)."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="logs")
    monkeypatch.setattr(console, "_send", mock_send)
    monkeypatch.setattr(console, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    _freeze_time(monkeypatch, 10000.0)

    await console.get_console_since("9999.0")
    call_args = mock_send.call_args[0][1]
    assert abs(call_args["since"] - 1.0) < 0.01


async def test_mark_id_invalid_returns_error(monkeypatch):
    """Non-parseable mark_id → 'err: invalid mark_id'."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="logs")
    monkeypatch.setattr(console, "_send", mock_send)
    _freeze_time(monkeypatch, 10000.0)

    result = await console.get_console_since("not_a_float")
    assert result == "err: invalid mark_id"
    mock_send.assert_not_called()


async def test_mark_id_future_timestamp_returns_error(monkeypatch):
    """mark_id timestamp in the future → 'err: mark_id timestamp in future'."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="logs")
    monkeypatch.setattr(console, "_send", mock_send)
    _freeze_time(monkeypatch, 1000.0)  # now=1000, mark=9999 is future

    result = await console.get_console_since("mark:9999.0")
    assert result == "err: mark_id timestamp in future"


async def test_keyword_forwarded(monkeypatch):
    """keyword= param forwarded to get_console call."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="logs")
    monkeypatch.setattr(console, "_send", mock_send)
    monkeypatch.setattr(console, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    _freeze_time(monkeypatch, 10000.0)

    await console.get_console_since("mark:9000.0", keyword="error")
    call_args = mock_send.call_args[0][1]
    assert call_args.get("keyword") == "error"


async def test_count_only_forwarded(monkeypatch):
    """count_only=True forwarded to get_console as 'true' string."""
    from unity_mcp.tools import console
    mock_send = AsyncMock(return_value="5")
    monkeypatch.setattr(console, "_send", mock_send)
    monkeypatch.setattr(console, "_args", lambda **kw: {k: v for k, v in kw.items() if v is not None})
    _freeze_time(monkeypatch, 10000.0)

    await console.get_console_since("mark:9000.0", count_only=True)
    call_args = mock_send.call_args[0][1]
    assert call_args.get("count_only") == "true"
