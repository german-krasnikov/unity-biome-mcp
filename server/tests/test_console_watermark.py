"""TDD tests for console_mark / get_console_since (P1.6 Console Watermarks)."""
import time
import pytest
from unittest.mock import AsyncMock, patch


@pytest.fixture
def mod():
    import unity_mcp.tools.console as m
    return m


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch, mod):
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


# ── console_mark ──────────────────────────────────────────────────────────────

async def test_console_mark_returns_mark_format(mod):
    result = await mod.console_mark()
    parts = result.split(":")
    assert parts[0] == "mark"
    float(parts[1])  # must be a valid float


async def test_console_mark_with_label(mod):
    result = await mod.console_mark(label="phase1")
    assert result.startswith("mark:")
    assert result.endswith(":phase1")


async def test_console_mark_is_pure_python(_patch_send, mod):
    await mod.console_mark()
    _patch_send.assert_not_called()


# ── get_console_since ─────────────────────────────────────────────────────────

async def test_get_console_since_invalid_mark(mod):
    result = await mod.get_console_since("not-a-mark")
    assert result == "err: invalid mark_id"


async def test_get_console_since_future_mark(mod):
    future_ts = time.time() + 9999
    mark = f"mark:{future_ts:.3f}"
    result = await mod.get_console_since(mark)
    assert result == "err: mark_id timestamp in future"


async def test_get_console_since_valid_mark(mod, monkeypatch):
    captured = {}

    async def fake_get_console(count, level, since):
        captured["since"] = since
        captured["count"] = count
        return "logs"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark, count=200)

    assert result == "logs"
    assert isinstance(captured["since"], float)
    assert captured["since"] >= 0
    assert captured["count"] == 200


async def test_get_console_since_with_level(mod, monkeypatch):
    captured = {}

    async def fake_get_console(count, level, since):
        captured["level"] = level
        return "err logs"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    await mod.get_console_since(mark, level="error,exception")

    assert captured["level"] == "error,exception"


async def test_roundtrip_mark_then_since(mod, monkeypatch):
    since_values = []

    async def fake_get_console(count, level, since):
        since_values.append(since)
        return "ok"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    time.sleep(0.01)
    await mod.get_console_since(mark)

    assert len(since_values) == 1
    assert since_values[0] >= 0.0
