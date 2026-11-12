"""DRY guard (T5 pilot slice 0): dsl/test-plans/animation.contract.json's "call" action
params must equal -- both directions -- the non-LOCAL ARG names of TOOL animation in
dsl/tools.biome. Nothing generates the contract from the catalog yet
(Plans/biome-test-pilot.md Blockers #1); this guard is the stand-in that fails loudly
on drift until a real catalog-to-contract bridge exists.
"""
import json
import re
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
TOOLS_BIOME = REPO_ROOT / "dsl" / "tools.biome"
CONTRACT = REPO_ROOT / "dsl" / "test-plans" / "animation.contract.json"

_ARG_LINE = re.compile(r"^\s*ARG\s+(\w+)\s+\S+(.*)$", re.MULTILINE)


def _tool_block(text: str, tool: str) -> str:
    match = re.search(rf"^TOOL {tool}\b.*?\n(.*?)\nEND\n", text, re.DOTALL | re.MULTILINE)
    if not match:
        raise LookupError(f"TOOL {tool} not found in dsl/tools.biome")
    return match.group(1)


def catalog_non_local_args(tool: str = "animation", text: str | None = None) -> frozenset[str]:
    block = _tool_block(text if text is not None else TOOLS_BIOME.read_text(encoding="utf-8"), tool)
    return frozenset(m.group(1) for m in _ARG_LINE.finditer(block) if "LOCAL" not in m.group(2).split())


def contract_call_params(spec: dict | None = None) -> frozenset[str]:
    data = spec if spec is not None else json.loads(CONTRACT.read_text(encoding="utf-8"))
    return frozenset(data["actions"]["call"]["params"])


def diff_both_ways(catalog: frozenset[str], contract: frozenset[str]) -> tuple[frozenset[str], frozenset[str]]:
    """(catalog-only, contract-only); both empty means the two sets are equal."""
    return catalog - contract, contract - catalog


def test_contract_call_params_equal_the_catalogs_non_local_args():
    catalog_only, contract_only = diff_both_ways(catalog_non_local_args(), contract_call_params())
    assert not catalog_only, f"catalog ARGs missing from the contract: {sorted(catalog_only)}"
    assert not contract_only, f"contract params not a catalog ARG: {sorted(contract_only)}"


def test_diff_both_ways_catches_an_arg_the_catalog_has_and_the_contract_lacks():
    catalog_only, contract_only = diff_both_ways(frozenset({"a", "b"}), frozenset({"a"}))
    assert catalog_only == frozenset({"b"})
    assert contract_only == frozenset()


def test_diff_both_ways_catches_a_param_the_contract_has_and_the_catalog_lacks():
    catalog_only, contract_only = diff_both_ways(frozenset({"a"}), frozenset({"a", "b"}))
    assert catalog_only == frozenset()
    assert contract_only == frozenset({"b"})
