# Runtime Tools

Inspect and exercise the live game while Unity is in Play Mode. Enter Play Mode
before calling a runtime-only tool, stop after collecting evidence, and remember
that runtime state normally disappears when Play Mode ends.

```python
await editor(action="play")
try:
    state = await query_state(
        queries="/Player|Health|CurrentHealth,/Player|Rigidbody|speed"
    )
finally:
    await editor(action="stop")
```

Use [Object Tools](objects.md) for serialized Edit Mode authoring. Use public
runtime methods or the Playtest `SET` step for intentional Play Mode changes.

## `run_playtest`

Runs one deterministic Playtest DSL script, inline or from a `.playtest` path. The
[Playtest Guide](../features/playtest.md) is the canonical reference for syntax,
parameters, hooks, aliases, evidence, and examples.

```python
result = await run_playtest(
    script=(
        "ASSERT /Player|Health|CurrentHealth > 0\n"
        "ASSERT_CONSOLE_CLEAN"
    ),
    timeout=30,
)
```

`script` and `path` are mutually exclusive. Long reports may be replaced by a
compact generated summary when optional LLM sampling is enabled. Keep that
behavior in mind when a workflow requires the verbatim per-step report.

## `run_playtest_suite`

Runs multiple `.playtest` files and returns a pass/fail matrix. It accepts either
`pattern` (a glob or comma/newline-separated project-relative paths) or
`suite_path` (an absolute `.suite` file path), never both.

```python
result = await run_playtest_suite(
    pattern="Playtests/*.playtest",
    auto_play=True,
    stop_on_fail=True,
    stop_after=True,
)
```

See [Run a suite](../features/playtest.md#run_playtest_suite) for restart and
timeout behavior.

## Playtest linting and aliases

Use `lint_playtest` or `lint_playtest_suite` before entering Play Mode. Alias
validation, import, and export are documented with the complete workflow in
[Playtest aliases](../features/playtest.md#aliases-substitution).

```python
lint = await lint_playtest(path="Playtests/combat.playtest")
suite_lint = await lint_playtest_suite(pattern="Playtests/*.playtest")
```

For scene-reference preflight, use
[`resolve_scene_refs`](diagnostics.md#resolve_scene_refs) and
[`lint_scene_refs`](diagnostics.md#lint_scene_refs).

## `runtime_snapshot`

Reads every live object containing a component type. Narrow by object-name
substring and strip defaults when the result is large.

```python
enemies = await runtime_snapshot(
    type="EnemyController",
    name="Boss",
    compress=True,
)
```

`component` can request a different component to serialize; when omitted, it
defaults to `type`.

## `query_state`

Reads several live values in one call. `queries` is a comma-separated list of
`path|component|field_or_method` triplets.

```python
state = await query_state(
    queries=(
        "/Player|Health|CurrentHealth,"
        "/Enemy|EnemyAI|IsAlerted,"
        "/Player|Rigidbody|speed"
    )
)
```

Use one call for related values so the snapshot is as close to one moment as the
bridge permits.

## `invoke_method`

Calls a component method through runtime reflection.

```python
await invoke_method(
    path="/Enemy",
    component="HealthComponent",
    method="TakeDamage",
    args="10",
)

await invoke_method(
    path="/Weapon",
    component="WeaponController",
    method="FireAt",
    args="10,0,5",
)
```

Arguments are comma-separated and converted to the selected overload's parameter
types. A `Vector3` consumes three comma-separated values. If overloads remain
ambiguous, expose a uniquely named project method rather than relying on selection
order.

## `wait_until`

Polls one runtime field/property until its string value matches, or differs when
`negate=True`.

```python
await wait_until(
    path="/Enemy",
    component="EnemyAI",
    field="IsDead",
    value="true",
    timeout=10,
    abort_on_fail=False,
)
```

The direct tool supports equality only. For numeric comparisons, compound waits,
aliases, and fail-fast DSL behavior, see [Wait Conditions](../features/wait-conditions.md).

## `move_to`

Moves a character through the project-configured movement method and waits for its
completion callback.

```python
result = await move_to(path="/Player", position="10,0,5", timeout=20)
```

Configure `moveComponent` and `moveMethod` in `PlaytestConfig`. Without that
configuration, the tool searches for a public method with the signature
`(Vector3, Action<bool>)`. If neither is available, it returns an explanatory
error rather than teleporting the object.

## `test_step`

Combines a movement step with before/after state reads and a console check.

```python
report = await test_step(
    path="/Player",
    position="10,0,0",
    checks_before="/Player|Health|CurrentHealth",
    checks_after="/Player|Health|CurrentHealth",
    wait_after=0.5,
    timeout=20,
)
```

For reusable acceptance tests, prefer a `.playtest` script; `test_step` is a
compact interactive probe.

## Intent tools

`animator_intent`, `vfx_intent`, and `ui_intent` are direct-only authoring tools,
not runtime state readers. Their presets, sampling requirements, preview behavior,
and limitations are documented in the [Intent Tools Guide](../features/intent-tools.md).

## Targeted debugging

### `debug`

Collects a small batch of scene context based on a symptom. It does not itself
diagnose compile/reload state.

```python
context = await debug(
    symptom="enemy does not move",
    path="/Enemy",
)
```

Pass `gather="inspect,get_console"` only when you need to override the automatic
selection. Capture visual evidence separately, because `screenshot` writes a
project-local PNG:

```python
image = await screenshot(camera="single_view", path="/Enemy")
```

Use [`diagnose`](diagnostics.md#diagnose) for compile or domain problems.

### `debug_animator` and `debug_physics`

Both require Play Mode and read one target without changing it:

```python
animator_state = await debug_animator(path="/Player")
physics_state = await debug_physics(path="/Player", radius=8)
```

`debug_animator` reports layers, parameters, transitions, and the current clip.
`debug_physics` reports Rigidbody, collider, contact, and nearby-body context.

## Performance and memory

### `get_frame_stats`

Returns the current Play Mode FPS/frame-time, rendering, and memory snapshot.

### `profile`

Captures a Play Mode profiling session.

```python
started = await profile(action="start", mode="burst", duration=5)
# Poll profile(action="status") until it reports idle, then use the session ID
# returned by start/list_sessions.
report = await profile(action="analyze", session="p1", focus="cpu")
```

Actions are `start`, `stop`, `status`, `analyze`, `compare`, and `list_sessions`.
Supported start modes are `burst` (auto-stop after `duration`) and `manual`
(explicit `stop`). The wrapper accepts `triggered`, but the current Unity recorder
returns `err:triggered mode not yet implemented`; do not build automation around it.
`analyze` requires `session`. `compare` requires the newer `session` and the
reference `compare_with` IDs. Sessions are in memory and are cleared by a domain
reload.

### `get_memory`

Returns an Editor or Play Mode memory snapshot. `include` accepts `all`,
`textures`, `meshes`, `audio`, or `gc`.

### `get_metrics`

Returns Python MCP telemetry as text or JSON. `reset=True` atomically returns the
current snapshot and clears the counters, so use reset only when that side effect
is intended.

## Watches

`watch` manages Play Mode polling for a field and can log or pause when a condition
is met.

```python
watch_id = await watch(
    action="add",
    path="/Player",
    component="Health",
    field="CurrentHealth",
    condition="< 20",
    trigger_action="pause",
    interval_ms=500,
)

watches = await get_watches()
await watch(action="remove", watch_id=watch_id)
```

Other actions are `clear` and `reset`; `remove` and `reset` require `watch_id`.

## `snapshot`

Stores an in-memory object inspection under a label and can compare a later
capture with it.

```python
await snapshot(path="/Player", label="before")
# ...exercise the game...
diff = await snapshot(path="/Player", label="after", compare="before")
```

Snapshots live in the Python server process and are lost when it restarts. They
also include recent console text in the comparison. Use screenshot baselines for
visual evidence and Unity/source-control checkpoints for durable recovery.

## Related workflows

- [Playtest Guide](../features/playtest.md) — canonical DSL and suite reference.
- [Wait Conditions](../features/wait-conditions.md) — direct and DSL waits.
- [NUnit Test Tools](tests.md) — durable Unity Test Framework runs.
- [Diagnostics](diagnostics.md) — compile, console, and reference evidence.
- [Generated Tool Schema](../tools-schema/index.md) — exhaustive signatures and defaults.
