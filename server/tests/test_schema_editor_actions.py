"""Tests: editor() schema must expose all 8 action values (MCPAUDIT-004).

FastMCP generates enum from Literal type annotation.
Without Literal, schema is just {"type": "string"} — clients can't validate.
"""
import inspect
from typing import Literal, get_args, get_type_hints

import pytest

from unity_mcp.tools.editor_control import editor


def _get_literal_args():
    """Extract Literal values from the editor function's action annotation."""
    hints = get_type_hints(editor, include_extras=True)
    action_hint = hints.get("action")
    if action_hint is None:
        return None
    args = get_args(action_hint)
    return list(args) if args else None


EXPECTED_ACTIONS = [
    "state", "play", "pause", "stop", "select",
    "project_path", "fast_play_mode", "mutation_mode",
]


def test_editor_schema_has_action_param():
    """Baseline: editor function has an 'action' parameter."""
    sig = inspect.signature(editor)
    assert "action" in sig.parameters


def test_editor_action_schema_includes_all_8_values():
    """All 8 action literals must appear in the type annotation."""
    schema_values = _get_literal_args()
    assert schema_values is not None, "action param has no Literal type — just 'str'"
    for action in EXPECTED_ACTIONS:
        assert action in schema_values, f"Missing action: {action}"


def test_editor_schema_has_enable_param():
    """Regression anchor: enable param exists."""
    sig = inspect.signature(editor)
    assert "enable" in sig.parameters
