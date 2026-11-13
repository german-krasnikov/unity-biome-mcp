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

from unity_mcp.tools.generated import catalog_actions, catalog_examples, catalog_tools

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
    # dsl-bottlenecks-2026-11 Part B3: ExecUitkFile (CommandRouter.MediaHandlers.cs:291-315) dispatches 5
    # underscore-style actions itself via an if-chain, then forwards every other action string to
    # UIFileHelper.EditUIFile, whose switch (UIFileHelper.cs:102-121) dispatches the 7 hyphenated surgical
    # actions ("set-attr", "add-class", ...) -- now expressible since CONSTRUCT TOOL's `ITEM ACTION` pattern
    # was widened to admit inner hyphens.
    "uitk_file": (
        ("CommandRouter.MediaHandlers.cs", "ExecUitkFile"),
        ("UIFileHelper.cs", "EditUIFile"),
    ),
    # dsl-bottlenecks-2026-11 Part 2: the remaining flat (non-scoped-by-a-second-parameter) action-dispatched
    # TOOLs, each mapped to its one real C# dispatch site.
    "animator": (("CommandRouter.MediaHandlers.cs", "ExecAnimatorConsolidated"),),
    # BuildHelper.Execute (BuildHelper.cs:15-23) only ever accepts the single action "build": `if (action !=
    # "build")` is a negated single-literal check, not a switch/case/`action == "..."` arm -- the first tool to
    # need that shape (see the scanner self-test below).
    "build": (("BuildHelper.cs", "Execute"),),
    # ExecManageComponent (CommandRouter.ObjectHandlers.cs:304-320) does not itself validate `action` (its
    # ternary only picks response text); the real dispatch -- and its only error path -- is
    # ObjectManager.ManageComponent's if/else-if chain (ObjectManager.cs:211-243).
    "manage_component": (("ObjectManager.cs", "ManageComponent"),),
    "material": (("MaterialHelper.cs", "Execute"),),
    "material_audit": (("MaterialAuditHelper.cs", "Execute"),),
    "menu": (("CommandRouter.MediaHandlers.cs", "ExecMenu"),),
    "package": (("PackageManagerHelper.cs", "Execute"),),
    "particle": (("CommandRouter.MediaHandlers.cs", "ExecParticleConsolidated"),),
    "profile": (("Profiling/ProfileRecorder.cs", "Dispatch"),),
    "references": (("CommandRouter.MediaHandlers.cs", "ExecReferencesConsolidated"),),
    # dsl-bottlenecks-2026-11 Part 2 (leftover): EnvironmentHelper.Execute is a flat `action switch
    # { "get" => ..., "set" => ..., _ => throw }` -- not scoped by a second parameter (07a688df's
    # commit message claimed otherwise; bake and project_settings genuinely are, scene_environment
    # was not).
    "scene_environment": (("EnvironmentHelper.cs", "Execute"),),
    "spatial_query": (("SpatialHelper.cs", "Execute"),),
    "transfer_object": (("ObjectManager.Transfer.cs", "TransferObject"),),
}

# [a-z][a-z0-9_-]* (not [a-z_]+): dsl-bottlenecks-2026-11 Part B3 widened CONSTRUCT TOOL's own ACTION name
# pattern to admit inner hyphens (uitk_file's set-attr, add-class, ...) -- these scanners read the same C#
# dispatch text that pattern now describes, so they must recognize the same shape.
_PATTERN_BEFORE_ARROW = re.compile(r'((?:"[a-z][a-z0-9_-]*"\s*(?:or\s*)?)+)=>')
_QUOTED = re.compile(r'"([a-z][a-z0-9_-]*)"')
_INVALID_ACTION_LIST = re.compile(r'ErrorHelper\.InvalidAction\(\s*action,\s*new\[\]\s*\{([^}]*)\}\)')
# A hint array passed by name instead of inline (AssetDatabaseHelper.cs, ScriptableObjectHelper.cs): resolved by
# finding the field's own declaration in the full file text.
_NAMED_INVALID_ACTION_LIST = re.compile(r'ErrorHelper\.InvalidAction\(\s*action,\s*(\w+)\)')
# A plain `if (action == "...")` dispatch arm -- the shape a switch-EXPRESSION scanner cannot see (`editor`,
# `uitk_element`); and a classic `case "...":` switch-STATEMENT label -- the shape the arrow-only scanner above
# cannot see (AssetDatabaseHelper.cs, ScriptableObjectHelper.cs, EditorStateHelper.cs all use `switch { case: }`,
# not `action switch { => }`).
_IF_EQUALITY_ARM = re.compile(r'\baction\s*==\s*"([a-z][a-z0-9_-]*)"')
# dsl-bottlenecks-2026-11 Part 2: `build` (BuildHelper.cs:19) is the first tool whose entire action vocabulary
# is a single negated check -- `if (action != "build")` bails out instead of accepting -- rather than a
# positive `action == "..."` arm, a switch arm, or a `case` label.
_IF_NOT_EQUALITY_ARM = re.compile(r'\baction\s*!=\s*"([a-z][a-z0-9_-]*)"')
_CASE_LABEL = re.compile(r'case\s+"([a-z][a-z0-9_-]*)"\s*:')
# dsl-bottlenecks-2026-11 Part A: a handler that passes the generated table straight to
# ErrorHelper.InvalidAction instead of a hand valid-list -- the C# counterpart of this test's own
# "replace the hand copy" slice.
_TOOL_ACTIONS_NAMES_CALL = re.compile(r'ErrorHelper\.InvalidAction\(\s*action,\s*ToolActions\.Names\("([a-z_]+)"\)\)')


def _strip_comments(text: str) -> str:
    """`text` with every `//...` line comment and `/* ... */` block comment blanked out (same length, so brace
    offsets are unaffected), tracking `"..."` string-literal state so a comment-start sequence that is only a
    substring of a string literal's own content (a URL in a help string, say) is left alone -- dsl-bottlenecks-
    2026-11, review minor 1: dispatch_arms() must not read a commented-out case/switch arm as real, and must
    not be fooled the other way either (a string literal that merely contains comment-like or case-like text)."""
    out, i, n, in_string = [], 0, len(text), False
    while i < n:
        ch = text[i]
        if in_string:
            if ch == "\\" and i + 1 < n:
                out.append(text[i:i + 2])
                i += 2
                continue
            if ch == '"':
                in_string = False
            out.append(ch)
            i += 1
            continue
        if ch == '"':
            in_string = True
            out.append(ch)
            i += 1
            continue
        if text[i:i + 2] == "//":
            j = text.find("\n", i)
            j = n if j == -1 else j
            out.append(" " * (j - i))
            i = j
            continue
        if text[i:i + 2] == "/*":
            j = text.find("*/", i + 2)
            j = n if j == -1 else j + 2
            out.append("".join(c if c == "\n" else " " for c in text[i:j]))
            i = j
            continue
        out.append(ch)
        i += 1
    return "".join(out)


def _function_body(text: str, func_name: str) -> str:
    """The brace-balanced body of `func_name`, found from its signature's opening brace, with comments
    blanked out (see `_strip_comments`) before any dispatch/valid-list scanner reads it."""
    start = text.index(f"{func_name}(")
    open_brace = text.index("{", start)
    depth, i = 0, open_brace
    while True:
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
            if depth == 0:
                return _strip_comments(text[open_brace:i + 1])
        i += 1


def dispatch_arms(body: str) -> set[str]:
    """The action strings a dispatch body really accepts: every pattern-side string of an `action switch { ... }`
    expression arm or a classic `switch (action) { case "...": ... }` statement label, plus every plain
    `action == "..."` if-chain comparison."""
    switch_arms = {name for match in _PATTERN_BEFORE_ARROW.finditer(body) for name in _QUOTED.findall(match.group(1))}
    case_labels = set(_CASE_LABEL.findall(body))
    return (switch_arms | case_labels | set(_IF_EQUALITY_ARM.findall(body))
            | set(_IF_NOT_EQUALITY_ARM.findall(body)))


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


def passes_tool_actions_names(body: str, tool: str) -> bool:
    """Whether `body` calls `ErrorHelper.InvalidAction(action, ToolActions.Names("<tool>"))` --
    the generated-list replacement for a hand valid-list (dsl-bottlenecks-2026-11 Part A)."""
    return tool in _TOOL_ACTIONS_NAMES_CALL.findall(body)


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
# dsl-bottlenecks-2026-11 Part B2 fixed both entries this dict ever held: RenderAnalyzer.cs and
# UIHelper.UIToolkit.cs now call ErrorHelper.InvalidAction(action, ToolActions.Names(cmd)).
#
# dsl-bottlenecks-2026-11 Part B (slice R2) fixed the 6 entries added by Part 2 (2026-11-13): build,
# manage_component, material_audit, package, profile, and transfer_object all now call
# ErrorHelper.InvalidAction(action, ToolActions.Names(cmd)) too -- empty until the next one.
_NO_INVALID_ACTION_LIST_ARTIFACT: dict[str, str] = {}

# A real, dated disagreement between the C# switch and its own hand hint array -- recorded here (not fixed in C#)
# so the test documents the bug and goes red (XPASS) if only one side is ever fixed without updating this entry.
# dsl-bottlenecks-2026-11 Part B1 fixed the one editor entry this dict ever held (EditorStateHelper.cs's hand
# array now reads ToolActions.Names("editor"), which includes "mutation_mode") -- empty until the next one.
_KNOWN_INVALID_ACTION_LIST_DISAGREEMENT: dict[str, str] = {}


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


# Tools whose hand valid-list (an inline `new[] {...}` literal, a named `static readonly
# string[] ValidActions` field, or -- render_analyze/uitk_element -- a bare interpolated string
# with no array at all) was replaced by `ToolActions.Names(cmd)` in dsl-bottlenecks-2026-11
# Part A/B1/B2: one fact in one place, generated from the catalog's own ACTION declaration -- there
# is no hand list left to re-parse and compare, so the test below asserts the call exists instead.
#
# dsl-bottlenecks-2026-11 Part A (slice R2) added the six below: their hand `new[] {...}` literals
# in CommandRouter.MediaHandlers.cs (animator, menu, particle, references), MaterialHelper.cs
# (material), and SpatialHelper.cs (spatial_query) were byte-identical in content and order to the
# catalog's own ACTION declaration -- pure replacement, no observable error-text change.
#
# dsl-bottlenecks-2026-11 Part B (slice R2) added the 6 below: each previously threw/returned free
# text with no catalog list at all (see the now-empty _NO_INVALID_ACTION_LIST_ARTIFACT above) --
# an intended error-text change, not a pure replacement. Each kept its own delivery mechanism
# (BuildHelper.cs/PackageManagerHelper.cs: TaskCompletionSource, "err:" prefix; MaterialAuditHelper.cs:
# returned string, "err:" prefix; ObjectManager.cs/ObjectManager.Transfer.cs/Profiling/ProfileRecorder.cs:
# thrown ArgumentException, no prefix).
_USES_GENERATED_VALID_LIST = {
    "animation", "animator", "asset", "build", "editor", "manage_component", "material",
    "material_audit", "menu", "package", "particle", "prefab", "profile", "references",
    "render_analyze", "scene", "scene_environment", "scriptable_object", "shader", "spatial_query",
    "timeline", "transfer_object", "uitk_element", "uitk_file",
}


@pytest.mark.parametrize("tool", _cases(skip=_NO_INVALID_ACTION_LIST_ARTIFACT,
                                         xfail=_KNOWN_INVALID_ACTION_LIST_DISAGREEMENT))
def test_catalog_actions_match_the_csharp_invalid_action_list(tool):
    if tool in _USES_GENERATED_VALID_LIST:
        assert any(passes_tool_actions_names(body, tool) for _, body in _real_sources(tool)), (
            f"{tool}: expected ErrorHelper.InvalidAction(action, ToolActions.Names(\"{tool}\")) "
            "-- a hand valid-list must not be reintroduced"
        )
        return
    assert set(catalog_actions.TOOL_ACTIONS[tool]) == _real_invalid_action_list(tool)


@pytest.mark.parametrize("tool", sorted(catalog_actions.TOOL_ACTIONS))
def test_arg_example_of_an_action_tool_is_a_declared_action(tool):
    example = catalog_examples.ARG_EXAMPLES.get(tool, {}).get("action")
    if example is not None:
        assert example in catalog_actions.TOOL_ACTIONS[tool]


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
    """A catalog action not dispatched by the real C# (here: animation's own 15 real actions, written out
    literally, plus a bogus one) must make the comparison fail. The expected side is a literal set, not read
    from catalog_actions.TOOL_ACTIONS: an expectation must not be derived from the very table the test is
    probing (dsl-bottlenecks-2026-11, owner review) -- otherwise a bug that corrupts the generated table the
    same way it corrupts the comparison would go unnoticed."""
    declared_plus_bogus = {
        "get", "create", "edit", "add_key", "remove_key", "remove_curve", "set_keys", "set_loop",
        "set_wrap", "set_framerate", "preview", "get_clip_path", "get_events", "add_event", "remove_event",
        "bogus_action",
    }
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


_SCRATCH_NEGATED_SINGLE_ACTION = '''
        private static void ExecScratchSingleAction(string action)
        {
            if (action != "build")
            {
                return;
            }
        }
'''


def test_scanner_catches_a_negated_single_action_if_arm():
    """`build` (BuildHelper.Execute, BuildHelper.cs:19) accepts exactly one action via a negated check --
    `if (action != "build")` bails out -- not a switch arm, a `case` label, or a positive `action == "..."`
    comparison. dsl-bottlenecks-2026-11 Part 2: the first tool needing this shape."""
    body = _function_body(_SCRATCH_NEGATED_SINGLE_ACTION, "ExecScratchSingleAction")
    assert dispatch_arms(body) == {"build"}


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
    """`asset`/`scriptable_object` used to pass a named `static readonly string[] ValidActions`
    field to ErrorHelper.InvalidAction instead of an inline `new[] {...}` literal, until
    dsl-bottlenecks-2026-11 Part A replaced both with `ToolActions.Names(cmd)`. No real site
    exercises this shape any more, but the scanner must still resolve it (not just the inline
    shape) if a future tool reintroduces a named hand list -- an inline-array-only scanner would
    see an empty set here."""
    body = _function_body(_SCRATCH_NAMED_ARRAY, "Execute")
    assert invalid_action_list(body) == set()  # no inline array, and no full_text to resolve the name from
    assert invalid_action_list(body, _SCRATCH_NAMED_ARRAY) == {"find", "create"}


_SCRATCH_HYPHEN_SWITCH = '''
        private static string ExecScratchHyphen(string args)
        {
            var action = ScratchArgs.Action(args);
            return action switch
            {
                "get" => ExecGet(args),
                "set-attr" => ExecSetAttr(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get", "set-attr" }))
            };
        }
'''


def test_scanner_recognizes_a_hyphenated_action_name():
    """uitk_file's surgical actions (set-attr, add-class, ...) are the first hyphenated action names the catalog
    declares (dsl-bottlenecks-2026-11 Part B3) -- a scanner whose character class was [a-z_]+ only (no hyphen)
    would see 0 arms and an empty invalid-action list here."""
    body = _function_body(_SCRATCH_HYPHEN_SWITCH, "ExecScratchHyphen")
    assert dispatch_arms(body) == {"get", "set-attr"}
    assert invalid_action_list(body) == {"get", "set-attr"}


_SCRATCH_LINE_COMMENTED_CASE = '''
        private static string ExecScratchCommented(string args)
        {
            var action = ScratchArgs.Action(args);
            return action switch
            {
                "get" => ExecGet(args),
                // "ghost_line" => ExecGhost(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get" }))
            };
        }
'''


def test_scanner_ignores_a_line_commented_out_case():
    """A `//`-commented-out switch arm must not be read as a real dispatch arm or a real valid-list entry."""
    body = _function_body(_SCRATCH_LINE_COMMENTED_CASE, "ExecScratchCommented")
    assert dispatch_arms(body) == {"get"}
    assert invalid_action_list(body) == {"get"}


_SCRATCH_BLOCK_COMMENTED_CASE = '''
        private static string ExecScratchBlockComment(string args)
        {
            var action = ScratchArgs.Action(args);
            return action switch
            {
                "get" => ExecGet(args),
                /* "ghost_block" => ExecGhost(args), */
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get" }))
            };
        }
'''


def test_scanner_ignores_a_block_commented_out_case():
    """A `/* ... */`-commented-out switch arm must not be read as a real dispatch arm or a real valid-list entry."""
    body = _function_body(_SCRATCH_BLOCK_COMMENTED_CASE, "ExecScratchBlockComment")
    assert dispatch_arms(body) == {"get"}
    assert invalid_action_list(body) == {"get"}


_SCRATCH_STRING_LITERAL_TRAP = '''
        private static string ExecScratchStringTrap(string args)
        {
            var action = ScratchArgs.Action(args);
            var help = "see http://example.com/docs for the case \\"ghost_string\\": syntax, not a real action";
            return action switch
            {
                "get" => ExecGet(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "get" }))
            };
        }
'''


def test_scanner_is_not_fooled_by_a_string_literal_that_merely_contains_case_like_text():
    """A string literal's own content (here: help text that happens to contain `//`, and a quoted phrase that
    reads like a case label) must not be read as a comment-start or a real dispatch arm."""
    body = _function_body(_SCRATCH_STRING_LITERAL_TRAP, "ExecScratchStringTrap")
    assert dispatch_arms(body) == {"get"}
    assert "ghost_string" not in dispatch_arms(body)
    assert invalid_action_list(body) == {"get"}


_SCRATCH_GENERATED_CALL = '''
        private static string ExecScratchGenerated(string args)
        {
            var action = ScratchArgs.Action(args);
            return action switch
            {
                "get" => ExecGet(args),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    ToolActions.Names("scratch")))
            };
        }
'''


def test_scanner_detects_a_handler_that_passes_the_generated_table_to_invalid_action():
    """animation/scene/timeline/shader/asset/scriptable_object/prefab pass
    `ToolActions.Names("<cmd>")` straight to ErrorHelper.InvalidAction (dsl-bottlenecks-2026-11
    Part A) instead of a hand list -- `passes_tool_actions_names` must recognize that call, and
    must not be satisfied by an unrelated hand list or the wrong tool name."""
    generated_body = _function_body(_SCRATCH_GENERATED_CALL, "ExecScratchGenerated")
    assert passes_tool_actions_names(generated_body, "scratch")
    assert not passes_tool_actions_names(generated_body, "not_scratch")
    hand_list_body = _function_body(_SCRATCH_SWITCH, "ExecScratchConsolidated")
    assert not passes_tool_actions_names(hand_list_body, "get")


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
    # TOOL uitk_file has neither FORWARD nor BODY either (FLAG direct_only + unity_transport, hand-registered
    # in CommandRouter.Registration.cs): catalog_tools has no `uitk_file` attribute, so its docstring is
    # hand-written in tools/uitk.py too.
    "uitk_file": ("unity_mcp.tools.uitk", "uitk_file"),
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
