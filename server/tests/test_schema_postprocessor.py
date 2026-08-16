"""Pure unit tests for _schema_postprocessor.postprocess_schema.

No FastMCP, no Unity, no mocks — all inputs are plain dicts.
"""
import pytest
from unity_mcp.tools._schema_postprocessor import _inject_description, postprocess_schema
from unity_mcp.tools._param_descriptions import _COMMON, PARAM_DESCRIPTIONS


# ─── _inject_description ──────────────────────────────────────────────────────

class TestInjectDescription:
    def test_common_description_applied(self):
        pdef = {"type": "string"}
        _inject_description("path", pdef, {})
        assert pdef["description"] == _COMMON["path"]

    def test_tool_specific_overrides_common(self):
        tool_descs = {"path": "Custom path description"}
        pdef = {"type": "string"}
        _inject_description("path", pdef, tool_descs)
        assert pdef["description"] == "Custom path description"

    def test_existing_description_not_overwritten(self):
        pdef = {"type": "string", "description": "original"}
        _inject_description("path", pdef, {})
        assert pdef["description"] == "original"

    def test_unknown_param_no_description(self):
        pdef = {"type": "string"}
        _inject_description("zzz_unknown_param", pdef, {})
        assert "description" not in pdef


# ─── postprocess_schema ───────────────────────────────────────────────────────

class TestAdditionalProperties:
    def test_injected_when_absent(self):
        schema = {"properties": {"x": {"type": "string"}}}
        postprocess_schema("unknown_tool", schema)
        assert schema["additionalProperties"] is False

    def test_not_overwritten_when_present(self):
        schema = {"properties": {"x": {"type": "string"}}, "additionalProperties": True}
        postprocess_schema("unknown_tool", schema)
        assert schema["additionalProperties"] is True

    def test_not_injected_without_properties(self):
        schema = {"type": "object"}
        postprocess_schema("unknown_tool", schema)
        assert "additionalProperties" not in schema


class TestDescriptionInjection:
    def test_common_desc_injected(self):
        schema = {"properties": {"path": {"type": "string"}}}
        postprocess_schema("unknown_tool", schema)
        assert "description" in schema["properties"]["path"]
        assert "Scene path" in schema["properties"]["path"]["description"]

    def test_tool_specific_desc_injected(self):
        schema = {"properties": {"type": {"type": "string"}}}
        postprocess_schema("get_component", schema)
        desc = schema["properties"]["type"]["description"]
        assert "Transform" in desc or "Rigidbody" in desc

    def test_existing_desc_not_overwritten(self):
        schema = {"properties": {"path": {"type": "string", "description": "kept"}}}
        postprocess_schema("unknown_tool", schema)
        assert schema["properties"]["path"]["description"] == "kept"

    @pytest.mark.parametrize(
        "tool,param,token,forbidden",
        [
            ("asset", "path", "asset/package", "Scene path"),
            ("asset", "name", "Asset-name", "Name of the GameObject"),
            ("asset", "type", "asset type", "Component type"),
            ("asset", "value", "asset action", "New value to set"),
            ("build", "path", "build output", "Scene path"),
            ("lint_playtest", "path", ".playtest", "Scene path"),
            ("package", "name", "package identifier", "GameObject"),
            ("screenshot_baseline", "name", "baseline identifier", "Name of the GameObject"),
            ("screenshot_compare", "name", "baseline identifier", "Name of the GameObject"),
            ("scriptable_object", "path", ".asset", "Scene path"),
            ("shader", "path", ".shader", "Scene path"),
            ("shader", "name", "Shader Graph", "Name of the GameObject"),
            ("shader", "type", "property value type", "Component type"),
            ("uitk_file", "path", ".uxml", "Scene path"),
            ("uitk_intent", "name", "filename", "Name of the GameObject"),
            ("uitk_intent", "path", "output folder", "Scene path"),
            ("wait_until", "value", "compare", "New value to set"),
            ("apply_template", "name", "template identifier", "GameObject"),
            ("save_skill", "name", "learned skill", "GameObject"),
            ("save_template", "name", "scene template", "GameObject"),
            ("use_skill", "name", "skill identifier", "GameObject"),
            ("autofit_collider", "type", "Collider shape", "Component type"),
            ("create_ui", "type", "uGUI element type", "Component type"),
            ("lint_scene_refs", "path", ".playtest", "Scene path"),
            ("lint_uitk", "path", "UXML or USS", "Scene path"),
            ("menu", "path", "menu-item path", "Scene path"),
            ("uitk_element", "name", "VisualElement name", "GameObject"),
            ("timeline", "name", "Timeline", "GameObject"),
            ("timeline", "value", "Timeline value", "New value to set"),
            ("get_console", "level", "assert", ""),
        ],
    )
    def test_false_generic_fallbacks_are_overridden_in_rendered_schema(
        self, tool, param, token, forbidden
    ):
        schema = {"properties": {param: {"type": "string"}}}

        postprocess_schema(tool, schema)

        description = schema["properties"][param]["description"]
        assert token in description
        if forbidden:
            assert forbidden not in description


class TestIdempotency:
    def test_calling_twice_is_safe(self):
        schema = {"properties": {"path": {"type": "string"}}}
        postprocess_schema("get_component", schema)
        first_desc = schema["properties"]["path"]["description"]
        postprocess_schema("get_component", schema)
        assert schema["properties"]["path"]["description"] == first_desc
        assert schema["additionalProperties"] is False

    def test_additional_properties_not_doubled(self):
        schema = {"properties": {}}
        postprocess_schema("unknown_tool", schema)
        postprocess_schema("unknown_tool", schema)
        assert schema["additionalProperties"] is False


class TestAnyOfPreserved:
    def test_anyof_not_stripped(self):
        """Per architect correction: anyOf must NOT be stripped."""
        schema = {"properties": {"path": {"anyOf": [{"type": "string"}, {"type": "null"}]}}}
        postprocess_schema("unknown_tool", schema)
        assert "anyOf" in schema["properties"]["path"]


class TestIntegerPreserved:
    def test_integer_type_not_converted(self):
        """Per architect correction: integer must NOT be converted to number."""
        schema = {"properties": {"count": {"type": "integer"}}}
        postprocess_schema("unknown_tool", schema)
        assert schema["properties"]["count"]["type"] == "integer"
