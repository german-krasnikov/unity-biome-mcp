"""The tool catalog owns the ToolSpec and the schema descriptions of its tools: tool_specs.py and _param_descriptions.py merge
what dsl/tools.biome generates, and a tool is declared in the catalog or by hand, never in both."""
from pathlib import Path

import pytest

from unity_mcp.tools import _param_descriptions, tool_specs
from unity_mcp.tools.generated import catalog_params, catalog_specs


def _run_source(module, monkeypatch, generated, name, value):
    """Execute the source of `module` in a scratch namespace, with one attribute of a generated module patched.
    The real module stays untouched, and its merge loop meets the patched data."""
    monkeypatch.setattr(generated, name, value)
    namespace = {"__name__": f"{module.__name__}_probe", "__package__": module.__package__}
    exec(compile(Path(module.__file__).read_text(encoding="utf-8"), module.__file__, "exec"), namespace)
    return namespace


def test_the_catalog_tools_are_merged_and_owned_by_the_catalog():
    assert catalog_specs.SPEC_KWARGS
    for name, kwargs in catalog_specs.SPEC_KWARGS.items():
        assert tool_specs._SPEC_OWNERS[name] == "catalog"
        assert tool_specs._SPECS[name].category == kwargs["category"]
    for name, descriptions in catalog_params.PARAM_DESCRIPTIONS.items():
        assert _param_descriptions.PARAM_DESCRIPTIONS[name] == descriptions


def test_each_effect_gives_its_mutability(monkeypatch):
    catalog = {name: {"category": "SCENE", "effect": effect, "max_chars": 5} for name, effect in
               [("a", "RO"), ("b", "RW"), ("c", "RW_IDEM"), ("d", "DEL")]}
    specs = _run_source(tool_specs, monkeypatch, catalog_specs, "SPEC_KWARGS", catalog)["_SPECS"]
    assert [specs[name].mutability for name in "abcd"] == ["read", "write", "write", "write"]


def test_a_tool_in_the_catalog_and_in_the_tool_specs_literal_is_refused(monkeypatch):
    catalog = {"ping": {"category": "SCENE", "effect": "RO"}}
    with pytest.raises(ValueError, match="ping"):
        _run_source(tool_specs, monkeypatch, catalog_specs, "SPEC_KWARGS", catalog)


def test_a_tool_described_in_the_catalog_and_by_hand_is_refused(monkeypatch):
    with pytest.raises(ValueError, match="get_component"):
        _run_source(_param_descriptions, monkeypatch, catalog_params, "PARAM_DESCRIPTIONS", {"get_component": {}})
