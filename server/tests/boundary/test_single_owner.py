"""Every tool and every Unity command has exactly one owner (P15).

FastMCP keeps the first of two same-named tools and only logs a warning, and CommandRegistry skips the second Register of a
command with a warning. A tool registered twice (by hand and by the catalog) therefore passes every other test while the
winner depends on the order of the registrations. These tests count the owners.
"""
import re
import sys
from collections import Counter
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock

import pytest

_EDITOR = Path(__file__).resolve().parents[3] / "unity-plugin" / "Editor"
_CS_REGISTER = re.compile(r'CommandRegistry\.Register(?:Async|Action)?\(\s*"([^"]+)"')


class Recorder:
    """Stands in for FastMCP and records the name of every tool that a register() hands over."""

    def __init__(self):
        self.names = []

    def tool(self, name=None, **_options):
        def decorate(fn):
            self.names.append(name or fn.__name__)
            return fn
        return decorate


def repeated(names):
    return sorted(name for name, count in Counter(names).items() if count > 1)


def cs_command_names(sources):
    return [name for text in sources for name in _CS_REGISTER.findall(text)]


@pytest.fixture
def registered_names(monkeypatch):
    """The tool names register_all hands to FastMCP; the module state it binds is put back afterwards."""
    from unity_mcp.tools import register_all

    monkeypatch.setattr("unity_mcp.editor_log.init_corroboration", lambda: None)
    modules = [m for name, m in sys.modules.items() if name.startswith(("unity_mcp.tools", "unity_mcp.debug")) and m]
    saved = [(module, dict(vars(module))) for module in modules]
    recorder = Recorder()
    try:
        register_all(recorder, AsyncMock(), MagicMock(), get_slot=lambda: None)
    finally:
        for module, state in saved:
            vars(module).update(state)
    return recorder.names


def test_every_python_tool_has_one_owner(registered_names):
    assert repeated(registered_names) == []


def test_the_recorder_sees_every_tool_the_server_registers(registered_names):
    from unity_mcp.server import mcp

    live = {name for name, tool in mcp._tool_manager._tools.items() if tool.fn.__module__.startswith("unity_mcp.")}
    assert set(registered_names) == live


@pytest.mark.csharp_parity
def test_every_unity_command_has_one_owner():
    sources = [path.read_text(encoding="utf-8") for path in _EDITOR.rglob("*.cs") if "Tests" not in path.parts]
    names = cs_command_names(sources)
    assert len(names) > 100, "the registration pattern matches nothing: this test would pass without checking"
    assert repeated(names) == []


async def search_scene():
    """A name for the recorder to see twice."""


def test_a_second_registration_is_found_in_python_and_in_csharp():
    recorder = Recorder()
    recorder.tool(annotations=None)(search_scene)
    recorder.tool(name="search_scene")(lambda: None)
    assert repeated(recorder.names) == ["search_scene"]
    cs = 'CommandRegistry.Register("a", x);\nCommandRegistry.RegisterAsync("a", y);\nCommandRegistry.Register("b", z);'
    assert repeated(cs_command_names([cs])) == ["a"]
