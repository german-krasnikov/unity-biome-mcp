"""The action vocabulary of a tool has one source: the ACTION lines of dsl/tools.biome (dsl-bottlenecks-2026-11, Q1).

Before this, a tool's action vocabulary existed as three hand copies that could drift from each other and from the
real C# dispatch: the switch/switch-expression arms, the hand valid-list passed to `ErrorHelper.InvalidAction`,
and the catalog's DOC prose. This test reads the catalog's generated `TOOL_ACTIONS` (server/tests read the
generated table instead of re-parsing dsl/tools.biome -- dsl/README.md) and the real C# dispatch arms, and fails
when either one knows an action the other does not.

`_ACTION_TOOLS` is the (currently one-tool) map of a catalog-declared-ACTION tool to its C# dispatcher; it grows as
more tools gain ACTION lines in later slices.
"""
import re
from pathlib import Path

import pytest

from unity_mcp.tools.generated import catalog_actions, catalog_examples, catalog_reads, catalog_tools

pytestmark = pytest.mark.csharp_parity

_EDITOR = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor"

# tool name -> (file relative to unity-plugin/Editor, C# dispatcher method name)
_ACTION_TOOLS = {
    "animation": ("CommandRouter.MediaHandlers.cs", "ExecAnimationConsolidated"),
}

_PATTERN_BEFORE_ARROW = re.compile(r'((?:"[a-z_]+"\s*(?:or\s*)?)+)=>')
_QUOTED = re.compile(r'"([a-z_]+)"')
_INVALID_ACTION_LIST = re.compile(r'ErrorHelper\.InvalidAction\(\s*action,\s*new\[\]\s*\{([^}]*)\}\)')


def _function_body(text: str, func_name: str) -> str:
    """The brace-balanced body of `func_name`, found from its signature's opening brace."""
    start = text.index(f"{func_name}(")
    open_brace = text.index("{", start)
    depth, i = 0, open_brace
    while True:
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return text[open_brace:i + 1]
        i += 1


def dispatch_arms(body: str) -> set[str]:
    """The pattern-side action strings of every arm of a `switch (action) { ... }` or `action switch { ... }` block."""
    return {name for match in _PATTERN_BEFORE_ARROW.finditer(body) for name in _QUOTED.findall(match.group(1))}


def invalid_action_list(body: str) -> set[str]:
    """The hand valid-list array passed to `ErrorHelper.InvalidAction(action, new[] {...})`, or empty if absent."""
    match = _INVALID_ACTION_LIST.search(body)
    return set(_QUOTED.findall(match.group(1))) if match else set()


def _real_body(tool: str) -> str:
    path, func = _ACTION_TOOLS[tool]
    return _function_body((_EDITOR / path).read_text(encoding="utf-8"), func)


@pytest.mark.parametrize("tool", sorted(_ACTION_TOOLS))
def test_catalog_actions_match_the_csharp_dispatch_arms(tool):
    assert set(catalog_actions.TOOL_ACTIONS[tool]) == dispatch_arms(_real_body(tool))


@pytest.mark.parametrize("tool", sorted(_ACTION_TOOLS))
def test_catalog_actions_match_the_csharp_invalid_action_list(tool):
    assert set(catalog_actions.TOOL_ACTIONS[tool]) == invalid_action_list(_real_body(tool))


@pytest.mark.parametrize("tool", sorted(catalog_actions.TOOL_ACTIONS))
def test_arg_example_of_an_action_tool_is_a_declared_action(tool):
    example = catalog_examples.ARG_EXAMPLES.get(tool, {}).get("action")
    if example is not None:
        assert example in catalog_actions.TOOL_ACTIONS[tool]


@pytest.mark.parametrize("tool", sorted(catalog_actions.TOOL_ACTIONS))
def test_every_read_of_an_action_tool_names_a_declared_action(tool):
    reads = catalog_reads.ACTION_READS.get(tool, frozenset())
    assert reads <= set(catalog_actions.TOOL_ACTIONS[tool])


# --- Scanner self-test: the comparison must go red in BOTH directions (neither assertion above is a tautology). ---

_SCRATCH_SWITCH = '''
        private static string ExecScratchConsolidated(string args)
        {
            var action = ScratchArgs.Action(args);
            return action switch
            {
                "get" => ExecGet(args),
                "edit" or "surprise_arm" => ExecEdit(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "edit" }))
            };
        }
'''


def test_scanner_catches_a_csharp_arm_the_catalog_does_not_know():
    """A C# arm ('surprise_arm') not in the declared set must make the comparison fail."""
    body = _function_body(_SCRATCH_SWITCH, "ExecScratchConsolidated")
    declared = {"get", "edit"}
    assert dispatch_arms(body) != declared
    assert "surprise_arm" in dispatch_arms(body) - declared


def test_scanner_catches_a_declared_action_the_csharp_does_not_dispatch():
    """A catalog action not dispatched by the real C# (here: animation + a bogus action) must make the comparison fail."""
    declared_plus_bogus = set(catalog_actions.TOOL_ACTIONS["animation"]) | {"bogus_action"}
    assert declared_plus_bogus != dispatch_arms(_real_body("animation"))
    assert "bogus_action" in declared_plus_bogus - dispatch_arms(_real_body("animation"))


def test_scanner_catches_a_csharp_invalid_action_list_missing_a_declared_action():
    body = _function_body(_SCRATCH_SWITCH, "ExecScratchConsolidated")
    declared_plus_bogus = {"get", "edit", "bogus_action"}
    assert declared_plus_bogus != invalid_action_list(body)


# --- Every declared action must be named in the text the MCP client actually receives. ---
#
# The client-visible description of a tool is `fn.__doc__` of its generated wrapper in
# server/src/unity_mcp/tools/generated/catalog_tools.py: ToolSet.register() hands that function straight to
# `mcp.tool()(fn)` (server/src/unity_mcp/tools/_catalog_runtime.py), and the MCP SDK reads the tool's description
# from the function's docstring, not from the `tools.expect(...)` summary string above it. scripts/dsl_golden.py
# confirms this: `surface_lines()` reads `tool.description` off the live registered MCP tool for the golden-surface
# test (test_tool_surface_golden.py). `catalog_specs.py` (the ToolSpec table) carries no description field at all.

_WORD_BOUNDARY_TEMPLATE = r"\b{}\b"


def _names_whole_word(text: str, word: str) -> bool:
    """Whether `word` appears in `text` as a whole word: surrounded by non-word characters (or the string's ends).

    `_` counts as a word character (Python's `\\b`), so 'get' does not match inside 'get_events', and 'edit' does
    not match inside 'edited'.
    """
    return re.search(_WORD_BOUNDARY_TEMPLATE.format(re.escape(word)), text) is not None


def test_word_boundary_matcher_rejects_substring_matches():
    """Synthetic self-test of the matcher itself, no catalog edit: a short action name must not be satisfied by a
    longer word that merely contains it."""
    assert _names_whole_word("action: get (list clips)", "get")
    assert not _names_whole_word("action: get_events only", "get")
    assert _names_whole_word("action: get_events only", "get_events")
    assert not _names_whole_word("already edited", "edit")
    assert _names_whole_word("ready to edit now", "edit")


@pytest.mark.parametrize("tool", sorted(catalog_actions.TOOL_ACTIONS))
def test_declared_action_is_named_in_the_client_visible_description(tool):
    """Substitute for generating the action enumeration into the description: the catalog template cannot yet
    nest the IF/EACH needed to emit it (dsl-bottlenecks-2026-11, Core gap reported to the owner), so until that is
    fixed, this test guards the hand-written DOC prose instead -- delete it once the enumeration is generated.
    """
    description = getattr(catalog_tools, tool).__doc__ or ""
    missing = [action for action in catalog_actions.TOOL_ACTIONS[tool] if not _names_whole_word(description, action)]
    assert not missing, f"{tool}: description does not name action(s) {missing} as whole words -- {description!r}"
