"""Unit tests for scripts/gen_conformance.py.

No Unity, no unity_mcp import — tests inject FakeConformanceToolSchema directly.
"""

import pathlib
import sys
from dataclasses import dataclass, field

import yaml

# Make gen_conformance importable without installing
SCRIPTS_DIR = pathlib.Path(__file__).parent.parent
sys.path.insert(0, str(SCRIPTS_DIR))

from gen_conformance import (  # noqa: E402
    BatchTestGenerator,
    SchemaTestGenerator,
    SeamTestGenerator,
)

# ---------------------------------------------------------------------------
# Fake schema for isolated unit tests
# ---------------------------------------------------------------------------

@dataclass
class FakeConformanceToolSchema:
    name: str = "fake_tool"
    input_schema: dict = field(default_factory=dict)
    required: list = field(default_factory=list)
    properties: dict = field(default_factory=dict)
    mutability: str = "read"
    category: str = "CORE"


# ---------------------------------------------------------------------------
# SchemaTestGenerator._minimal_valid_args
# ---------------------------------------------------------------------------

class TestMinimalValidArgs:
    def test_string_required_write_uses_ns(self):
        schema = FakeConformanceToolSchema(
            required=["name"],
            properties={"name": {"type": "string"}},
            mutability="write",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {"name": "$NS$_test"}

    def test_string_required_read_uses_path(self):
        schema = FakeConformanceToolSchema(
            required=["path"],
            properties={"path": {"type": "string"}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert "$NS$" not in str(result)
        assert isinstance(result["path"], str)

    def test_integer_required(self):
        schema = FakeConformanceToolSchema(
            required=["depth"],
            properties={"depth": {"type": "integer"}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {"depth": 1}

    def test_boolean_required(self):
        schema = FakeConformanceToolSchema(
            required=["flag"],
            properties={"flag": {"type": "boolean"}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {"flag": False}

    def test_array_required(self):
        schema = FakeConformanceToolSchema(
            required=["items"],
            properties={"items": {"type": "array"}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {"items": []}

    def test_enum_required_picks_first(self):
        schema = FakeConformanceToolSchema(
            required=["mode"],
            properties={"mode": {"enum": ["list", "get", "set"]}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {"mode": "list"}

    def test_anyof_null_string_picks_string(self):
        schema = FakeConformanceToolSchema(
            required=["mode"],
            properties={"mode": {"anyOf": [{"type": "string"}, {"type": "null"}]}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result["mode"] is not None

    def test_anyof_null_first_picks_other(self):
        schema = FakeConformanceToolSchema(
            required=["mode"],
            properties={"mode": {"anyOf": [{"type": "null"}, {"type": "integer"}]}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result["mode"] == 1

    def test_action_field_no_enum_returns_get(self):
        schema = FakeConformanceToolSchema(
            required=["action"],
            properties={"action": {"type": "string"}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {"action": "get"}

    def test_action_field_with_enum_uses_first_enum(self):
        schema = FakeConformanceToolSchema(
            required=["action"],
            properties={"action": {"enum": ["list", "get", "set"]}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {"action": "list"}

    def test_no_required_returns_empty(self):
        schema = FakeConformanceToolSchema(required=[], properties={})
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result == {}

    def test_field_override_for_bake_target(self):
        schema = FakeConformanceToolSchema(
            name="bake",
            required=["target"],
            properties={"target": {"type": "string"}},
            mutability="write",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result["target"] == "lighting"

    def test_field_override_for_batch_commands(self):
        schema = FakeConformanceToolSchema(
            name="batch",
            required=["commands"],
            properties={"commands": {"type": "string"}},
            mutability="write",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result["commands"] == "get_status"

    def test_field_override_for_asset_action(self):
        schema = FakeConformanceToolSchema(
            name="asset",
            required=["action"],
            properties={"action": {"type": "string"}},
            mutability="write",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result["action"] == "find"

    def test_unknown_tool_action_still_returns_get(self):
        schema = FakeConformanceToolSchema(
            name="some_other_tool",
            required=["action"],
            properties={"action": {"type": "string"}},
            mutability="read",
        )
        result = SchemaTestGenerator()._minimal_valid_args(schema)
        assert result["action"] == "get"


# ---------------------------------------------------------------------------
# SchemaTestGenerator.generate_valid
# ---------------------------------------------------------------------------

class TestGenerateValid:
    def test_one_case_per_tool(self):
        schemas = [FakeConformanceToolSchema(name=f"tool_{i}") for i in range(5)]
        gen = SchemaTestGenerator()
        cases = [c for s in schemas for c in gen.generate_valid(s)]
        assert len(cases) >= 5

    def test_valid_case_has_expect_ok_true(self):
        schema = FakeConformanceToolSchema(name="get_status", mutability="read")
        cases = SchemaTestGenerator().generate_valid(schema)
        assert len(cases) >= 1
        assert all(c["expect_ok"] is True for c in cases)

    def test_valid_case_has_id(self):
        schema = FakeConformanceToolSchema(name="get_status")
        cases = SchemaTestGenerator().generate_valid(schema)
        assert all("id" in c and "get_status" in c["id"] for c in cases)

    def test_valid_case_has_cmd(self):
        schema = FakeConformanceToolSchema(name="get_status")
        cases = SchemaTestGenerator().generate_valid(schema)
        assert all(c["cmd"] == "get_status" for c in cases)

    def test_all_valid_cases_have_expect_ok_true(self):
        # All schema_valid cases must set expect_ok=True — reachability, not result
        for name in ("autofit_collider", "set_material", "animation", "animator",
                     "particle", "timeline", "get_status"):
            schema = FakeConformanceToolSchema(name=name)
            cases = SchemaTestGenerator().generate_valid(schema)
            assert cases[0]["expect_ok"] is True, f"{name} should have expect_ok=True"

    def test_create_ui_type_override(self):
        # create_ui must get type=Canvas, not __seam_type
        schema = FakeConformanceToolSchema(
            name="create_ui",
            required=["type"],
            properties={"type": {"type": "string"}},
            mutability="write",
        )
        cases = SchemaTestGenerator().generate_valid(schema)
        assert cases[0]["args"]["type"] == "Canvas"

    def test_compile_preflight_field_overrides(self):
        # compile_preflight must get valid C# content and assets path
        schema = FakeConformanceToolSchema(
            name="compile_preflight",
            required=["file_path", "new_content"],
            properties={"file_path": {"type": "string"}, "new_content": {"type": "string"}},
            mutability="read",
        )
        cases = SchemaTestGenerator().generate_valid(schema)
        args = cases[0]["args"]
        assert "Assets/" in args["file_path"]
        assert "class" in args["new_content"]

    def test_extra_args_injected(self):
        # asset tool gets extra type=Material via _EXTRA_ARGS
        schema = FakeConformanceToolSchema(
            name="asset",
            required=["action"],
            properties={"action": {"type": "string"}},
            mutability="read",
        )
        cases = SchemaTestGenerator().generate_valid(schema)
        assert cases[0]["args"].get("type") == "Material"

    def test_extra_args_not_injected_for_unknown(self):
        # Tools not in _EXTRA_ARGS get no extra args
        schema = FakeConformanceToolSchema(name="get_status")
        cases = SchemaTestGenerator().generate_valid(schema)
        assert "type" not in cases[0]["args"]

    def test_ns_in_write_tool_string_args(self):
        schema = FakeConformanceToolSchema(
            name="create_object",
            required=["name"],
            properties={"name": {"type": "string"}},
            mutability="write",
        )
        cases = SchemaTestGenerator().generate_valid(schema)
        assert any("$NS$" in str(c["args"]) for c in cases)

    def test_ns_not_in_read_tool_args(self):
        schema = FakeConformanceToolSchema(
            name="get_hierarchy",
            required=["depth"],
            properties={"depth": {"type": "integer"}},
            mutability="read",
        )
        cases = SchemaTestGenerator().generate_valid(schema)
        assert not any("$NS$" in str(c["args"]) for c in cases)


# ---------------------------------------------------------------------------
# SchemaTestGenerator.generate_invalid
# ---------------------------------------------------------------------------

class TestGenerateInvalid:
    def test_one_case_per_required_field(self):
        schema = FakeConformanceToolSchema(
            name="get_component",
            required=["path", "type"],
            properties={"path": {"type": "string"}, "type": {"type": "string"}},
        )
        cases = SchemaTestGenerator().generate_invalid(schema)
        assert len(cases) == 2

    def test_each_case_omits_one_field(self):
        schema = FakeConformanceToolSchema(
            name="get_component",
            required=["path", "type"],
            properties={"path": {"type": "string"}, "type": {"type": "string"}},
        )
        cases = SchemaTestGenerator().generate_invalid(schema)
        cmds = {frozenset(c["args"].keys()) for c in cases}
        assert frozenset({"type"}) in cmds   # path missing
        assert frozenset({"path"}) in cmds   # type missing

    def test_empty_when_no_required(self):
        schema = FakeConformanceToolSchema(required=[], properties={})
        cases = SchemaTestGenerator().generate_invalid(schema)
        assert cases == []

    def test_invalid_case_has_expect_ok_false(self):
        schema = FakeConformanceToolSchema(
            name="get_component",
            required=["path"],
            properties={"path": {"type": "string"}},
        )
        cases = SchemaTestGenerator().generate_invalid(schema)
        assert all(c["expect_ok"] is False for c in cases)


# ---------------------------------------------------------------------------
# SeamTestGenerator
# ---------------------------------------------------------------------------

class TestSeamTestGenerator:
    def test_generates_six_cases(self):
        cases = SeamTestGenerator().generate()
        assert len(cases) == 6

    def test_each_case_has_steps(self):
        cases = SeamTestGenerator().generate()
        assert all("steps" in c for c in cases)

    def test_each_case_has_id(self):
        cases = SeamTestGenerator().generate()
        assert all("id" in c for c in cases)

    def test_seam_create_hierarchy_present(self):
        cases = SeamTestGenerator().generate()
        ids = [c["id"] for c in cases]
        assert "seam_create_hierarchy" in ids

    def test_cleanup_present_for_write_cases(self):
        cases = SeamTestGenerator().generate()
        # All seam cases should have a cleanup block
        assert all("cleanup" in c for c in cases)


# ---------------------------------------------------------------------------
# BatchTestGenerator
# ---------------------------------------------------------------------------

class TestBatchTestGenerator:
    def test_generates_eight_cases(self):
        cases = BatchTestGenerator().generate()
        assert len(cases) == 8

    def test_each_case_has_id_and_cmd(self):
        cases = BatchTestGenerator().generate()
        assert all("id" in c and "cmd" in c for c in cases)

    def test_batch_two_reads_present(self):
        cases = BatchTestGenerator().generate()
        ids = [c["id"] for c in cases]
        assert "batch_two_reads" in ids


# ---------------------------------------------------------------------------
# YAML round-trip
# ---------------------------------------------------------------------------

def test_yaml_round_trip():
    cases = [{"id": "foo", "cmd": "get_hierarchy", "args": {"depth": 1}, "expect_ok": True}]
    text = yaml.dump({"tests": cases})
    loaded = yaml.safe_load(text)
    assert loaded["tests"] == cases
