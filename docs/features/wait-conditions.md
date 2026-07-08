# Wait Conditions

Poll game state until a condition is true (or times out). Works in Play Mode only.

## Overview

Wait conditions let you synchronize your test script with game events — enemy dies, door opens, animation completes — without arbitrary `WAIT` sleeps. The engine polls at 50ms intervals until the field matches or the timeout expires.

Two entry points: **`WAIT_UNTIL`** (DSL inside `run_playtest`) and **`wait_until`** (direct MCP tool). See [Playtest Guide](playtest.md) for the overall DSL.

---

## Built-in Conditions

All comparison operators from the [Playtest Guide](playtest.md) apply (`==`, `!=`, `>`, `<`, `>=`, `<=`, `~=`). `WAIT_UNTIL` adds one extra operator: `contains` (substring match on string fields).

**Syntax (DSL):**
```
WAIT_UNTIL path|Component|field op value [TIMEOUT n] [ABORT]
```

**Examples:**
```
WAIT_UNTIL /Enemy|AI|IsDead == true TIMEOUT 10
WAIT_UNTIL /Player|PlayerController|Health < 50
WAIT_UNTIL /Door|Door|State contains open TIMEOUT 8
WAIT_UNTIL /Enemy|AI|IsPatrolling == true TIMEOUT 5 ABORT
```

**Direct tool call:**
```python
await wait_until("/Enemy", "AI", "IsDead", "true", timeout=10.0)
await wait_until("/Player", "PlayerController", "Health", "0", negate=True)  # wait until != 0
```

---

## Combining Conditions

Combine multiple conditions in a single `WAIT_UNTIL` with `AND` or `OR`. Cannot mix both in the same line.

**AND — all conditions must be true:**
```
WAIT_UNTIL /Player|HP|value > 0 AND /Enemy|HP|value == 0 TIMEOUT 10
```

**OR — at least one condition must be true:**
```
WAIT_UNTIL /Door|Door|IsOpen == true OR /Player|Player|IsDead == true TIMEOUT 15
```

**Poll interval:** 50ms. All sub-conditions evaluated on each tick.

---

## Custom Conditions

Use method dispatch to call any public C# method as a condition:

```
WAIT_UNTIL /Inventory|Inventory|HasItem(sword) == true TIMEOUT 5
WAIT_UNTIL /Player|Movement|DistanceTo(5,0,3) < 1.0 TIMEOUT 8
WAIT_UNTIL /Grid|GridController|IsCellFree(3,5) == true
```

**Syntax:** `path|Component|MethodName(arg1,arg2)` — method invoked via reflection on each poll tick.

A Vector3 argument reads three comma-separated values. Zero-arg methods: `MethodName()`.

---

## Timeout and Error Handling

**Default timeout:** 5 seconds per `WAIT_UNTIL` step.

**Override per step:**
```
WAIT_UNTIL /Boss|BossAI|Phase == 2 TIMEOUT 30
```

**Global override** — all steps inherit script timeout:
```python
await run_playtest(script, timeout=120.0)
```

**On timeout:**
| Mode | Behavior |
|------|----------|
| Default | Step marked FAIL, script continues |
| `ABORT` token | Play Mode stops immediately |
| `ABORT_ON_FAIL` directive | All timeouts stop Play Mode |

```
# Stop on first timeout
ABORT_ON_FAIL
WAIT_UNTIL /Enemy|AI|IsDead == true TIMEOUT 10

# Stop only this step
WAIT_UNTIL /Player|HP|value > 0 TIMEOUT 5 ABORT
```

**Python buffer:** `wait_until` tool adds 5s to the Unity timeout to prevent the Python side from timing out before Unity responds.

---

## Examples

### Wait for enemy to die after combat

```
SECTION "Combat"
INVOKE /Enemy HealthComponent TakeDamage 100
WAIT_UNTIL /Enemy|HealthComponent|CurrentHealth == 0 TIMEOUT 5
ASSERT /Enemy|AI|IsDead == true
ASSERT_CONSOLE_CLEAN
```

### Gate on multiple conditions (level transition)

```
SECTION "Level Complete"
WAIT_UNTIL /Player|Score|value >= 500 AND /LevelTimer|Timer|IsRunning == false TIMEOUT 20
ASSERT /UI/WinScreen|Canvas|enabled == true
```

### Fail fast on unexpected death

```
ABORT_ON_FAIL
SECTION "Boss Fight"
INVOKE /Boss BossController StartPhase2
WAIT_UNTIL /Boss|BossController|Phase == 2 TIMEOUT 15 ABORT
ASSERT /Boss|BossController|IsVulnerable == true
WAIT_UNTIL /Boss|BossController|IsDead == true OR /Player|HP|value == 0 TIMEOUT 60
ASSERT /Player|HP|value > 0 AS "player must survive boss"
```

---

## Tips

- Start with `TIMEOUT 30` on first run — tighten after you know the baseline.
- Use `ABORT` on critical gates to stop the entire playtest early.
- Combine AND conditions for multi-field checks instead of chaining WAIT_UNTIL lines.

---

**See also:** [Playtest Guide](playtest.md) — full DSL, fire-and-forget pattern, report compression.
