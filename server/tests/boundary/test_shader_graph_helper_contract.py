"""ShaderGraphHelper WithGraph contract (DSL gap analysis #20).

Before this contract, 5 ShaderGraphHelper mutation methods (ManageNode,
ManageEdge, AddProperty, RemoveProperty, RenameProperty) each hand-rolled the
same ReadAllText -> SplitBlocks -> FindRoot -> ... -> WriteAllText -> import
boilerplate. A 6th copy-pasted mutation could drift (e.g. forget the
AssetDatabase re-import, or the "No GraphData block found" guard) with no test
catching it.

This test scans the C# source directly (no Unity needed) and fails if the
read/write boilerplate reappears outside the single shared `WithGraph` helper.
"""
from pathlib import Path

_MUTATIONS_CS = (
    Path(__file__).parents[3]
    / "unity-plugin"
    / "Editor"
    / "ShaderGraphHelper.Mutations.cs"
).read_text(encoding="utf-8")

# Every mutation that needs the full graph (read -> mutate -> write -> import).
_WITHGRAPH_CALLERS = (
    "ManageNode",
    "ManageEdge",
    "AddProperty",
    "RemoveProperty",
    "RenameProperty",
)


def test_read_all_text_appears_exactly_once():
    """File.ReadAllText(path) must exist in exactly one place -- WithGraph --
    not once per mutation method."""
    assert _MUTATIONS_CS.count("File.ReadAllText(path)") == 1, (
        "File.ReadAllText(path) appears more than once in "
        "ShaderGraphHelper.Mutations.cs -- a mutation is re-reading the file "
        "itself instead of going through the shared WithGraph helper"
    )


def test_write_all_text_appears_exactly_once():
    """File.WriteAllText(path, content) must exist in exactly one place --
    WithGraph -- not once per mutation method."""
    assert _MUTATIONS_CS.count("File.WriteAllText(path, content)") == 1, (
        "File.WriteAllText(path, content) appears more than once in "
        "ShaderGraphHelper.Mutations.cs -- a mutation is re-writing the file "
        "itself instead of going through the shared WithGraph helper"
    )


def test_with_graph_helper_exists():
    """The shared helper must exist with the expected signature."""
    assert "static void WithGraph(string path, Func<string, List<string>, string, string> mutate)" in _MUTATIONS_CS, (
        "WithGraph helper not found with the expected signature in "
        "ShaderGraphHelper.Mutations.cs"
    )


def test_every_mutation_delegates_to_with_graph():
    """Each of the 5 mutation methods must call WithGraph(path, ...) rather
    than hand-rolling its own read/mutate/write sequence."""
    for name in _WITHGRAPH_CALLERS:
        marker = f"public static string {name}("
        start = _MUTATIONS_CS.find(marker)
        assert start >= 0, f"could not find method {name} in ShaderGraphHelper.Mutations.cs"
        # Isolate this method's body up to the next "public static string" or EOF.
        next_start = _MUTATIONS_CS.find("public static string ", start + len(marker))
        body = _MUTATIONS_CS[start:next_start if next_start >= 0 else len(_MUTATIONS_CS)]
        assert "WithGraph(path," in body, (
            f"{name} does not delegate to the shared WithGraph helper"
        )
