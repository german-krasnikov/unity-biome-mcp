"""Integration tests for distiller in wrap_send."""
import pytest
from unittest.mock import AsyncMock
from collections import OrderedDict
from unity_mcp.middleware import Middleware


def test_focus_tracker_appends_from_get_component():
    mw = Middleware()
    mw._track_focus("get_component", {"path": "/Player"}, "result")
    assert "/Player" in mw._recent_focus


def test_focus_tracker_maxlen_8():
    mw = Middleware()
    for i in range(20):
        mw._track_focus("get_component", {"path": f"/Obj{i}"}, "x")
    assert len(mw._recent_focus) == 8


def test_focus_tracker_dedups():
    mw = Middleware()
    mw._track_focus("get_component", {"path": "/A"}, "x")
    mw._track_focus("get_component", {"path": "/A"}, "x")
    assert list(mw._recent_focus).count("/A") == 1


def test_distill_off_by_default(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_DISTILL", raising=False)
    mw = Middleware()
    assert not mw._distiller_enabled


def test_distill_on_via_env(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")
    mw = Middleware()
    assert mw._distiller_enabled


async def test_maybe_distill_passthrough_when_disabled(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_DISTILL", raising=False)
    mw = Middleware()
    text = "x" * 2000
    result = await mw._maybe_distill("get_hierarchy", {}, text)
    assert result == text


async def test_maybe_distill_no_distill_arg_bypasses(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")
    mw = Middleware()
    mw._recent_focus.append("/Player")
    text = "Scene\n/Player\n/X\n" * 100
    result = await mw._maybe_distill("get_hierarchy", {"_no_distill": True}, text)
    assert result == text


async def test_maybe_distill_audit_footer(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")
    mw = Middleware()
    mw._recent_focus.append("/Player")
    # Build a hierarchy text where Player is a small fraction
    text = "Scene Total: 50\n/Player\n" + "/Other{}\n".format(0) * 200
    result = await mw._maybe_distill("get_hierarchy", {}, text)
    assert "DISTILLED" in result
    assert "full=true" in result
    assert "→" in result


async def test_wrap_send_no_distill_arg_bypasses_through_pipeline(monkeypatch):
    """End-to-end: agent passes _no_distill=True through wrap_send → distill skipped."""
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")
    monkeypatch.setenv("UNITY_MCP_MIDDLEWARE", "1")
    monkeypatch.setenv("UNITY_MCP_REFLECT", "0")
    monkeypatch.setenv("UNITY_MCP_VALIDATE", "0")
    monkeypatch.setenv("UNITY_MCP_PREFETCH_CACHE", "0")
    from unity_mcp.middleware import Middleware, wrap_send

    mw = Middleware()
    mw._recent_focus.append("/Player")

    huge_text = "Scene\n/Player\n" + "/X\n" * 500

    async def mock_send(cmd, args, timeout=30.0):
        assert "_no_distill" not in args
        return {"ok": True, "data": huge_text}

    wrapped = wrap_send(mock_send, mw)
    result = await wrapped("get_hierarchy", {"_no_distill": True})

    assert "DISTILLED" not in result
    assert len(result) >= len(huge_text) * 0.9


def test_seed_preimage_set_property_with_snapshot():
    mw = Middleware()
    if mw._prefetch_cache is None:
        pytest.skip("Prefetch cache disabled in this env")
    # Mock a set_property result with reflect snapshot
    result = "transform.position.x = 1.0\n---\n[Transform]\nposition: (1.0, 2.0, 3.0)\nrotation: (0, 0, 0)"
    mw._seed_preimage("set_property", {"path": "/Player", "component": "Transform"}, result)
    cached = mw._prefetch_cache.get("get_component", {"path": "/Player", "type": "Transform"})
    assert cached is not None
    assert "[CACHED:reflect-snapshot]" in cached
    assert "position" in cached


def test_seed_preimage_no_snapshot_skips():
    mw = Middleware()
    if mw._prefetch_cache is None:
        pytest.skip("Prefetch cache disabled")
    result = "no snapshot here, just plain output"
    mw._seed_preimage("set_property", {"path": "/A", "component": "X"}, result)
    cached = mw._prefetch_cache.get("get_component", {"path": "/A", "type": "X"})
    assert cached is None


# ── D4: full= parameter + distill footer update ───────────────────────────────

def test_distill_footer_message_template():
    """Footer strings in middleware_async.py must reference 'full=true' and 'distilled for brevity'."""
    import inspect
    import unity_mcp.middleware_async as ma
    src = inspect.getsource(ma)
    assert "_no_distill=true" not in src, "Footer must use 'full=true' hint, not '_no_distill=true'"
    assert "full=true" in src, "Footer must say 're-call with full=true' for supported cmds"
    assert "distilled for brevity" in src, "Footer must say 'distilled for brevity' for unsupported cmds"


# ── P-426: distill hint conditional by command ───────────────────────────────

async def test_distill_hint_full_for_supported_cmd(monkeypatch):
    """P-426: get_hierarchy supports full=true, hint must say so."""
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")
    mw = Middleware()
    mw._recent_focus.append("/Player")
    text = "Scene Total: 50\n/Player\n" + "/Other{}\n".format(0) * 200
    result = await mw._maybe_distill("get_hierarchy", {}, text)
    assert "DISTILLED" in result
    assert "full=true" in result


async def test_distill_hint_generic_for_unsupported_cmd(monkeypatch):
    """P-426: get_console does NOT support full=true, hint must NOT say full=true."""
    monkeypatch.setenv("UNITY_MCP_DISTILL", "1")
    mw = Middleware()
    mw._recent_focus.append("/Player")
    # Force a large text that triggers heuristic distillation
    text = "LOG: entry\n" * 500
    result = await mw._maybe_distill("get_console", {}, text)
    assert "DISTILLED" in result, "heuristic must distill 500-line console output"
    assert "full=true" not in result
    assert "distilled for brevity" in result


def test_distill_footer_message_template_conditional():
    """P-426: source must contain both 'full=true' and 'distilled for brevity'."""
    import inspect
    import unity_mcp.middleware_async as ma
    src = inspect.getsource(ma)
    assert "full=true" in src
    assert "distilled for brevity" in src


def test_seed_preimage_no_path_skips():
    mw = Middleware()
    if mw._prefetch_cache is None:
        pytest.skip()
    result = "ok\n---\n[Health]\nhp: 100"
    mw._seed_preimage("set_property", {"component": "Health"}, result)
    # No path → no cache entry. Verify nothing was cached.
    assert mw._prefetch_cache.get("get_component", {"type": "Health"}) is None
