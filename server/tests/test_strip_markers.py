"""Unit tests for strip_markers helper used in live tests."""
# Import from live conftest — will fail until implemented
from tests.live.conftest import _ok, strip_markers


def test_strip_markers_removes_confidence_suffix():
    text = "hello\n[confidence: 1.00]extra stuff"
    assert strip_markers(text) == "hello"


def test_strip_markers_preserves_clean_text():
    assert strip_markers("clean response") == "clean response"


def test_strip_markers_inline_confidence():
    """[confidence:...] mid-line: only strip from the newline before it."""
    text = "line1\nline2\n[confidence: 0.87] something"
    assert strip_markers(text) == "line1\nline2"


def test_strip_markers_empty_string():
    assert strip_markers("") == ""


def test_strip_markers_only_marker():
    assert strip_markers("[confidence: 1.00]") == ""


def test_ok_strips_console_error_annotation():
    """_ok() must strip_markers like _execute_checked/_response_data does.

    Otherwise unrelated console noise (e.g. from a concurrent probe) appended
    as a "CONSOLE ERRORS:" suffix breaks exact-string assertions on the raw
    execute_code payload (e.g. additive_scene's created_path == scene_path).
    """
    result = {
        "ok": True,
        "data": "Assets/Foo.unity\n⚠ CONSOLE ERRORS:\nunrelated error",
    }
    assert _ok(result) == "Assets/Foo.unity"
