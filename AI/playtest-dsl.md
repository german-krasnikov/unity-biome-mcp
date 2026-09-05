# Playtest DSL Reference

Run Play Mode scenarios with deterministic step-by-step assertions. Parser processes directives in phases: INCLUDE expansion → MACRO collection → CALL expansion → VAL substitution → VAR binding → step execution.

## Path Syntax

Paths identify GameObjects in the hierarchy. Standard form: `/RootName/ChildName/GrandchildName` or `SceneName:/RootName/Child`. Paths support special characters in GameObject names through escaping.

### Backslash Escaping

Use backslash to escape literal `/` or `\` characters in GameObject names:

- `\/` = literal `/` in a name
- `\\` = literal `\` in a name

**Example:** A GameObject named `Day/Night` (slash in name):
```
ASSERT Day\/Night|Health > 0      # escapes the embedded /
MOVE to Day\/Night                # same rule applies to all path contexts
```

**Round-trip guarantee:** `ComponentSerializer.GetPath(go)` always generates a path that `FindObject` can resolve back to the original object, preserving special characters.

### Bracket Protection

Square brackets protect content from path splitting. Use brackets when a GameObject name already contains `/` and escaping is inconvenient:

```
[Zone A/Zone B]                    # single path segment, slash is literal
/[Zone A/Zone B]/Child             # child of "Zone A/Zone B"
ASSERT /[Zone A/Zone B]/Child|Comp|field == value
```

Brackets are nested-depth aware: `[[nested]]` is valid (inner brackets are data, not delimiters).

### Multi-Scene Paths

Qualify a path with a scene name using the colon operator:

```
SceneName:/RootObj/Child           # path in a specific scene
SceneName:/RootObj|Component|field # works with component paths too
```

Only the rightmost `/` before the path body is split by the parser. Slashes inside brackets or escaped with `\` are preserved.

### Component Tokens (UIDocument, PanelRenderer, UI)

When accessing UI element properties, the component token identifies the host:

```text
ASSERT /UIRoot|UIDocument|element|text == "Label"       # explicit UIDocument
ASSERT /UIRoot|PanelRenderer|element|text == "Label"    # PanelRenderer (Unity 6.4+)
ASSERT /UIRoot|UI|element|text == "Label"               # generic UI (normalized to UIDocument)
```

All three forms `|UIDocument|`, `|PanelRenderer|`, and `|UI|` resolve to the
same internal representation at parse time. This allows playtest scripts to
reference UI hosts compatibly across Unity versions:

- Unity 6.0–6.3 use only `UIDocument`; `|PanelRenderer|` and `|UI|` are normalized to `|UIDocument|`
- Unity 6.4+ may have either component; the parser accepts all three forms and selects the correct host at runtime

### Parser Internals

- **SplitTokens**: Bracket/quote-aware tokenizer. Ignores spaces inside:
  - Double quotes: `"text with spaces"`
  - Square brackets: `[content/with/slashes]`
  - Backslash escapes: `\"` and `\[` inside quotes; `\/` and `\\` in paths
- **ParseQOV** (Query-Operator-Value): Scans tokens for operators (`==`, `!=`, `>`, `<`, `>=`, `<=`), extracts query/op/value. Empty op = bool shorthand (e.g., `ASSERT $flag` = `ASSERT $flag == True`). Bool value normalization (InputNormalizer): case-insensitive aliases map to `True` or `False`: `true/yes/on` → `True`; `false/no/off` → `False`. Applies to `SET_ACTIVE`, `SET`, `ASSERT`, and all comparisons.
- **NormalizeUIHostPath**: Replaces `|PanelRenderer|` and `|UI|` tokens with `|UIDocument|` to unify host reference syntax across versions

## Setup and Teardown Blocks

### SETUP

Declare a block of steps that run before the main test steps. Without global
abort, a failed SETUP step skips the remaining SETUP and all main steps, then
runs TEARDOWN if present.

```
SETUP
  TELEPORT /Player 0,0,0
  ASSERT /Player|Health|hp == 100
SETUP_END

# main steps
ASSERT /Player|Score > 0
```

**Syntax:** `SETUP` → steps → `SETUP_END` (no other tokens required)

With `ABORT_ON_FAIL`, the same failure stops Play Mode and finishes immediately;
pending TEARDOWN does not run.

---

### TEARDOWN

Declare a cleanup block that runs after normal main completion or after a
non-global SETUP failure. Useful for reverting test state or capturing final
diagnostics.

```
TEARDOWN
  LOG Test finished
  ASSERT_CONSOLE_CLEAN
TEARDOWN_END
```

**Syntax:** `TEARDOWN` → steps → `TEARDOWN_END`

Without global abort, TEARDOWN continues through its steps even when an ordinary
main or TEARDOWN step fails. Global abort stops immediately after any failed
step or automatic console failure, so remaining TEARDOWN steps are skipped.

---

## Step Types (Alphabetical)

### ALIAS (removed — use VAL)

**ALIAS was removed from the parser in v0.92.x.** The keyword is no longer recognized; scripts using ALIAS will have the line treated as an unknown command. `PlaytestLinter` emits **ERROR** (not just WARN) when ALIAS is detected. Migrate to `VAL $name value`.

```
# Old (BROKEN — parser no longer handles ALIAS):
ALIAS player_start 100,50,0
MOVE player TO player_start

# Correct:
VAL $player_start 100,50,0
MOVE player TO $player_start
```

---

### ABORT_ON_FAIL

Global directive — after any failed step or automatic console failure, stop Play
Mode and finish immediately. All remaining SETUP, main, and TEARDOWN steps are
skipped.

```
ABORT_ON_FAIL
WAIT_UNTIL /Player|HP|value > 0 TIMEOUT 3
ASSERT /Player|HP|value == 100
```

Must appear as its own line anywhere in the script. Does not emit a step.

**Syntax:** `ABORT_ON_FAIL`

The per-step `ABORT` token is narrower: it stops Play Mode only when that
particular `WAIT_UNTIL` times out. It does not enable the global policy for other
failures.

```
WAIT_UNTIL /Enemy|AI|IsDead == true TIMEOUT 10 ABORT
```

---

### ASSERT

Test single property value with comparison operator.

```
ASSERT Player/Health == 100
ASSERT Player/Score > 50
ASSERT Enemy/Status != active
ASSERT /Player|HP|value > 0 AS "player must be alive"
```

**Syntax:** `ASSERT path/component/field op value [AS "description"]`  
**Operators (numeric):** `==`, `!=`, `>`, `<`, `>=`, `<=`  
**Operators (string):** `==` (case-insensitive), `!=`, `contains` (substring match)  
**Float equality:** `==` uses 0.001f tolerance — no `~=` operator exists  
**AS suffix:** inline label shown in report instead of raw query  
**Timeout:** default 5s

**Bool sugar (single-token form):** omit op and value when the field is boolean:

```
ASSERT $isAlive            # expands to: ASSERT $isAlive == True
ASSERT !$isAlive           # expands to: ASSERT $isAlive == False
ASSERT ($hp,$mana)         # batch: all listed $sigils must equal True
ASSERT !($hp,$mana)        # batch: all listed $sigils must equal False
```

- Token must be a `$sigil` (or `!$sigil`); bare paths without `$` are rejected
- Group form `($a,$b,...)` expands to an `ASSERT_BATCH` with `op=="==" value=="True"`
- Standard 3-token form unchanged when operator is present

---

### ASSERT_BATCH...END

Multiple assertions in one block, stops at first failure.

```
ASSERT_BATCH
  ASSERT Player/Health == 100
  ASSERT Enemy/Health > 0
  ASSERT Score != -1
END
```

**Syntax:**
```
ASSERT_BATCH
  ASSERT query op value
  ASSERT query op value
  ...
END
```

---

### ASSERT_NEAR

Check distance between two GameObjects ≤ threshold (meters).

```
ASSERT_NEAR Player Enemy 5.0
```

**Syntax:** `ASSERT_NEAR path1 path2 distance_threshold`  
**Output:** `(dist=X.XX)` actual distance in result

---

### ASSERT_CTA

Verify Call-To-Action state (VISIBLE or CLICKABLE).

```
ASSERT_CTA VISIBLE
ASSERT_CTA CLICKABLE
```

**Syntax:** `ASSERT_CTA VISIBLE | ASSERT_CTA CLICKABLE`  
**Default:** `VISIBLE` if mode omitted

---

### ASSERT_CONSERVED

Check that a sum of quantities remains constant over a time window (conservation law check).

```
ASSERT_CONSERVED SUM /Player|Health + /Enemy|Health == CONSTANT OVER 5
ASSERT_CONSERVED SUM ammo_current + ammo_used == CONSTANT OVER 2
```

**Syntax:** `ASSERT_CONSERVED SUM q1 + q2 [+ q3...] == CONSTANT OVER duration`  
**duration:** seconds to observe conservation  
**Use case:** verify totals are preserved (health sum, ammo conservation, resource balance)

---

### ASSERT_CAPTURED

Verify captured value matches condition.

```
CAPTURE initial_pos /Player|Transform|position
MOVE Player TO 100,100,0
ASSERT_CAPTURED initial_pos != 100,100,0
```

**Syntax:** `ASSERT_CAPTURED label op [subOp value]`  
**3-token form:** `ASSERT_CAPTURED label op` — checks mode only (no value comparison)  
**5-token form:** `ASSERT_CAPTURED label MODE subOp value` — MODE in Op, subOp in Args, value in Value

---

### ASSERT_CHANGED

Verify that a previously captured value has changed. String comparison is case-insensitive.

```
CAPTURE status_before /NPC|Status|name
INVOKE /NPC Status Advance
ASSERT_CHANGED status_before
```

**Syntax:** `ASSERT_CHANGED label`

---

### ASSERT_CONSOLE_CLEAN

Verify no error/exception logs since last call.

```
ASSERT_CONSOLE_CLEAN
ASSERT_CONSOLE_CLEAN IGNORE "NullReferenceException,deprecation_warning"
```

**Syntax:** `ASSERT_CONSOLE_CLEAN [IGNORE "pattern1,pattern2"]`  
**Filters out:** comma-separated substring patterns (trimmed, quotes stripped) — all matched as substrings against Console error text

---

### ASSERT_FRAMES_DIFFER / ASSERT_FRAMES_STATIC

Check a frame set captured by `CAPTURE_FRAMES`. A frame assertion requires at least two captured frames.

```
CAPTURE_FRAMES 4 INTERVAL 0.25 LABEL movement
ASSERT_FRAMES_DIFFER movement

CAPTURE_FRAMES 3 INTERVAL 0.2 LABEL idle
ASSERT_FRAMES_STATIC idle
```

**Syntax:** `ASSERT_FRAMES_DIFFER label` or `ASSERT_FRAMES_STATIC label`

---

### ASSERT_ONE_ACTIVE

Verify that exactly one GameObject in a list is active in the hierarchy (inclusive of parent state).

```
ASSERT_ONE_ACTIVE /Cam_Intro /Cam_Menu /Cam_Game
```

**Syntax:** `ASSERT_ONE_ACTIVE path1 path2 [path3 ...]`

At least two paths are required. Missing objects count as inactive. Uses `GameObject.activeInHierarchy`, which returns true only if the object and all its parents are active; it differs from `activeSelf` (object's own state, ignoring parents).

---

### CAPTURE

Snapshot current value for later comparison.

```
CAPTURE health_before /Player|Health|currentHealth
SET Player Health currentHealth 50
ASSERT_CAPTURED health_before != 50
```

**Syntax:** `CAPTURE label query`  
**label:** key used later in ASSERT_CAPTURED  
**query:** `path|Component|field` expression evaluated at capture time

---

### CAPTURE_FRAMES

Capture a timed frame sequence for motion or stability assertions.

```
CAPTURE_FRAMES 4 INTERVAL 0.25
CAPTURE_FRAMES 3 INTERVAL 0.5 CAMERA Main MODE list LABEL run1
```

**Syntax:** `CAPTURE_FRAMES count INTERVAL seconds [CAMERA name] [MODE strip|list] [LABEL name]`

- `count` must be at least 2.
- Default camera is `game`.
- Default mode is `strip`; `list` returns individual frame paths.
- When `LABEL` is omitted, the runner assigns a step-based label.

Use the label with `ASSERT_FRAMES_DIFFER` or `ASSERT_FRAMES_STATIC`.

---

### COMPLETE_PURCHASE

Parse-time expansion: invoke `PlacementPurchase.CompletePurchase()` then wait for all expected conditions.

```
COMPLETE_PURCHASE /Store/Item EXPECT
  /Store|PlacementPurchase|IsPurchased
  /Player|Currency|Coins > 0
TIMEOUT 5
```

**Syntax:**
```
COMPLETE_PURCHASE <path> EXPECT
  query1
  query2,...
TIMEOUT n
```

- Expands to one `Invoke` step + one `WaitUntil` (AND of all EXPECT queries, each `== True`)
- EXPECT lines may be comma-separated; multiple EXPECT lines are merged
- `TIMEOUT` line (optional) sets the WaitUntil timeout; default 5s
- `component = "PlacementPurchase"`, `method = "CompletePurchase"` are hard-coded

---

### CLICK / TAP

Simulate a click on a UI element or world object. `TAP` is a synonym for `CLICK`.

```
CLICK /UI/StartButton
CLICK /WorldObject/Button WAIT 0.5
TAP /UI/ResumeButton
```

**Syntax:** `CLICK path [WAIT delay]` or `TAP path [WAIT delay]`  
**WAIT:** optional post-click delay in seconds before next step  
**Priority:** tries `Button.onClick.Invoke()` first; falls back to `ExecuteEvents` with `IPointerClickHandler`  
**ERR:** object not found, object inactive, or `Button.interactable == false`  
**FAIL:** object has neither `Button` nor `IPointerClickHandler`

---

### DESC

Label the next emitted step in the report. Does not emit a step itself. Complements `AS` on ASSERT when you want to label non-ASSERT steps.

```
DESC "move player to start position"
TELEPORT Player 0,0,0
DESC "check health after spawn"
ASSERT /Player|Health|current == 100
```

**Syntax:** `DESC "label text"`  
**Scope:** applies to the immediately following step only; label is cleared after use

---

### INCLUDE

Load a `.defs` file from `Assets/PlaytestDefs/` and inline its content at parse time (Phase -1, before MACROs).

```
INCLUDE aliases.defs
INCLUDE shared/combat.defs
```

**Syntax:** `INCLUDE filename`

**Constraints:** filename may not contain `..` or be rooted (path traversal rejected). Resolved against `Assets/PlaytestDefs/` only. Max recursion depth 5.

**Typical content:** `VAL` lines, reusable across multiple scripts.

---

### INVARIANT

Assert condition continuously through next phase.

```
INVARIANT Player/Health > 0
MOVE Player TO (100,0,0)  # health must stay > 0 during move
```

**Syntax:** `INVARIANT query op value`

---

### INVOKE

Call public method on component via reflection.

```
INVOKE Player Rigidbody AddForce 10,0,0
INVOKE Enemy HealthComponent TakeDamage 25
INVOKE /Player PlayerController Heal           # no arguments
```

**Syntax:** `INVOKE path component method [args]`  
**path:** GameObject path (token 1)  
**component:** component type name (token 2)  
**method:** method name (token 3)  
**args:** single token of space-joined arguments (token 4, optional)  
**Returns:** "PASS" if method executed, "ERR" if component/method not found

**Overload matching (v1.15.0):** Arity-based resolution — if the method is overloaded, the system picks the overload whose parameter count matches the provided args. If multiple overloads match (e.g., two zero-arg variants), the first declaration wins. Optional C# parameters are supported; callers may omit trailing optional arguments.

---

### SET_ACTIVE

Set a GameObject's active state in the hierarchy.

```
SET_ACTIVE /Player true
SET_ACTIVE /Boss false
```

**Syntax:** `SET_ACTIVE path active_state`  
**path:** GameObject path  
**active_state:** `true` or `false`  
**Returns:** "PASS" if state was set, "ERR" if path not found

**Difference from MOVE/INVOKE:** SET_ACTIVE is a first-class DSL command optimized for readability in play-mode test scripts. Equivalent to reflection: `SetActive(bool)`.

---

### INVOKE_REPEAT

Parse-time expansion: call the same method N times, then optionally wait for a condition.

```
INVOKE_REPEAT 3 /Player PlayerController TakeDamage 10
EXPECT /Player|PlayerController|Health < 70 TIMEOUT 5
```

**Syntax:**
```
INVOKE_REPEAT <count> <path> <component> <method> [args]
[EXPECT <query> <op> <value> [TIMEOUT n]]
```

- Expands to `count` Invoke steps with identical arguments
- EXPECT line (optional) emits a single `WaitUntil` after the last Invoke
- EXPECT line must immediately follow (blank/comment lines skipped); stops at first non-EXPECT non-blank line
- Label (preceding `DESC`) is applied to the first Invoke step only

---

### LOG

Print message to results.

```
LOG Starting combat test
INVOKE Enemy Attack
LOG Combat finished
```

**Syntax:** `LOG message_text`

---

### MCP

Execute an MCP command through the CommandRouter, capturing output into an optional result variable for later assertions.

```
MCP create_object name=TestCube primitive=Cube
MCP get_hierarchy depth=2 INTO $tree
ASSERT $tree contains TestCube
MCP set_property path=/TestCube component=Transform prop=position value=1,2,3
ASSERT /TestCube|Transform|position.x == 1
```

**Syntax:** `MCP <cmd> param1=value1 [param2=value2 ...] [INTO $varname]`

**Parameters:** MCP command parameters as `key=value` pairs (no spaces around `=`). See `tool_specs.py` for available commands and their parameter names.

**INTO clause (optional):** Capture the raw response into a VAL for use in subsequent steps:
```
MCP get_component path=/TestCube component=Transform INTO $result
ASSERT $result contains "position"
```

**System variable `$RUN_ID`:** Automatically injected parallel-safe unique ID per playtest run (hexadecimal, fixed length). Use in object names to avoid collisions:
```
MCP create_object name=obj_$RUN_ID primitive=Cube
MCP delete_object path=/obj_$RUN_ID
```

**EXPECT_FAIL modifier (single step):** Expect a step to fail (error, missing path, type mismatch). Inverts pass/fail for that step only; console errors are still checked unless EXPECT_FAIL precedes:
```
EXPECT_FAIL
MCP set_property path=/NonexistentObject component=Transform prop=position value=1,2,3
```

**MCP Command Restrictions (enforced at compile time):**
- Play Mode gate: MCP steps require Play Mode unless the script has `# @needs editmode` header
- EditMode execution policy: `# @needs editmode` scripts may call MCP commands only if the target tools support EditMode dispatch
- Denied commands (9): `play`, `stop`, `pause`, `step`, `set_timescale`, `editor(action='play')`, `editor(action='stop')`, `editor(action='play_single')` (mode-transition and timing-control commands always require Play Mode state transition)
- Runtime mutation guard: mutations (`set_property`, `set_active`, `create_object`, etc.) are rejected at dispatch time if called during Play Mode simulation

**Format and receipts:**
- Default format is text (human-readable report)
- `run_playtest(format="json")` produces structured output with per-step receipt objects
- Each MCP step receipt includes `command`, `status` (ok/error), `error_message` (if failed), and response data
- Canonical receipt JSON persisted to `PlaytestReceiptStore.ReceiptPath(runId)` for archival

---

### MACRO / CALL

Define reusable command blocks with positional parameters. Macros are collected in phase 0 (before ALIAS), expanded in place. Nesting up to 10 levels. Cannot nest MACRO definitions.

```
MACRO check_health $path $expected
  ASSERT $path|HealthComponent|CurrentHealth == $expected
END_MACRO

CALL check_health /Player 100
CALL check_health /Enemy 50
```

**Define syntax:** `MACRO name $param1 $param2 ... ` → body lines → `END_MACRO`  
**Call syntax:** `CALL name arg1 arg2 ...`  
**Parameters:** positional `$1`/`$param` style — whole-word substitution in body lines  
**Forward references:** CALL may appear before MACRO definition in the script

---

### MONITOR

Watch expression value during the following steps, show graph in results. Data is collected until `MONITOR STOP` or end of script, then appended to the report.

```
MONITOR Player/Health
WAIT 3.0
MOVE Player TO 100,0,0
MONITOR STOP
```

**Syntax:** `MONITOR query` to start, `MONITOR STOP` to stop  
**STOP:** emits a Monitor step with null query — stops all active monitors  
**Report:** sampled values appended after main PLAYTEST summary

---

### MOVE

Pathfind and walk character to world position.

```
MOVE Player TO 100,50,0
MOVE TO 0,0,0             # auto-detect Player path
MOVE Player TO @/Enemy.position            # use enemy's current position
MOVE Player TO @/Enemy.position + (5,0,0)  # offset from enemy
MOVE Player TO @/Enemy.position - (0,0,3)  # negative offset
```

**Syntax:** `MOVE [path] TO x,y,z | @/GoPath.position [+|- (dx,dy,dz)]`  
**Position forms:**
- Literal `x,y,z` — resolved at parse time
- `@/GoPath.position` — resolved at runtime from the referenced GameObject's `transform.position`; optional `+` or `-` offset tuple appended

**Speed:** 15 m/s default  
**Timeout:** default 5s  
**Returns:** "PASS" when within 0.1m of target

---

### MOVE_PATH

Multi-waypoint movement — parser expands to N sequential `MOVE` steps.

```
MOVE_PATH 1,0,0 > 5,0,0 > 10,0,3
MOVE_PATH 0,0,0 > 5,0,5 > 10,0,0 TIMEOUT 8
```

**Syntax:** `MOVE_PATH x1,y1,z1 > x2,y2,z2 [> ...] [TIMEOUT n]`  
**Expansion:** each `x,y,z` segment between `>` separators becomes one `MOVE` step  
**TIMEOUT:** applied to every expanded step  
**Path:** uses auto-detect player path (no explicit path token supported)

**Need dwell at each waypoint or a stop condition?** Use `SWEEP_PATH` instead.

---

### SNAPSHOT

Capture game view (optional visual verification).

```
SNAPSHOT width=1280 height=720 camera="MainCamera"
```

**Syntax:** `SNAPSHOT [width=640] [height=480] [camera="name"]`  
**Output:** `.png` path in result

---

### SECTION

Group header shown in report regardless of pass/fail state. Use to divide a long script into named phases.

```
SECTION "Setup"
TELEPORT Player 0,0,0
SECTION "Combat"
INVOKE Enemy Attack
SECTION "Teardown"
ASSERT_CONSOLE_CLEAN
```

**Syntax:** `SECTION "title"` (quotes optional but recommended for multi-word titles)

---

### SIMULATE

Run a named simulator for a duration with optional parameters.

```
SIMULATE physics DURATION 1.0
SIMULATE ai_patrol DURATION 2.0 TIMESCALE 2.0 TARGET "Enemy"
SIMULATE wave_spawner DURATION 5.0 FREQUENCY 10
```

**Syntax:** `SIMULATE name [DURATION n] [TIMESCALE n] [TARGET "path"] [FREQUENCY n]`  
**name:** simulator identifier (required)  
**DURATION:** seconds (stored as Timeout)  
**TIMESCALE:** time scale multiplier (stored as Delay)  
**TARGET:** GameObject path (quoted)  
**FREQUENCY:** rate parameter (stored as Value)

---

### SET

Set runtime property on object (Play Mode only).

```
SET Player Rigidbody velocity "0,10,0"
SET Enemy Health currentHealth 50
```

**Syntax:** `SET path component field value`  
**path:** GameObject path (token 1)  
**component:** component type name (token 2)  
**field:** field/property name (token 3)  
**value:** new value (token 4)

---

### SWEEP_PATH

Multi-waypoint movement with a dwell wait at each waypoint, plus an optional termination condition.

```
SWEEP_PATH /Player DWELL 1.5
  10,0,0 > 20,0,0 > 30,0,5
UNTIL /Trigger|Sensor|Activated == true TIMEOUT 10
```

**Syntax:**
```
SWEEP_PATH <path> DWELL <seconds>
  x,y,z > x,y,z [> ...]
[UNTIL <query> <op> <value> [TIMEOUT n]]
```

**Expansion** (parse time):
1. One `Move` step + one `Wait <dwell>` per waypoint
2. Optional `WaitUntil` from the `UNTIL` line (reads TIMEOUT from that line; default none)

- Waypoints on any number of following lines until `UNTIL` or next DSL keyword
- `>` separator between coordinates (ignored as separator token)
- `DWELL 0` emits Move steps only (no Wait)
- Label (`DESC`) applied to the first Move step
- `path` token must be explicit (no auto-detect like bare `MOVE_PATH`)

**Difference from MOVE_PATH:** MOVE_PATH has no dwell and no UNTIL clause; SWEEP_PATH is the dwell+condition variant.

---

### TELEPORT

Instantly move GameObject to position (no pathfind).

```
TELEPORT Player 100,50,0
TELEPORT Boss 0,0,0
TELEPORT Enemy @/Player.position + (2,0,0)  # spawn offset from player
```

**Syntax:** `TELEPORT path x,y,z | @/GoPath.position [+|- (dx,dy,dz)]`

Position forms identical to MOVE: literal `x,y,z` or `@/GoPath.position` runtime-resolved with optional offset.

---

### TIMESCALE

Change Time.timeScale for slow-mo / speedup.

```
TIMESCALE 0.5  # half speed
TIMESCALE 2.0  # double speed
TIMESCALE 1.0  # normal
```

**Syntax:** `TIMESCALE scale_factor`

---

### TRACE_FLOW

Trace data/event flow between two GameObjects over a field.

```
TRACE_FLOW FROM /Player TO /Enemy FIELD health
TRACE_FLOW FROM /Spawner TO /WaveManager FIELD waveCount TIMEOUT 10
```

**Syntax:** `TRACE_FLOW FROM /path1 TO /path2 FIELD fieldName [TIMEOUT n]`  
**FROM:** source GameObject path  
**TO:** destination GameObject path  
**FIELD:** field name to observe  
**TIMEOUT:** seconds (default 5)

---

### VAL

Static text substitution at parse time (Phase 0.7). Uses `$name` sigil — not whole-word replacement.

```
VAL $player_start 100,50,0
VAL $hp_path /Player|Health|currentHp

MOVE Player TO $player_start       → expands to: MOVE Player TO 100,50,0
ASSERT $hp_path > 0               → expands to: ASSERT /Player|Health|currentHp > 0
```

**Syntax:** `VAL $name value`

**Rules:**
- `$name` must match `[A-Za-z_][A-Za-z0-9_]*`
- Value cannot start with a DSL keyword (e.g., `VAL $x MOVE …` is rejected — prevents command injection)
- VAL values can reference earlier VALs: `VAL $base /Player`, `VAL $hp $base|Health|hp` → resolves chain via topo-sort
- Circular references detected at parse time (error thrown)
- Defined anywhere in the script; all VALs collected before expansion

**vs ALIAS:** VAL uses `$sigil` substitution (regex-based). ALIAS was removed from the parser in v0.92.x — linter flags it as ERROR. Migrate all ALIAS usages to VAL.

---

### VAR

Runtime-resolved sigil. Query executed via `ReadValue()` at each step that uses the variable (not at parse time).

```
VAR $hp @/Player|Health|currentHp
VAR $pos @/Enemy|Transform|position

ASSERT $hp > 0          → reads /Player|Health|currentHp at assertion time
WAIT_UNTIL $hp < 50 TIMEOUT 10
TELEPORT Boss $pos      → reads enemy position each time this step runs
```

**Syntax:** `VAR $name @path|Component|field`

**Rules:**
- Query must start with `@`
- Must have pipe-separated `path|Component|field` format (3 parts minimum)
- `$name` replaced by live Unity value each step it appears
- VAL sigils in the `@query` are expanded at parse time (allows `VAR $hp @$base|Health|hp`)
- Collected in Phase 1.1; `PlaytestVarRegistry` holds bindings and expands per-step

**Use case:** values that change during the script (dynamic positions, live health values).

---

### Sigil Resolution and Error Handling

During parsing (Phase 0.7), `$sigil` tokens are matched against defined VAL and VAR names.
Unresolved sigils trigger different behavior based on the source:

- **File-based .playtest**: strict mode enabled → unresolved `$sigil` **fatal error** (ParseResult.Errors list) blocks execution
- **Inline script= parameter**: lenient mode → unresolved `$sigil` **warning** (ParseResult.Warnings list) logged but allows continuation

This prevents silent failures in saved fixtures (which are often used for conformance and regression testing)
while permitting exploratory inline scripts to degrade gracefully. Both modes suggest the closest matching VAL/VAR name.

Example (strict mode rejects):
```
VAL $player /Player
ASSERT $palyer == true   # Fatal: unresolved $palyer (did you mean $player?)
```

Example (lenient mode warns but runs):
```
run_playtest(script="""
VAL $player /Player
ASSERT $palyer == true   # Warning: unresolved $palyer
""")
```

---

### WAIT

Pause execution for N seconds.

```
WAIT 2.0
WAIT 0.1
```

**Syntax:** `WAIT duration_seconds`  
**Blocks:** all subsequent steps until delay expires

---

### WAIT_UNTIL

Poll condition with timeout, continue when true.

```
WAIT_UNTIL Player/Health == 100 TIMEOUT 10
WAIT_UNTIL Enemy/IsDead == true
```

**Compound conditions — AND (all must be true):**
```
WAIT_UNTIL /Player|HP|value > 0 AND /Enemy|HP|value == 0 TIMEOUT 10
```

**Compound conditions — OR (any must be true):**
```
WAIT_UNTIL /Door|Door|IsOpen == true OR /Player|Player|IsDead == true TIMEOUT 15
```

**Per-step abort on timeout:**
```
WAIT_UNTIL /Enemy|AI|IsPatrolling == true TIMEOUT 5 ABORT
```

`ABORT` applies only to this wait's timeout. Use the standalone
`ABORT_ON_FAIL` directive for fail-fast behavior across every step type and
automatic console failures.

**Syntax:** `WAIT_UNTIL query op value [AND|OR query op value ...] [TIMEOUT n] [ABORT]`  
**Operators (numeric):** `==`, `!=`, `>`, `<`, `>=`, `<=`  
**Operators (string):** `==` (case-insensitive), `!=`, `contains` (substring)  
**Constraint:** cannot mix AND and OR in the same step  
**Default timeout:** 5s  
**Poll interval:** 0.05s

---

### WAIT_CAPTURED

Poll until a CAPTURE delta condition is met (compare current value against captured baseline).

```
CAPTURE gold /Player|Wallet|Gold
INVOKE /Store StoreFront BuyItem sword
WAIT_CAPTURED gold DECREASED TIMEOUT 5
WAIT_CAPTURED gold DECREASED_BY == 50 TIMEOUT 5
WAIT_CAPTURED gold UNCHANGED OVER 2 TIMEOUT 8
```

**Syntax:** `WAIT_CAPTURED <label> <mode> [subOp value] [TIMEOUT n] [OVER n]`

**Modes:**
| Mode | Condition |
|------|-----------|
| `INCREASED` | current > captured |
| `DECREASED` | current < captured |
| `UNCHANGED` | current == captured |
| `INCREASED_BY` | current − captured (subOp) value |
| `DECREASED_BY` | captured − current (subOp) value |

- `subOp value` (optional for `*_BY` modes): e.g., `== 50`, `>= 10`
- `OVER n`: for `UNCHANGED`, requires condition stable for n seconds (window check)
- `TIMEOUT n`: max wait in seconds (default 5s); TIMEOUT and OVER are independent keywords
- The step polls the same query used in the original CAPTURE, reading its live value each tick

---

## DSL Structure

```
# Phase -1: INCLUDE inlines Assets/PlaytestDefs/ files before anything else
INCLUDE shared_aliases.defs   # expands inline

# Phase 0: MACRO definitions extracted (not emitted as steps)
MACRO move_patrol $start $end
  MOVE_PATH $start > $end > $start
END_MACRO

# Phase 0.7: VAL = static substitution at parse time
VAL $player_start 100,0,0
VAL $hp_query /Player|Health|currentHp
VAL $enemy_patrol 50,0,50

# Phase 1.1: VAR = runtime-resolved per-step
VAR $live_hp @/Player|Health|currentHp

ABORT_ON_FAIL

SECTION "Patrol intercept"
LOG Test: Enemy patrol intercept
CAPTURE initial_pos Enemy

CALL move_patrol $enemy_patrol 80,0,80   # macro expanded to 2 MOVE steps

WAIT_UNTIL $hp_query > 0 AND /Enemy|AI|IsPatrolling == true TIMEOUT 8

DESC "verify combat started"
ASSERT /Player|Score|value > 0 AS "player scored during patrol"

ASSERT_BATCH
  ASSERT $hp_query > 0
  ASSERT Enemy/IsDead == false
END

TELEPORT Player $player_start
ASSERT $live_hp > 0     # $live_hp resolved fresh from Unity each time
ASSERT_CONSOLE_CLEAN IGNORE "warning"
SNAPSHOT
LOG Test completed
```

## Method Args in Field Queries

Fields support method dispatch via `field(args)` syntax in query strings:

```
WAIT_UNTIL /Inventory|Inventory|HasItem(sword) == true
ASSERT /Player|Movement|DistanceTo(5,0,3) < 1.0
```

**Syntax:** `path|Component|MethodName(arg1,arg2)` — reflection invoked with parsed args  
**Vector3:** method taking Vector3 consumes 3 comma-separated values (smart grouping)  
**Zero-arg:** `MethodName()` — invoked with no arguments

## Parsing Rules

**Preamble injection (v0.78.9):** Before Parse() is called, `PlaytestRunner` prepends two VAL blocks to the user script: (1) Unity Tag strings (spaces → underscores), always; (2) `PlaytestAliasHelpers.FormatVALBlock(config.aliases)` from the active `PlaytestConfig` asset (skipped when aliases list is empty). Because VAL resolution uses last-write-wins, any `VAL $name` or `INCLUDE` directive in the user script overrides the preamble defaults.

**AliasExpander pipe fix (v0.78.11):** `AliasExpander.GetTable()` — the C#-side lookup used for `$sigil` expansion in batch DSL lines and direct MCP tool argsJson — now correctly builds `path|component|field` via `BuildPipePath`. Before this fix, ValPath aliases with a component+field set would expand to path-only, silently stripping the `|Comp|field` suffix. `FormatVALBlock` → `CollectVals` (playtest DSL parse path) was always correct; the regression only affected the `AliasExpander` table used outside the DSL parser.

1. **Phase -1 — INCLUDE:** `INCLUDE filename` replaced inline with content of `Assets/PlaytestDefs/filename`; recursive max depth 5; path traversal rejected
2. **Phase 0 — MACROs:** All `MACRO … END_MACRO` blocks collected and removed
3. **Phase 0.5 — CALL expansion:** `CALL name arg1 arg2` replaced with expanded body lines (recursive, max depth 10)
4. **Phase 0.7 — VAL:** `VAL $name value` lines collected; `$sigil` regex substitution applied to all remaining lines; VAL-in-VAL chain resolved via topo-sort; cycles rejected at parse time
5. **Phase 1 — ALIAS (removed v0.92.x):** ALIAS keyword removed from parser; linter emits ERROR if encountered. Use `VAL $name value` instead.
6. **Phase 1.1 — VAR:** `VAR $name @path|Comp|field` collected into `PlaytestVarRegistry`; each step that contains a known sigil is expanded at runtime by `ReadValue()`
7. **Comments:** Lines starting with `#` ignored
8. **Whitespace:** Leading/trailing trimmed; tokens split by space
9. **Case-insensitive:** Commands (`MOVE`, `move`, `Move` equivalent)
10. **Queries:** Path syntax = `Parent/Child/Component.field` or `Component.field` (scene root)
11. **Position resolver (MOVE/TELEPORT):** `@/GoPath.position` deferred until step execution; literal `x,y,z` parsed at step parse time. ParseResult stores deferred form in `step.RawPosition` (null for literals).
11. **DESC:** consumed and stored as pending label; applied to next step, not emitted itself
12. **ABORT_ON_FAIL:** consumed as global directive; not emitted as step

## Provenance Tracking

Each parsed step carries four optional fields populated during parse:

| Field | Content |
|-------|---------|
| `SourceFile` | Origin `.defs` or `.playtest` filename; null = inline script |
| `SourceLine` | 0-based line index within `SourceFile` (or inline script) |
| `MacroStack` | Non-null when step came from a MACRO CALL; outermost-first chain, e.g. `["outer_macro", "inner_macro"]` |
| `SectionContext` | `SECTION` label active when the step was parsed; null = no enclosing section |

On failure, the provenance is appended inline:

```
[3] ASSERT $hp == 100 — FAIL (75)  [source: combat.defs:12 | macro: check_hp | section: Combat]
```

- `source:` omitted when step is inline
- `macro:` omitted when step is not inside a MACRO body
- `section:` omitted when no enclosing SECTION

## Verification Rules

- **PASS:** condition satisfied
- **FAIL:** condition false or timeout expired
- **ERR:** exception during evaluation (e.g., path not found, component missing)
- **CONSOLE_ERR:** A console error/exception occurred during step execution. This increments the failed-step count even if other assertions passed.
- **Result format:** `[step_number] COMMAND ... — PASS/FAIL/ERR`
- **Failure provenance:** source file, line, macro chain, and section label appended on FAIL (see Provenance Tracking above)
- **Failure snapshot** (`snapshot_on_failure=true`): on FAIL or ERR, appends current `$sigil` values and recent console errors inline — controlled by `run_playtest(snapshot_on_failure=True)` or `run_playtest(path="...", snapshot_on_failure=True)`

## GD Integration (@label namespace)

`GdSnapshotSerializer.ToPlaytestPreamble(snapshots)` converts GD region annotations to `VAL $label` preamble lines using the `@label` namespace (updated v0.92.x from ALIAS to VAL format):

```
# generated preamble
VAL $spawn_zone 5.00,0.00,3.00
VAL $patrol_start_0 1.00,0.00,0.00
VAL $patrol_start_1 10.00,0.00,0.00

# script uses them via $sigil substitution
TELEPORT Player $spawn_zone
MOVE_PATH $patrol_start_0 > $patrol_start_1
```

**Label format:** `$<sanitized_label>` — lowercase, underscores, stripped special chars  
**Annotation types:** `Point` → single VAL; `Path` → `_start`/`_end` pair + vertex list `_0`, `_1`, …

## Player Runtime Support (v1.53.0+)

The playtest DSL can run in Player builds as well as EditMode/PlayMode. Player runtime is
enabled by the `UnityMCP.Playtest.Core` engine-free assembly and shared `Compare()` 
evaluation logic.

### Supported Step Types in Player

Player runtime supports 9 core step verbs (validation, assertions, and flow):

1. `ASSERT` — test value with operators (`==`, `!=`, `>`, `<`, `>=`, `<=`, `contains`)
2. `ASSERT_BATCH` — multiple assertions, stop at first failure
3. `ASSERT_NEAR` — distance check between GameObjects
4. `ASSERT_CONSOLE_CLEAN` — verify no console errors
5. `WAIT` — simple delay by seconds
6. `WAIT_UNTIL` — loop until condition true or timeout
7. `LOG` — write to console
8. `SNAPSHOT` — capture query values (no-op in Player, logged)
9. `INVOKE` — call method by reflection with parse-time argument binding

Unsupported: `MCP` (no Player bridge), `MOVE`, `TELEPORT`, `CLICK`, `FILL`, `FOCUS` (Editor-only verbs).

### Pre-Scan Gate (Player Load-Time Validation)

Before any Player script runs, `PlayerPlaytestRunner.Parse()` performs a pre-scan that:
- Rejects scripts with unsupported verbs (e.g., `MCP`, `MOVE`)
- Rejects `# @needs editmode` header (EditMode-only directive)
- Validates path syntax and operator tokens
- Returns a **detailed error** naming the offending line

Failed pre-scan halts execution without entering Play Mode simulation. This catch-all gate
prevents script failures mid-run and ensures deterministic behavior across platform builds.

---

**See also:** `run_playtest` (inline `script=` or file `path=`) in
`AI/runtime-playtest.md`, `AI/playtest-composer.md` for the visual editor, and
`unity-plugin/ClientSkills/skills/unity-testing-verification/references/playtest-dsl.md`
for the concise installed-agent workflow.
