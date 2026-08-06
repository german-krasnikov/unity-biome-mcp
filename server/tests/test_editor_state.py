"""Tests for editor_state utility — parse C# EditorStateHelper.GetState() responses."""
import pytest
from unity_mcp.tools.editor_state import (
    parse_editor_field,
    is_play_mode,
    is_compiling,
    is_paused,
)


# ── parse_editor_field ────────────────────────────────────────────────────────

class TestParseEditorField:
    @pytest.mark.parametrize("raw,field,expected", [
        # canonical C# format
        ("playing:True\npaused:False\ncompiling:False\n", "playing", "True"),
        ("playing:True\npaused:False\ncompiling:False\n", "paused", "False"),
        ("playing:True\npaused:False\ncompiling:False\n", "compiling", "False"),
        # path with colons in value — partition preserves it
        ("scene:Assets/Scenes/Main.unity\n", "scene", "Assets/Scenes/Main.unity"),
        # case-insensitive field lookup
        ("PLAYING:FALSE\n", "playing", "FALSE"),
        ("Playing:True\n", "playing", "True"),
        # missing field → None
        ("paused:True\n", "playing", None),
        ("", "playing", None),
        (None, "playing", None),
        ("garbage no colon here", "playing", None),
    ])
    def test_field_extraction(self, raw, field, expected):
        assert parse_editor_field(raw, field) == expected


# ── is_play_mode ──────────────────────────────────────────────────────────────

class TestIsPlayMode:
    @pytest.mark.parametrize("raw,expected", [
        # canonical C# formats (bool.ToString() = "True" / "False")
        ("playing:True\npaused:False\ncompiling:False\n", True),
        ("playing:False\npaused:False\ncompiling:False\n", False),
        # lowercase variations
        ("playing:true\npaused:false\n", True),
        ("playing:false\npaused:false\n", False),
        # uppercase variations
        ("PLAYING:FALSE\nPAUSED:FALSE\n", False),
        ("PLAYING:TRUE\nPAUSED:FALSE\n", True),
        # fail-open cases
        ("", False),
        (None, False),
        ("garbage no colon here", False),
        ("paused:True\ncompiling:False\n", False),   # no playing key → fail-open
        ("dirty:True\nscene:Assets/Scenes/Main.unity\n", False),
        # should not match substring "playing" inside a value
        ("scene:playing:true/object\n", False),
    ])
    def test_is_play_mode(self, raw, expected):
        assert is_play_mode(raw) == expected


# ── is_compiling ──────────────────────────────────────────────────────────────

class TestIsCompiling:
    @pytest.mark.parametrize("raw,expected", [
        ("compiling:True\n", True),
        ("compiling:False\n", False),
        ("compiling:true\n", True),
        ("COMPILING:TRUE\n", True),
        ("", False),
        (None, False),
        ("playing:True\n", False),
    ])
    def test_is_compiling(self, raw, expected):
        assert is_compiling(raw) == expected


# ── is_paused ─────────────────────────────────────────────────────────────────

class TestIsPaused:
    @pytest.mark.parametrize("raw,expected", [
        ("paused:True\n", True),
        ("paused:False\n", False),
        ("paused:true\n", True),
        ("PAUSED:TRUE\n", True),
        ("", False),
        (None, False),
        ("playing:True\n", False),
    ])
    def test_is_paused(self, raw, expected):
        assert is_paused(raw) == expected
