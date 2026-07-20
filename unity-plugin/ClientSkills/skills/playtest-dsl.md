---
description: run_playtest DSL — complete command reference, verified against C# parser
---

# run_playtest DSL Reference

TIER1 tool. Requires Play Mode. Executes a multi-step script with assertions.

```
editor action="play"   # required before run_playtest*
run_playtest script="..." path="Tests/foo.playtest" timeout=300 abort_on_fail=false defs="VAL $hp /Player|Health|hp" snapshot_on_failure=false fresh=false
```

- `script` — inline DSL text; `path` — project-relative `.playtest` file (one is required)
- `timeout` — max execution time in seconds (default 300)
- `abort_on_fail` — stop Play Mode on first assertion failure (default false)
- `defs` — inline `VAL` lines prepended to `script` (one `name path|comp|field` per line, `VAL ` prefix optional) — use for session-level aliases without editing the script body
- `snapshot_on_failure` — save screenshot on first ASSERT failure (v0.81.4, default false)
- `fresh` — stop and restart Play Mode before running the script (v0.89, default false) — use after a test leaves bad state

**Query format:** `/ObjectPath|ComponentType|fieldName` (pipe-separated). **Operators:** `==` `!=` `>` `>=` `<` `<=` (numeric), `==` `!=` `contains` (string). **Float equality:** `==` uses 0.001f tolerance.

## Aliases & Variables

`$name` is the sigil pattern (`[A-Za-z_][A-Za-z0-9_]*`). It expands two ways — parse-time (`VAL`) or runtime (`VAR`). Unresolved sigils are left intact and retried at the next stage; still-unresolved ones produce a non-fatal parse warning ("typo in VAL/VAR name?") logged to console, not a hard failure.

| Directive | Syntax | Expands | Notes |
|---|---|---|---|
| `VAL` | `VAL $name value` | parse-time, before the script runs | value = path / pipe-query / literal / position triplet; can reference other `VAL`s (topo-sorted, cycle → error); value cannot start with a DSL keyword |
| `VAR` | `VAR $name @path\|Comp\|field` | runtime, re-read fresh every step | query must start with `@` and have 3 pipe parts; can nest a `VAL`: `VAR $hp @$player\|Health\|current` |
| `INCLUDE` | `INCLUDE file.defs` | before parsing (Phase -1) | loads `Assets/PlaytestDefs/<file>`; recursive, max depth 5; `..` and rooted paths rejected |
| `ALIAS` *(deprecated)* | `ALIAS name /path\|Comp\|field` | parse-time, whole-word (no `$`) | use `VAL $name ...` instead — logs a deprecation warning; kept for backward compat only |

```
VAL $player /Player/Character
VAR $hp @$player|Health|current       # VAL expands inside VAR's query too
ASSERT $hp == 100
INVOKE $player Sword Attack
WAIT_UNTIL $hp < 80 TIMEOUT 5
```

`MOVE`/`TELEPORT` positions accept a *different* `@` grammar — an object's live transform, not a field query: `MOVE TO @/Enemy.position` or `MOVE TO @/Enemy.position + (0,1,0)` (offset parens required, sign `+`/`-`).

**Scene-defined aliases:** entries on the `PlaytestConfig` asset (set up in the Unity Editor) are auto-injected — call `get_aliases` once per session, or read the `--- ALIASES ---` block auto-appended to `get_hierarchy`, to populate `$name` without writing `VAL` yourself.

**Outside the DSL:** any MCP tool argument (not just `run_playtest` scripts) gets whole-value `$name` resolved against the same alias cache (`middleware_alias.py`) — e.g. `get_component(path="$player", type="Health")`. `"$hp"` resolves; `"/prefix/$hp"` does not — define a full-path `VAL` instead.

### Preprocessor

| Command | Syntax | Notes |
|---------|--------|-------|
| `MACRO` | `MACRO name $p1 $p2 ... ` / `END_MACRO` | Reusable block with positional params — whole-word substitution, independent of VAL/VAR |
| `CALL` | `CALL name arg1 arg2` | Expand macro in place (recursive, max depth 10, forward refs OK) |
| `FOR` | `FOR $i IN 0..4` / `END_FOR` | Integer loop unrolled at parse time (v0.90). `$i` expands to current value. Max 10000 iterations. Nested loops supported |
| `PATH_PREFIX` | `PATH_PREFIX /Level1/Zone_A` | Prefix applied to all subsequent `VAL` path aliases (v0.90). First occurrence wins — no override. Trailing `/` stripped automatically |
| `ABORT_ON_FAIL` | `ABORT_ON_FAIL` | Global directive — stop Play Mode on any WAIT_UNTIL timeout |
| `#` | `# comment text` | Line comments, ignored |

```
MACRO check_health $path $expected
  ASSERT $path|HealthComponent|CurrentHealth == $expected
END_MACRO
CALL check_health /Player 100
```

```
# FOR — unrolled at parse time into 5 ASSERT steps
FOR $i IN 0..4
  ASSERT /Slot_$i|Item|count > 0
END_FOR

# PATH_PREFIX — prepend to all VAL path aliases that follow
PATH_PREFIX /Level1/Zone_A
VAL $enemy /Enemy_01          # $enemy expands to /Level1/Zone_A/Enemy_01
```

### Control

| Command | Syntax | Notes |
|---------|--------|-------|
| `TIMESCALE` | `TIMESCALE 3` | Sets Time.timeScale (auto-restored to 1 on finish) |
| `WAIT` | `WAIT 2` | Wait N seconds (real time) |
| `WAIT_UNTIL` | `WAIT_UNTIL query op value [AND\|OR ...] [TIMEOUT N] [ABORT]` | Poll until condition met, default timeout 5s. Cannot mix AND/OR |
| `LOG` | `LOG any text here` | Adds message to report |
| `SECTION` | `SECTION "title"` | Group header in report (divides script into named phases) |
| `DESC` | `DESC "label text"` | Label the next step in the report (consumed, not emitted) |

```
WAIT_UNTIL /Player|HP|value > 0 AND /Enemy|HP|value == 0 TIMEOUT 10   # compound (AND/OR, not mixed)
WAIT_UNTIL /Enemy|AI|IsPatrolling == true TIMEOUT 5 ABORT              # per-step abort on timeout
```

### Actions

| Command | Syntax | Notes |
|---------|--------|-------|
| `MOVE` | `MOVE TO x,y,z` or `MOVE /path TO x,y,z` | NavMesh move, 15s timeout. Path optional (auto-resolves player). Position accepts `@path.position` |
| `MOVE_PATH` | `MOVE_PATH x1,y1,z1 > x2,y2,z2 [> ...] [TIMEOUT n]` | Multi-waypoint — expands to N sequential MOVE steps |
| `SWEEP_PATH` | `SWEEP_PATH x1,y1,z1 > x2,y2,z2 [> ...] DWELL n` | Move along waypoints, pause `n` seconds at each |
| `TELEPORT` | `TELEPORT /path x,y,z` | Instant position set + Physics.SyncTransforms. Also accepts `@path.position` |
| `INVOKE` | `INVOKE /path CompType MethodName [args]` | Call method via reflection |
| `SET` | `SET /path CompType fieldName value` | Set field/property at runtime |
| `CLICK` / `TAP` | `CLICK /path [WAIT delay]` | Simulate click — tries Button.onClick first, falls back to IPointerClickHandler |

### Assertions & Capture

| Command | Syntax | Notes |
|---------|--------|-------|
| `ASSERT` | `ASSERT query op value [AS "label"]` | Single value assertion. AS suffix = inline label in report |
| `ASSERT_BATCH` | multiline block, see below | Multiple assertions, one pass/fail |
| `ASSERT_NEAR` | `ASSERT_NEAR /pathA /pathB threshold` | Distance between two objects <= threshold |
| `ASSERT_CONSOLE_CLEAN` | `ASSERT_CONSOLE_CLEAN [IGNORE "pat1,pat2"]` | Fail if errors in console |
| `ASSERT_CTA` | `ASSERT_CTA VISIBLE` or `ASSERT_CTA CLICKABLE` | CTA button check (config/tag/name) |
| `ASSERT_CONSERVED` | `ASSERT_CONSERVED SUM q1 + q2 == CONSTANT OVER N` | Sum must not change over N seconds |
| `CAPTURE` | `CAPTURE label query` | Snapshot numeric value for later comparison |
| `ASSERT_CAPTURED` | `ASSERT_CAPTURED label MODE [subOp value]` | `INCREASED`/`DECREASED`/`UNCHANGED`, or `INCREASED_BY op value`/`DECREASED_BY op value` |
| `ASSERT_CHANGED` | `ASSERT_CHANGED $label` | Value differs from what was captured under `$label` (v0.90) |
| `CAPTURE_FRAMES` | `CAPTURE_FRAMES n INTERVAL s CAMERA CamName LABEL label [MODE strip\|list]` | Capture N screenshots at interval (v0.90). n >= 2. Labels the set for ASSERT_FRAMES_* |
| `ASSERT_FRAMES_DIFFER` | `ASSERT_FRAMES_DIFFER label` | Consecutive captured frames differ — motion check (v0.90) |
| `ASSERT_FRAMES_STATIC` | `ASSERT_FRAMES_STATIC label` | All captured frames identical — stability check (v0.90) |
| `WAIT_CAPTURED` | `WAIT_CAPTURED` | Waits until a screenshot is captured (no args) |

Method dispatch via `field(args)`: `WAIT_UNTIL /Inventory|Inventory|HasItem(sword) == true` — reflection invoked with parsed args, zero-arg `MethodName()`.

### GameObject Shorthands (v0.89)

No component name required for built-in GameObject properties:

| Shorthand | Notes |
|-----------|-------|
| `ASSERT /Obj\|activeSelf` | true if object is self-active |
| `ASSERT /Obj\|activeInHierarchy` | true if active in hierarchy |
| `ASSERT /Obj\|tag == Player` | tag string comparison |
| `ASSERT /Obj\|layer == 8` | layer as integer |
| `ASSERT /Obj\|name == Enemy` | object name |

### Virtual Fields (v0.89)

| Virtual field | Resolves to |
|---------------|-------------|
| `/Obj\|Animator\|currentState` | Active clip name (string) |
| `/Obj\|Rigidbody\|speed` | Velocity magnitude (float) |
| `/Obj\|Rigidbody2D\|speed` | Velocity magnitude (float) |

```
ASSERT /Player|activeSelf                         # bool — no == True needed
ASSERT /Player|tag == Player
ASSERT /Enemy|Animator|currentState == Idle
ASSERT /Ball|Rigidbody|speed > 0
```

```
ASSERT_BATCH
  ASSERT /Obj1|Comp|field == val
  ASSERT /Obj2|Comp|field >= val
END
```

```
# Frame capture — motion check
CAPTURE_FRAMES 5 INTERVAL 0.5 CAMERA MainCamera LABEL motion_test
ASSERT_FRAMES_DIFFER motion_test     # fails if object didn't move

# Frame capture — stability check
CAPTURE_FRAMES 3 INTERVAL 0.3 CAMERA UICam LABEL ui_snapshot
ASSERT_FRAMES_STATIC ui_snapshot     # fails if UI changed unexpectedly

# ASSERT_CHANGED — value changed since CAPTURE
CAPTURE $health_before /Player|Health|value
WAIT 2
ASSERT_CHANGED $health_before
```

### Monitoring & Diagnostics

| Command | Syntax | Notes |
|---------|--------|-------|
| `INVARIANT` | `INVARIANT query op value` | Checked EVERY frame until script ends |
| `MONITOR` | `MONITOR name` or `MONITOR STOP` | Start/stop registered monitor |
| `SIMULATE` | `SIMULATE name [DURATION n] [TIMESCALE n] [TARGET "path"] [FREQUENCY n]` | Run registered simulator |
| `SNAPSHOT` | `SNAPSHOT query1,query2,...` | Dump values to report (always shown) |
| `TRACE_FLOW` | `TRACE_FLOW FROM /src TO /dst FIELD fieldName [TIMEOUT n]` | **[UNIMPLEMENTED]** Parsed but always `failed++`. Use ASSERT instead |

## Parsing Phases

1. **-1 INCLUDE:** recursively expand `INCLUDE` (max depth 5, path traversal blocked)
2. **0 MACRO:** collect+remove `MACRO...END_MACRO` blocks
3. **0.5 CALL:** expand `CALL name args` with the macro body (recursive, max depth 10)
4. **0.7 VAL/$sigil:** collect `VAL` defs (topo-sorted, cycle-checked); expand `$name` in every line
5. **0.8 Warnings:** unresolved `$name` (not a VAL, retried as VAR) → non-fatal parse warning
6. **1 ALIAS (deprecated):** collect `ALIAS name value`; whole-word substitution
7. **1.1 VAR:** collect `VAR $name @path|Comp|field` bindings — resolved live at runtime, not here
8. **2 Commands:** dispatch by command name; `#`/blank lines skipped; case-insensitive; `DESC` becomes the next step's label; `ABORT_ON_FAIL` becomes the global directive

## Examples

### Cargo delivery + money (VAL)
```
run_playtest script="
TIMESCALE 3
VAL $cargo /Inventory|Storage|SlotCount
VAL $money /Money|Currency|Value
CAPTURE start_money $money
CAPTURE start_cargo $cargo
MOVE TO 5,0,-3
WAIT_UNTIL $cargo > 0 TIMEOUT 10
ASSERT_CAPTURED start_cargo INCREASED
MOVE TO -2,0,4
WAIT_UNTIL $cargo == 0 TIMEOUT 10
ASSERT_CAPTURED start_money INCREASED_BY >= 50
ASSERT_CTA VISIBLE
ASSERT_CONSOLE_CLEAN
TIMESCALE 1
"
```

### Combat check with sections (VAR + INCLUDE)
Assumes `Assets/PlaytestDefs/combat.defs` contains `VAL $player /Player/Character`.
```
run_playtest script="
INCLUDE combat.defs
VAR $hp @$player|Health|current
TIMESCALE 3
ABORT_ON_FAIL
SECTION \"Setup\"
ASSERT_BATCH
  ASSERT $hp == 100
  ASSERT /Money|Currency|Value >= 0
END
SECTION \"Combat\"
DESC \"attack the boss\"
INVOKE $player Sword Attack
WAIT_UNTIL $hp < 100 TIMEOUT 5 ABORT
ASSERT_CONSOLE_CLEAN
"
```

**Evidence:** `run_playtest` output IS the CLAIM/EVIDENCE proof — e.g. `ASSERT_CAPTURED c0 INCREASED -- PASS (was=0, now=18)` is the claim and its evidence in one line. See `playmode-verification.md` for the full CLAIM/EVIDENCE/VERDICT protocol.

## File-based Execution

```
run_playtest(path="Playtests/farm_pipeline_early.playtest")       # single file
run_playtest_suite(paths="Playtests/*.playtest")                  # glob, comma list, or newline list
lint_playtest(script="...")                        # validate DSL without executing
lint_playtest_suite(paths="Playtests/*.playtest")                 # validate matching files
```

Store scripts as project-relative `.playtest` files under `Playtests/` and run via `run_playtest(path=...)` for regression. `run_playtest*` requires Play Mode; `lint_playtest` / `lint_playtest_suite` are read-only and do not. Run lint before execution to catch parse errors cheaply.

## Anti-Patterns

```
# BAD: WAIT 10 (hope) + ASSERT cargo > 0        GOOD: WAIT_UNTIL cargo > 0 TIMEOUT 15 (fails fast)
# BAD: "I see items in cargo" (screenshot)      GOOD: ASSERT cargo == 18 (exact numeric proof)
```
