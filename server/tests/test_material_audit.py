"""TDD tests for material_audit tool (Phase 5/4).

Red: these tests FAIL until material_audit is added to asset.py.
"""
import asyncio
from unittest.mock import AsyncMock, MagicMock
import pytest

_ARGS = lambda **kw: {k: v for k, v in kw.items() if v is not None}


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _run(coro):
    return asyncio.run(coro)


def _call_material_audit(action="summary", platform=None):
    import unity_mcp.tools.asset as m
    orig_send, orig_args = m._send, m._args
    m._args = _ARGS
    mock_send = AsyncMock(return_value="MATERIAL AUDIT summary ok")
    m._send = mock_send
    try:
        _run(m.material_audit(action=action, platform=platform))
    finally:
        m._send, m._args = orig_send, orig_args
    return mock_send


# ---------------------------------------------------------------------------
# Tests
# ---------------------------------------------------------------------------

def test_material_audit_default_action_summary():
    mock_send = _call_material_audit()
    assert mock_send.called
    cmd, sent = mock_send.call_args[0]
    assert cmd == "material_audit"
    assert sent.get("action") == "summary"


def test_material_audit_forwards_platform():
    mock_send = _call_material_audit(action="compression", platform="Android")
    _, sent = mock_send.call_args[0]
    assert sent.get("platform") == "Android"


@pytest.mark.parametrize("action", [
    "summary", "materials", "textures", "duplicates", "compression", "recommendations",
])
def test_material_audit_all_valid_actions(action):
    mock_send = _call_material_audit(action=action)
    _, sent = mock_send.call_args[0]
    assert sent.get("action") == action


def test_material_audit_null_platform_not_in_args():
    mock_send = _call_material_audit(action="summary", platform=None)
    _, sent = mock_send.call_args[0]
    assert "platform" not in sent


def test_material_audit_registered_readonly():
    """material_audit must be registered with readOnlyHint=True."""
    import unity_mcp.tools.asset as m
    from mcp.types import ToolAnnotations

    registered = {}

    def mock_tool(annotations=None):
        def decorator(fn):
            registered[fn.__name__] = annotations
            return fn
        return decorator

    mock_mcp = MagicMock()
    mock_mcp.tool = mock_tool
    orig_send, orig_args = m._send, m._args
    try:
        m._send = AsyncMock()
        m._args = _ARGS
        m.register(mock_mcp, AsyncMock(), _ARGS)
        assert "material_audit" in registered, "material_audit not registered"
        ann = registered["material_audit"]
        assert isinstance(ann, ToolAnnotations), f"Expected ToolAnnotations, got {ann!r}"
        assert ann.readOnlyHint is True, "material_audit must be readOnly"
    finally:
        m._send, m._args = orig_send, orig_args


def test_material_audit_in_assets_category():
    from unity_mcp.tools.gating import _THEMED_CATEGORIES
    assert "material_audit" in _THEMED_CATEGORIES["ASSETS"], (
        "material_audit must be in ASSETS gating category"
    )
