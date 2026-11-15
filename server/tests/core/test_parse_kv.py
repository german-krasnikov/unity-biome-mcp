"""TDD tests for the unified parse_kv / parse_kv_line in utils.py."""
from unity_mcp.utils import parse_kv, parse_kv_line


def test_parse_kv_simple():
    assert parse_kv("key=value other=123") == {"key": "value", "other": "123"}


def test_parse_kv_quoted():
    assert parse_kv('key="hello world" b=2') == {"key": "hello world", "b": "2"}


def test_parse_kv_parens():
    assert parse_kv("value=(1, 0, 0)") == {"value": "(1, 0, 0)"}


def test_parse_kv_parens_no_spaces():
    assert parse_kv("value=(1,0,0)") == {"value": "(1,0,0)"}


def test_parse_kv_mixed():
    result = parse_kv("path=/Player component=Transform prop=m_LocalPosition value=(1, 0, 0)")
    assert result == {
        "path": "/Player",
        "component": "Transform",
        "prop": "m_LocalPosition",
        "value": "(1, 0, 0)",
    }


def test_parse_kv_empty():
    assert parse_kv("") == {}


def test_parse_kv_line_with_cmd():
    cmd, kv = parse_kv_line("set_property path=/Player value=(1,0,0)")
    assert cmd == "set_property"
    assert kv == {"path": "/Player", "value": "(1,0,0)"}


def test_parse_kv_line_empty():
    assert parse_kv_line("") == ("", {})


def test_parse_kv_line_cmd_only():
    cmd, kv = parse_kv_line("some_command")
    assert cmd == "some_command"
    assert kv == {}


def test_parse_kv_hex_color():
    assert parse_kv("color=#ff0000 bg=#c33") == {"color": "#ff0000", "bg": "#c33"}


def test_parse_kv_line_with_parens_and_spaces():
    cmd, kv = parse_kv_line("set_property path=/NPC value=(1, 0, 0) component=Transform")
    assert cmd == "set_property"
    assert kv["value"] == "(1, 0, 0)"
    assert kv["component"] == "Transform"


# ── BUG B: unquoted values with spaces ───────────────────────────────────────

def test_parse_kv_unquoted_value_with_spaces():
    # Value "Assets/bubble blue small.png" is unquoted but multi-word
    result = parse_kv("path=/Obj value=Assets/bubble blue small.png component=Image")
    assert result["value"] == "Assets/bubble blue small.png"
    assert result["component"] == "Image"
    assert "blue" not in result  # regression: "blue" was becoming a spurious key


def test_parse_kv_unquoted_two_spaced_tokens():
    result = parse_kv("a=foo bar b=baz")
    assert result["a"] == "foo bar"
    assert result["b"] == "baz"


def test_parse_kv_quoted_value_with_spaces_still_works():
    # Regression: quoted branch must not break
    result = parse_kv('key="hello world" other=simple')
    assert result["key"] == "hello world"
    assert result["other"] == "simple"


def test_parse_kv_unquoted_single_word_unchanged():
    # Baseline: single-word values must not change behavior
    result = parse_kv("path=/Player value=true")
    assert result["path"] == "/Player"
    assert result["value"] == "true"


def test_parse_kv_value_with_multiple_spaces():
    result = parse_kv("value=Assets/a b c.png comp=SpriteRenderer")
    assert result["value"] == "Assets/a b c.png"
    assert result["comp"] == "SpriteRenderer"


# ── MINOR 3: trailing space handling (symmetric with C# TrimEnd) ──────────────

def test_parse_kv_no_trailing_spaces_in_unquoted_value():
    # Unquoted value with trailing spaces must be rstripped (symmetric with C# TrimEnd)
    result = parse_kv("value=hello   ")
    assert result["value"] == "hello", f"got {result['value']!r}"


def test_parse_kv_quoted_preserves_internal_and_edge_spaces():
    # Quoted values: internal and trailing spaces inside quotes must be preserved
    result = parse_kv('value="hello   " other=x')
    assert result["value"] == "hello   ", f"got {result['value']!r}"


# ── C5: unified regex handles dotted keys (was autobatch._DOTTED_KV_RE only) ─

def test_parse_kv_dotted_key():
    result = parse_kv("Transform.m_LocalPosition=(1,0,0) Health.maxHp=100")
    assert result == {"Transform.m_LocalPosition": "(1,0,0)", "Health.maxHp": "100"}
