"""The action vocabulary of a tool has one source: the ACTION lines of dsl/tools.biome (dsl-bottlenecks-2026-11, Q1).

Before this, a tool's action vocabulary existed as three hand copies that could drift from each other and from the
real C# dispatch: the switch/switch-expression arms, the hand valid-list passed to `ErrorHelper.InvalidAction`,
and the catalog's DOC prose. This test reads the catalog's generated `TOOL_ACTIONS` (server/tests read the
generated table instead of re-parsing dsl/tools.biome -- dsl/README.md) and the real C# dispatch arms, and fails
when either one knows an action the other does not.

`_ACTION_TOOLS` is the map of a catalog-declared-ACTION tool to the C# source(s) that really dispatch it -- a tuple
of (file, function) pairs, because some tools (`editor`) split their dispatch across an if-chain in one method and
a switch in another; the scanner unions the arms/hints of every source. It grows as more tools gain ACTION lines
in later slices.
"""
import importlib
import re
from pathlib import Path

import pytest

from unity_mcp.tools.generated import catalog_actions, catalog_examples, catalog_reads, catalog_tools

pytestmark = pytest.mark.csharp_parity

_EDITOR = Path(__file__).resolve().parents[2] / "unity-plugin" / "Editor"

# tool name -> ((file relative to unity-plugin/Editor, C# dispatcher method name), ...)
_ACTION_TOOLS: dict[str, tuple[tuple[str, str], ...]] = {
    "animation": (("CommandRouter.MediaHandlers.cs", "ExecAnimationConsolidated"),),
    "asset": (("AssetDatabaseHelper.cs", "Execute"),),
    "editor": (
        # ExecEditor (CommandRouter.MediaHandlers.cs:323-331) handles state/mutation_mode itself via an if-chain
        # and delegates everything else to EditorStateHelper.Control's switch (EditorStateHelper.cs:57-89).
        ("CommandRouter.MediaHandlers.cs", "ExecEditor"),
        ("EditorStateHelper.cs", "Control"),
    ),
    "prefab": (("PrefabHelper.cs", "Execute"),),
    "render_analyze": (("RenderAnalyzer.cs", "Execute"),),
    "scene": (("CommandRouter.MediaHandlers.cs", "ExecScene"),),
    "scriptable_object": (("ScriptableObjectHelper.cs", "Execute"),),
    "shader": (("CommandRouter.MediaHandlers.cs", "ExecShaderConsolidated"),),
    "timeline": (("CommandRouter.MediaHandlers.cs", "ExecTimelineConsolidated"),),
    # ExecUitkElement (CommandRouter.MediaHandlers.cs:273-282) is a one-line forwarder with no action logic of its
    # own; the real dispatch (an if for "query" plus a switch for the rest) lives in UIHelper.UitkElement.
    "uitk_element": (("UIHelper.UIToolkit.cs", "UitkElement"),),
}

# uitk_file is intentionally left out of _ACTION_TOOLS: ExecUitkFile (CommandRouter.MediaHandlers.cs:297-321)
# dispatches 5 underscore-style actions itself, then forwards every other action string (whatever it is) to
# UIFileHelper.EditUIFile, whose switch (UIFileHelper.cs:111-121) dispatches 7 more -- but those 7 are spelt with
# hyphens ("set-attr", "add-class", ...), which CONSTRUCT TOOL's `ITEM ACTION <act:match([a-z][a-z0-9_]*)>` cannot
# express. Declaring only the 5 that fit would misrepresent the tool's real (12-action) vocabulary -- the exact
# "undocumented action" defect this slice exists to fix -- so the tool is skipped rather than partially declared.

_PATTERN_BEFORE_ARROW = re.compile(r'((?:"[a-z_]+"\s*(?:or\s*)?)+)=>')
_QUOTED = re.compile(r'"([a-z_]+)"')
_INVALID_ACTION_LIST = re.compile(r'ErrorHelper\.InvalidAction\(\s*action,\s*new\[\]\s*\{([^}]*)\}\)')
# A hint array passed by name instead of inline (AssetDatabaseHelper.cs, ScriptableObjectHelper.cs): resolved by
# finding the field's own declaration in the full file text.
_NAMED_INVALID_ACTION_LIST = re.compile(r'ErrorHelper\.InvalidAction\(\s*action,\s*(\w+)\)')
# A plain `if (action == "...")` dispatch arm -- the shape a switch-EXPRESSION scanner cannot see (`editor`,
# `uitk_element`); and a classic `case "...":` switch-STATEMENT label -- the shape the arrow-only scanner above
# cannot see (AssetDatabaseHelper.cs, ScriptableObjectHelper.cs, EditorStateHelper.cs all use `switch { case: }`,
# not `action switch { => }`).
_IF_EQUALITY_ARM = re.compile(r'\baction\s*==\s*"([a-z_]+)"')
_CASE_LABEL = re.compile(r'case\s+"([a-z_]+)"\s*:')


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
    """The action strings a dispatch body really accepts: every pattern-side string of an `action switch { ... }`
    expression arm or a classic `switch (action) { case "...": ... }` statement label, plus every plain
    `action == "..."` if-chain comparison."""
    switch_arms = {name for match in _PATTERN_BEFORE_ARROW.finditer(body) for name in _QUOTED.findall(match.group(1))}
    case_labels = set(_CASE_LABEL.findall(body))
    return switch_arms | case_labels | set(_IF_EQUALITY_ARM.findall(body))


def invalid_action_list(body: str, full_text: str | None = None) -> set[str]:
    """The hand valid-list passed to `ErrorHelper.InvalidAction`: an inline `new[] {...}` literal, or -- given the
    declaring file's `full_text` -- a `static readonly string[] <name>` field passed by name. Empty if neither
    artifact exists in `body`."""
    match = _INVALID_ACTION_LIST.search(body)
    if match:
        return set(_QUOTED.findall(match.group(1)))
    named = _NAMED_INVALID_ACTION_LIST.search(body)
    if named and full_text is not None:
        decl = re.search(r"static\s+readonly\s+string\[\]\s+" + re.escape(named.group(1)) + r"\s*=\s*\{([^}]*)\}",
                          full_text)
        if decl:
            return set(_QUOTED.findall(decl.group(1)))
    return set()


def _real_sources(tool: str) -> list[tuple[str, str]]:
    """(full file text, function body) for every C# dispatch site of `tool`."""
    sources = []
    for path, func in _ACTION_TOOLS[tool]:
        text = (_EDITOR / path).read_text(encoding="utf-8")
        sources.append((text, _function_body(text, func)))
    return sources


def _real_body(tool: str) -> str:
    """The single dispatch body of a one-source tool (kept for the scanner self-tests below)."""
    (_, body), = _real_sources(tool)
    return body


def _real_dispatch_arms(tool: str) -> set[str]:
    return {name for _, body in _real_sources(tool) for name in dispatch_arms(body)}


def _real_invalid_action_list(tool: str) -> set[str]:
    return {name for text, body in _real_sources(tool) for name in invalid_action_list(body, text)}


# Tools whose C# fallback throws free text with no `ErrorHelper.InvalidAction` call at all -- there is no
# valid-list artifact to compare, so the test is skipped with a recorded reason instead of silently passing.
_NO_INVALID_ACTION_LIST_ARTIFACT: dict[str, str] = {
    "render_analyze": "RenderAnalyzer.cs:40-41 throws an inline interpolated \"Unknown action ... Valid: a|b|c\" "
                       "message, not ErrorHelper.InvalidAction(action, new[] {...}) -- no array to compare.",
    "uitk_element": "UIHelper.UIToolkit.cs:100 throws an inline interpolated 'err: unknown action ... Valid: a b c' "
                    "message, not ErrorHelper.InvalidAction(action, new[] {...}) -- no array to compare.",
}

# A real, dated disagreement between the C# switch and its own hand hint array -- recorded here (not fixed in C#)
# so the test documents the bug and goes red (XPASS) if only one side is ever fixed without updating this entry.
_KNOWN_INVALID_ACTION_LIST_DISAGREEMENT: dict[str, str] = {
    "editor": "2026-11-13 dsl-bottlenecks-2026-11: EditorStateHelper.cs:87 ErrorHelper.InvalidAction hint array "
              "omits 'mutation_mode', which ExecEditor (CommandRouter.MediaHandlers.cs:328) really dispatches. "
              "Not fixed here (DSL/Python-only slice); the next C# slice replaces this hand array with "
              "ToolActions.Names(\"editor\") and must remove this xfail in the same change.",
}


def _cases(skip: dict[str, str] | None = None, xfail: dict[str, str] | None = None) -> list:
    skip = skip or {}
    xfail = xfail or {}
    cases = []
    for tool in sorted(_ACTION_TOOLS):
        marks = []
        if tool in skip:
            marks.append(pytest.mark.skip(reason=skip[tool]))
        if tool in xfail:
            marks.append(pytest.mark.xfail(reason=xfail[tool], strict=True))
        cases.append(pytest.param(tool, marks=marks) if marks else tool)
    return cases


@pytest.mark.parametrize("tool", sorted(_ACTION_TOOLS))
def test_catalog_actions_match_the_csharp_dispatch_arms(tool):
    assert set(catalog_actions.TOOL_ACTIONS[tool]) == _real_dispatch_arms(tool)


@pytest.mark.parametrize("tool", _cases(skip=_NO_INVALID_ACTION_LIST_ARTIFACT,
                                         xfail=_KNOWN_INVALID_ACTION_LIST_DISAGREEMENT))
def test_catalog_actions_match_the_csharp_invalid_action_list(tool):
    assert set(catalog_actions.TOOL_ACTIONS[tool]) == _real_invalid_action_list(tool)


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


_SCRATCH_IF_CHAIN = '''
        private static string ExecScratchIfChain(string args)
        {
            var action = ScratchArgs.Action(args);
            if (action == "state")
                return GetState();
            if (action == "mutation_mode")
                return SetMutationIntent(args);
            return Delegate(action, args);
        }
'''


def test_scanner_catches_an_if_chain_arm_a_switch_cannot_see():
    """`editor` and `uitk_element` dispatch some actions with a plain `if (action == "...")` chain instead of a
    switch (CommandRouter.MediaHandlers.cs:326-329; UIHelper.UIToolkit.cs:63) -- a switch-arm-only scanner would
    see 0 arms here."""
    body = _function_body(_SCRATCH_IF_CHAIN, "ExecScratchIfChain")
    assert dispatch_arms(body) == {"state", "mutation_mode"}


_SCRATCH_NAMED_ARRAY = '''
    internal static class ScratchHelper
    {
        static readonly string[] ValidActions = { "find", "create" };

        internal static string Execute(string action, string argsJson)
        {
            switch (action)
            {
                case "find": return Find(argsJson);
                case "create": return Create(argsJson);
                default: throw new System.Exception(ErrorHelper.InvalidAction(action, ValidActions));
            }
        }
    }
'''


def test_scanner_resolves_a_named_valid_actions_array_not_only_an_inline_one():
    """`asset`/`scriptable_object` pass a named `static readonly string[] ValidActions` field to
    ErrorHelper.InvalidAction instead of an inline `new[] {...}` literal (AssetDatabaseHelper.cs:13,34;
    ScriptableObjectHelper.cs:12,22) -- an inline-array-only scanner would see an empty set here."""
    body = _function_body(_SCRATCH_NAMED_ARRAY, "Execute")
    assert invalid_action_list(body) == set()  # no inline array, and no full_text to resolve the name from
    assert invalid_action_list(body, _SCRATCH_NAMED_ARRAY) == {"find", "create"}


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


# tool -> (module, function) of the registered function whose __doc__ is really client-visible, for a tool the
# catalog cannot generate a wrapper for at all (no FORWARD, no BODY) and that is instead hand-registered elsewhere.
_DOC_SOURCE_OVERRIDE: dict[str, tuple[str, str]] = {
    # TOOL uitk_element declares HANDLER (C# dispatch registration) but neither FORWARD nor BODY, so
    # catalog_tools has no `uitk_element` attribute: its real wrapper -- and the docstring the MCP client reads --
    # is hand-written in tools/uitk.py, because its action argument needs a real client-side Literal/enum type
    # that the catalog's ARG grammar cannot express (see uitk.py's own comment on that function).
    "uitk_element": ("unity_mcp.tools.uitk", "uitk_element"),
}


def _client_visible_doc(tool: str) -> str:
    if tool in _DOC_SOURCE_OVERRIDE:
        module_name, func_name = _DOC_SOURCE_OVERRIDE[tool]
        return getattr(importlib.import_module(module_name), func_name).__doc__ or ""
    return getattr(catalog_tools, tool).__doc__ or ""


def test_doc_source_override_is_only_used_for_a_tool_the_catalog_cannot_generate_a_wrapper_for():
    """Synthetic self-test: the override must stay a last resort, not a way to dodge the real catalog wrapper."""
    for tool in _DOC_SOURCE_OVERRIDE:
        assert not hasattr(catalog_tools, tool), f"{tool} has a catalog_tools wrapper -- drop the override"


@pytest.mark.parametrize("tool", sorted(catalog_actions.TOOL_ACTIONS))
def test_declared_action_is_named_in_the_client_visible_description(tool):
    """Substitute for generating the action enumeration into the description: the catalog template cannot yet
    nest the IF/EACH needed to emit it (dsl-bottlenecks-2026-11, Core gap reported to the owner), so until that is
    fixed, this test guards the hand-written DOC prose instead -- delete it once the enumeration is generated.
    """
    description = _client_visible_doc(tool)
    missing = [action for action in catalog_actions.TOOL_ACTIONS[tool] if not _names_whole_word(description, action)]
    assert not missing, f"{tool}: description does not name action(s) {missing} as whole words -- {description!r}"
