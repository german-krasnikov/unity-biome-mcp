"""Phase 5 Part B, final regression gate: with UNITY_MCP_GD_TOOLS=0, every GD-category
tool name is gone from BOTH the registration side (_catalog_runtime.ToolSet.install(),
exercised through register_all()) and the discoverability side (gating.py's
_THEMED_CATEGORIES/_ALL_KNOWN/TIER1, exercised through discover_tools()) in one test
module -- a fix that only covers one side fails this file (task-p5 risk table).

Also asserts the one caveat that is easy to miss: the 11 GD tools individually flagged
tier1 (always-visible) don't just lose their tier1 promotion when the category is
disabled -- they vanish entirely, because tier1 only grants visibility to a tool that
exists, and a category-disabled tool does not exist at all.
"""
import importlib
import sys

import pytest
from unittest.mock import AsyncMock, MagicMock

from unity_mcp.tools import gating, tool_specs

_CORE_WITNESSES = ("create_object", "get_component", "get_hierarchy", "set_property", "get_console")


class Recorder:
    """Stands in for FastMCP and records the name of every tool register_all hands over."""

    def __init__(self):
        self.names = []

    def tool(self, name=None, **_options):
        def decorate(fn):
            self.names.append(name or fn.__name__)
            return fn
        return decorate


@pytest.fixture
def gd_tools_disabled(monkeypatch):
    """Reloads gating.py (its _THEMED_CATEGORIES/_ALL_KNOWN/TIER1 are import-time-derived)
    under UNITY_MCP_GD_TOOLS=0, and always reloads it back afterward. register_all() needs
    no reload -- it reads tool_specs.gd_tools_enabled() fresh at call time (Phase 5 B2)."""
    monkeypatch.setenv("UNITY_MCP_GD_TOOLS", "0")
    importlib.reload(gating)
    yield
    monkeypatch.delenv("UNITY_MCP_GD_TOOLS", raising=False)
    importlib.reload(gating)


def _registered_names(monkeypatch) -> list[str]:
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


def test_all_65_gd_tool_names_absent_from_both_surfaces(gd_tools_disabled, monkeypatch):
    gd_names = {name for name, spec in tool_specs._SPECS.items() if spec.category in tool_specs.GD_CATEGORIES}
    assert len(gd_names) == 65, "catalog drift: Phase 5 Part B scoped exactly 65 GD-category tools"

    discovered = gating.CATEGORIES
    discovered_gd = {name for key in tool_specs.GD_CATEGORIES for name in discovered.get(key, set())}
    assert not discovered_gd, f"gating.py still discovers {sorted(discovered_gd)} under UNITY_MCP_GD_TOOLS=0"

    registered = set(_registered_names(monkeypatch))
    leaked = gd_names & registered
    assert not leaked, f"register_all() still registered {sorted(leaked)} under UNITY_MCP_GD_TOOLS=0"


def test_register_all_completes_without_exception(gd_tools_disabled, monkeypatch):
    names = _registered_names(monkeypatch)  # raises on any catalog drift/missing function -- must not
    assert names, "register_all() must still register the non-GD catalog"


def test_the_11_tier1_gd_tools_vanish_not_just_lose_tier1(gd_tools_disabled, monkeypatch):
    tier1_gd_names = {name for name, spec in tool_specs._SPECS.items()
                       if spec.category in tool_specs.GD_CATEGORIES and (spec.core or spec.tier1)}
    assert tier1_gd_names == {
        "apply_scene_change", "delete_object", "scene", "scene_change_plan", "search_scene",
        "set_active", "set_parent", "screenshot", "vfx_intent", "ui_intent", "uitk_intent",
    }
    assert not (tier1_gd_names & gating.TIER1), "tier1 GD tools must not merely lose tier1 -- they must vanish"
    registered = set(_registered_names(monkeypatch))
    assert not (tier1_gd_names & registered), "tier1 GD tools must not be registered at all"


def test_core_tools_survive_untouched(gd_tools_disabled, monkeypatch):
    registered = set(_registered_names(monkeypatch))
    for witness in _CORE_WITNESSES:
        assert witness in gating._ALL_KNOWN, f"{witness} must stay known"
        assert witness in gating.TIER1, f"{witness} must stay tier1/core-visible"
        assert witness in registered, f"{witness} must still register"
