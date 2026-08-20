"""Tests for screenshot invalid source and aspect mismatch (Subtask 5, MEDIA-033).

Guards that screenshot() returns typed error strings — never raises — when
C# reports a source error or dimension mismatch.
"""
import unity_mcp.tools.screenshot as _mod


def _make_send(msg: str):
    async def _send(cmd, args, **_kw):
        return msg
    return _send


def _args(**kwargs):
    return {k: v for k, v in kwargs.items() if v is not None}


async def test_screenshot_invalid_source_returns_typed_error(monkeypatch):
    """_send returning source-not-found error → screenshot returns string; no exception."""
    error_msg = "Error: source 'nonexistent_camera' not found"
    monkeypatch.setattr(_mod, "_send", _make_send(error_msg))
    monkeypatch.setattr(_mod, "_args", _args)

    result = await _mod.screenshot(camera="nonexistent_camera")

    assert isinstance(result, str)
    assert "source" in result
    assert "not found" in result


async def test_screenshot_aspect_mismatch_is_non_fatal(monkeypatch):
    """_send returning dimensions mismatch → screenshot returns string; no exception raised."""
    mismatch_msg = "Error: Screenshot dimensions mismatch: got 1920x1080, expected 640x480"
    monkeypatch.setattr(_mod, "_send", _make_send(mismatch_msg))
    monkeypatch.setattr(_mod, "_args", _args)

    result = await _mod.screenshot(width=640, height=480)

    assert isinstance(result, str)
    assert "mismatch" in result.lower()
