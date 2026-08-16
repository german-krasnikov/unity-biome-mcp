# Playtest Composer

Playtest Composer is a visual editor for the Playtest DSL. Use it to assemble,
validate, save, and run common runtime checks without writing every DSL line by
hand.

## Open the Composer

- Choose **🧬MCP > Playtest Composer** or press **Shift+Alt+P**.
- Or select **Composer** in the MCP Chat toolbar.

The current step list, timeout, abort setting, and last file path persist in
`Library/PlaytestComposerState.json`. The `Library` copy is local editor state;
save reusable scenarios as `.playtest` files under version control.

## Build and run a playtest

Use **+** to add a step, the left handle to reorder it, and the row menu to
duplicate or delete it. The editor exposes the fields relevant to the selected
step type. A red row has a validation error; hover it for the reason.

The visual editor supports the most common steps: movement, waits, assertions,
logging and sections, method invocation, runtime field changes, clicks, captures,
invariants, and proximity checks. The [Playtest Guide](playtest.md) remains the
canonical reference for the complete DSL.

Toolbar controls:

| Control | Result |
|---|---|
| **Run** | Runs the validated list against the current Play Mode scene |
| **Save / Load** | Writes or reads a `.playtest` file; the default folder is `Playtests/` |
| **Copy DSL** | Copies the raw script |
| **Copy for AI** | Copies a fenced `playtest` block with timeout and abort metadata |
| **Smart Command** | Converts a description into candidate steps |
| **TO** | Sets the run timeout in seconds |
| **Abort** | Prepends `ABORT_ON_FAIL` and enables fail-fast execution |

**Run does not enter Play Mode.** Enter Play Mode first, wait for the scene to be
ready, then run the script. The button remains disabled for an empty or invalid
step list.

## Use hierarchy drag and drop

Drop a GameObject onto the list to create a movement, assertion, wait, invocation,
monitor, runtime set, click, capture, or proximity step. Actions that need a
component open a component/field picker.

You can also drop a GameObject or Component onto a row:

- A path field can take the object path, its current position, or both.
- A query field opens a serialized field picker and fills the query.
- Dropping a Component skips the component-selection step.
- The eyedropper on Move and Teleport uses the current Scene selection.

Review generated paths after renaming or reparenting objects. Saved DSL contains
paths, not durable links to scene objects.

## Use Smart Command

Smart Command accepts English or Russian descriptions. Dragging a GameObject into
the input can insert its `[/path]`, coordinates, or a complete Move/Teleport line.

1. Enter one or more desired steps.
2. Leave **Use AI** enabled to use the configured sampling backend. If generation
   is unavailable or fails, the Composer falls back to its heuristic parser.
3. Disable **Use AI** for heuristic-only parsing.
4. Select **Parse** and review every candidate line.
5. Select **OK** to append parseable lines. Invalid syntax is skipped;
   `LOG # UNPARSED` becomes a visible Log step, so replace or delete it before
   treating the script as runnable evidence.

Smart Command is a drafting aid. Confirm object paths, component fields, expected
values, and timeouts before running the result.

## Examples

### Respawn check

```text
SECTION "Kill"
TELEPORT /Player 0,-100,0
WAIT 1
SECTION "Respawn"
WAIT_UNTIL /Player|HealthComponent|currentHealth > 0 TIMEOUT 10
ASSERT /Player|HealthComponent|currentHealth == 100
ASSERT_CONSOLE_CLEAN
```

### UI transition

```text
SECTION "Main Menu"
ASSERT_CTA VISIBLE
CLICK /UI/Canvas/StartButton WAIT 0.5
SECTION "Gameplay"
WAIT_UNTIL /GameManager|GameManager|IsPlaying == true TIMEOUT 5
ASSERT /GameManager|GameManager|IsPlaying == true
ASSERT_CONSOLE_CLEAN
```

### Damage check with captured state

```text
CAPTURE hp_before /Enemy|HealthComponent|currentHealth
INVOKE /Enemy HealthComponent TakeDamage 25
WAIT 0.2
ASSERT_CAPTURED hp_before DECREASED
ASSERT /Enemy|HealthComponent|currentHealth > 0
ASSERT_CONSOLE_CLEAN
```

These component and field names are examples; use the names in your project.

## Save maintainable scenarios

- Loading is semantic, not a source-format round trip. The parser expands
  macros and static directives before the visual list is built. A recognized
  step that Composer cannot edit remains visible and keeps its source line;
  when that line used `VAL` or `PATH_PREFIX`, saving writes the resolved,
  self-contained command so it does not contain an unresolved sigil. Directive
  formatting and comments are not preserved. Use a text editor when their
  original structure matters.
- Keep one behavioral purpose per `.playtest` file.
- Add `SECTION` and `DESC` labels where they improve failure reports.
- Prefer observable conditions over long fixed waits.
- End acceptance scenarios with relevant state assertions and
  `ASSERT_CONSOLE_CLEAN`.
- Create **Assets > Create > 🧬MCP > Playtest Config** when the project needs a
  shared player path, movement method, or query aliases.

See [Wait Conditions](wait-conditions.md) for polling semantics and
[Playtest Guide](playtest.md) for aliases, suites, hooks, and the full language.
