# Playtest Composer

Visual drag-and-drop panel for building Playtest DSL scripts without writing raw text. Create, reorder, and run steps directly in the Unity Editor.

## Opening the Panel

- **Menu:** `MCP / Playtest Composer` — shortcut `Shift+Alt+P`
- **Chat toolbar:** "Composer" button (when `UNITY_MCP_CHAT` define is active)

State persists across Unity sessions in `<ProjectRoot>/Library/PlaytestComposerState.json`.

## Building a Playtest

### Toolbar

| Control | Action |
|---------|--------|
| **▶ Run** | Enters Play Mode and runs the step list; disabled when any step has a validation error |
| **Save** | Saves to a `.playtest` file (default folder: `<ProjectRoot>/Playtests/`) |
| **Load** | Opens a `.playtest` file; macros and aliases are expanded on load |
| **Copy DSL** | Copies raw DSL text to clipboard |
| **Copy for AI** | Copies a ` ```playtest ``` ` fenced block — paste directly into Claude chat |
| **＋ Smart Command** | Opens natural language → steps window |
| **TO: [float]** | Global timeout in seconds (default 60s) |
| **Abort [toggle]** | Prepends `ABORT_ON_FAIL` when checked |

### Adding Steps

Click **＋** in the footer to add a new step (defaults to `WAIT 1s`). Remove with **−** or right-click → Delete. Duplicate via right-click → Duplicate.

Drag the handle on the left of any row to reorder. Invalid steps show a red border — hover to see the error.

### Step Types and Fields

Select a type from the dropdown. Each type exposes relevant fields:

| Type | Fields |
|------|--------|
| Move, Teleport | Path, Position (Vector3), ⊙ eyedropper |
| Wait | Delay (seconds) |
| TimeScale | Scale multiplier |
| WaitUntil | Query, Op, Value, Timeout, Abort toggle |
| Assert, Invariant | Query, Op, Value |
| Section, Log | Message text |
| Invoke | Path, Component, Method, Args |
| Set | Path, Component, Field, Value |
| Monitor | Query |
| Click | Path, Wait delay |
| Capture | Label, Query |
| AssertCaptured | Label, Mode (`DELTA` / `RATIO` / `INCREASED` / `DECREASED` / `UNCHANGED` / `CHANGED`) |
| AssertNear | Path A, Path B, Distance threshold |
| AssertConsoleClean | Ignore patterns (comma-separated) |

### Drag & Drop from Hierarchy

Drag one or more GameObjects from the Hierarchy directly onto the step list. For each object, a menu appears:

| Action | Step created |
|--------|-------------|
| Move `'Name'` | Move step with current position |
| Teleport `'Name'` | Teleport step with current position |
| Assert field on `'Name'` | Assert step; opens field picker |
| WaitUntil field on `'Name'` | WaitUntil step (10s timeout); opens field picker |
| Invoke method on `'Name'` | Invoke step; opens method picker |
| Monitor field on `'Name'` | Monitor step; opens field picker |
| Set field on `'Name'` | Set step; pre-fills current value |
| Click `'Name'` | Click step with path |
| Capture field on `'Name'` | Capture step; opens field picker |
| AssertNear `'Name'` | AssertNear step with path |

You can also **drop a GameObject or Component directly onto a Path or Query field** inside a step row:

- **Path field:** choose "Set path + position", "Set path only", or "Set position only"
- **Query field:** opens field picker; auto-fills query and value from the current runtime value
- **Drop a Component** directly onto a Query field → skips the GameObject step, goes straight to field picker
- **⊙ Eyedropper** on Move/Teleport rows: fills path and position from the current Scene Selection

## Smart Command

Click **＋ Smart Command** to open the natural language window (400×360).

1. Type what you want — in English or Russian, one step per line or a paragraph.
2. Optionally drag GameObjects into the text field to insert `[/path]` references or `(x,y,z)` coordinates. The panel auto-converts these to `MOVE`/`TELEPORT` DSL lines.
3. Turn **Use AI** on or off:
   - **On** — sends text to LLM; falls back to heuristic on failure
   - **Off** — heuristic parser only (no LLM call, instant)
4. Click **Parse ▶**. Preview pane shows each line:
   - Green = valid DSL step
   - Red = `LOG # UNPARSED: <original text>` — could not convert
5. Click **OK** — all valid steps are appended to the Composer list.

## DSL Extensions

Two constructs are available in `.playtest` files but are expanded into individual steps when the file is loaded:

**MACRO / CALL** — define reusable step blocks with parameters:

```
MACRO check_health $path $expected
  ASSERT $path|HealthComponent|CurrentHealth == $expected
END_MACRO

CALL check_health /Player 100
CALL check_health /Enemy 50
```

**SECTION** — a group header shown in the results report:

```
SECTION "Setup"
TELEPORT /Player 0,0,0
SECTION "Combat"
INVOKE /Enemy HealthComponent TakeDamage 50
SECTION "Teardown"
ASSERT_CONSOLE_CLEAN
```

For the full DSL reference (all 26 steps, operators, parsing rules), see [Playtest Guide](playtest.md).

## Examples

### 1. Respawn sanity check

Drag `/Player` onto the list → "Assert field on 'Player'" → pick `HealthComponent / currentHealth`. Add a Teleport to a death zone, wait, then assert that health has recovered.

```
SECTION "Kill"
TELEPORT /Player 0,-100,0
WAIT 1.0
SECTION "Respawn"
WAIT_UNTIL /Player|HealthComponent|currentHealth > 0 TIMEOUT 10
ASSERT /Player|HealthComponent|currentHealth == 100
ASSERT_CONSOLE_CLEAN
```

### 2. UI button flow

```
SECTION "Main Menu"
ASSERT_CTA VISIBLE
CLICK /UI/Canvas/StartButton WAIT 0.5
SECTION "Gameplay"
WAIT_UNTIL /GameManager|GameManager|IsPlaying == true TIMEOUT 5
ASSERT /GameManager|GameManager|IsPlaying == true
```

### 3. Combat with before/after snapshot

```
CAPTURE hp_before /Player|HealthComponent|currentHealth
INVOKE /Enemy HealthComponent TakeDamage 25
WAIT 0.2
ASSERT_CAPTURED hp_before DECREASED
ASSERT /Player|HealthComponent|currentHealth > 0
ASSERT_CONSOLE_CLEAN
```

After building the script, click **Copy for AI** and paste it into Claude chat to run it programmatically.

## Tips

- **Save early** — `.playtest` files are plain text and diff cleanly in git.
- **Sections pay off** — in long scripts, `SECTION` headers make the results report scannable at a glance.
- **Copy for AI → paste into chat** — the fenced block is picked up by Claude and run via `run_playtest` automatically. No manual DSL typing needed.
- **Validation before Run** — fix all red-bordered steps first; the Run button stays disabled until every step is valid.
- **PlaytestConfig ScriptableObject** — create one at `Assets > Create > MCP > Playtest Config` to set your player path, movement component, and query aliases once for the whole project.

---

**See also:** [Playtest Guide](playtest.md) — full DSL reference, operators, timeout config, and error handling.
