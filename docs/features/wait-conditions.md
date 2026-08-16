# Wait Conditions

Wait for game state instead of guessing how long an animation, spawn, or state
transition will take. Both entry points below require Play Mode:

- `wait_until` is a direct equality/inequality wait for one runtime value.
- `WAIT_UNTIL` is the Playtest DSL form and supports comparisons, compound
  conditions, aliases, and per-step failure behavior.

See the [Playtest Guide](playtest.md) for the complete DSL.

## Direct `wait_until`

The direct tool compares one field or property with a string value. Comparison is
case-insensitive; `negate=True` waits until the value is different.

```python
await wait_until(
    path="/Enemy",
    component="EnemyAI",
    field="IsDead",
    value="true",
    timeout=10.0,
)

await wait_until(
    path="/Door",
    component="DoorController",
    field="State",
    value="Closed",
    negate=True,
    timeout=8.0,
)
```

Nested member paths and reflected method calls are supported by the runtime reader:

```python
await wait_until(
    path="/Inventory",
    component="Inventory",
    field="HasItem(sword)",
    value="true",
    timeout=5.0,
)
```

The Unity side polls approximately every 0.1 seconds. The Python transport timeout
adds a small buffer to the requested Unity timeout. Set `abort_on_fail=True` only
when a timeout should also stop Play Mode.

`wait_until` does not implement `>`, `<`, or compound conditions. Use the DSL for
those cases.

## Playtest `WAIT_UNTIL`

The query format is `path|Component|field`, followed by an operator and value:

```text
WAIT_UNTIL /Enemy|EnemyAI|IsDead == true TIMEOUT 10
WAIT_UNTIL /Player|Health|CurrentHealth > 0 TIMEOUT 5
WAIT_UNTIL /Door|DoorController|State contains Open TIMEOUT 8
```

Supported operators are `==`, `!=`, `>`, `<`, `>=`, `<=`, and `contains`.
Ordering operators require numeric values. String equality is case-insensitive;
`contains` is case-sensitive.

The same runtime reader supports nested fields and methods:

```text
WAIT_UNTIL /Inventory|Inventory|HasItem(sword) == true TIMEOUT 5
WAIT_UNTIL /Player|Movement|DistanceTo(5,0,3) < 1 TIMEOUT 8
```

## Combine conditions

Use either `AND` or `OR` on one line. Mixing the two operators in one
`WAIT_UNTIL` is rejected.

```text
WAIT_UNTIL /Player|Health|CurrentHealth > 0 AND /Enemy|Health|CurrentHealth == 0 TIMEOUT 10
WAIT_UNTIL /Door|DoorController|IsOpen == true OR /Player|Health|IsDead == true TIMEOUT 15
```

For reusable paths, define aliases in a `.defs` file or `PlaytestConfig` and use
their `$name` in the query. Boolean aliases also support shorthand:

```text
WAIT_UNTIL $level_loaded
WAIT_UNTIL !$is_paused
```

## Timeout and failure behavior

Without `TIMEOUT`, the runner uses the script's default wait timeout, which is five
seconds unless `SET_DEFAULT_TIMEOUT` changes it. The `timeout` argument to
`run_playtest` limits the complete script; it does not replace each step's timeout.

```text
SET_DEFAULT_TIMEOUT 8
WAIT_UNTIL /Enemy|EnemyAI|IsReady == true
WAIT_UNTIL /Boss|BossAI|Phase == 2 TIMEOUT 30 ABORT
```

- A normal timeout records a failed step and continues the script.
- `ABORT` on the step records the failure and stops Play Mode.
- `ABORT_ON_FAIL` is global: any failed step or automatic console failure skips
  every remaining step, including TEARDOWN. The per-step `ABORT` token above is
  narrower and stops Play Mode only when that `WAIT_UNTIL` times out.

Use `ABORT` only for a gate after which later steps cannot provide useful evidence.
Finish successful paths with `ASSERT_CONSOLE_CLEAN`.

## Example

```text
SECTION "Combat"
INVOKE /Enemy HealthComponent TakeDamage 100
WAIT_UNTIL /Enemy|HealthComponent|CurrentHealth == 0 TIMEOUT 5 ABORT
ASSERT /Enemy|EnemyAI|IsDead == true
ASSERT_CONSOLE_CLEAN
```

Prefer a state-based wait over `WAIT n` whenever the state can be observed. Start
with a realistic upper bound, then tighten it only after measuring normal runs.
