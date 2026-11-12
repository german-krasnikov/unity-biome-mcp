# Generated from animation.editmode
# source-sha256=295d252a14c77429b33d261691f74168a280fd424310e9af1b43ef295dc86713
# Edit the source, then run biome generate.

import pytest
from animation_host import Run


def _biome_cmp(op, a, b):
    def portable(v):
        if v is None or type(v) in (bool, str):
            return v
        if type(v) in (int, float):
            try:
                f = float(v)
            except OverflowError:
                f = None
            if f is not None and f - f == 0:
                return f
        raise ValueError(f"PLAN_VALUE: {v!r} is outside the portable domain")
    a, b = portable(a), portable(b)
    if op in ("eq", "ne"):
        same = type(a) is type(b) and a == b
        return same if op == "eq" else not same
    if type(a) is not float or type(b) is not float:
        raise ValueError(f"PLAN_ORDER: {op} is defined on two numbers only")
    return (
        a < b if op == "lt"
        else a <= b if op == "le"
        else a > b if op == "gt"
        else a >= b
    )


@pytest.mark.parametrize("case", [
    pytest.param({"action": "create", "path": "/A", "clip": "ClipA", "clip_name":
            "ClipA2", "property": "localPosition", "keys": "t:0 v:(1,2,3)", "time": 0,
            "component_type": "Transform", "binding_path": "Root", "tangent": "auto",
            "function_name": "OnA", "int_param": 1, "float_param": 0.5, "string_param":
            "sA", "_id": "create"}, id="create"),
    pytest.param({"action": "edit", "path": "/B", "clip": "ClipB", "clip_name":
            "ClipB2", "property": "intensity", "keys": "t:0 v:5", "time": 1,
            "component_type": "Light", "binding_path": "ChildLight", "tangent":
            "linear", "function_name": "OnB", "int_param": 2, "float_param": 1.5,
            "string_param": "sB", "_id": "edit"}, id="edit"),
])
def test_animation_forwards_every_non_local_arg_with_a_distinguishing_value(case):
    r = Run("animation forwards every non-local arg with a distinguishing value", case[
            "_id"])
    try:
        r.fixture("tool", **{})
        r.act("call", **{"action": case["action"], "path": case["path"], "clip": case[
                "clip"], "clip_name": case["clip_name"], "property": case["property"],
                "keys": case["keys"], "time": case["time"], "component_type": case[
                "component_type"], "binding_path": case["binding_path"], "tangent":
                case["tangent"], "function_name": case["function_name"], "int_param":
                case["int_param"], "float_param": case["float_param"], "string_param":
                case["string_param"], "compact": True})
        assert _biome_cmp("eq", r.q("tool.action"), case[
                "action"]), "animation.editmode:12"
        assert _biome_cmp("eq", r.q("tool.path"), case["path"]), "animation.editmode:13"
        assert _biome_cmp("eq", r.q("tool.clip"), case["clip"]), "animation.editmode:14"
        assert _biome_cmp("eq", r.q("tool.clip_name"), case[
                "clip_name"]), "animation.editmode:15"
        assert _biome_cmp("eq", r.q("tool.property"), case[
                "property"]), "animation.editmode:16"
        assert _biome_cmp("eq", r.q("tool.keys"), case["keys"]), "animation.editmode:17"
        assert _biome_cmp("eq", r.q("tool.time"), case["time"]), "animation.editmode:18"
        assert _biome_cmp("eq", r.q("tool.component_type"), case[
                "component_type"]), "animation.editmode:19"
        assert _biome_cmp("eq", r.q("tool.binding_path"), case[
                "binding_path"]), "animation.editmode:20"
        assert _biome_cmp("eq", r.q("tool.tangent"), case[
                "tangent"]), "animation.editmode:21"
        assert _biome_cmp("eq", r.q("tool.function_name"), case[
                "function_name"]), "animation.editmode:22"
        assert _biome_cmp("eq", r.q("tool.int_param"), case[
                "int_param"]), "animation.editmode:23"
        assert _biome_cmp("eq", r.q("tool.float_param"), case[
                "float_param"]), "animation.editmode:24"
        assert _biome_cmp("eq", r.q("tool.string_param"), case[
                "string_param"]), "animation.editmode:25"
        assert _biome_cmp("eq", r.q("tool.compact"), "true"), "animation.editmode:26"
    finally:
        r.teardown()
