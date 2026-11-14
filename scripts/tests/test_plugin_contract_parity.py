"""Parity test: dsl/plugins.biome declarations match C# IMCPPlugin implementations."""
import re
from pathlib import Path

import pytest

_REPO = Path(__file__).resolve().parents[2]
_PLUGINS_BIOME = _REPO / "dsl" / "plugins.biome"
_PLUGIN_DIR = _REPO / "unity-plugin" / "Editor"

_PLUGIN_LINE_RE = re.compile(
    r"^PLUGIN\s+(\S+)\s+PREFIX\s+(\S+)\s+ASSEMBLY\s+(\S+)"
)


def _parse_plugins_biome():
    lines = _PLUGINS_BIOME.read_text("utf-8").splitlines()
    plugins = []
    for line in lines:
        m = _PLUGIN_LINE_RE.match(line.strip())
        if m:
            plugins.append({"name": m.group(1), "prefix": m.group(2), "assembly": m.group(3)})
    return plugins


@pytest.fixture
def declared_plugins():
    return _parse_plugins_biome()


def test_plugins_biome_declares_at_least_one_plugin(declared_plugins):
    assert len(declared_plugins) >= 1, "dsl/plugins.biome must declare at least one PLUGIN"


def test_chat_plugin_declared(declared_plugins):
    names = [p["name"] for p in declared_plugins]
    assert "Chat" in names


def test_chat_plugin_prefix_matches_csharp():
    """Verify the declared prefix matches the C# ChatPlugin.CommandPrefix."""
    plugins = _parse_plugins_biome()
    chat = next(p for p in plugins if p["name"] == "Chat")

    # Find ChatPlugin.cs and extract CommandPrefix
    chat_plugin_cs = list(_PLUGIN_DIR.rglob("ChatPlugin.cs"))
    assert chat_plugin_cs, "ChatPlugin.cs not found"
    source = chat_plugin_cs[0].read_text("utf-8")

    # Extract CommandPrefix value
    m = re.search(r'CommandPrefix\s*=>\s*"(\w+)"', source)
    assert m, "CommandPrefix not found in ChatPlugin.cs"
    assert chat["prefix"] == m.group(1), (
        f"plugins.biome prefix '{chat['prefix']}' != C# '{m.group(1)}'"
    )


def test_chat_plugin_name_matches_csharp():
    plugins = _parse_plugins_biome()
    chat = next(p for p in plugins if p["name"] == "Chat")

    chat_plugin_cs = list(_PLUGIN_DIR.rglob("ChatPlugin.cs"))
    source = chat_plugin_cs[0].read_text("utf-8")

    m = re.search(r'Name\s*=>\s*"(\w+)"', source)
    assert m, "Name not found in ChatPlugin.cs"
    assert chat["name"] == m.group(1)
