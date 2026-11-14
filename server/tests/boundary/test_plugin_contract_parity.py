"""Plugin contract parity: dsl/plugins.biome vs. the production IMCPPlugin registrants.

plugins.biome documents itself as declarative-only ("Not a generation source --
parity enforced by test_plugin_contract_parity.py, not codegen") -- this module
is that promised enforcement (Phase 1.1).
"""
import re
from dataclasses import dataclass
from pathlib import Path

import pytest

_REPO_ROOT = Path(__file__).resolve().parents[3]
_PLUGINS_BIOME = _REPO_ROOT / "dsl" / "plugins.biome"
_EDITOR_DIR = _REPO_ROOT / "unity-plugin" / "Editor"

_PLUGIN_LINE = re.compile(
    r"^PLUGIN\s+(?P<name>\S+)\s+PREFIX\s+(?P<prefix>\S+)\s+ASSEMBLY\s+(?P<assembly>\S+)\s*$"
)
_IMCPPLUGIN_CLASS = re.compile(r"class\s+\w+\s*:\s*IMCPPlugin\b")
_NAME_PROPERTY = re.compile(r'public\s+string\s+Name\s*=>\s*"([^"]*)"')
_PREFIX_PROPERTY = re.compile(r'public\s+string\s+CommandPrefix\s*=>\s*"([^"]*)"')


@dataclass(frozen=True)
class DeclaredPlugin:
    name: str
    prefix: str
    assembly: str


def _parse_plugins_biome() -> list[DeclaredPlugin]:
    plugins = []
    for line in _PLUGINS_BIOME.read_text("utf-8").splitlines():
        m = _PLUGIN_LINE.match(line.strip())
        if m:
            plugins.append(DeclaredPlugin(m["name"], m["prefix"], m["assembly"]))
    return plugins


def _find_asmdef(assembly: str) -> Path | None:
    matches = [p for p in _EDITOR_DIR.rglob(f"{assembly}.asmdef") if "Tests" not in p.parts]
    return matches[0] if len(matches) == 1 else None


def _find_plugin_registrant(asmdef_dir: Path) -> Path | None:
    candidates = [
        p for p in asmdef_dir.rglob("*.cs")
        if "Tests" not in p.parts and _IMCPPLUGIN_CLASS.search(p.read_text("utf-8"))
    ]
    return candidates[0] if len(candidates) == 1 else None


_DECLARED = _parse_plugins_biome()


def test_plugins_biome_is_not_empty():
    """An empty/broken DSL file would make every parametrized test below vacuously pass."""
    assert _DECLARED, "dsl/plugins.biome declared no PLUGIN lines"


def test_plugins_biome_declares_chat_contract():
    """Pins the one documented plugin's exact contract so a silent DSL edit is caught."""
    chat = next(p for p in _DECLARED if p.name == "Chat")
    assert chat.prefix == "chat"
    assert chat.assembly == "UnityMCP.Editor.Chat.View"


@pytest.mark.parametrize("plugin", _DECLARED, ids=lambda p: p.name)
def test_declared_plugin_assembly_has_asmdef(plugin: DeclaredPlugin):
    asmdef = _find_asmdef(plugin.assembly)
    assert asmdef is not None, (
        f"plugins.biome declares ASSEMBLY {plugin.assembly} for PLUGIN {plugin.name}, "
        f"but no unique {plugin.assembly}.asmdef exists under unity-plugin/Editor"
    )


@pytest.mark.parametrize("plugin", _DECLARED, ids=lambda p: p.name)
def test_declared_plugin_has_imcpplugin_registrant(plugin: DeclaredPlugin):
    asmdef = _find_asmdef(plugin.assembly)
    assert asmdef is not None
    registrant = _find_plugin_registrant(asmdef.parent)
    assert registrant is not None, (
        f"No single IMCPPlugin implementation found under {asmdef.parent} for PLUGIN {plugin.name}"
    )


@pytest.mark.parametrize("plugin", _DECLARED, ids=lambda p: p.name)
def test_declared_prefix_matches_csharp_command_prefix(plugin: DeclaredPlugin):
    asmdef = _find_asmdef(plugin.assembly)
    registrant = _find_plugin_registrant(asmdef.parent)
    source = registrant.read_text("utf-8")
    match = _PREFIX_PROPERTY.search(source)
    assert match, f'{registrant} has no `CommandPrefix => "..."` property'
    assert match.group(1) == plugin.prefix, (
        f"plugins.biome PREFIX={plugin.prefix!r} for {plugin.name} does not match "
        f"CommandPrefix={match.group(1)!r} in {registrant}"
    )


@pytest.mark.parametrize("plugin", _DECLARED, ids=lambda p: p.name)
def test_declared_name_matches_csharp_plugin_name(plugin: DeclaredPlugin):
    asmdef = _find_asmdef(plugin.assembly)
    registrant = _find_plugin_registrant(asmdef.parent)
    source = registrant.read_text("utf-8")
    match = _NAME_PROPERTY.search(source)
    assert match, f'{registrant} has no `Name => "..."` property'
    assert match.group(1) == plugin.name, (
        f"plugins.biome PLUGIN {plugin.name} does not match Name={match.group(1)!r} in {registrant}"
    )
