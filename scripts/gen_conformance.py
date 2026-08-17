"""Generate conformance test YAML from _SPECS tool schemas.

Usage:
    python scripts/gen_conformance.py               # write 4 YAML files
    python scripts/gen_conformance.py --dry-run     # print counts only
    python scripts/gen_conformance.py --diff        # exit 2 if files differ from committed
    python scripts/gen_conformance.py --seed 42     # hypothesis seed (default 42)
"""
from __future__ import annotations

import argparse
import hashlib
import pathlib
import sys
from dataclasses import dataclass
from datetime import datetime, timezone
from typing import Any

import yaml

# Make server/src importable from scripts/
_ROOT = pathlib.Path(__file__).parent.parent
sys.path.insert(0, str(_ROOT / "server" / "src"))

_OUT_DIR = _ROOT / "server" / "tests" / "conformance" / "generated"


# ---------------------------------------------------------------------------
# Data model
# ---------------------------------------------------------------------------

@dataclass(frozen=True)
class ConformanceToolSchema:
    name: str
    input_schema: dict
    required: list[str]
    properties: dict
    mutability: str   # 'read' | 'write'
    category: str


# ---------------------------------------------------------------------------
# Schema loader
# ---------------------------------------------------------------------------

def _load_schemas() -> list[ConformanceToolSchema]:
    from unity_mcp.server import mcp  # noqa: PLC0415
    from unity_mcp.tools.tool_specs import _SPECS  # noqa: PLC0415

    tools_map: dict = mcp._tool_manager._tools
    result = []
    for name, spec in sorted(_SPECS.items()):
        if spec.direct_only or spec.runtime_only or spec.category == "_INTERNAL":
            continue
        tool = tools_map.get(name)
        if tool is None:
            continue
        schema: dict = tool.parameters or {}
        result.append(ConformanceToolSchema(
            name=name,
            input_schema=schema,
            required=schema.get("required", []),
            properties=schema.get("properties", {}),
            mutability=spec.mutability,
            category=spec.category,
        ))
    return result


# ---------------------------------------------------------------------------
# SchemaTestGenerator
# ---------------------------------------------------------------------------

# Per-tool field value overrides when schema has no enum and the generic default fails.
# Key: (tool_name, field_name) → value to use in generated test args.
_FIELD_OVERRIDES: dict[tuple[str, str], Any] = {
    ("asset", "action"): "find",          # 'get' is not a valid asset action
    ("bake", "target"): "lighting",       # target has no enum; 'lighting' is the first valid value
    ("batch", "commands"): "get_status",  # __seam_commands is not a valid command
    # create_ui: type has no enum in schema; use first value from docstring description
    ("create_ui", "type"): "Canvas",
    # compile_preflight: valid C# so the preflight check can route correctly
    ("compile_preflight", "file_path"): "Assets/TestsTemp/PreflightTest.cs",
    ("compile_preflight", "new_content"): "using UnityEngine; public class PreflightTest : MonoBehaviour {}",
}

# Extra optional args injected per-tool beyond the required fields.
# Used when a tool needs at least one optional param to route without error.
_EXTRA_ARGS: dict[str, dict[str, Any]] = {
    "asset": {"type": "Material"},  # asset find needs type or name; neither is required
}


class SchemaTestGenerator:
    def __init__(self, seed: int = 42):
        self._seed = seed

    def generate_valid(self, schema: ConformanceToolSchema) -> list[dict]:
        args = self._minimal_valid_args(schema)
        case: dict[str, Any] = {
            "id": f"{schema.name}_valid",
            "cmd": schema.name,
            "args": args,
            "expect_ok": True,
        }
        # Write tools that create objects need cleanup
        if schema.mutability == "write" and "name" in args and "$NS$" in str(args.get("name", "")):
            case["cleanup"] = [{"cmd": "delete_object", "args": {"path": f"/${args['name']}"}}]
        return [case]

    def generate_invalid(self, schema: ConformanceToolSchema) -> list[dict]:
        if not schema.required:
            return []
        cases = []
        # Fill all required fields first
        all_args = self._minimal_valid_args(schema)
        for missing_field in schema.required:
            args = {k: v for k, v in all_args.items() if k != missing_field}
            cases.append({
                "id": f"{schema.name}_missing_{missing_field}",
                "cmd": schema.name,
                "args": args,
                "expect_ok": False,
            })
        return cases

    def _minimal_valid_args(self, schema: ConformanceToolSchema) -> dict:
        result = {}
        for field_name in schema.required:
            override = _FIELD_OVERRIDES.get((schema.name, field_name))
            if override is not None:
                result[field_name] = override
            else:
                prop = schema.properties.get(field_name, {})
                result[field_name] = self._minimal_value(field_name, prop, schema.mutability)
        # Inject extra optional args for tools that need them to route correctly
        result.update(_EXTRA_ARGS.get(schema.name, {}))
        return result

    def _minimal_value(self, field_name: str, prop: dict, mutability: str) -> Any:
        # Handle anyOf — prefer non-null branch
        if "anyOf" in prop:
            non_null = [b for b in prop["anyOf"] if b.get("type") != "null"]
            branch = non_null[0] if non_null else prop["anyOf"][0]
            if branch.get("type") == "null":
                return None
            return self._minimal_value(field_name, branch, mutability)

        if "enum" in prop:
            return prop["enum"][0]

        t = prop.get("type", "string")
        if t == "string":
            # Write tools that use 'name' get $NS$ prefix
            if mutability == "write" and field_name == "name":
                return "$NS$_test"
            # Known path-like fields
            if field_name in ("path", "paths", "parent"):
                return "/Main Camera"
            # Action-pattern tools need a valid read-safe action
            if field_name == "action":
                return "get"
            return f"__seam_{field_name}"
        if t == "integer":
            return 1
        if t == "number":
            return 1.0
        if t == "boolean":
            return False
        if t == "array":
            return []
        if t == "object":
            return {}
        return f"__seam_{field_name}"


# ---------------------------------------------------------------------------
# SeamTestGenerator
# ---------------------------------------------------------------------------

class SeamTestGenerator:
    _SEAM_CASES: list[dict] = [
        {
            "id": "seam_create_hierarchy",
            "steps": [
                {"cmd": "create_object", "args": {"name": "$NS$_cth"}, "expect_ok": True},
                {"cmd": "get_hierarchy", "args": {"depth": 1},
                 "expect_ok": True, "expect_data_contains": "$NS$_cth"},
            ],
            "cleanup": [{"cmd": "delete_object", "args": {"path": "/$NS$_cth"}}],
        },
        {
            "id": "seam_set_property_readback",
            "steps": [
                {"cmd": "create_object", "args": {"name": "$NS$_spr"}, "expect_ok": True},
                {"cmd": "set_property", "args": {
                    "path": "/$NS$_spr", "component": "Transform",
                    "prop": "m_LocalPosition", "value": "3,5,7"
                }, "expect_ok": True},
                {"cmd": "get_component", "args": {"path": "/$NS$_spr", "type": "Transform"},
                 "expect_ok": True, "expect_data_contains": "3"},
            ],
            "cleanup": [{"cmd": "delete_object", "args": {"path": "/$NS$_spr"}}],
        },
        {
            "id": "seam_manage_component",
            "steps": [
                {"cmd": "create_object", "args": {"name": "$NS$_mc"}, "expect_ok": True},
                {"cmd": "manage_component", "args": {
                    "path": "/$NS$_mc", "type": "Rigidbody", "action": "add"
                }, "expect_ok": True},
                {"cmd": "get_component", "args": {"path": "/$NS$_mc", "type": "Rigidbody"},
                 "expect_ok": True},
            ],
            "cleanup": [{"cmd": "delete_object", "args": {"path": "/$NS$_mc"}}],
        },
        {
            "id": "seam_set_active_false",
            "steps": [
                {"cmd": "create_object", "args": {"name": "$NS$_sa"}, "expect_ok": True},
                {"cmd": "set_active", "args": {"path": "/$NS$_sa", "active": "false"},
                 "expect_ok": True},
                {"cmd": "get_hierarchy", "args": {"depth": 1},
                 "expect_ok": True, "expect_data_contains": "$NS$_sa"},
            ],
            "cleanup": [{"cmd": "delete_object", "args": {"path": "/$NS$_sa"}}],
        },
        {
            "id": "seam_rename",
            "steps": [
                {"cmd": "create_object", "args": {"name": "$NS$_old"}, "expect_ok": True},
                {"cmd": "rename_object", "args": {
                    "path": "/$NS$_old", "name": "$NS$_renamed"
                }, "expect_ok": True},
                {"cmd": "get_hierarchy", "args": {"depth": 1},
                 "expect_ok": True, "expect_data_contains": "$NS$_renamed"},
            ],
            "cleanup": [{"cmd": "delete_object", "args": {"path": "/$NS$_renamed"}}],
        },
        {
            "id": "seam_delete_absent",
            "steps": [
                {"cmd": "create_object", "args": {"name": "$NS$_del"}, "expect_ok": True},
                {"cmd": "delete_object", "args": {"path": "/$NS$_del"}, "expect_ok": True},
                {"cmd": "find_objects", "args": {"name": "$NS$_del"},
                 "expect_ok": True, "expect_data_not_contains": "$NS$_del"},
            ],
            "cleanup": [],
        },
    ]

    def generate(self) -> list[dict]:
        return list(self._SEAM_CASES)


# ---------------------------------------------------------------------------
# BatchTestGenerator
# ---------------------------------------------------------------------------

class BatchTestGenerator:
    _BATCH_CASES: list[dict] = [
        {
            "id": "batch_two_reads",
            "cmd": "batch",
            "args": {"commands": "get_status\nget_compile_errors"},
            "expect_ok": True,
            "expect_data_contains": "ok:",
        },
        {
            "id": "batch_error_then_continue",
            "cmd": "batch",
            "args": {"commands": "__zzz_nonexistent\nget_status", "on_error": "continue"},
            "expect_ok": False,
        },
        {
            "id": "batch_count_three",
            "cmd": "batch",
            "args": {"commands": "get_status\nget_compile_errors\nget_hierarchy depth=1"},
            "expect_ok": True,
            "expect_result_count": 3,
        },
        {
            "id": "batch_single_read",
            "cmd": "batch",
            "args": {"commands": "get_status"},
            "expect_ok": True,
            "expect_data_contains": "ok:",
        },
        {
            "id": "batch_compile_errors",
            "cmd": "batch",
            "args": {"commands": "get_compile_errors"},
            "expect_ok": True,
        },
        {
            "id": "batch_hierarchy",
            "cmd": "batch",
            "args": {"commands": "get_hierarchy depth=2"},
            "expect_ok": True,
        },
        {
            "id": "batch_empty_string",
            "cmd": "batch",
            "args": {"commands": "get_status"},
            "expect_ok": True,
        },
        {
            "id": "batch_error_stop",
            "cmd": "batch",
            "args": {"commands": "__zzz_bad\nget_status", "on_error": "stop"},
            "expect_ok": False,
        },
    ]

    def generate(self) -> list[dict]:
        return list(self._BATCH_CASES)


# ---------------------------------------------------------------------------
# YAML writer helpers
# ---------------------------------------------------------------------------

def _write_yaml(path: pathlib.Path, header_comment: str, cases: list[dict]) -> str:
    body = yaml.dump({"tests": cases}, allow_unicode=True, sort_keys=False)
    return header_comment + body


def _header(tool_count: int) -> str:
    ts = datetime.now(tz=timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
    return (
        "# Auto-generated by gen_conformance.py — do not edit manually\n"
        "# schema_version: 1\n"
        f"# generated: {ts}\n"
        f"# tool_count: {tool_count}\n"
    )


def _sha256_body(text: str) -> str:
    # Strip the "# generated:" timestamp line for stable comparison
    lines = [l for l in text.splitlines() if not l.startswith("# generated:")]
    return hashlib.sha256("\n".join(lines).encode()).hexdigest()


# ---------------------------------------------------------------------------
# Main CLI
# ---------------------------------------------------------------------------

def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate conformance YAML test cases")
    parser.add_argument("--dry-run", action="store_true", help="Print counts only, no file writes")
    parser.add_argument("--diff", action="store_true", help="Exit 2 if generated output differs from committed files")
    parser.add_argument("--seed", type=int, default=42, help="Hypothesis seed (default 42)")
    args = parser.parse_args(argv)

    schemas = _load_schemas()
    tool_count = len(schemas)
    gen = SchemaTestGenerator(seed=args.seed)

    valid_cases   = [c for s in schemas for c in gen.generate_valid(s)]
    invalid_cases = [c for s in schemas for c in gen.generate_invalid(s)]
    seam_cases    = SeamTestGenerator().generate()
    batch_cases   = BatchTestGenerator().generate()

    header = _header(tool_count)

    files = {
        "schema_valid.yaml":   (header, valid_cases),
        "schema_invalid.yaml": (header, invalid_cases),
        "seam_tests.yaml":     (header, seam_cases),
        "batch_tests.yaml":    (header, batch_cases),
    }

    if args.dry_run:
        print(f"TCP-reachable tools: {tool_count}")
        print(f"schema_valid: {len(valid_cases)} cases")
        print(f"schema_invalid: {len(invalid_cases)} cases")
        print(f"seam_tests: {len(seam_cases)} cases")
        print(f"batch_tests: {len(batch_cases)} cases")
        return 0

    drift_detected = False
    _OUT_DIR.mkdir(parents=True, exist_ok=True)

    for filename, (hdr, cases) in files.items():
        new_text = _write_yaml(_OUT_DIR / filename, hdr, cases)
        target = _OUT_DIR / filename

        if args.diff:
            if target.exists():
                existing = target.read_text(encoding="utf-8")
                if _sha256_body(existing) != _sha256_body(new_text):
                    print(f"DRIFT: {filename} differs from committed version")
                    drift_detected = True
            else:
                print(f"MISSING: {filename} not committed")
                drift_detected = True
        else:
            target.write_text(new_text, encoding="utf-8")
            print(f"Wrote {filename} ({len(cases)} cases)")

    # Ensure __init__.py exists
    init_file = _OUT_DIR / "__init__.py"
    if not args.diff and not init_file.exists():
        init_file.write_text("", encoding="utf-8")

    if drift_detected:
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
