# Generated from animation-omit.editmode
# source-sha256=2512a364a3f290f9fbb85abe08ef16bb236e776de6416ecc7b2580700fd5f523
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
    pytest.param({"action": "edit", "path": "/Obj", "_id": "minimal"}, id="minimal"),
])
def test_animation_omits_every_optional_and_compact_when_unset(case):
    r = Run("animation omits every optional and compact when unset", case["_id"])
    try:
        r.fixture("tool", **{})
        r.act("call_minimal", **{"action": "edit", "path": "/Obj"})
        assert _biome_cmp("eq", r.q("tool.clip_sent"),
                "ABSENT"), "animation-omit.editmode:11"
        assert _biome_cmp("eq", r.q("tool.clip_name_sent"),
                "ABSENT"), "animation-omit.editmode:12"
        assert _biome_cmp("eq", r.q("tool.property_sent"),
                "ABSENT"), "animation-omit.editmode:13"
        assert _biome_cmp("eq", r.q("tool.keys_sent"),
                "ABSENT"), "animation-omit.editmode:14"
        assert _biome_cmp("eq", r.q("tool.time_sent"),
                "ABSENT"), "animation-omit.editmode:15"
        assert _biome_cmp("eq", r.q("tool.component_type_sent"),
                "ABSENT"), "animation-omit.editmode:16"
        assert _biome_cmp("eq", r.q("tool.binding_path_sent"),
                "ABSENT"), "animation-omit.editmode:17"
        assert _biome_cmp("eq", r.q("tool.tangent_sent"),
                "ABSENT"), "animation-omit.editmode:18"
        assert _biome_cmp("eq", r.q("tool.function_name_sent"),
                "ABSENT"), "animation-omit.editmode:19"
        assert _biome_cmp("eq", r.q("tool.int_param_sent"),
                "ABSENT"), "animation-omit.editmode:20"
        assert _biome_cmp("eq", r.q("tool.float_param_sent"),
                "ABSENT"), "animation-omit.editmode:21"
        assert _biome_cmp("eq", r.q("tool.string_param_sent"),
                "ABSENT"), "animation-omit.editmode:22"
        assert _biome_cmp("eq", r.q("tool.compact_sent"),
                "ABSENT"), "animation-omit.editmode:23"
    finally:
        r.teardown()
