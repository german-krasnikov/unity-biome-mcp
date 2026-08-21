# Playtest Guide

Run deterministic gameplay scenarios with assertions and state snapshots.

## Overview

Playtesting executes a script of gameplay steps: move, wait, assert state, check console. Results are compressed for readability.

## Quick Start

```python
script = """MOVE /Player TO 5,0,0
WAIT 1.0
ASSERT /Player|Health|hp > 0
ASSERT_CONSOLE_CLEAN"""

await editor(action="play")
try:
    result = await run_playtest(script=script, timeout=30.0)
finally:
    await editor(action="stop")

# PLAYTEST: 4/4 (1.1s) OK
```

`run_playtest` requires Play Mode. Successful step details are removed from the
compact result; failures, snapshots, and logs remain.

<span id="run_playtest-parameters"></span>

## Run one scenario

Pass either inline `script` text or a saved `.playtest` `path`, never both.
Use `defs` for reusable `VAL` definitions, `fresh=True` to reload the active
scene before the first step, and `snapshot_on_failure=True` when a failure must
include current alias values and recent console errors.

```python
# From file
await run_playtest(path="Assets/Playtests/combat.playtest")

# With defs and fresh start
await run_playtest(
    script="ASSERT $player|Health|hp > 0",
    defs="VAL $player /Player",
    fresh=True,
    snapshot_on_failure=True,
)
```

<span id="run_playtest_suite"></span>

## Run a suite

Run multiple `.playtest` files sequentially with a compact pass/fail matrix.
Provide either `pattern` (a glob or comma/newline-separated paths) or
`suite_path` (a file listing one scenario per line), never both. Use
`restart_between=True` only when each case needs a fresh Play Mode session; a
failed stop or start is a suite failure, not a best-effort warning.

```python
await run_playtest_suite(pattern="Playtests/*.playtest", stop_on_fail=True)
# → SUITE: 5/6 passed (42s)
#   ✓ movement.playtest (3s)
#   ✗ combat.playtest (8s) — ASSERT Health > 0 FAIL
#   ...
```

An empty match is a failure: a passing suite always reports a non-zero exact
ratio such as `SUITE: 5/5 passed`. See the
[generated schema](../tools-schema/index.md#run_playtest_suite) for every option
and default.

## lint_playtest

Static DSL validation without executing. Checks: unresolved `$alias`, deprecated `ALIAS` keyword, unknown `CALL` targets, mixed AND/OR, missing `ASSERT_CONSOLE_CLEAN`.

```python
await lint_playtest(script="ASSERT $unknown|Health > 0")
# → WARN:1: unresolved alias $unknown

await lint_playtest(path="Assets/Playtests/combat.playtest")
# → OK
```

`lint_playtest_suite(pattern=..., suite_path=...)` lints multiple files at once.

## Setup and Teardown

Use `SETUP` and `TEARDOWN` blocks to organize initialization and cleanup.

```text
SETUP
  # Runs before main steps
  TELEPORT /Player 0,0,0
  ASSERT /Player|Health == 100
SETUP_END

# Main test steps
ASSERT /Player|Score > 0

TEARDOWN
  # Runs after normal completion or a non-aborting failure
  ASSERT_CONSOLE_CLEAN
  LOG Test complete
TEARDOWN_END
```

**Behavior:**
- **SETUP** runs before main steps. If any SETUP step fails, the runner skips remaining SETUP steps and jumps directly to TEARDOWN (if present). Main steps do NOT execute.
- **TEARDOWN** runs after normal completion and non-aborting setup or main-step
  failures. A global `ABORT_ON_FAIL`, a per-step timeout `ABORT`, an external
  Play Mode stop, a global timeout, or an unhandled runner error can end the run
  before TEARDOWN completes. Do not use it as the only restoration mechanism.
- Use SETUP for one-time initialization (spawn objects, set state).
- Use TEARDOWN for verification of final state and log collection.

## DSL Quick Reference

### Core Commands

| Command | Purpose | Example |
|---------|---------|---------|
| `MOVE` | Walk to position | `MOVE /Player TO 5,0,0` |
| `MOVE_PATH` | Walk through waypoints | `MOVE_PATH 0,0,0 > 5,0,0 > 10,0,0 TIMEOUT 15` |
| `TELEPORT` | Instant move | `TELEPORT /Player 0,0,0` |
| `SET_ACTIVE` | Toggle active | `SET_ACTIVE /Boss false` |
| `WAIT` | Sleep | `WAIT 2.0` |
| `WAIT_UNTIL` | Poll condition | `WAIT_UNTIL /Player\|Health == 100 TIMEOUT 10` |
| `WAIT_CAPTURED` | Poll captured value | `WAIT_CAPTURED hp_before INCREASED TIMEOUT 10` |
| `SET` | Set runtime field | `SET /Player Health value 50` |
| `INVOKE` | Call method | `INVOKE /Enemy EnemyAI AttackPlayer` |
| `CLICK` / `TAP` | Click UI object | `CLICK /Canvas/StartButton WAIT 0.5` |

### Assertions

| Command | Purpose | Example |
|---------|---------|---------|
| `ASSERT` | Test condition | `ASSERT /Player\|Health == 100` |
| `ASSERT_BATCH` | Multiple asserts | `ASSERT_BATCH ... END` |
| `ASSERT_NEAR` | Distance check | `ASSERT_NEAR /Player /Enemy 5.0` |
| `ASSERT_CTA` | CTA visibility or interactivity | `ASSERT_CTA CLICKABLE` |
| `ASSERT_CONSOLE_CLEAN` | No errors | `ASSERT_CONSOLE_CLEAN IGNORE "warning"` |
| `ASSERT_CONSERVED` | Sum constant check | `ASSERT_CONSERVED SUM /A\|val + /B\|val == CONSTANT OVER 3` |
| `ASSERT_CAPTURED` | Compare snapshot | `ASSERT_CAPTURED health_before INCREASED` |
| `ASSERT_CHANGED` | Value changed since capture | `ASSERT_CHANGED $label` |
| `ASSERT_ONE_ACTIVE` | Exactly one active | `ASSERT_ONE_ACTIVE /Cam_Intro /Cam_Menu /Cam_Game` |
| `ASSERT_FRAMES_DIFFER` | Captured frames differ | `ASSERT_FRAMES_DIFFER my_label` |
| `ASSERT_FRAMES_STATIC` | Captured frames identical | `ASSERT_FRAMES_STATIC my_label` |

### Capture & Monitor

| Command | Purpose | Example |
|---------|---------|---------|
| `CAPTURE` | Save value | `CAPTURE health_before /Player\|Health` |
| `CAPTURE_FRAMES` | Capture N screenshots | `CAPTURE_FRAMES 5 INTERVAL 0.2 CAMERA game LABEL anim` |
| `SNAPSHOT` | Capture state | `SNAPSHOT /Player\|Health` |
| `MONITOR` | Watch value over time | `MONITOR /Player\|Health` |
| `INVARIANT` | Always true check | `INVARIANT /Player\|Health > 0` |

### Flow Control

| Command | Purpose | Example |
|---------|---------|---------|
| `SECTION` | Label a group of steps | `SECTION "Combat Phase"` |
| `DESC` | Label the next step | `DESC "Check initial health"` |
| `LOG` | Print to results | `LOG Starting combat test` |
| `TIMESCALE` | Time speed | `TIMESCALE 0.5` |
| `SIMULATE` | Run simulation | `SIMULATE physics DURATION 2.0 TIMESCALE 1.0` |
| `TRACE_FLOW` | Trace value flow | `TRACE_FLOW FROM /A TO /B FIELD Health` |

### Compound Commands (parse-time expansion)

| Command | Purpose | Example |
|---------|---------|---------|
| `SWEEP_PATH` | Move along path with dwell | See below |
| `COMPLETE_PURCHASE` | Invoke purchase + wait for expected state | `COMPLETE_PURCHASE $buy_gate EXPECT` followed by expected aliases and `TIMEOUT` |
| `INVOKE_REPEAT` | Invoke N times | `INVOKE_REPEAT 3 /Enemy Health TakeDamage 10` |

### Directives (not emitted as steps)

| Directive | Purpose | Example |
|-----------|---------|---------|
| `VAL` | Path or const alias | `VAL $player /Player` |
| `VAR` | Runtime alias (resolves live) | `VAR $hp @/Player\|Health\|value` |
| `MACRO` / `END_MACRO` | Define reusable block | `MACRO check_health ... END_MACRO` |
| `CALL` | Invoke a macro | `CALL check_health` |
| `INCLUDE` | Import definitions file | `INCLUDE path/to/file.defs` |
| `FOR` / `END_FOR` | Loop with range | `FOR $i IN 0..5 ... END_FOR` |
| `ABORT_ON_FAIL` | Stop after the first failed step or automatic console failure; remaining steps, including TEARDOWN, are skipped | `ABORT_ON_FAIL` |
| `SET_DEFAULT_TIMEOUT` | Default timeout for steps | `SET_DEFAULT_TIMEOUT 10` |
| `PATH_PREFIX` | Prefix for all paths | `PATH_PREFIX /Level1` |
| `COMMENT` / `END_COMMENT` | Block comment | `COMMENT ... END_COMMENT` |

## SWEEP_PATH Example

```
SWEEP_PATH /Player DWELL 0.5
  0,0,0 > 5,0,0 > 10,0,0
UNTIL /Player|Trigger|activated == true TIMEOUT 10
```

Expands at parse time to Move+Wait per waypoint, then a WaitUntil.

## Aliases & Substitution

Define once, use everywhere. Sigil syntax is `$name` (no curly braces).

```
VAL $spawn 100,0,0
VAL $alive true

TELEPORT /Player $spawn
ASSERT /Player|IsAlive == $alive
```

Three alias types:

| Type | Syntax | Behavior |
|------|--------|----------|
| Path alias | `VAL $name /path\|Comp\|field` | Expands at parse time to the path string |
| Const alias | `VAL $name some_literal` | Expands at parse time to the literal value |
| Runtime alias | `VAR $name @/path\|Comp\|field` | Resolves live value each step |

Aliases work in `batch` and all direct MCP tools. Suffix preserved: `$alias|Comp|field` expands to `expanded-path|Comp|field`.

Use `INCLUDE path/to/file.defs` to import alias definitions from external files.

## Path Special Characters

Handle literal slashes and backslashes in GameObject names using backslash escaping or bracket protection.

**Escaping Rules:**
- `\/` — literal forward slash in the GameObject name
- `\\` — literal backslash in the GameObject name
- `[Name/With/Slashes]` — bracket protection (entire segment as one path component, no escaping needed)

**Examples:**
```
# GameObject named "Day/Night"
ASSERT /Day\/Night|Health|hp == 100

# GameObject named "Folder\Path" (Windows-style)
ASSERT /Folder\\Path|Component|field == value

# Using brackets for "Zone A/Zone B"
ASSERT /[Zone A/Zone B]/Child|Comp|field == value
```

`GetPath(go)` round-trips through `FindObject(path)` only when every hierarchy
segment name identifies a unique child. Duplicate root or sibling names make a
text path ambiguous; use the transient `$HEX` entity reference reported by the
ambiguity response for the current Editor process instead.

## GameObject Property Shorthands

Assert on GameObject properties without specifying a component:

```
ASSERT /Player|activeSelf
ASSERT /Player|activeInHierarchy
ASSERT /Player|tag == Player
ASSERT /Player|layer == 0
ASSERT /Player|name == Player
```

Bool fields like `activeSelf` don't need `== true` — bare ASSERT is sufficient.

## Bool Value Aliases

When setting or comparing bool values, use common bool spellings — all normalize to Unity's serialized format automatically:

| Input | Normalizes to |
|-------|---|
| `true`, `yes`, `on` | `True` |
| `false`, `no`, `off` | `False` |

Works in `SET_ACTIVE`, `SET`, and all assertions:

```
SET_ACTIVE /Player true      # or: yes, on
SET /NPC Enabled enabled no  # sets to False
ASSERT /Door|Locked == yes   # equivalent to == True
```

All inputs are case-insensitive (`TRUE`, `Yes`, `OFF` all work).

## Virtual Fields

Synthetic fields on well-known components:

| Component | Field | Returns |
|-----------|-------|---------|
| `Animator` | `currentState` or `stateName` | Active clip name |
| `Rigidbody` | `speed` | Velocity magnitude |
| `Rigidbody2D` | `speed` | Velocity magnitude |

```
ASSERT /Enemy|Animator|currentState == Idle
ASSERT /Ball|Rigidbody|speed > 0
```

## FOR Loops

```
FOR $i IN 0..5
  ASSERT /Slot_$i|activeSelf
END_FOR
```

Range is exclusive (`0..5` = 0,1,2,3,4). Max 10000 iterations.

## Macros

```
MACRO check_health
  ASSERT /Player|Health|value > 0
  ASSERT_CONSOLE_CLEAN
END_MACRO

CALL check_health
```

Macros expand at parse time. Nested macros are not supported.

## Full Example: Combat Test

```python
script = """
LOG Starting combat scenario
SECTION "Setup"

# Snapshot initial state
CAPTURE initial_health /Player|Health|value

# Trigger combat
INVOKE /Enemy EnemyAI AttackPlayer
WAIT_CAPTURED initial_health DECREASED TIMEOUT 5

# Verify damage
SECTION "Verification"
ASSERT_CAPTURED initial_health DECREASED
ASSERT_CONSOLE_CLEAN IGNORE "test_warning"
ASSERT /Player|Health|value > 0

LOG Combat completed
SNAPSHOT /Player|Health
"""

await run_playtest(script=script, timeout=60.0)
```

## Comparison Operators

| Op | Meaning | Example |
|----|---------|---------|
| `==` | Equals | `Health == 50` |
| `!=` | Not equals | `Status != dead` |
| `>` | Greater | `Score > 100` |
| `<` | Less | `Health < 50` |
| `>=` | Greater-equal | `Distance >= 2.0` |
| `<=` | Less-equal | `Time <= 10.0` |
| `contains` | Substring match | `Name contains Player` |

## Common Patterns

**Before/after snapshots:**
```
CAPTURE $hp /Player|Health|value
SET /Player Health value 25
ASSERT_CHANGED $hp
```

**Frame comparison (animation playing):**
```
CAPTURE_FRAMES 5 INTERVAL 0.1 LABEL walk_anim
ASSERT_FRAMES_DIFFER walk_anim
```

**Exclusive camera check:**
```
ASSERT_ONE_ACTIVE /Cam_Intro /Cam_Menu /Cam_Game
```

**Physics conservation check:**
```
MONITOR /Player|Velocity
SIMULATE physics DURATION 3.0
ASSERT /Player|Position != (0,0,0)
```

## Timeout & Performance

| Config | Default | Notes |
|--------|---------|-------|
| Script timeout | 120s | Total execution time |
| MOVE timeout | 15s | Per movement command |
| WAIT_UNTIL timeout | 5s | Per poll condition |
| SET_DEFAULT_TIMEOUT | custom | Overrides per-step default |
| SIMULATE duration | per DURATION | TIMESCALE defaults to 1.0 |

## Error Handling

| Result | Meaning | Example |
|--------|---------|---------|
| PASS | Assertion true | `ASSERT Health > 0 — PASS` |
| FAIL | Assertion false | `ASSERT Health > 100 — FAIL` |
| ERR | Exception | `ASSERT NonExistent/Field — ERR` |
| TIMEOUT | Deadline exceeded | `WAIT_UNTIL X timeout=5 — TIMEOUT` |

Use `abort_on_fail=True` or the global `ABORT_ON_FAIL` directive to stop after
the first `FAIL`, `ERR`, `TIMEOUT`, or automatic `CONSOLE_ERR`. This skips every
remaining step, including TEARDOWN. A per-step `ABORT` applies only to that
`WAIT_UNTIL` timeout and stops Play Mode.

Use `snapshot_on_failure=True` to capture alias values and console errors on failure.

## Console Filtering

Ignore known warnings:
```
ASSERT_CONSOLE_CLEAN IGNORE "DeprecationWarning", "test_info"
```

## Report Compression

When sampling is enabled, long reports may be summarized to keep the result
compact:

```
[Compressed] 24/25 passed
  [3] ASSERT Player/Health > 0 — FAIL
  [15] WAIT_UNTIL enemy_dead timeout=5 — TIMEOUT
```

---

**See also:** [Runtime Tools](../tools/runtime.md) for multi-step verification.
