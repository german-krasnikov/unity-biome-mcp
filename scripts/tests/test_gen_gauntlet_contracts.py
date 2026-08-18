"""Unit tests for scripts/gen_gauntlet_contracts.py.

No Unity, no live deps — uses injected data directly.
"""

import pathlib
import sys

import pytest

SCRIPTS_DIR = pathlib.Path(__file__).parent.parent
sys.path.insert(0, str(SCRIPTS_DIR))

from gen_gauntlet_contracts import (  # noqa: E402
    _effects_for,
    _generate_round_trip_contracts,
    _generate_routing_contracts,
    _load_base,
    _merge,
    _parse_args_str,
    _retry_for,
    _sanitize_id,
)

# ---------------------------------------------------------------------------
# Fake ToolSpec
# ---------------------------------------------------------------------------

class FakeSpec:
    def __init__(self, category="CORE", mutability="read", direct_only=False, runtime_only=False):
        self.category = category
        self.mutability = mutability
        self.direct_only = direct_only
        self.runtime_only = runtime_only


# ---------------------------------------------------------------------------
# _parse_args_str
# ---------------------------------------------------------------------------

class TestParseArgsStr:
    def test_empty_returns_empty_dict(self):
        assert _parse_args_str("") == {}

    def test_single_token(self):
        assert _parse_args_str("action=list") == {"action": "list"}

    def test_multi_token(self):
        result = _parse_args_str("path=/__seam type=Transform")
        assert result == {"path": "/__seam", "type": "Transform"}

    def test_value_with_slash(self):
        result = _parse_args_str("path=/__seam_nonexistent")
        assert result == {"path": "/__seam_nonexistent"}

    def test_whitespace_stripped(self):
        result = _parse_args_str("  action=get  ")
        assert result == {"action": "get"}


# ---------------------------------------------------------------------------
# _effects_for
# ---------------------------------------------------------------------------

class TestEffectsFor:
    def test_read_returns_pure_read(self):
        spec = FakeSpec(category="CORE", mutability="read")
        assert _effects_for("get_hierarchy", spec) == ["pure_read"]

    def test_write_scene_returns_unity_persistent(self):
        spec = FakeSpec(category="SCENE", mutability="write")
        assert _effects_for("create_object", spec) == ["unity_persistent"]

    def test_write_core_returns_unity_persistent(self):
        spec = FakeSpec(category="CORE", mutability="write")
        assert _effects_for("batch", spec) == ["unity_persistent"]

    def test_write_assets_returns_filesystem(self):
        spec = FakeSpec(category="ASSETS", mutability="write")
        assert _effects_for("bake", spec) == ["filesystem"]

    def test_write_verify_returns_observer_state(self):
        spec = FakeSpec(category="VERIFY", mutability="write")
        assert _effects_for("diagnose", spec) == ["observer_state"]

    def test_write_unknown_category_defaults_to_unity_persistent(self):
        spec = FakeSpec(category="UNKNOWN_XYZ", mutability="write")
        result = _effects_for("some_tool", spec)
        assert result == ["unity_persistent"]


# ---------------------------------------------------------------------------
# _retry_for
# ---------------------------------------------------------------------------

class TestRetryFor:
    def test_pure_read_is_blind_safe(self):
        assert _retry_for(["pure_read"]) == "blind_safe"

    def test_write_is_reconcile(self):
        assert _retry_for(["unity_persistent"]) == "reconcile"

    def test_filesystem_is_reconcile(self):
        assert _retry_for(["filesystem"]) == "reconcile"

    def test_observer_state_is_reconcile(self):
        assert _retry_for(["observer_state"]) == "reconcile"


# ---------------------------------------------------------------------------
# _sanitize_id
# ---------------------------------------------------------------------------

class TestSanitizeId:
    def test_create_object_hierarchy(self):
        result = _sanitize_id("create_object→hierarchy")
        assert result == "rt.create-object.hierarchy"

    def test_set_property_get_component(self):
        result = _sanitize_id("set_property→get_component")
        assert result == "rt.set-property.get-component"

    def test_result_matches_id_pattern(self):
        import re
        pattern = re.compile(r"^[a-z0-9][a-z0-9._-]*$")
        for raw in ["create_object→hierarchy", "manage_component_add→get_component"]:
            result = _sanitize_id(raw)
            assert pattern.fullmatch(result), f"{result!r} doesn't match ID pattern"

    def test_no_underscore_in_result(self):
        # All underscores should become hyphens
        result = _sanitize_id("set_active_false→hierarchy_inactive_marker")
        assert "_" not in result


# ---------------------------------------------------------------------------
# _generate_routing_contracts
# ---------------------------------------------------------------------------

class TestGenerateRoutingContracts:
    def _minimal_specs(self):
        return {
            "get_hierarchy": FakeSpec(category="CORE", mutability="read"),
            "create_object": FakeSpec(category="CORE", mutability="write"),
            "mcp_status": FakeSpec(category="SYSTEM", mutability="read", direct_only=True),
            "debug_animator": FakeSpec(category="RUNTIME", mutability="read", runtime_only=True),
        }

    def test_direct_only_excluded(self):
        specs = self._minimal_specs()
        contracts = _generate_routing_contracts(specs, {})
        ids = [c["id"] for c in contracts]
        assert not any("mcp_status" in i for i in ids)

    def test_runtime_only_excluded(self):
        specs = self._minimal_specs()
        contracts = _generate_routing_contracts(specs, {})
        ids = [c["id"] for c in contracts]
        assert not any("debug_animator" in i for i in ids)

    def test_read_tool_has_pure_read_effect(self):
        specs = {"get_hierarchy": FakeSpec(category="CORE", mutability="read")}
        contracts = _generate_routing_contracts(specs, {})
        assert len(contracts) == 1
        assert contracts[0]["effects"] == ["pure_read"]
        assert contracts[0]["retry"] == "blind_safe"

    def test_write_tool_has_unity_persistent(self):
        specs = {"create_object": FakeSpec(category="CORE", mutability="write")}
        contracts = _generate_routing_contracts(specs, {"create_object": "name=__seam"})
        assert len(contracts) == 1
        assert contracts[0]["effects"] == ["unity_persistent"]
        assert contracts[0]["retry"] == "reconcile"

    def test_contract_has_all_eight_keys(self):
        required_keys = {
            "id", "action", "effects", "retry",
            "arguments", "preconditions", "expect_error",
            "forbidden_success_patterns",
        }
        specs = {"get_hierarchy": FakeSpec(category="CORE", mutability="read")}
        contracts = _generate_routing_contracts(specs, {})
        assert set(contracts[0].keys()) == required_keys

    def test_id_prefix_is_route(self):
        specs = {"get_hierarchy": FakeSpec(category="CORE", mutability="read")}
        contracts = _generate_routing_contracts(specs, {})
        assert contracts[0]["id"].startswith("route.")

    def test_args_parsed_from_minimal_args(self):
        specs = {"get_hierarchy": FakeSpec(category="CORE", mutability="read")}
        minimal = {"get_hierarchy": "depth=1"}
        contracts = _generate_routing_contracts(specs, minimal)
        assert contracts[0]["arguments"] == {"depth": "1"}


# ---------------------------------------------------------------------------
# _generate_round_trip_contracts
# ---------------------------------------------------------------------------

class TestGenerateRoundTripContracts:
    def _make_param(self, mutate_cmd, mutate_args, read_cmd, read_args, case_id):
        import types
        p = types.SimpleNamespace()
        p.values = (mutate_cmd, mutate_args, read_cmd, read_args, lambda resp, ns: True)
        p.id = case_id
        return p

    def test_count_matches_input(self):
        params = [
            self._make_param("create_object", {"name": "{ns}_test"}, "get_hierarchy", {"depth": "1"}, "create_object→hierarchy"),
            self._make_param("delete_object", {"path": "/{ns}_test"}, "get_hierarchy", {"depth": "1"}, "delete_object→hierarchy_absent"),
        ]
        contracts = _generate_round_trip_contracts(params)
        assert len(contracts) == 2

    def test_id_prefix_is_rt(self):
        params = [
            self._make_param("create_object", {"name": "{ns}_test"}, "get_hierarchy", {"depth": "1"}, "create_object→hierarchy")
        ]
        contracts = _generate_round_trip_contracts(params)
        assert contracts[0]["id"].startswith("rt.")

    def test_effects_are_unity_persistent(self):
        params = [
            self._make_param("create_object", {"name": "{ns}_test"}, "get_hierarchy", {"depth": "1"}, "create_object→hierarchy")
        ]
        contracts = _generate_round_trip_contracts(params)
        assert contracts[0]["effects"] == ["unity_persistent"]

    def test_ns_substituted_in_args(self):
        params = [
            self._make_param("create_object", {"name": "{ns}_test"}, "get_hierarchy", {"depth": "1"}, "create_object→hierarchy")
        ]
        contracts = _generate_round_trip_contracts(params)
        args = contracts[0]["arguments"]
        assert "{ns}" not in str(args)
        assert "__seam-ns" in args.get("name", "")

    def test_contract_has_all_eight_keys(self):
        required_keys = {
            "id", "action", "effects", "retry",
            "arguments", "preconditions", "expect_error",
            "forbidden_success_patterns",
        }
        params = [
            self._make_param("create_object", {"name": "{ns}_test"}, "get_hierarchy", {"depth": "1"}, "create_object→hierarchy")
        ]
        contracts = _generate_round_trip_contracts(params)
        assert set(contracts[0].keys()) == required_keys


# ---------------------------------------------------------------------------
# _load_base
# ---------------------------------------------------------------------------

class TestLoadBase:
    def test_loads_hand_written_contracts(self, tmp_path):
        import json
        catalog = {
            "schema_version": 2,
            "catalog_version": "1.0.0",
            "scope": "builtin",
            "owner": None,
            "contracts": [
                {
                    "id": "mcp-status-read",
                    "action": "mcp_status",
                    "effects": ["pure_read"],
                    "retry": "blind_safe",
                    "arguments": {},
                    "preconditions": {"connected": True},
                    "expect_error": False,
                    "forbidden_success_patterns": ["^error:"],
                }
            ]
        }
        f = tmp_path / "contracts.json"
        f.write_text(json.dumps(catalog))
        header, hand_written = _load_base(f)
        assert header["schema_version"] == 2
        assert len(hand_written) == 1
        assert hand_written[0]["id"] == "mcp-status-read"

    def test_excludes_generated_contracts(self, tmp_path):
        import json
        catalog = {
            "schema_version": 2,
            "catalog_version": "1.0.0",
            "scope": "builtin",
            "owner": None,
            "contracts": [
                {"id": "mcp-status-read", "action": "mcp_status", "effects": ["pure_read"],
                 "retry": "blind_safe", "arguments": {}, "preconditions": {"connected": True},
                 "expect_error": False, "forbidden_success_patterns": []},
                {"id": "route.get_hierarchy", "action": "get_hierarchy", "effects": ["pure_read"],
                 "retry": "blind_safe", "arguments": {}, "preconditions": {"connected": True},
                 "expect_error": False, "forbidden_success_patterns": []},
            ]
        }
        f = tmp_path / "contracts.json"
        f.write_text(json.dumps(catalog))
        _, hand_written = _load_base(f)
        assert len(hand_written) == 1
        assert hand_written[0]["id"] == "mcp-status-read"


# ---------------------------------------------------------------------------
# _merge
# ---------------------------------------------------------------------------

class TestMerge:
    def _make_contract(self, id_):
        return {"id": id_, "action": id_, "effects": ["pure_read"], "retry": "blind_safe",
                "arguments": {}, "preconditions": {}, "expect_error": False,
                "forbidden_success_patterns": []}

    def test_merge_combines_lists(self):
        hand = [self._make_contract("mcp-status-read")]
        gen = [self._make_contract("route.get_hierarchy")]
        result = _merge(hand, gen)
        assert len(result) == 2

    def test_merge_raises_on_duplicate_id(self):
        hand = [self._make_contract("duplicate-id")]
        gen = [self._make_contract("duplicate-id")]
        with pytest.raises(ValueError, match="duplicate"):
            _merge(hand, gen)

    def test_hand_written_preserved(self):
        hand = [self._make_contract("mcp-status-read")]
        gen = [self._make_contract("route.get_hierarchy")]
        result = _merge(hand, gen)
        ids = [c["id"] for c in result]
        assert "mcp-status-read" in ids


# ---------------------------------------------------------------------------
# Integration: catalog loads output
# ---------------------------------------------------------------------------

class TestCatalogLoadsOutput:
    def test_generated_contracts_pass_catalog_validation(self, tmp_path):
        import json
        import sys
        sys.path.insert(0, str(SCRIPTS_DIR))

        specs = {
            "get_hierarchy": FakeSpec(category="CORE", mutability="read"),
            "get_status": FakeSpec(category="CORE", mutability="read"),
        }
        routing = _generate_routing_contracts(specs, {})
        hand_written = [
            {"id": "mcp-status-read", "action": "mcp_status", "effects": ["pure_read"],
             "retry": "blind_safe", "arguments": {}, "preconditions": {"connected": True},
             "expect_error": False, "forbidden_success_patterns": ["^error:"]}
        ]
        all_contracts = _merge(hand_written, routing)

        catalog_data = {
            "schema_version": 2,
            "catalog_version": "1.1.0",
            "scope": "builtin",
            "owner": None,
            "contracts": all_contracts,
        }
        f = tmp_path / "contracts.json"
        f.write_text(json.dumps(catalog_data, indent=2))

        sys.path.insert(0, str(SCRIPTS_DIR))
        from gauntlet.contract_catalog import load_contract_catalog  # noqa: PLC0415
        catalog = load_contract_catalog(f)
        assert len(catalog.contracts) > 0
