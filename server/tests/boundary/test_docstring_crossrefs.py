"""Regression: all 'use `tool`' cross-references in docstrings name real tools."""
import re
import inspect
import importlib
import pytest
from unity_mcp.tools.tool_specs import _SPECS

# Add new tool modules here when created
_TOOL_MODULE_NAMES = [
    "unity_mcp.tools.animation",
    "unity_mcp.tools.ask_tool",
    "unity_mcp.tools.ask_user_tool",
    "unity_mcp.tools.asset",
    "unity_mcp.tools.autobatch",
    "unity_mcp.tools.batch",
    "unity_mcp.tools.debug_tool",
    "unity_mcp.tools.diagnose",
    "unity_mcp.tools.diagnostics",
    "unity_mcp.tools.do_tool",
    "unity_mcp.tools.editor_control",
    "unity_mcp.tools.objects",
    "unity_mcp.tools.profiling",
    "unity_mcp.tools.rendering",
    "unity_mcp.tools.runtime",
    "unity_mcp.tools.scene",
    "unity_mcp.tools.screenshot",
    "unity_mcp.tools.skills",
    "unity_mcp.tools.spatial",
    "unity_mcp.tools.testing",
    "unity_mcp.tools.ui",
    "unity_mcp.tools.watch",
]

_ALL_TOOLS: frozenset[str] = frozenset(_SPECS.keys())

# Only matches explicit "use `tool_name`" cross-reference pointers,
# not every backtick (actions, params, enum values, etc.)
_CROSSREF_RE = re.compile(r"use\s+`([a-z][a-z0-9_]*)`")


def _collect_cases():
    """Yield (qualified_name, tool_name) for each cross-ref found in all docstrings."""
    cases = []
    for mod_name in _TOOL_MODULE_NAMES:
        mod = importlib.import_module(mod_name)
        for fn_name, fn in inspect.getmembers(mod, inspect.isfunction):
            doc = fn.__doc__ or ""
            for ref in _CROSSREF_RE.findall(doc):
                cases.append((f"{mod_name}.{fn_name}", ref))
    return cases


@pytest.mark.parametrize("fn_qual,ref", _collect_cases())
def test_cross_reference_is_known_tool(fn_qual, ref):
    """Each 'use `tool`' cross-reference must name a real tool in tool_specs._SPECS."""
    assert ref in _ALL_TOOLS, (
        f"{fn_qual}: docstring says 'use `{ref}`' but '{ref}' is not in tool_specs._SPECS.\n"
        f"  Either fix the docstring or add '{ref}' to _SPECS."
    )
