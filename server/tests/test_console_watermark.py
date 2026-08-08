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

    async def fake_get_console(count, level, since, keyword=None, count_only=False):
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


async def test_get_console_since_accepts_full_labeled_mark(mod, monkeypatch):
    captured = {}

    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        captured["since"] = since
        return "logs"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark(label="phase1")
    result = await mod.get_console_since(mark)

    assert result == "logs"
    assert captured["since"] >= 0


async def test_get_console_since_accepts_bare_timestamp_with_label(mod, monkeypatch):
    captured = {}

    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        captured["since"] = since
        return "logs"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = f"{time.time()}:phase1"
    result = await mod.get_console_since(mark)

    assert result == "logs"
    assert captured["since"] >= 0


async def test_get_console_since_with_level(mod, monkeypatch):
    captured = {}

    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        captured["level"] = level
        return "err logs"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    await mod.get_console_since(mark, level="error,exception")

    assert captured["level"] == "error,exception"


async def test_roundtrip_mark_then_since(mod, monkeypatch):
    since_values = []

    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        since_values.append(since)
        return "ok"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    time.sleep(0.01)
    await mod.get_console_since(mark)

    assert len(since_values) == 1
    assert 0 < since_values[0] < 5  # elapsed seconds since mark: positive and recent


# ── P-051: MCP-internal synthetic truncation lines ────────────────────────────

async def test_get_console_since_filters_mcp_internal_lines(mod, monkeypatch):
    """P-051: synthetic '[+N older problem entries dropped]' tagged #MCP_INTERNAL
    must be stripped before returning — not treated as a real Unity log entry."""
    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        return (
            "[Error] 12:34:56.789 Some real error\n"
            "#MCP_INTERNAL [+3 older problem entries dropped]"
        )

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark)

    assert "#MCP_INTERNAL" not in result
    assert "Some real error" in result


async def test_get_console_since_filters_mcp_internal_only_line(mod, monkeypatch):
    """P-051: when ONLY the synthetic line is present, result should be empty string."""
    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        return "#MCP_INTERNAL [+1 older problem entries dropped]"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark)

    assert result == ""


# ── P-413: overflow is a warning, not a hard error ───────────────────────────

async def test_get_console_since_overflow_returns_warning_not_error(mod, monkeypatch):
    """P-413: overflow predating mark must be a warning, not a hard error."""
    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        return "[Error] 12:00:00.000 Something broke\n#MCP_INTERNAL overflow:3"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark)

    assert not result.startswith("err:"), f"P-413: overflow returned error: {result}"
    assert "Something broke" in result, f"Entry missing from result: {result}"
    assert "WARN" in result or "overflow" in result, f"Warning missing: {result}"


async def test_get_console_since_overflow_only_returns_warning(mod, monkeypatch):
    """P-413: when only overflow marker is present (no entries), return warning not error."""
    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        return "#MCP_INTERNAL overflow:5"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark)

    assert not result.startswith("err:"), f"P-413: overflow returned error: {result}"
    assert "overflow" in result or "WARN" in result


async def test_get_console_since_no_overflow_clean(mod, monkeypatch):
    """Baseline: no overflow returns clean entries without warning."""
    async def fake_get_console(count, level, since, keyword=None, count_only=False):
        return "[Error] 12:00:00.000 NullRef\n[Warning] 12:00:00.001 Minor"

    monkeypatch.setattr(mod, "get_console", fake_get_console)
    mark = await mod.console_mark()
    result = await mod.get_console_since(mark)

    assert "NullRef" in result
    assert not result.startswith("err:")
    assert "WARN" not in result
