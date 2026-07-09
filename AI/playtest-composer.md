# Playtest Composer

Visual editor for building and running Playtest DSL scripts step-by-step, without writing raw DSL.

## Opening

- **Menu:** `MCP / Playtest Composer` — shortcut `Shift+Alt+P`
- **Chat toolbar:** "Composer" button (visible when `UNITY_MCP_CHAT` define is active)

State persists across Unity sessions in `<ProjectRoot>/Library/PlaytestComposerState.json`.

---

## Toolbar

| Control | Action |
|---------|--------|
| **▶ Run** | Run step list in Play Mode; disabled when any step has a validation error |
| **Save** | SaveFilePanel → `.playtest` file; default folder `<ProjectRoot>/Playtests/` (auto-created) |
| **Load** | OpenFilePanel → parses DSL via PlaytestParser → repopulates step list (macros/aliases are expanded and flattened) |
| **Copy DSL** | Copies raw DSL text to clipboard |
| **Copy for AI** | Copies a fenced ` ```playtest timeout=N abort_on_fail=true/false ``` ` block — paste directly into Claude chat |
| **＋ Smart Command** | Opens the NL→DSL entry window (see below) |
| **TO: [float]** | Global timeout in seconds (default 60s); passed as `globalTimeout` to `PlaytestRunner.Run` |
| **Abort [toggle]** | Prepends `ABORT_ON_FAIL` to the generated DSL when checked |

---

## Step List

- **Reorder:** animated drag handle (UI Toolkit ListView)
- **Add:** footer `+` button — new step defaults to `WAIT 1s`
- **Remove:** footer `−` button, or right-click → Delete
- **Duplicate:** right-click → Duplicate (inserted immediately after original)
- **Validation:** invalid steps get a red border; hover to see the error tooltip
- DSL preview auto-refreshes every 400 ms

---

## Step Row Fields

Each row has a **Type** dropdown (all `StepType` values) and a **Description** field (becomes a `DESC` line in DSL). The detail panel below the header switches based on type:

| Type(s) | Editable fields |
|---------|----------------|
| Move, Teleport | Path text, ⊙ eyedropper, Position (Vector3) |
| Wait | Delay in seconds |
| TimeScale | Scale multiplier (label shows `×` instead of `sec`) |
| WaitUntil | Query, Op (`==` `!=` `>` `>=` `<` `<=`), Value, Timeout, Abort toggle |
| Assert, Invariant | Query, Op, Value |
| Section, Log | Message text |
| Invoke, Set | Path, Component, Method (`Invoke`) / Field (`Set`), Args |
| Monitor | Query |
| Click | Path, Wait-delay (seconds after click) |
| Capture | Label, Query |
| AssertCaptured | Label, Mode (`DELTA` / `RATIO` / `INCREASED` / `DECREASED` / `UNCHANGED` / `CHANGED`) |
| AssertNear | Path A, Path B, Distance threshold |
| AssertConsoleClean | Ignore pattern (comma-separated substrings) |

**Shared interactions on Path and Query text fields:**
- **Double-click** — pings the GameObject in Hierarchy (`EditorGUIUtility.PingObject`)
- **Drag & Drop** — see section below

---

## Drag & Drop

### Multi-drop onto the step list

Drag one or more GameObjects from the Hierarchy directly onto the list area. For each object an action menu appears:

| Menu item | Step created |
|-----------|-------------|
| Move `'Name'` | Move step with path + current position |
| Teleport `'Name'` | Teleport step with path + current position |
| Assert field on `'Name'` | Assert step; opens component → field picker to fill query |
| WaitUntil field on `'Name'` | WaitUntil step (timeout 10s); opens component → field picker |
| Invoke method on `'Name'` | Invoke step; opens component → method picker |
| Monitor field on `'Name'` | Monitor step; opens component → field picker |
| Set field on `'Name'` | Set step; opens component → field picker (pre-fills current value) |
| Click `'Name'` | Click step with path |
| Capture field on `'Name'` | Capture step; opens component → field picker |
| AssertNear `'Name'` | AssertNear step with path |

### Drop onto a Path field

Shows a contextual menu:

| Option | Effect |
|--------|--------|
| Set path + position (x,y,z) | Fills path AND position from the dropped object |
| Set path only | Fills path only |
| Set position only (x,y,z) | Fills position only |

Drop a **Component** directly → skips the "Set path" menu and opens the field/method picker immediately.

### Drop onto a Query field

- Drop a **Component** → opens field picker (Fields / Properties / Methods grouped; zero-arg methods shown)
- Drop a **GameObject** → opens component picker → then field picker
- Auto-fills both the query string and the value with the component's current field value (live pre-fill)

### Eyedropper ⊙

Fills path + position from the active Scene Selection (`Selection.activeGameObject`).

---

## Field Picker

Shown when a drop onto a query/invoke/set field reaches the member level.

- **Fields/** — public fields + `[SerializeField]` private fields (excludes Unity base types)
- **Properties/** — public readable instance properties (excludes index properties and base types)
- **Methods/** — public zero-argument instance methods (excludes special names and base types)

For **Assert / WaitUntil / Monitor / Capture**: sets `query = path|Component|member` and pre-fills `value` with the current runtime value.  
For **Invoke**: sets `path`, `component`, `method` (strips `()` suffix).  
For **Set**: sets `path`, `component`, `method`, and pre-fills `args` with the current value.

---

## Smart Command (＋ Smart Command)

Modal 400×360 window for entering steps in natural language.

1. Type a description in Russian or English (multi-line).
2. Drop GameObjects into the text field:
   - `Insert reference [/path]`
   - `Insert coordinates (x,y,z)`
   - `MOVE path TO coords` / `TELEPORT path coords` — inserts raw DSL line
3. Click **Parse ▶**:
   - **Use AI on** → calls `NlComposerBridge.ParseAsync` (LLM); falls back to heuristic on error
   - **Use AI off** → `NlStepParser.ConvertToDsl` heuristic only
4. Preview pane shows each output line:
   - Green = valid parseable DSL
   - Red = `LOG # UNPARSED: <original text>` — LLM could not convert this fragment
5. **OK** → appends all valid parsed steps to the Composer list (invalid lines silently skipped).

---

## File Format

- Extension: `.playtest` (plain UTF-8 text, raw DSL)
- Default folder: `<ProjectRoot>/Playtests/` (auto-created on first Save)
- Last used path is remembered per session
- **Load round-trip note:** MACRO/ALIAS/MOVE_PATH directives are expanded by the parser; the loaded step list shows the flattened result, not the original macro structure

---

## PlaytestConfig (ScriptableObject)

Optional per-project configuration. Create via `Assets > Create > MCP > Playtest Config`.

| Field | Purpose |
|-------|---------|
| `characterPath` | Default player path for `MOVE` auto-detect (fallback searches for "Player", "GridPlayer", "Character", "Hero") |
| `moveComponent` | Component type with movement API |
| `moveMethod` | Method name to call for movement |
| `isMovingField` | Field to poll for "still moving" (default `IsMoving`) |
| `arrivalOp` / `arrivalValue` | Arrival detection condition (default `== False`) |
| `timeScaleClass` / `timeScaleProperty` | Static property to set instead of `Time.timeScale` |
| `ctaPath` | GameObject path for `ASSERT_CTA` |
| `aliases` | List of `QueryAlias` — maps short alias strings to full `path|component|field` tuples |

---

## Validation Rules (per step)

| Type | Error condition |
|------|----------------|
| Move, Teleport | path empty AND position == Vector3.zero |
| Wait | delay ≤ 0 |
| TimeScale | delay < 0 |
| WaitUntil | query empty, or timeout ≤ 0 |
| Assert | query empty |
| Invoke | path / component / method empty |
| Set | path / component / field empty |
| Click | path empty |
| Capture | label or query empty |
| Invariant | query empty |
| AssertCaptured | label empty |
| AssertNear | path A or path B empty |

Run button is disabled while any step fails validation.

---

## Alias Manager (PlaytestAliasWindow)

Visual manager for `PlaytestConfig.aliases` — maps short `$name` sigils to `path|component|field` tuples that expand in DSL scripts via `VAL`.

### Opening

- **Menu:** `MCP / Alias Manager` — shortcut `Shift+Alt+A`
- **Chat toolbar:** "Aliases" button (order 21; visible when `UNITY_MCP_CHAT` define is active; `MenuOnly = true` so the button opens the window rather than inline)

### Toolbar

| Button | Action |
|--------|--------|
| **+ Add** | Insert empty alias row |
| **Export .defs** | Write all aliases as VAL lines to `Assets/PlaytestDefs/aliases.defs` (auto-creates folder) |
| **Copy VAL block** | Copy multi-line `VAL $name path|comp|field` block to clipboard |
| Token label | Displays `~N tokens saved` — live estimate from `PlaytestAliasHelpers.TokenSavingsEstimate` |

### Drop Zone

Drag a **GameObject from the Hierarchy** onto the drop zone to auto-create an alias row:
- `alias` ← `PlaytestAliasHelpers.SuggestName(go.name)` (lowercase, spaces→underscore)
- `path` ← `ComponentSerializer.GetPath(go)` (scene-relative path)
- `component` and `field` left blank (fill via **Pick…** or manually)

### Typed Alias Cards (v0.77.12, PlaytestAliasCardBuilder)

Card rendering is extracted from `PlaytestAliasWindow` into `PlaytestAliasCardBuilder` (static). Each card has a **TypeDropdown** (`AliasType` enum) that drives layout:

| Type | Enum | Layout | DSL output |
|------|------|--------|------------|
| **VAL Path** | `ValPath` (0) | path + comp dropdown + field dropdown (cascading) | `VAL $name /path\|Comp\|field` |
| **Constant** | `ValConst` (1) | single `constValue` text field | `VAL $name literal_value` (no pipes) |
| **VAR** | `VarRuntime` (2) | same as VAL Path, path placeholder shows "runtime resolve" | `VAR $name @/path\|Comp\|field` |

**Status dot** (8px circle, right of alias name field):
- green (`alias-status-dot--valid`) — alias name + content all filled
- yellow (`alias-status-dot--partial`) — alias name present, content incomplete
- empty (`alias-status-dot--empty`) — no alias name

**Cascading dropdowns:** comp dropdown lists all `Component` types on the GO at `path` (via `GetUserComponents`); selecting a comp refreshes field dropdown via `GetMemberNames(type)`. Both dropdowns disabled when path resolves to no GO.

**DnD on path field:** dropping a `Component` auto-fills comp and opens field picker; dropping a `GameObject` opens comp picker.

**`BuildAliasSection` skips `VarRuntime`** — VAR aliases are DSL-only; `get_aliases` response only includes ValPath and ValConst rows.

| Control | Action |
|---------|--------|
| TypeDropdown | Switch card type; rebuilds row2 in-place |
| **Copy** | Copy single `FormatLine(alias)` (type-dispatched: VAL Path/Const or VAR) via `PlaytestAliasHelpers.FormatLine` |
| **X** | Delete alias row |

### DSL Preview

Below the list, a live preview shows the full `VAL` block that will be generated (`FormatVALBlock`).

### PlaytestAliasHelpers (static, no Editor dependency except ExportToDefs)

| Method | Purpose |
|--------|---------|
| `FormatLine(alias)` | Type-dispatched: ValPath → `"VAL $name path\|comp\|field"`, ValConst → `"VAL $name literal"`, VarRuntime → `"VAR $name @path\|comp\|field"` (no trailing pipes when comp/field empty) |
| `FormatVALBlock(aliases)` | Newline-joined block of `FormatLine` calls; empty string when list is empty |
| `ExportToDefs(aliases, filename="aliases")` | Write to `Assets/PlaytestDefs/<filename>.defs`; returns absolute path |
| `TokenSavingsEstimate(aliases)` | Net tokens saved assuming ≥3 uses per alias; returns 0 when aliases don't pay off |
| `SuggestName(goName)` | Lowercase + underscore + strip non-alphanum for drag-drop name hint |

### Typical Workflow

1. Open Alias Manager (`Shift+Alt+A`).
2. Drag GameObjects from Hierarchy into the drop zone.
3. Set `component` and `field` via **Pick…** or manually.
4. Click **Copy VAL block** and paste at the top of the DSL script, OR click **Export .defs** and add `INCLUDE aliases.defs` to the script.
5. Alternatively: pass the block as `defs` to `run_playtest(script, defs=block)` from Python.

---

**See also:** `AI/playtest-dsl.md` for full DSL syntax reference; `.claude/skills/playmode-verification.md` for assertion patterns.
