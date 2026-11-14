"""TDD tests for surface-parity fixes: D7 direct_only, R2 docstring, U19 docstring."""
import pytest


# ── D7 / V10-B2a: Python-only tools must have direct_only=True ───────────────

def test_checkpoint_create_direct_only():
    from unity_mcp.tools.tool_specs import _SPECS
    assert _SPECS['checkpoint_create'].direct_only is True


def test_checkpoint_restore_direct_only():
    from unity_mcp.tools.tool_specs import _SPECS
    assert _SPECS['checkpoint_restore'].direct_only is True


def test_brief_build_direct_only():
    from unity_mcp.tools.tool_specs import _SPECS
    assert _SPECS['brief_build'].direct_only is True


def test_get_changeset_direct_only():
    from unity_mcp.tools.tool_specs import _SPECS
    assert _SPECS['get_changeset'].direct_only is True


# ── R2: undo_last docstring must mention file-system limitation ───────────────

def test_undo_last_docstring_mentions_filesystem():
    from unity_mcp.tools.editor_control import undo_last
    doc = undo_last.__doc__ or ""
    assert "file" in doc.lower(), (
        "undo_last docstring must warn about file-system operations not being undoable"
    )


# ── U19: screenshot docstring must mention ScreenSpaceOverlay limitation ──────

def test_screenshot_docstring_mentions_sso():
    from unity_mcp.tools.screenshot import screenshot
    doc = screenshot.__doc__ or ""
    assert "ScreenSpaceOverlay" in doc or "screen-space-overlay" in doc.lower(), (
        "screenshot docstring must warn about SSO canvas limitation in Edit Mode"
    )
