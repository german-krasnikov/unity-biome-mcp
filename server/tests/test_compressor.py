"""Tests for response compressor — strip default values from component reads."""
from unity_mcp.compressor import strip_defaults, project_fields


SAMPLE_COMPONENT = """\
[Transform]
position: (0, 0, 0)
rotation: (0, 0, 0, 1)
localScale: (1, 1, 1)
localPosition: (5, 3, 0)
localRotation: (0, 0.5, 0, 0.866)
---
[Rigidbody]
mass: 1
drag: 0
angularDrag: 0.05
useGravity: true
isKinematic: false
velocity: (0, 0, 0)
"""


def test_strip_defaults_removes_zero_tuple():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "position: (0, 0, 0)" not in result


def test_strip_defaults_removes_identity_quaternion():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "rotation: (0, 0, 0, 1)" not in result


def test_strip_defaults_removes_unit_scale():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "localScale: (1, 1, 1)" not in result


def test_strip_defaults_keeps_nondefault_values():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "localPosition: (5, 3, 0)" in result
    assert "localRotation: (0, 0.5, 0, 0.866)" in result


def test_strip_defaults_keeps_section_headers():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "[Transform]" in result
    assert "[Rigidbody]" in result


def test_strip_defaults_keeps_separators():
    result = strip_defaults(SAMPLE_COMPONENT)
    assert "---" in result


def test_strip_defaults_removes_false():
    data = "[C]\nisKinematic: false\nenabled: true\n"
    result = strip_defaults(data)
    assert "isKinematic" not in result
    assert "enabled: true" in result


def test_strip_defaults_removes_zero_int():
    data = "[C]\ndrag: 0\nmass: 2\n"
    result = strip_defaults(data)
    assert "drag" not in result
    assert "mass: 2" in result


def test_strip_defaults_removes_null():
    data = "[C]\ntarget: null\nname: Player\n"
    result = strip_defaults(data)
    assert "target" not in result
    assert "name: Player" in result


def test_strip_defaults_removes_empty_string():
    data = '[C]\ntag: ""\nname: Cube\n'
    result = strip_defaults(data)
    assert 'tag' not in result
    assert "name: Cube" in result


def test_strip_defaults_removes_empty_list():
    data = "[C]\nevents: []\nchildren: [a, b]\n"
    result = strip_defaults(data)
    assert "events" not in result
    assert "children" in result


def test_strip_defaults_removes_transparent_color():
    data = "[C]\ncolor: #00000000\noutlineColor: #FF0000FF\n"
    result = strip_defaults(data)
    assert "color: #00000000" not in result
    assert "outlineColor" in result


def test_strip_defaults_keeps_error_lines():
    data = "[C]\nerr: something went wrong\nvalue: 0\n"
    result = strip_defaults(data)
    assert "err:" in result


def test_strip_defaults_noop_on_empty():
    assert strip_defaults("") == ""


def test_strip_defaults_removes_none_string():
    data = "[C]\nparent: None\nname: X\n"
    result = strip_defaults(data)
    assert "parent" not in result
    assert "name: X" in result


# ─── F08: expanded defaults ───────────────────────────────────────────────────

def test_strip_defaults_removes_mass_one():
    # Item 22: "mass: 1" is a user field — NOT stripped. Only "m_mass: 1" is stripped.
    data = "[C]\nmass: 1\ndrag: 5\n"
    result = strip_defaults(data)
    assert "mass: 1" in result
    assert "drag: 5" in result


def test_strip_defaults_removes_untagged():
    data = "[C]\ntag: Untagged\nlayer: Default\nname: Cube\n"
    result = strip_defaults(data)
    assert "tag" not in result
    assert "layer" not in result
    assert "name: Cube" in result


def test_strip_defaults_removes_vector2_zero():
    data = "[C]\noffset: (0, 0)\nsize: (100, 50)\n"
    result = strip_defaults(data)
    assert "offset" not in result
    assert "size: (100, 50)" in result


def test_strip_defaults_removes_white_color():
    data = "[C]\ncolor: #FFFFFFFF\noutline: #FF0000FF\n"
    result = strip_defaults(data)
    assert "color" not in result
    assert "outline: #FF0000FF" in result


def test_strip_defaults_removes_vector4_zero():
    data = "[C]\npadding: (0, 0, 0, 0)\nmargin: (1, 2, 3, 4)\n"
    result = strip_defaults(data)
    assert "padding" not in result
    assert "margin: (1, 2, 3, 4)" in result


# ─── F07: fields= projection ──────────────────────────────────────────────────

def test_project_fields_keeps_only_requested():
    result = project_fields(SAMPLE_COMPONENT, "mass,localPosition")
    assert "mass: 1" in result
    assert "localPosition: (5, 3, 0)" in result
    assert "drag: 0" not in result
    assert "velocity" not in result


def test_project_fields_case_insensitive():
    result = project_fields(SAMPLE_COMPONENT, "MASS")
    assert "mass: 1" in result


def test_project_fields_dotted_subfields():
    data = "[Transform]\nm_LocalPosition.x: 5\nm_LocalPosition.y: 3\nm_Mass: 2\n"
    result = project_fields(data, "m_LocalPosition")
    assert "m_LocalPosition.x: 5" in result
    assert "m_LocalPosition.y: 3" in result
    assert "m_Mass" not in result


def test_project_fields_exact_subfield():
    data = "[T]\nm_LocalPosition.x: 5\nm_LocalPosition.y: 3\n"
    result = project_fields(data, "m_LocalPosition.x")
    assert "m_LocalPosition.x: 5" in result
    assert "m_LocalPosition.y" not in result


def test_project_fields_prefix_requires_dot_boundary():
    """Requesting 'pos' must NOT match 'position' (no loose substring matching)."""
    data = "[C]\nposition: (5, 0, 0)\npos: 1\n"
    result = project_fields(data, "pos")
    assert "pos: 1" in result
    assert "position:" not in result


def test_project_fields_keeps_headers_and_separators():
    result = project_fields(SAMPLE_COMPONENT, "mass")
    assert "[Transform]" in result
    assert "[Rigidbody]" in result
    assert "---" in result


def test_project_fields_keeps_error_lines():
    data = "[C]\nerr: not found\nmass: 1\n"
    result = project_fields(data, "drag")
    assert "err: not found" in result


def test_project_fields_no_match_returns_available():
    """When requested field doesn't match, report available keys."""
    data = "[Rigidbody]\nm_Mass: 1.0\nm_Drag: 0.0\n"
    result = project_fields(data, "velocity")
    assert "0 fields matched" in result
    assert "m_mass" in result or "m_drag" in result


def test_project_fields_empty_fields_returns_all():
    assert project_fields(SAMPLE_COMPONENT, "") == SAMPLE_COMPONENT
    assert project_fields(SAMPLE_COMPONENT, "  ,  ") == SAMPLE_COMPONENT


def test_project_fields_noop_on_empty_text():
    assert project_fields("", "mass") == ""


# ─── Zone 9: compression edge cases ──────────────────────────────────────────

# float 0.0 and 1.0 are in _DEFAULTS — verify stripping
def test_strip_defaults_removes_float_zero():
    data = "[C]\nspeed: 0.0\nhealth: 50.0\n"
    result = strip_defaults(data)
    assert "speed" not in result
    assert "health: 50.0" in result


def test_strip_defaults_removes_float_one():
    # Item 22: "1.0" removed from _DEFAULTS; plain "volume: 1.0" is now kept.
    data = "[C]\nvolume: 1.0\npitch: 2.0\n"
    result = strip_defaults(data)
    assert "volume: 1.0" in result
    assert "pitch: 2.0" in result


# float-tuple variants
def test_strip_defaults_removes_float_zero_tuple_3():
    data = "[C]\nposition: (0.0, 0.0, 0.0)\nlocalPos: (1.0, 2.0, 3.0)\n"
    result = strip_defaults(data)
    assert "position: (0.0, 0.0, 0.0)" not in result
    assert "localPos: (1.0, 2.0, 3.0)" in result


def test_strip_defaults_removes_float_identity_quaternion():
    data = "[C]\nrotation: (0.0, 0.0, 0.0, 1.0)\nother: (0.0, 0.0, 0.0, 0.5)\n"
    result = strip_defaults(data)
    assert "rotation: (0.0, 0.0, 0.0, 1.0)" not in result
    assert "other: (0.0, 0.0, 0.0, 0.5)" in result


def test_strip_defaults_removes_float_unit_scale():
    data = "[C]\nscale: (1.0, 1.0, 1.0)\nsize: (2.0, 2.0, 2.0)\n"
    result = strip_defaults(data)
    assert "scale: (1.0, 1.0, 1.0)" not in result
    assert "size: (2.0, 2.0, 2.0)" in result


# blank line behavior
def test_strip_defaults_preserves_trailing_blank_line():
    data = "[C]\nvalue: 5\n   \n"
    result = strip_defaults(data)
    # Blank/whitespace-only lines are kept (not stripped as default)
    assert "value: 5" in result


def test_strip_defaults_preserves_multiple_blank_lines():
    data = "[C]\n\n\nvalue: 5\n\n"
    result = strip_defaults(data)
    assert "value: 5" in result


# None as fields parameter
def test_project_fields_none_fields_returns_all():
    result = project_fields(SAMPLE_COMPONENT, None)
    assert result == SAMPLE_COMPONENT


# empty string input
def test_strip_defaults_empty_string_returns_empty():
    assert strip_defaults("") == ""


# single-line input (no newlines)
def test_strip_defaults_single_line_default_removed():
    # Item 22: "mass: 1" is not a Unity internal field — kept.
    result = strip_defaults("[C]\nmass: 1")
    assert "mass: 1" in result


def test_strip_defaults_single_line_nondefault_kept():
    result = strip_defaults("[C]\nmass: 5")
    assert "mass: 5" in result


def test_project_fields_single_line_no_newline():
    data = "[C]\nmass: 5"
    result = project_fields(data, "mass")
    assert "mass: 5" in result


# unicode content
def test_strip_defaults_unicode_value_kept():
    data = "[C]\nname: Игрок\nvalue: 0\n"
    result = strip_defaults(data)
    assert "name: Игрок" in result
    assert "value" not in result


def test_project_fields_unicode_key_matched():
    data = "[C]\n名前: テスト\nname: Player\n"
    result = project_fields(data, "name")
    assert "name: Player" in result


# ── D5: Field aliases ──────────────────────────────────────────────────────────

SERIALIZED_DATA = """\
[Transform]
m_LocalPosition.x: 5
m_LocalPosition.y: 3
m_LocalRotation.x: 0
m_LocalScale.x: 1
m_Mass: 2
m_Enabled: true
m_IsActive: true
m_Name: Player
m_TagString: Untagged
m_Layer: 0
"""


def test_alias_position_maps_to_m_localposition():
    result = project_fields(SERIALIZED_DATA, "position")
    assert "m_LocalPosition.x: 5" in result
    assert "m_LocalPosition.y: 3" in result
    assert "m_Mass" not in result


def test_alias_localposition_maps_to_m_localposition():
    result = project_fields(SERIALIZED_DATA, "localposition")
    assert "m_LocalPosition.x: 5" in result


def test_alias_rotation_maps_to_m_localrotation():
    result = project_fields(SERIALIZED_DATA, "rotation")
    assert "m_LocalRotation.x: 0" in result


def test_alias_scale_maps_to_m_localscale():
    result = project_fields(SERIALIZED_DATA, "scale")
    assert "m_LocalScale.x: 1" in result


def test_alias_mass_maps_to_m_mass():
    result = project_fields(SERIALIZED_DATA, "mass")
    assert "m_Mass: 2" in result


def test_alias_enabled_maps_to_m_enabled():
    result = project_fields(SERIALIZED_DATA, "enabled")
    assert "m_Enabled: true" in result


def test_alias_active_maps_to_m_isactive():
    result = project_fields(SERIALIZED_DATA, "active")
    assert "m_IsActive: true" in result


def test_alias_name_maps_to_m_name():
    result = project_fields(SERIALIZED_DATA, "name")
    assert "m_Name: Player" in result


def test_alias_mixed_canonical_and_alias():
    """Can mix canonical names with aliases in one call."""
    result = project_fields(SERIALIZED_DATA, "position,mass")
    assert "m_LocalPosition.x: 5" in result
    assert "m_Mass: 2" in result
    assert "m_Layer" not in result


def test_alias_case_insensitive():
    result = project_fields(SERIALIZED_DATA, "POSITION")
    assert "m_LocalPosition.x: 5" in result


# ─── Item 22: context-aware strip_defaults ───────────────────────────────────

def test_strip_defaults_keeps_mass_one():
    """'mass: 1' must NOT be stripped — 'mass' is not a known-default field."""
    data = "[C]\nmass: 1\ndrag: 5\n"
    result = strip_defaults(data)
    assert "mass: 1" in result
    assert "drag: 5" in result


def test_strip_defaults_strips_m_mass_default():
    """'m_mass: 1' IS stripped — m_mass Unity internal field defaults to 1."""
    data = "[C]\nm_mass: 1\ndrag: 5\n"
    result = strip_defaults(data)
    assert "m_mass" not in result
    assert "drag: 5" in result


def test_strip_defaults_still_strips_zero():
    """'x: 0' still stripped — 0 remains in _DEFAULTS unconditionally."""
    data = "[C]\nx: 0\ny: 5\n"
    result = strip_defaults(data)
    assert "x: 0" not in result
    assert "y: 5" in result
