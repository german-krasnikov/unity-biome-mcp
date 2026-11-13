"""The READ lines of the tool catalog (step 7.2 A0a): the tools whose actions isMutating tells apart.

ACTION_READS is printed from the READ lines of dsl/tools.biome into tools/generated/catalog_reads.py; middleware_types adds the wire
alias navmesh. The values (which action reads) are pinned by the golden of test_action_reads_golden.py; these are the rules of the
catalog that no printer can state: a tool with READ lines takes an `action` to tell them apart, and nothing but the alias is added.
"""
from unity_mcp.middleware_types import ACTION_READS
from unity_mcp.server import mcp
from unity_mcp.tools.generated import catalog_reads


def test_a_tool_with_read_actions_takes_an_action_argument():
    assert catalog_reads.ACTION_READS, "the catalog has no READ line: this test would pass without checking"
    for name in catalog_reads.ACTION_READS:
        tool = mcp._tool_manager._tools.get(name)
        assert tool is not None, f"{name} has READ lines and is no MCP tool"
        assert "action" in tool.parameters["properties"], f"{name} has READ lines and takes no action"


def test_middleware_adds_only_the_wire_alias_of_navmesh_query_to_the_catalog():
    assert set(ACTION_READS) - set(catalog_reads.ACTION_READS) == {"navmesh"}
    assert ACTION_READS["navmesh"] == catalog_reads.ACTION_READS["navmesh_query"]
    assert all(ACTION_READS[name] is actions for name, actions in catalog_reads.ACTION_READS.items())


def test_a_read_action_is_a_word_in_lower_case():
    """A tool entry may legitimately be an empty frozenset now (dsl-bottlenecks-2026-11 S2): a tool that dispatches on
    ACTION but has no READ-qualified action gets one anyway, because `IF ANY ACTION` (tool.construct) cannot filter on
    WHERE read -- isMutating asks the same (empty) set either way, so this is inert, not a fact to assert away."""
    for name, actions in ACTION_READS.items():
        assert all(action == action.lower() and action.replace("_", "").isalnum() for action in actions), name
