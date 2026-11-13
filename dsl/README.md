# Tool catalog

`tools.biome` states what a tool is once: its name, category, effect, flags, arguments and, for a forwarder, its description.
`scripts/dsl.sh generate` prints it:

- in `server/src/unity_mcp/tools/generated/`, `catalog_specs.py` and `catalog_params.py`: the ToolSpec and the schema descriptions, which `tool_specs.py` and
  `_param_descriptions.py` merge;
- there too, `catalog_tools.py`: what the catalog expects of the Python side and, for a `FORWARD` or `BODY` tool, the MCP wrapper itself
  (`FORWARD` sends straight to `_send`; `BODY <module>` calls that module's hand-written `async def _<name>_body(...)`). A hand-written
  tool (a `PYTOOL`, or a `TOOL` without `FORWARD`/`BODY`) states only its expectation; its module's `register()` registers the function with
  `tools.register(mcp, fn)`, which checks it against the catalog (parameters, types, defaults, the first line of the description) and
  registers it with the annotation of its effect, where `mcp.tool(annotations=...)(fn)` stood. `catalog_tools.register()` runs last: it
  checks that every tool of the catalog has a function (and, for `BODY`, that the hand-written function's signature matches) and registers
  the generated wrappers. The runtime is `tools/_catalog_runtime.py`.
- the `catalog` region of `docs/tools/index.md`: a row per tool with its plane, category and effect.
- `catalog_reads.py` there and `unity-plugin/Editor/Generated/ActionReads.g.cs`: which actions of a tool only read (its `READ` lines), the table that
  the decision `isMutating` asks on both sides.
- `catalog_actions.py` there (`TOOL_ACTIONS`) and `unity-plugin/Editor/Generated/ToolActions.g.cs` (`ToolActions.Names(cmd)`, `IsKnown(cmd, action)`):
  the declared actions of each tool that dispatches on `action` (its `ACTION` lines; see [Actions](#actions)).
- `unity-plugin/Editor/Generated/ToolArgs.g.cs`: the typed argument accessors that a C# handler reads through (see [C# arguments](#c-arguments)).

This is step 7.1 of the biome-dsl repository (`Plans/STEP_7_1_TOOL_CONSTRUCT_DESIGN.md`). Every tool is a block of `tools.biome`: `TOOL` (Unity
handles it), `PYTOOL` (only the Python server implements it) or `INTERNAL` (a wire command that is no MCP tool: a ToolSpec and nothing else).
Every tool's ToolSpec comes from the catalog now (S0): `_SPECS` in `tool_specs.py` is an empty literal, filled solely by merging
`catalog_specs.py`; no module owns a ToolSpec by hand any more.

A tool lives in one place: a name that is in the catalog and also in the hand-written `_SPECS`, `PARAM_DESCRIPTIONS` or `mcp.tool`
registrations fails at import or in `test_single_owner.py`. A tool is `FORWARD` when its wrapper only forwards its arguments and the
catalog can say how each goes out (text, `RAW`, `OMIT`), or `BODY <module>` when it calls a hand-written `_<name>_body` in that module
instead: either way the wrapper is generated and the hand-written one (if any) deleted. The wrappers of the remaining, fully hand-written
tools stay in their modules.

| File | Role |
|---|---|
| `tools.biome` | the catalog, one block per tool, by name |
| `constructs/tool.construct` | the words of a block (`TOOL`, `PYTOOL`, `INTERNAL`, `FORWARD`, `FLAG`, `RUNTIME`, `TIMEOUT`, `MAXCHARS`, `DOC`, `READ`, `ARG` with `DEFAULT`, `DEFAULT_TEXT`, `OMIT`, `RAW`) and what is printed from them |
| `constructs/tool.examples` | examples of the construct: the lines it prints and the mistakes it refuses |
| `decisions/` | the rules both planes share: per module `<m>.logic`, `<m>.contract`, `<m>.examples` and the printed `<m>.json` rows (see Decisions) |
| `chat/` | how a turn of the chat window ends: `chatturn.feature` (the ordered list of what each of the eight exits does), `chatturn.logic` and `.contract` (its two guards), `chatturn.examples` (what must hold; see Chat turn) |
| `biome.project` | the compiler it needs (`TOOLCHAIN`) and the generate steps |
| `content-lock.json` | printed by `scripts/dsl_content_lock.py generate`, which `scripts/dsl.sh generate` runs last: the sha256 of every source and every generated file, which `test_dsl_content_lock.py` checks without Node (see Checks); never edited |

## Change a tool

1. Edit `tools.biome`.
2. `export BIOME_DSL_ROOT=<a clean biome-dsl checkout at the TOOLCHAIN commit>` (Node 22 or newer), then `scripts/dsl.sh generate`.
3. Commit the catalog, the generated files and `dsl/content-lock.json` together. Never edit a file in `generated/`.

If a changed `ARG` is read by a migrated C# handler, the handler's call to its accessor changes with it (see [C# arguments](#c-arguments)).

If the tool dispatches on `action`, an added or removed action changes its `ACTION` line, its dispatch arm and its `DOC` wording in the same
commit (see [Actions](#actions)).

## Add a tool

1. A `TOOL` (Unity-handled) or `PYTOOL` (Python-only) block in `tools.biome`, in alphabetical place: its category, effect, `ARG` lines
   and any `TAG`/`FLAG` it needs, and an `ACTION` line for each action if it dispatches on `action`. A wrapper that only forwards its arguments adds `FORWARD` — the catalog generates the whole wrapper,
   skip step 2. Anything with real logic adds `BODY <module>` (the Python module that implements it) instead.
2. For `BODY`: write the handler as `async def _<name>_body(...)` in `server/src/unity_mcp/tools/<module>.py`, matching the catalog's
   `ARG` lines in name and order.
3. `export BIOME_DSL_ROOT=<a clean biome-dsl checkout at the TOOLCHAIN commit>` (Node 22 or newer), then
   `scripts/dsl.sh generate && scripts/dsl.sh check`.
4. The new tool is not in `server/tests/golden/tool_surface.txt` yet: `python scripts/dsl_golden.py <name>` prints the lines to add there
   (adding a new tool's lines is not the "regenerate to remove a difference" the freeze note at the top of that file forbids).
5. For a `TOOL`: declare a `HANDLER` line (`register`, `register_async` or `register_action`, with its method). `generate` prints the
   `CommandRegistry` call into `unity-plugin/Editor/Generated/ToolBindings.g.cs`. A command without a `HANDLER` line is registered by hand in
   `unity-plugin/Editor/CommandRouter.Registration.cs`. A `PYTOOL` needs no C#.
6. Commit the catalog, the generated files, the golden addition and `dsl/content-lock.json` together; never edit a file in `generated/`.

The biome CLI's own `TOOLCHAIN_PIN_STALE` warning still appears on a direct `biome generate` when its checkout is at another commit than `TOOLCHAIN`; the generated bytes are compared anyway. `scripts/dsl.sh check` does not warn: it refuses a wrong pin first (see [Checks](#checks)).

## Actions

A tool that dispatches on its `action` argument declares each action once, as an `ACTION` line in its block:

```
  ACTION set-attr
  ACTION add-class
```

- `generate` prints the list to `catalog_actions.py` (`TOOL_ACTIONS`) and to `ToolActions.g.cs`: `Names(cmd)` returns the actions, `IsKnown(cmd, action)`
  tests one. A name is lowercase, may contain inner hyphens, and has no leading, trailing or doubled hyphen.
- The handler keeps no list of its own. An unknown action is reported with `ErrorHelper.InvalidAction(action, ToolActions.Names("<cmd>"))`, which gives
  `Unknown action '<x>'. Valid: a|b|c.`. A handler that returns the text, rather than throwing it, adds the `err:` prefix.
- Adding or removing an action is three edits in one commit:
  1. the `ACTION` line in `tools.biome`;
  2. the dispatch arm in C#;
  3. the wording in the description the client sees (the `DOC` lines, or the docstring of a hand-written wrapper): every declared action is named
     in it as a whole word.

  Step 3 is still hand prose because the `TEMPLATE` language does not nest the `IF` and `EACH` that a generated enumeration needs (a toolchain gap,
  reported to the biome-dsl owner). Until it is generated, the parity test below guards it.
- `server/tests/test_action_vocabulary_parity.py` (`csharp_parity`) checks, for each tool that declares actions: the declared set equals the C# dispatch
  arms; the invalid-action list is `ToolActions.Names`; every `ARG action` example and every `READ` name is declared; each action is named in the
  description. `unity-plugin/Tests~/Decisions/ToolActionsTests.cs` checks `Names` and `IsKnown`.
- Not declared: `bake` and `project_settings` (dispatch is genuinely scoped by a second parameter, `target`, first —
  `BakeHelper.cs`/`ProjectSettingsHelper.cs`), and the PYTOOLs `navmesh_query` and `watch` (`CONSTRUCT PYTOOL` has no
  `ACTION` item). `scene_environment` (`EnvironmentHelper.cs`) is a flat `action` dispatch, not scoped by a second
  parameter, and is declared like any other flat tool.

## C# arguments

`ToolArgs.g.cs` gives a C# handler one accessor per argument, so the handler names the argument once, in the catalog:

```csharp
string action = AnimationArgs.Action(args);   // ARG action str
float? time = AnimationArgs.Time(args);       // ARG time ofloat
```

- The class is `{Tool}Args`, the PascalCase of the tool name (`animation` gives `AnimationArgs`). The accessor is the PascalCase of the
  argument's wire key (`component_type` gives `ComponentType`). Casing makes every name a valid C# identifier; a collision fails the tests (see [Checks](#checks)).
- `LOCAL` (an argument only the Python side reads, such as the `timeout` of `batch`) has no accessor.
- `WIRE "key"` names the accessor after the key and reads that key.
- `DEFAULT` is never baked in. An absent key gives `null` for a string and `0` or `false` for a plain scalar; the `o`-types give `null`.
  The handler keeps its own fallback.

A migrated handler reads its arguments through these accessors. `animation` and `timeline` are migrated; the other handlers still call
`JsonHelper.Extract*` with the key. Migrate a handler only after a characterization fixture that passes on the old code (see
[`AI/testing.md`](../AI/testing.md)).

## Decisions

`dsl/decisions/` holds the rules that the Python server and the Unity plugin both need, as Logic DECISIONs (step 7.2 of the biome-dsl migration;
the design is `Plans/STEP_7_2_LOGIC_DECISIONS_DESIGN.md` of that repository). A module answers one question and is three files:
`<m>.logic` (the rules: the first match wins, the lowest `AT`), `<m>.contract` (the typed facts the rules read) and `<m>.examples` (what must hold,
transcribed from the tests the project already has, each row naming its `file:line`; a row that restates the code is not allowed, and the few
`characterized-*` rows state the order the code has today where no test pins it, marked as such and not endorsed). `scripts/dsl.sh generate` prints the Python (`server/src/unity_mcp/decisions/generated/<m>_logic.py`), the C# (`unity-plugin/Editor/Generated/<M>.g.cs`, C# 9, with its
`.meta`) and the machine rows of the examples (`dsl/decisions/<m>.json`, which pytest and dotnet run).

| Module | The question | Wired at |
|---|---|---|
| `mutability` | is this wire call a write? (`isMutating`) | Python `middleware_types.is_write`, C# `CommandRegistry.IsMutating`; Python is the authority (owner, 2026-10-29) |
| `verdict` | did the playtest pass? (`passes`, `outcome`, `fileState`, `suiteInner`, `suiteOuter`) | Python `tools/runtime._is_playtest_pass`, `_classify_outcome`, `suite_verdict`; C# `CommandRouter.IsPlaytestSuccess` |
| `guards` | which guard stops a call? (`guardVerdict`, sites `top`, `batch`, `py`) | C# `CommandRouter.CheckGuards`, `BatchHelper.Execute`; Python read-only, play-mode, direct_only and batch gates |
| `retry` | why does a failed send retry or stop? (`retryGate`, then `retryReason` when it says `observe`) | Python `bridge_retry.RetryPolicy.decide` (no C#) |

The decisions only decide. The facts are read by native code beside them: `decisions/verdict_facts.py` and `decisions/guard_facts.py` in Python,
`Editor/Decisions/{MutabilityFacts,PlaytestFacts,GuardFacts}.cs` in C#, `bridge_retry.py` for the retry; the texts of the answers and the effects stay with
the code that owns them. `unity_mcp.decisions` is a leaf (import-linter, `test_decisions_boundary.py`). The read actions of a tool are `READ` lines of
`tools.biome` (`ACTION_READS` and the C# `ActionReads` are printed from them).

### Change a rule

1. Edit the rule in `<m>.logic`, and the expectation in `<m>.examples` (from a test, with its `file:line`; a change of the requirement is its own commit with the
   reason, and the rows that said the opposite are replaced, never edited to fit the output).
2. `scripts/dsl.sh generate`. Commit the sources, the generated files, the rows and `dsl/content-lock.json` together; never edit `generated/` or `dsl/decisions/*.json`.
3. `scripts/dsl.sh check`, `pytest server/tests/test_decisions_examples.py test_dsl_fresh.py test_dsl_content_lock.py` and the golden of the family, and, for a C# change,
   `dotnet test unity-plugin/Tests~/Decisions/UnityMCP.Decisions.Tests.csproj -c Release`.

What checks what:
- The examples are the expectations: the Python rows run in `test_decisions_examples.py`, the C# rows in `Tests~/Decisions` (`RowsTests`).
- The goldens `server/tests/golden/decisions/*.txt` are what the hand-written code answered before its rule moved (`test_is_write_golden.py`,
  `test_verdict_golden.py`, `test_guards_golden.py`, `test_retry_golden.py`, `test_action_reads_golden.py`), taken once by `scripts/decisions_golden.py`
  and never regenerated to remove a difference: an approved difference is an `INTENDED` entry with its reason. `verdict_reports.txt` and
  `is_write_wire.txt` are their inputs for C#.
- `Tests~/Decisions` compiles the generated C# with the real `CommandRegistry.cs`, `JsonHelper.cs` and `Editor/Decisions/*.cs` (a stub file stands for Unity) at
  LangVersion 9, and checks them against the verbatim old code (`Oracle/`) or the golden: `MutabilityWireTests`, `GuardsOracleTests`, `PlaytestOracleTests`.
  `CommandRouter` and `BatchHelper` themselves need Unity: only the EditMode tests run them.
- `test_dsl_fresh.py` and `test_dsl_content_lock.py` (no Node) and `dsl.sh check` (Node) find a stale generated file; `test_decisions_boundary.py` and import-linter keep
  `unity_mcp.decisions` a leaf; `test_csharp_mutating_flags.py` and `test_catalog_vs_csharp.py` read the Register calls of the plugin.

## Chat turn

`dsl/chat/` states how a turn of the Unity chat window ends (step 7.3 of the biome-dsl migration; the design is `Plans/STEP_7_3_CHAT_EXIT_DESIGN.md` of that
repository). A turn ends in eight ways (an answer, an error event, a dead process, the inactivity timeout, Stop, a switch of backend or model, New Session, the
window closing). Before, each way cleaned up by hand in one of five partials of `MCPChatWindow`, in different orders; now the cleanup is **one ordered list**:

- `chatturn.feature` is a FEATURE (`USES elm`): `STATE phase` (idle, sending, receiving), `exit` (none and the eight reasons) and `asks` (0..8); 16 `PORT`s, which are the
  effects; one `ACTION` per intent (`send`, `firstToken`, `askRaised`, `askAnswered`, and one per exit); and 15 `ON phase|exit BECOMES ... -> CALL port` lines. **The order
  of those lines is the order of the effects**, the same for every exit: the resources (unlock the reload, cancel the asks of the turn, close the undo group), the transcript
  (sync, resolver, notes, finalize, error chip, usage, Restore, Approve), the backend, and the per-turn scratch last. An exit is a rising edge of `phase`, so a repeated or
  late one finds no turn open and does nothing. The comments of the file say why each line stands where it stands.
- `chatturn.logic` and `chatturn.contract` are the two guards of the FEATURE as DECISIONs (`canSend`: idle; `active`: not idle). A `WHEN` names a decision, so "is a turn open" is
  one.
- `chatturn.examples` is what must hold, 19 examples written from the hand-written exits before the generator ran: the calls of each exit in order, the inert repeat, the
  asks that no exit may leave, the renders. It is the file to read first, and the one whose change is a change of the requirement.

`scripts/dsl.sh generate` prints the reducer into `unity-plugin/Editor/Chat/CLI/Generated/ChatTurn/` (C# 9, flat, with the `.biome-files` manifest that owns the files, and a hand-written
`.meta` beside each) and the NUnit tests of the examples into `unity-plugin/Tests~/ChatTurn/Generated/`. The native side stays native: `Chat/CLI/ChatTurnDriver.cs` is the one
door (a queue, so an effect that dispatches waits for the exit that is running, and a redraw that throws skips no effect), `Chat/View/MCPChatWindow.TurnPorts.cs` holds the bodies of
the 16 ports, and the window only detects an exit (an event, the dead-process and inactivity checks, a click, a switch, `OnDisable`) and dispatches the intent.

### Change an exit

1. State the change in `chatturn.examples` (a change of what an exit does is a change of the requirement: its own commit, with the reason, and the rows that said the
   opposite are replaced, never fitted to the output), then in `chatturn.feature`: a new effect is a `PORT`, an `ON` line at the place it must run and a body in `TurnPorts.cs`;
   a new way out is an `ACTION` that ends in `phase = idle` with its `exit`, an example, and a `Dispatch` where the window detects it.
2. `scripts/dsl.sh generate`. Commit the sources, the generated files, `dsl/content-lock.json` and the `.meta` of a new file together; never edit `Generated/ChatTurn/`.
3. `scripts/dsl.sh check`, `pytest server/tests/test_dsl_fresh.py test_dsl_content_lock.py test_generated_cs_meta.py` and `dotnet test unity-plugin/Tests~/ChatTurn/ChatTurn.Tests.csproj -c Release`.
   The bodies of the ports and the wiring of the window need Unity: the EditMode tests `ChatTurnPortsTests` and `ChatTurnWiringTests`.

What checks what: the examples run as `ChatTurn.Examples.cs` (19 tests) beside `ChatTurnDriverTests` (7) in `Tests~/ChatTurn`, a pure dotnet project that compiles the
reducer and the driver at LangVersion 9 with warnings as errors, so a use of `UnityEngine` in either fails the build; the banner of every printed file states the digest of its source
(the `.feature`, or the `.logic` for `ChatTurn.Policy.cs`), which `test_dsl_fresh.py` (no Node) compares; a change of the examples only is found by `test_dsl_content_lock.py` (no Node) and `scripts/dsl.sh check`. Mutants of `chatturn.feature` (drop or move an `ON` line, drop a guard,
forget `exit = none`) were generated and run to show that the examples can fail: 16 of 17 are killed, the survivor is equivalent (`askAnswered` without its guard: the range saturates at 0).

## Playtest reader

The C# that reads a line of a playtest script by the grammar (`constructs/playtest.construct`, step 7.5) is printed by the compiler, not written here: `GENERATE playtest-reader reader --surface playtest --target cs`
writes `unity-plugin/Runtime/Playtest/Core/Generated/PlaytestReader.g.cs` (the table of forms, the tokens, the matcher, the diagnostic and the reader of a command; C# 9, namespace `UnityMCP.Playtest.Core`), and its `.meta` is hand-written beside it.
The runtime half of that file is `packages/languages/cs-print/reader/*.cs.tmpl` in biome-dsl: a defect of how a line is read is fixed there, then `generate --pin` to the new commit and `scripts/dsl.sh generate`, in one commit with the file it prints.
Never edit `PlaytestReader.g.cs`. The table of verbs of `AI/playtest-dsl.md` is the other step, `playtest-verbs` (`playtest/gen.mjs verbs`). The file is held by `scripts/dsl.sh check`, by `dotnet test unity-plugin/Tests~/PlaytestGrammar/UnityMCP.Playtest.Grammar.Tests.csproj -c Release`
(the table and the front of the reader, compiled alone at C# 9 with warnings as errors) and by `Tests~/Pure`, which compiles it with the rest of Core.

## Checks

- `scripts/dsl.sh check` first checks the pin, before any Node call. A wrong pin exits 1 with one diagnostic:
  - `TOOLCHAIN_PIN_MISSING`: `dsl/biome.project` does not have exactly one `TOOLCHAIN ` line.
  - `TOOLCHAIN_ROOT_NOT_GIT`: `BIOME_DSL_ROOT` is not a git checkout (a `git archive` copy or a download without `.git` fails here).
  - `TOOLCHAIN_PIN_MISMATCH`: the pin is not `git -C "$BIOME_DSL_ROOT" rev-parse HEAD`.

  Once the pin passes, it runs `generate --check` (a stale or hand-edited generated file), the examples of the constructs, the playtest reader and the content lock. Needs Node 22 or newer.
  `scripts/dsl.sh generate` runs no pin check, so a repin can be generated. Repin: check out biome-dsl at the new commit, set `TOOLCHAIN` in `dsl/biome.project` to its sha (`biome generate --pin`, as the mismatch message names it), run `scripts/dsl.sh generate`, and commit the pin, the generated files and `dsl/content-lock.json` together.
- `server/tests/test_dsl_toolchain_pin.py` and `scripts/tests/test_dsl_sh_toolchain_pin_guard.py` (no Node): the pin is exactly one 40-character lowercase hex sha; `TOOLCHAIN_PIN_MISSING`, `TOOLCHAIN_PIN_MISMATCH` and `TOOLCHAIN_ROOT_NOT_GIT` each fire on a copy of `dsl.sh` against a throwaway git checkout; a matching pin reaches the Node call.
- `server/tests/test_dsl_fresh.py`: without Node, each generated file states the sha256 of the current `tools.biome`. The docs region has
  no banner: `scripts/dsl.sh check` finds it stale and `test_dsl_content_lock.py` only a hand edit of it; `server/tests/test_catalog_docs.py` checks its rows against the ToolSpecs.
- `server/tests/test_dsl_content_lock.py`: without Node, `dsl/content-lock.json` states the sha256 of every source in `dsl/` (not the prose), of every file with the kit
  banner or in a `.biome-files` manifest, of the machine rows `dsl/decisions/*.json` and of the `catalog` region of the docs. A generated body edited under an intact
  banner, the rows, a `.contract`, `.examples` or construct edited without `scripts/dsl.sh generate`, a file deleted: stale, whatever the banner says. It also checks
  that the lock names every file that `biome.project` prints. It cannot see a lock that was refreshed by hand around a stale file
  (`python scripts/dsl_content_lock.py generate`, which `scripts/dsl.sh generate` runs): that the files are what the compiler prints is `scripts/dsl.sh check`.
  `python scripts/dsl_content_lock.py check` is the same check from the shell, with the standard library only.
- `server/tests/test_tool_surface_golden.py`: the MCP surface, the ToolSpec and the wire of every tool equal `golden/tool_surface.txt`, a
  frozen baseline taken at the close of Track #1 and never regenerated to remove a difference. A `wire` line is the digest of everything a
  wrapper that only forwards sends for a fixed set of calls (`scripts/dsl_golden.py`), so a generated wrapper sends what the hand-written
  one sent. A change of a tool on purpose is a dated `INTENDED` entry in that test, with its reason.
- `server/tests/test_action_vocabulary_parity.py`: the declared actions of a tool equal its C# dispatch and its error list (see [Actions](#actions)).
- `server/tests/test_single_owner.py`: no tool and no Unity command is registered twice.
- `server/tests/test_catalog_wire.py`: the command and arguments that the first generated wrappers send, as readable vectors.
- `server/tests/test_catalog_runtime.py`: the encoders and the `ToolSet` (a function that differs from its block is refused).
- `server/tests/test_catalog_vs_csharp.py`: while a Unity registration is still hand-written (not yet `ToolMeta.Get(cmd)`-delegated), its
  `maxResponseChars` says what the catalog's `MAXCHARS` says.
- `server/tests/test_toolmeta_vs_golden.py`: `ToolMeta.g.cs` (the generated switch a delegated `Register` call reads instead) states the
  same `mutating`/`runtime`/`required`/`optional`/guard flags as `golden/registration.txt`, a frozen census of the hand-written C# taken
  before `ToolMeta.g.cs` existed. `KNOWN_DIVERGENT` and `KNOWN_UNCOVERED` are that census's measured, shrink-only exceptions.
- `server/tests/test_tool_args_class_name_boundary.py` (no Node, no Unity): no class of `ToolArgs.g.cs` has the name of a type that
  `unity-plugin/Editor` declares by hand.
- `dotnet test unity-plugin/Tests~/Decisions/UnityMCP.Decisions.Tests.csproj -c Release` (no Unity) compiles `Editor/Generated/*.g.cs`
  except `ToolBindings.g.cs` (it needs UnityEditor), plus `Editor/Wire.cs`. `ToolArgsAccessorTests` checks each accessor against the
  extraction it replaced and the accessor set against `dsl/tools.biome`; `ToolArgsPascalCaseTests` checks the names for collisions.
- The EditMode fixtures `AnimationHandlerArgsCharacterizationTests` and `TimelineHandlerArgsCharacterizationTests` (`unity-plugin/Editor/Tests/`)
  dispatch the migrated commands through `CommandRouter`: they need Unity.
