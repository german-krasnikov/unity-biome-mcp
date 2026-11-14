"""Wire vectors of the generated MCP wrappers: for each call, the command and the arguments that reach Unity.

The expected arguments are what the hand-written wrappers of 86eca971 sent for the same call (this table was run against them:
14 of 14 agree), so they do not come from the generator.
"""
import pytest

from unity_mcp.server import _args
from unity_mcp.tools.generated import catalog_tools

# search_scene: limit 50 is left out (Unity applies it), 0 means unlimited and is sent, a None parameter is left out, "" is sent
VECTORS = [
    ("search_scene", ("t:Light",), {}, {"query": "t:Light"}),
    ("search_scene", ("t:Light",), {"root": "/R"}, {"query": "t:Light", "root": "/R"}),
    ("search_scene", ("t:Light",), {"limit": 5}, {"query": "t:Light", "limit": "5"}),
    ("search_scene", ("t:Light",), {"limit": 0}, {"query": "t:Light", "limit": "0"}),
    ("search_scene", ("t:Light",), {"limit": 50}, {"query": "t:Light"}),
    ("search_scene", ("t:Light",), {"scene": "S"}, {"query": "t:Light", "scene": "S"}),
    ("search_scene", ("",), {"root": "/R", "limit": 10, "scene": "S"}, {"query": "", "root": "/R", "limit": "10", "scene": "S"}),
    ("rename_object", ("/Grunt", "Boss"), {}, {"path": "/Grunt", "name": "Boss"}),
    ("rename_object", ("/Grunt", ""), {}, {"path": "/Grunt", "name": ""}),
    ("set_sibling_index", ("/A", 0), {}, {"path": "/A", "index": "0"}),
    ("set_sibling_index", ("/A", 3), {}, {"path": "/A", "index": "3"}),
    ("set_sibling_index", ("/A", -1), {}, {"path": "/A", "index": "-1"}),
    ("set_active", ("/A", True), {}, {"path": "/A", "active": "true"}),
    ("set_active", ("/A", False), {}, {"path": "/A", "active": "false"}),
]


@pytest.fixture
def sent(monkeypatch):
    calls = []

    async def send(command, args):
        calls.append((command, args))
        return "ok"

    monkeypatch.setattr(catalog_tools, "_send", send)
    monkeypatch.setattr(catalog_tools, "_args", _args)
    return calls


@pytest.mark.parametrize("name, args, kwargs, wire", VECTORS)
async def test_the_wrapper_sends_the_command_and_arguments_unity_reads(sent, name, args, kwargs, wire):
    assert await getattr(catalog_tools, name)(*args, **kwargs) == "ok"
    assert sent == [(name, wire)]
