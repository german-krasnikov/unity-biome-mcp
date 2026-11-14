"""Phase 5 Part B: UNITY_MCP_GD_TOOLS=0 excludes the 6 GD-themed categories (SCENE,
COMPONENTS, ASSETS, MEDIA, UITOOLKIT, UGUI) from gating.py's _THEMED_CATEGORIES/
_ALL_KNOWN/TIER1 derivation -- the same env var and category set
_catalog_runtime.ToolSet.install() reads (tool_specs.GD_CATEGORIES/gd_tools_enabled()),
read at this module's own import time. Mirrors the registration-side exclusion in
test_gd_tools_env_toggle.py so a one-sided fix (only install() honors the var, or only
gating.py does) fails one of the two test files, not neither.
"""
import importlib

import pytest

from unity_mcp.tools import gating

GD_TOOL = "delete_object"    # SCENE, also tier1 -- must disappear from both places at once
CORE_TOOL = "create_object"  # CORE, never a GD category -- must survive regardless


@pytest.fixture
def reload_gating(monkeypatch):
    """Reloads the already-imported gating module in place (so every other module's
    `from . import gating` reference stays valid) under a given UNITY_MCP_GD_TOOLS value,
    and always reloads back to the default (unset) state afterward."""
    def _reload(env_value):
        if env_value is None:
            monkeypatch.delenv("UNITY_MCP_GD_TOOLS", raising=False)
        else:
            monkeypatch.setenv("UNITY_MCP_GD_TOOLS", env_value)
        importlib.reload(gating)
    yield _reload
    _reload(None)


def test_gating_includes_gd_tools_by_default(reload_gating):
    reload_gating(None)
    assert GD_TOOL in gating._ALL_KNOWN
    assert GD_TOOL in gating.TIER1
    assert GD_TOOL in gating._THEMED_CATEGORIES["SCENE"]


def test_gating_excludes_gd_tools_when_disabled(reload_gating):
    reload_gating("0")
    assert GD_TOOL not in gating._ALL_KNOWN
    assert GD_TOOL not in gating.TIER1
    assert gating._THEMED_CATEGORIES["SCENE"] == []
    assert CORE_TOOL in gating._CORE_TOOLS, "CORE is never a GD category -- must be untouched"
    assert CORE_TOOL in gating._ALL_KNOWN
