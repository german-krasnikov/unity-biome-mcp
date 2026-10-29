"""The runtime of the generated tool module: the wire encoders, and the ToolSet that checks a function against the catalog and registers it."""
import pytest
from mcp.types import ToolAnnotations

from unity_mcp.tools import _catalog_runtime as rt

ostr = str | None


@pytest.mark.parametrize("encode, args, wire", [
    (rt.w_str, ("a",), "a"), (rt.w_str, (None,), None), (rt.w_str, ("", ""), None),
    (rt.w_int, (5,), "5"), (rt.w_int, (0,), "0"), (rt.w_int, (50, 50), None), (rt.w_int, (51, 50), "51"), (rt.w_int, (None, 50), None),
    (rt.w_float, (0.5,), "0.5"), (rt.w_float, (1.0, 1.0), None),
    (rt.w_bool, (True,), "true"), (rt.w_bool, (False,), "false"), (rt.w_bool, (True, True), None), (rt.w_bool, (False, True), "false"),
])
def test_a_value_is_sent_as_the_text_unity_reads_or_left_out(encode, args, wire):
    assert encode(*args) == wire


def test_an_optional_type_encodes_like_its_base():
    assert (rt.w_ostr, rt.w_oint, rt.w_ofloat, rt.w_obool) == (rt.w_str, rt.w_int, rt.w_float, rt.w_bool)


class _Mcp:
    def __init__(self):
        self.registered = []

    def tool(self, annotations):
        return lambda fn: self.registered.append((fn.__name__, annotations)) or fn


def _function(signature="path: str, name: str", doc="Rename a thing. More."):
    namespace = {"ostr": ostr}
    exec(f'async def rename({signature}) -> str:\n    """{doc}"""', namespace)
    return namespace["rename"]


def _install(fn, effect="RW", summary="Rename a thing.", params=(("path", str), ("name", str))):
    tools, mcp = rt.ToolSet(), _Mcp()
    tools.expect("rename", effect, summary, params)
    tools.provide(fn)
    tools.install(mcp)
    return mcp.registered


@pytest.mark.parametrize("effect, hints", [
    ("RO", {"readOnlyHint": True}), ("RW", {"readOnlyHint": False}),
    ("RW_IDEM", {"readOnlyHint": False, "idempotentHint": True}), ("DEL", {"readOnlyHint": False, "destructiveHint": True}),
])
def test_install_registers_the_function_with_the_annotation_of_its_effect(effect, hints):
    assert _install(_function(), effect) == [("rename", ToolAnnotations(**hints))]


@pytest.mark.parametrize("function, catalog, message", [
    ({"signature": "path: str, title: str"}, {}, "title"),                        # a parameter renamed
    ({"signature": "path: str, name: int"}, {}, "int"),                           # a type changed
    ({"signature": "path: str"}, {}, "rename"),                                    # a parameter missing
    ({"signature": "path: str, name: str = ''"}, {}, "''"),                       # a default the catalog does not have
    ({"signature": "path: str, name: ostr = ''"},
     {"params": (("path", str), ("name", ostr, None))}, "''"),                    # another default
    ({"doc": "Renames."}, {}, "description of rename"),                            # the first line of the description changed
])
def test_a_function_that_differs_from_the_catalog_is_refused(function, catalog, message):
    with pytest.raises(TypeError, match=message):
        _install(_function(**function), **catalog)


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
