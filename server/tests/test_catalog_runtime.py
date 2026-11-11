"""The runtime of the generated tool module: the wire encoders, and the ToolSet that checks a function against the catalog and registers it."""
import sys
import types

import pytest
from mcp.types import ToolAnnotations

from unity_mcp.tools import _catalog_runtime as rt
from unity_mcp.tools.generated import catalog_tools

ostr = str | None


@pytest.mark.parametrize("encode, args, wire", [
    (rt.w_str, ("a",), "a"), (rt.w_str, (None,), None), (rt.w_str, ("", ""), None),
    (rt.w_int, (5,), "5"), (rt.w_int, (0,), "0"), (rt.w_int, (50, 50), None), (rt.w_int, (51, 50), "51"), (rt.w_int, (None, 50), None),
    (rt.w_float, (0.5,), "0.5"), (rt.w_float, (1.0, 1.0), None),
    (rt.w_bool, (True,), "true"), (rt.w_bool, (False,), "false"), (rt.w_bool, (True, True), None), (rt.w_bool, (False, True), "false"),
])
def test_a_value_is_sent_as_the_text_unity_reads_or_left_out(encode, args, wire):
    assert encode(*args) == wire


@pytest.mark.parametrize("encode, args, wire", [
    (rt.w_int, (5, rt._NOTHING, True), 5), (rt.w_float, (2.5, rt._NOTHING, True), 2.5), (rt.w_str, ("a", rt._NOTHING, True), "a"),
    (rt.w_int, (50, 50, True), None), (rt.w_int, (51, 50, True), 51), (rt.w_int, (None, rt._NOTHING, True), None),
])
def test_a_raw_value_is_sent_as_it_is(encode, args, wire):
    assert encode(*args) == wire


def test_an_optional_type_encodes_like_its_base():
    assert (rt.w_ostr, rt.w_oint, rt.w_ofloat, rt.w_obool) == (rt.w_str, rt.w_int, rt.w_float, rt.w_bool)


class _Mcp:
    def __init__(self):
        self.registered = []

    def tool(self, annotations):
        return lambda fn: self.registered.append((fn.__name__, annotations)) or fn


def _function(signature="path: str, name: str", doc="Rename a thing. More.", name="rename"):
    namespace = {"ostr": ostr}
    exec(f'async def {name}({signature}) -> str:\n    """{doc}"""', namespace)
    return namespace[name]


def _install(fn, effect="RW", summary="Rename a thing.", params=(("path", str), ("name", str)), register=False):
    """Install a generated wrapper (provide) or register a hand-written tool (register) in a one-tool catalog."""
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("rename", effect, summary, params)
    if register:
        tools.register(mcp, fn)
    else:
        tools.provide(fn)
        tools.install(mcp)
    return mcp.registered


@pytest.mark.parametrize("effect, hints", [
    ("RO", {"readOnlyHint": True}), ("RW", {"readOnlyHint": False}),
    ("RW_IDEM", {"readOnlyHint": False, "idempotentHint": True}), ("DEL", {"readOnlyHint": False, "destructiveHint": True}),
])
@pytest.mark.parametrize("register", [False, True])
def test_a_function_is_registered_with_the_annotation_of_its_effect(effect, hints, register):
    assert _install(_function(), effect, register=register) == [("rename", ToolAnnotations(**hints))]


@pytest.mark.parametrize("function, catalog, message", [
    ({"signature": "path: str, title: str"}, {}, "title"),                        # a parameter renamed
    ({"signature": "path: str, name: int"}, {}, "int"),                           # a type changed
    ({"signature": "path: str"}, {}, "rename"),                                    # a parameter missing
    ({"signature": "path: str, name: str = ''"}, {}, "''"),                       # a default the catalog does not have
    ({"signature": "path: str, name: ostr = ''"},
     {"params": (("path", str), ("name", ostr, None))}, "''"),                    # another default
    ({"doc": "Renames."}, {}, "description of rename"),                            # the first line of the description changed
])
@pytest.mark.parametrize("register", [False, True])
def test_a_function_that_differs_from_the_catalog_is_refused(function, catalog, message, register):
    with pytest.raises(TypeError, match=message):
        _install(_function(**function), register=register, **catalog)


def test_a_parameter_the_catalog_types_any_is_checked_by_name_and_default_only():
    params = (("name", str), ("ctx", rt.ANY, None))
    assert _install(_function("name: str, ctx: int = None"), params=params, register=True)
    for signature in ("name: str, context: int = None", "name: str, ctx: int = 1", "title: str, ctx: int = None"):
        with pytest.raises(TypeError, match="drift"):
            _install(_function(signature), params=params, register=True)


def test_a_refused_catalog_registers_nothing():
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("rename", "RW", "Rename a thing.", (("path", str), ("name", str)))
    tools.expect("other", "RO", "Other.", (("x", int),))
    tools.provide(_function())
    tools.provide(_function("y: int", "Other.", name="other"))  # `other` states x, not y
    with pytest.raises(TypeError, match="other"):
        tools.install(mcp)
    assert mcp.registered == []


def test_a_refused_module_registers_none_of_its_tools():
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("rename", "RW", "Rename a thing.", (("path", str), ("name", str)))
    tools.expect("other", "RO", "Other.", (("x", int),))
    with pytest.raises(TypeError, match="other"):
        tools.register(mcp, _function(), _function("y: int", "Other.", name="other"))
    assert mcp.registered == []


def test_a_hand_written_tool_has_one_owner_and_may_be_registered_again_for_another_mcp():
    tools, fn = rt.ToolSet(), _function()
    tools.expect("rename", "RW", "Rename a thing.", (("path", str), ("name", str)))
    first, second = _Mcp(), _Mcp()
    tools.register(first, fn)
    tools.register(second, fn)                  # the same function again (a second server, a test)
    tools.register(second, _function())         # a function made again by the same code (a closure of register())
    assert len(first.registered) == 1 and len(second.registered) == 2
    other = _function()
    other.__module__ = "elsewhere"              # the same name from another place
    with pytest.raises(ValueError, match="two owners"):
        tools.register(_Mcp(), other)
    with pytest.raises(LookupError, match="outsider is not a tool of the catalog"):
        tools.register(_Mcp(), _function(name="outsider"))


def test_a_generated_wrapper_and_a_hand_written_function_of_one_name_are_refused():
    tools = rt.ToolSet()
    tools.expect("rename", "RW", "Rename a thing.", (("path", str), ("name", str)))
    tools.provide(_function())
    with pytest.raises(ValueError, match="two owners"):
        tools.register(_Mcp(), _function())


def test_install_counts_the_tools_that_modules_registered_and_does_not_register_them_again():
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("rename", "RW", "Rename a thing.", (("path", str), ("name", str)))
    tools.expect("other", "RO", "Other.", (("x", int),))
    tools.provide(_function("x: int", "Other.", name="other"))
    with pytest.raises(LookupError, match="rename"):
        tools.install(mcp)                       # `rename` has no function yet
    tools.register(mcp, _function())
    tools.install(mcp)
    assert [name for name, _ in mcp.registered] == ["rename", "other"]


def test_a_function_that_states_what_the_catalog_states_is_registered():
    fn = _function("path: str, name: ostr = None")
    assert _install(fn, params=(("path", str), ("name", ostr, None)))[0][0] == "rename"


def test_a_tool_without_a_function_or_a_function_without_a_tool_is_refused():
    expecting, providing = rt.ToolSet(), rt.ToolSet()
    expecting.expect("rename", "RW", "Rename.", ())
    providing.provide(_function())
    for tools in (expecting, providing):
        with pytest.raises(LookupError, match="rename"):
            tools.install(_Mcp())


def test_two_owners_of_a_name_are_refused():
    tools, fn = rt.ToolSet(), _function()
    tools.expect("rename", "RW", "Rename.", ())
    tools.provide(fn)
    tools.provide(fn)  # the same function again is fine
    with pytest.raises(ValueError, match="rename"):
        tools.expect("rename", "RO", "Again.", ())
    with pytest.raises(ValueError, match="rename"):
        tools.provide(_function())


def _fake_tools_module(monkeypatch, name, **attrs):
    """Register a fake module at unity_mcp.tools.<name> in sys.modules, so importlib.import_module (what
    ToolSet.install()'s body-drift check uses) resolves it without a real file on disk."""
    mod = types.ModuleType(f"unity_mcp.tools.{name}")
    for key, value in attrs.items():
        setattr(mod, key, value)
    monkeypatch.setitem(sys.modules, f"unity_mcp.tools.{name}", mod)
    return mod


def test_expect_body_unknown_name_raises():
    tools = rt.ToolSet()
    with pytest.raises(LookupError, match="nope"):
        tools.expect_body("nope", "objects")


def test_install_raises_on_missing_body_function(monkeypatch):
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("x", "RO", "X.", (("a", str),))
    tools.provide(_function("a: str", "X.", name="x"))
    tools.expect_body("x", "fakemod_missing")
    _fake_tools_module(monkeypatch, "fakemod_missing")  # no _x_body attribute
    with pytest.raises(LookupError, match="_x_body"):
        tools.install(mcp)


def test_install_raises_on_body_keyword_drift(monkeypatch):
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("x", "RO", "X.", (("a", str), ("b", str)))
    tools.provide(_function("a: str, b: str", "X.", name="x"))
    tools.expect_body("x", "fakemod_drift")

    async def _x_body(a, c):
        pass

    _fake_tools_module(monkeypatch, "fakemod_drift", _x_body=_x_body)
    with pytest.raises(TypeError, match="drift"):
        tools.install(mcp)


def test_install_passes_when_body_keywords_match_any_order_and_defaults(monkeypatch):
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("x", "RO", "X.", (("a", str), ("b", str)))
    tools.provide(_function("a: str, b: str", "X.", name="x"))
    tools.expect_body("x", "fakemod_order")

    async def _x_body(b, a=1):
        pass

    _fake_tools_module(monkeypatch, "fakemod_order", _x_body=_x_body)
    tools.install(mcp)
    assert [name for name, _ in mcp.registered] == ["x"]


def test_install_ignores_var_positional_and_var_keyword_in_body(monkeypatch):
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("x", "RO", "X.", (("a", str), ("b", str)))
    tools.provide(_function("a: str, b: str", "X.", name="x"))
    tools.expect_body("x", "fakemod_varargs")

    async def _x_body(a, b, **extra):
        pass

    _fake_tools_module(monkeypatch, "fakemod_varargs", _x_body=_x_body)
    tools.install(mcp)
    assert [name for name, _ in mcp.registered] == ["x"]


def test_the_hand_written_use_skill_is_checked_against_the_catalog_when_its_module_registers_it():
    """skills.register() registers use_skill through the ToolSet, so a drift of its signature or description stops it; install() registers
    the generated wrappers and nothing else."""
    import unity_mcp.server  # noqa: F401  (register_all lets every module register its tools)

    mcp = _Mcp()
    catalog_tools.tools.install(mcp)
    assert sorted(name for name, _ in mcp.registered) == sorted(catalog_tools.tools._provided)
    summary = "Execute a previously saved skill. params: comma-separated key=value for substitution."
    for signature, doc, message in [
        ("name: str, args: ostr = None", summary, "args"),   # a parameter renamed
        ("name: str, params: ostr = ''", summary, "''"),     # a default changed
        ("name: str, params: ostr = None", "Run it.", "description of use_skill"),   # the description changed
    ]:
        namespace = {"ostr": ostr, "__name__": "unity_mcp.tools.skills"}  # the module and name of the real use_skill
        exec(f'async def use_skill({signature}) -> str:\n    """{doc}"""', namespace)
        with pytest.raises(TypeError, match=message):
            catalog_tools.tools.register(_Mcp(), namespace["use_skill"])
