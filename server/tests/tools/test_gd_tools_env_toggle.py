"""Phase 5 Part B: UNITY_MCP_GD_TOOLS=0 excludes the 6 GD-themed categories (SCENE,
COMPONENTS, ASSETS, MEDIA, UITOOLKIT, UGUI) from registration -- without register_all()
raising, and without breaking the hand-written re-export modules that import generated
wrapper functions regardless of whether install() ever registers them with FastMCP
(dsl/README.md; the risk this guards is that exclusion was assumed to happen at codegen
time, not at the registration-pass layer).
"""
import os
import subprocess
import sys

import pytest
from unittest.mock import AsyncMock, MagicMock


class Recorder:
    """Stands in for FastMCP and records the name of every tool register_all hands over."""

    def __init__(self):
        self.names = []

    def tool(self, name=None, **_options):
        def decorate(fn):
            self.names.append(name or fn.__name__)
            return fn
        return decorate


def _register_all_names(monkeypatch) -> list[str]:
    """Runs register_all against a Recorder and returns the tool names it saw, restoring
    every unity_mcp.tools/unity_mcp.debug module's bound state afterward (same idiom as
    tests/boundary/test_single_owner.py's registered_names fixture)."""
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


# One concrete tool name per excluded category (SCENE, COMPONENTS, ASSETS, MEDIA,
# UITOOLKIT, UGUI) and one CORE witness, used as a spot check rather than re-deriving
# the full 65-name list this test does not own.
_GD_WITNESSES = ("delete_object", "wire_event", "material", "screenshot", "inspect_uitk", "create_ui")
_CORE_WITNESS = "create_object"


def test_register_all_boots_clean_with_gd_tools_default(monkeypatch):
    monkeypatch.delenv("UNITY_MCP_GD_TOOLS", raising=False)
    names = _register_all_names(monkeypatch)
    assert _CORE_WITNESS in names
    for witness in _GD_WITNESSES:
        assert witness in names, f"{witness} must register when UNITY_MCP_GD_TOOLS is unset"


def test_register_all_boots_clean_with_gd_tools_disabled(monkeypatch):
    monkeypatch.setenv("UNITY_MCP_GD_TOOLS", "0")
    names = _register_all_names(monkeypatch)
    assert _CORE_WITNESS in names, "CORE tools are never a GD category -- must still register"
    for witness in _GD_WITNESSES:
        assert witness not in names, f"{witness} must not register when UNITY_MCP_GD_TOOLS=0"


@pytest.mark.parametrize("module_name", ["scene", "objects", "asset", "uitk", "animation"])
def test_reexport_modules_import_clean_with_gd_tools_disabled(module_name):
    """scene.py/objects.py/asset.py/uitk.py/animation.py do `from .generated.catalog_tools
    import (...)` -- plain attribute imports of functions catalog_tools.py always defines
    (tools.expect/@tools.provide run unconditionally at module import time), regardless of
    whether install() later calls mcp.tool() on them. Exclusion happens at the registration
    pass, not at import, so these imports must never fail under UNITY_MCP_GD_TOOLS=0.

    A real subprocess, not an in-process sys.modules delete+reimport: deleting and
    reimporting a submodule also rebinds it as an attribute on the already-imported
    `unity_mcp.tools` package object (an import-machinery side effect pytest's monkeypatch
    does not track or undo), which orphans every other module's reference to the original,
    already-register()'d instance for the rest of the test session."""
    env = {**os.environ, "UNITY_MCP_GD_TOOLS": "0"}
    result = subprocess.run(
        [sys.executable, "-c", f"import unity_mcp.tools.{module_name}"],
        env=env, capture_output=True, text=True, encoding="utf-8", timeout=30,
    )
    assert result.returncode == 0, result.stderr
