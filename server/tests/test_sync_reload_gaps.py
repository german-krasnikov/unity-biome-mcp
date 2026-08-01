"""Gap tests for sync_unity: consecutive DomainReloadErrors, fast-path ConnectionError."""
import pytest
from unittest.mock import AsyncMock, patch, MagicMock

import unity_mcp.tools.sync as _sync
from unity_mcp.bridge import DomainReloadError


# ── Helpers (copied from test_sync.py — not importable) ──────────────────────

def _make_send(ack_response: str, status_seq, errors_response: str = ""):
    status_iter = iter(status_seq)
    synced = False

    async def _send(cmd, args=None, **kwargs):
        nonlocal synced
        if cmd == "sync":
            synced = True
            return ack_response
        if cmd == "force_refresh":
            return "force_refresh triggered"
        if cmd == "sync_status":
            if not synced:
                return "epoch=0|state=idle"
            try:
                val = next(status_iter)
            except StopIteration:
                return "epoch=0|state=ready"
            if isinstance(val, Exception):
                raise val
            return val
        if cmd == "get_compile_errors":
            return errors_response
        if cmd == "diagnose":
            return "main_mvid=absent"
        raise AssertionError(f"Unexpected cmd: {cmd}")

    return _send


@pytest.fixture(autouse=True)
def _patch_sleep():
    with patch("asyncio.sleep", new=AsyncMock(return_value=None)):
        yield


@pytest.fixture(autouse=True)
def _reset_send():
    original = _sync._send
    yield
    _sync._send = original


@pytest.fixture(autouse=True)
def _zero_recovery_timeout(monkeypatch):
    monkeypatch.setattr(_sync, "_RECOVERY_TIMEOUT", 0.0)


@pytest.fixture(autouse=True)
def _patch_corroborate():
    async def _default_get_corroborated(send):
        try:
            csharp = await send("get_compile_errors", {})
        except Exception:
            return ""
        if csharp.strip() == "No compilation errors":
            return ""
        return csharp

    with patch("unity_mcp.tools.sync.editor_log") as mock_el:
        mock_el.corroborate = lambda s: s
        mock_el.init_corroboration = MagicMock()
        mock_el.get_corroborated_errors = _default_get_corroborated
        yield mock_el


# T3: two consecutive DomainReloadErrors then success → "sync clean"
async def test_sync_unity_two_consecutive_domain_reload_errors():
    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=true",
        status_seq=[
            DomainReloadError("going_away"),
            DomainReloadError("still_reloading"),
            "epoch=1|state=ready",
        ],
    )
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result


# T4: fast path (will_compile=false) + ConnectionError on get_compile_errors → clean, no raise
async def test_sync_unity_fast_path_connection_error_on_get_errors(_patch_corroborate):
    async def _dead_errors(send, **kwargs):
        raise ConnectionError("TCP gone during error fetch")

    _patch_corroborate.get_corroborated_errors = _dead_errors

    _sync._send = _make_send(
        "sync_ack|epoch=1|will_compile=false",
        status_seq=[],
    )
    # Must not raise — ConnectionError swallowed in _get_errors()
    result = await _sync.sync_unity(timeout=60.0)
    assert "sync clean" in result or result == ""
