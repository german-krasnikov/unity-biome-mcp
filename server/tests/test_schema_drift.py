"""Schema drift guard: CORE tool docstrings must contain discriminating tokens.

Prevents description rot where tools lose their cross-references and
disambiguation cues, causing LLMs to pick the wrong tool.
"""
import importlib
import pkgutil
import pytest
import unity_mcp.tools as _tools_pkg
from unity_mcp.tools.gating import _CORE_TOOLS
from unity_mcp.tools.tool_specs import _SPECS

_ALL_MCP_NAMES = frozenset(n for n, s in _SPECS.items() if s.category != "_INTERNAL")

# Build tool_name → function map by scanning tool submodules + known extra packages.
_SCAN_MODULES = [f"unity_mcp.tools.{i.name}" for i in pkgutil.iter_modules(_tools_pkg.__path__)]
_SCAN_MODULES.append("unity_mcp.debug.snapshots")  # snapshot lives outside tools/

_TOOL_FN: dict = {}
_ALL_TOOL_FN: dict = {}
for _mod_name in _SCAN_MODULES:
    try:
        mod = importlib.import_module(_mod_name)
        for _name in dir(mod):
            fn = getattr(mod, _name)
            if not callable(fn):
                continue
            if _name in _CORE_TOOLS:
                _TOOL_FN[_name] = fn
            if _name in _ALL_MCP_NAMES:
                _ALL_TOOL_FN[_name] = fn
    except Exception:
        pass

_TIER1_NAMES = sorted(n for n, s in _SPECS.items() if s.tier1 and not s.core)
_IMPL_LEAK_TOKENS = ("C#-side", "bridge.send", "TCP command", "sent over wire", "internal")


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


@pytest.mark.parametrize("tool_name", _TIER1_NAMES)
def test_tier1_tools_have_descriptions(tool_name):
    """Every TIER1 (non-core) tool must have a non-trivial description (len > 20)."""
    fn = _ALL_TOOL_FN.get(tool_name)
    assert fn is not None, f"No function found for TIER1 tool '{tool_name}'"
    doc = fn.__doc__ or ""
    assert len(doc) > 20, f"'{tool_name}' description too short ({len(doc)} chars): {doc!r}"


@pytest.mark.parametrize("tool_name", sorted(_ALL_MCP_NAMES))
def test_no_implementation_leakage_in_descriptions(tool_name):
    """Tool descriptions must not expose internal implementation details to the LLM."""
    fn = _ALL_TOOL_FN.get(tool_name)
    assert fn is not None, f"No function found for tool '{tool_name}' — add its module to _SCAN_MODULES"
    doc = fn.__doc__ or ""
    for token in _IMPL_LEAK_TOKENS:
        assert token not in doc, (
            f"'{tool_name}' description leaks implementation detail '{token}': {doc[:120]!r}"
        )
