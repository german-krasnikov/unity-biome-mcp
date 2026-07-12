"""Unit tests for console.py tool functions (B2 split from scene.py): get_console,
get_compile_errors, recompile."""
import pytest
from unittest.mock import AsyncMock


@pytest.fixture(autouse=True)
def _patch_send(monkeypatch):
    """Replace module-level _send/_args with mocks for each test."""
    import unity_mcp.tools.console as mod
    send = AsyncMock(return_value="ok")
    args_fn = lambda **kw: {k: v for k, v in kw.items() if v is not None}
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr(mod, "_args", args_fn)
    return send


@pytest.fixture
def console_mod():
    import unity_mcp.tools.console as mod
    return mod


# ── T4: get_console keyword / count_only ─────────────────────────────────────

async def test_get_console_keyword_passed_to_send(console_mod, _patch_send):
    await console_mod.get_console(count=5, keyword="NullRef")

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_console"
    assert call_args[0][1].get("keyword") == "NullRef"


async def test_get_console_count_only_passed_to_send(console_mod, _patch_send):
    await console_mod.get_console(count=20, count_only=True)

    call_args = _patch_send.call_args
    assert call_args[0][1].get("count_only") == "true"


async def test_get_console_count_only_false_omitted(console_mod, _patch_send):
    """count_only=False should not appear in args (filtered by _args)."""
    await console_mod.get_console(count=5, count_only=False)

    call_args = _patch_send.call_args
    assert "count_only" not in call_args[0][1]


async def test_get_console_keyword_none_omitted(console_mod, _patch_send):
    await console_mod.get_console(count=5)

    call_args = _patch_send.call_args
    assert "keyword" not in call_args[0][1]


# ── S5: get_console since ─────────────────────────────────────────────────────

async def test_get_console_since_sends_param(console_mod, _patch_send):
    await console_mod.get_console(count=10, since=30.0)

    call_args = _patch_send.call_args
    assert call_args[0][0] == "get_console"
    assert call_args[0][1].get("since") == 30.0


async def test_get_console_since_none_omitted(console_mod, _patch_send):
    """since=None should not appear in args (filtered by _args)."""
    await console_mod.get_console(count=5)

    call_args = _patch_send.call_args
    assert "since" not in call_args[0][1]


# ── get_compile_errors / recompile ───────────────────────────────────────────

async def test_get_compile_errors_sends_command(console_mod, monkeypatch):
    import unity_mcp.tools.console as mod
    send = AsyncMock(return_value="clean")
    monkeypatch.setattr(mod, "_send", send)
    monkeypatch.setattr("unity_mcp.editor_log.corroborate", lambda result, compile_status="": result)

    result = await console_mod.get_compile_errors()

    send.assert_any_call("get_compile_errors", {})
    assert result == "clean"


async def test_get_compile_errors_suppresses_stale_warn_when_idle(console_mod, monkeypatch):
    """compile_status=idle is extracted from TCP and forwarded to corroborate."""
    import unity_mcp.tools.console as mod
    from unity_mcp import editor_log

    async def fake_send(cmd, args, **kw):
        return "idle|0" if cmd == "compile_status" else "No compilation errors"

    captured = {}

    def fake_corroborate(response, compile_status=""):
        captured["compile_status"] = compile_status
        return response

    monkeypatch.setattr(mod, "_send", fake_send)
    monkeypatch.setattr(editor_log, "corroborate", fake_corroborate)

    result = await console_mod.get_compile_errors()

    assert captured["compile_status"] == "idle"


async def test_recompile_sends_command_with_timeout(console_mod, _patch_send):
    await console_mod.recompile()

    _patch_send.assert_called_once_with("recompile", {}, timeout=60.0)
