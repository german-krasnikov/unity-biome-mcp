"""Schema drift guard: CORE tool docstrings must contain discriminating tokens.

Prevents description rot where tools lose their cross-references and
disambiguation cues, causing LLMs to pick the wrong tool.
"""
import importlib
import pkgutil
import pytest
import unity_mcp.tools as _tools_pkg
from unity_mcp.tools.gating import _CORE_TOOLS

# Build tool_name → function map by scanning all tool submodules once at import time.
_TOOL_FN: dict = {}
for _info in pkgutil.iter_modules(_tools_pkg.__path__):
    try:
        mod = importlib.import_module(f"unity_mcp.tools.{_info.name}")
        for _name in dir(mod):
            if _name in _CORE_TOOLS and callable(getattr(mod, _name)):
                _TOOL_FN[_name] = getattr(mod, _name)
    except Exception:
        pass


@pytest.mark.parametrize("tool_name", sorted(_CORE_TOOLS))
def test_core_tool_has_description(tool_name):
    """Every CORE tool must have a non-empty description (docstring len > 30)."""
    fn = _TOOL_FN.get(tool_name)
    assert fn is not None, f"No function found for CORE tool '{tool_name}'"
    doc = fn.__doc__ or ""
    assert len(doc) > 30, f"'{tool_name}' docstring too short ({len(doc)} chars): {doc!r}"


def test_get_console_cross_refs_compile_errors():
    """get_console docstring must mention compile_errors to avoid confusion with get_compile_errors."""
    from unity_mcp.tools.console import get_console
    assert "compile_error" in (get_console.__doc__ or "").lower(), (
        "get_console docstring must cross-ref 'compile_errors' (use get_compile_errors for that)"
    )


def test_get_hierarchy_cross_refs_search_scene():
    """get_hierarchy docstring must mention search_scene to guide filtering use-case."""
    from unity_mcp.tools.scene import get_hierarchy
    assert "search_scene" in (get_hierarchy.__doc__ or "").lower(), (
        "get_hierarchy docstring must cross-ref 'search_scene' for filtered lookups"
    )


def test_do_cross_refs_batch():
    """do docstring must mention batch to clarify its implementation strategy."""
    from unity_mcp.tools.do_tool import do
    assert "batch" in (do.__doc__ or "").lower(), (
        "do docstring must mention 'batch' to distinguish from direct tool calls"
    )
