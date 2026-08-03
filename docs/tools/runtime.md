# Runtime & PlayTest Tools

Execute methods, modify values at runtime, and run automated scenarios. Runtime mutation and Playtest execution require Play Mode; linting and reference validation do not.

## run_playtest

Execute a Play Mode test scenario using the Playtest DSL. Deterministic step-by-step assertions.

**Parameters:**
- `script` (string, optional) — DSL script (mutually exclusive with `path`; at least one required)
- `path` (string, optional) — Assets-relative or project-root-relative path to a `.playtest` file (mutually exclusive with `script`)
- `timeout` (float, default=120.0) — Max seconds for entire test
- `abort_on_fail` (bool, default=false) — Stop Play Mode on step timeout
- `defs` (string, optional) — Inline VAL definitions (`name path|comp|field` per line), prepended to script
- `snapshot_on_failure` (bool, default=false) — On assertion/timeout failure, appends current alias values and recent console errors
- `fresh` (bool, default=false) — Reload the active scene before the first step

**Output:** A compact `PLAYTEST: X/Y (...) OK` line on success. Failures,
snapshots, and logs remain in the report. Reports over 300 characters may be
summarized when LLM Sampling is enabled.

**Example:**

```python
result = await run_playtest(
    script="ASSERT /Player|Health|hp > 0\nASSERT_CONSOLE_CLEAN",
    timeout=30,
)
```

The [Playtest Guide](../features/playtest.md) is the canonical reference for DSL syntax, workflows, result handling, and complete examples.

---

## run_playtest_suite

Run multiple `.playtest` files sequentially and return a compact pass/fail matrix.

**Parameters:**
- `pattern` (string, optional) — Glob pattern (e.g. `Playtests/*.playtest`), comma-separated, or newline-separated list of project-relative paths (mutually exclusive with `suite_path`)
- `suite_path` (string, optional) — Absolute path to a `.suite` file (lines = project-relative `.playtest` paths, `#` = comment)
- `timeout_per_test` (float, default=120.0) — Max seconds per individual test
- `stop_on_fail` (bool, default=false) — Abort suite after first failure
- `stop_after` (bool, default=true) — Exit Play Mode when suite completes
- `auto_play` (bool, default=false) — Enter Play Mode automatically if not already playing
- `restart_between` (bool, default=false) — Stop and restart Play Mode between each file to reset runtime state

**Output:** `SUITE: X/Y passed (Zs)` + per-file line + full failure details.

**Example:**

```python
# Run all playtests in a directory
result = await run_playtest_suite(pattern="Playtests/*.playtest")

# Run specific files with restart between each
result = await run_playtest_suite(
    pattern="Playtests/combat.playtest,Playtests/movement.playtest",
    restart_between=True,
    stop_on_fail=True
)

# Run from an absolute suite-file path
from pathlib import Path
suite = str((Path.cwd() / "Playtests/smoke.suite").resolve())
result = await run_playtest_suite(suite_path=suite)
```

---

## lint_playtest

Read-only preflight check on a `.playtest` file or inline script. Does not execute anything.

**Parameters:**
- `path` (string, optional) — Project-relative path to `.playtest` file (mutually exclusive with `script`)
- `script` (string, optional) — Inline DSL to lint (mutually exclusive with `path`)

**Checks:** Unresolved `$alias`, deprecated `ALIAS` keyword, unimplemented `TRACE_FLOW`, unknown `CALL` macro, mixed `AND`/`OR`, missing `ASSERT_CONSOLE_CLEAN` at end.

**Returns:** `OK` or severity-tagged issues (`ERROR`/`WARN`/`INFO`) with `file:line`.

**Example:**

```python
# Lint a file
result = await lint_playtest(path="Playtests/combat.playtest")

# Lint inline DSL
result = await lint_playtest(script="ASSERT Player/Health == 100\nASSERT_CONSOLE_CLEAN")
```

---

## lint_playtest_suite

Read-only preflight check across multiple `.playtest` files.

**Parameters:**
- `pattern` (string, optional) — Glob pattern (e.g. `Playtests/*.playtest`) or comma-separated list (mutually exclusive with `suite_path`)
- `suite_path` (string, optional) — Absolute path to a `.suite` file

**Returns:** Aggregated lint report with `LINT: X/Y clean` header, one block per file.

**Example:**

```python
result = await lint_playtest_suite(pattern="Playtests/*.playtest")
```

---

## validate_playtest_aliases

Compare alias `.defs` text file vs `PlaytestConfig.asset`. Reports missing, extra, or changed aliases.

**Parameters:**
- `defs` (string, optional) — Project-relative path to the `.defs` file
- `asset` (string, optional) — Asset path to `PlaytestConfig`

The tool has package defaults. Pass both paths explicitly in reusable automation
so the workflow is independent of those defaults.

**Returns:** `ok: N aliases in sync` when identical, or a diff report.

**Example:**

```python
result = await validate_playtest_aliases(
    defs="Assets/Playtests/aliases.defs",
    asset="Assets/Playtests/PlaytestConfig.asset",
)
```

---

## sync_playtest_aliases_from_defs

Overwrite `PlaytestConfig.asset` aliases from a `.defs` text file. Invalidates `AliasExpander` cache after sync. Not allowed in Play Mode.

**Parameters:**
- `defs` (string, optional) — Project-relative path to the `.defs` file
- `asset` (string, optional) — Asset path to `PlaytestConfig`

The tool has package defaults. Pass both paths explicitly in reusable automation.

**Example:**

```python
result = await sync_playtest_aliases_from_defs(
    defs="Assets/Playtests/aliases.defs",
    asset="Assets/Playtests/PlaytestConfig.asset",
)
```

---

## export_playtest_aliases_to_defs

Export `PlaytestConfig.asset` aliases to a readable `.defs` text file.

**Parameters:**
- `asset` (string, optional) — Asset path to `PlaytestConfig`
- `defs` (string, optional) — Project-relative output path

The tool has package defaults. Pass both paths explicitly in reusable automation.

**Example:**

```python
result = await export_playtest_aliases_to_defs(
    asset="Assets/Playtests/PlaytestConfig.asset",
    defs="Assets/Playtests/aliases.defs",
)
```

---

## resolve_scene_refs

See [Diagnostics: resolve_scene_refs](diagnostics.md#resolve_scene_refs).

---

## lint_scene_refs

See [Diagnostics: lint_scene_refs](diagnostics.md#lint_scene_refs).

---

## runtime_snapshot

Snapshot all runtime objects of a given component type. Returns per-object field dump.

**Parameters:**
- `type` (string) — Component type name (e.g. `Rigidbody`, `EnemyController`)
- `name` (string, optional) — Name substring filter
- `component` (string, optional) — Component type to serialize (defaults to `type`)
- `compress` (bool, default=false) — Strip default-value fields to reduce response size

**Example:**

```python
result = await runtime_snapshot(type="Rigidbody")
result = await runtime_snapshot(type="EnemyController", name="Boss", compress=True)
```

---

## invoke_method

Call a public method on a component at runtime (Play Mode).

**Parameters:**
- `path` (string) — GameObject path
- `component` (string) — Component name (required; uses reflection)
- `method` (string) — Method name
- `args` (string, optional) — Comma-separated arguments

**Example:**

```python
# Call method with no arguments
await invoke_method(path="Enemy", component="EnemyAI", method="Attack")

# Call with arguments
await invoke_method(path="Player", component="Health", method="TakeDamage", args="10")

# Call with multiple arguments
await invoke_method(path="Weapon", component="WeaponController", method="Fire", args="10.0,5.0")

# Batch multiple invocations
await batch("""
invoke_method path=Enemy1 component=EnemyAI method=Attack
invoke_method path=Enemy2 component=EnemyAI method=Attack
""")
```

---

## wait_until

Poll a condition with timeout. Block until true or timeout. Play Mode only.

**Parameters:**
- `path` (string) — GameObject path (e.g., "Player")
- `component` (string) — Component type (e.g., "Health")
- `field` (string) — Field name (e.g., "hp")
- `value` (string) — Expected value
- `timeout` (float, default=5.0) — Max seconds to wait
- `negate` (bool, default=false) — If true, wait for value to NOT match
- `abort_on_fail` (bool, default=false) — Stop Play Mode on timeout

**Example:**

```python
# Wait for health to reach 100
await wait_until(path="Player", component="Health", field="hp", value="100", timeout=10)

# Wait for animation to finish
await wait_until(path="Player", component="Animator", field="IsPlaying", value="false", timeout=3)

# Wait for health to NOT be 100
await wait_until(path="Player", component="Health", field="hp", value="100", negate=True, timeout=5)
```

---

## move_to

Pathfind and walk character to world position (Play Mode).

**Parameters:**
- `path` (string) — GameObject to move
- `position` (string) — Position as "x,y,z"
- `timeout` (float, optional) — Max seconds (default: 15.0)

**Returns:** "arrived" when within 0.1m of target, "blocked" if timeout.

**Example:**

```python
# Walk to position
result = await move_to(path="Player", position="100,50,0")

# With custom timeout
result = await move_to(path="Player", position="0,0,0", timeout=20)
```

---

## query_state

Snapshot multiple game values in one call (Play Mode only).

**Parameters:**
- `queries` (string) — Comma-separated triplets: `path|component|field` (e.g., "/GridPlayer|GridPlayer|Score,/GridPlayer|GridPlayer|PosX")

**Example:**

```python
# Query multiple fields
result = await query_state(queries="/Player|Health|hp,/Enemy|Health|hp")

# Single query (comma-separated format)
result = await query_state(queries="/Player|Health|hp")
```

---

## test_step

Move character, snapshot state before/after, check console. Play Mode only.

**Parameters:**
- `path` (string) — GameObject to move
- `position` (string) — Target position as "x,y,z"
- `checks_before` (string, default="") — Comma-separated `path|component|field` triplets to snapshot before move
- `checks_after` (string, default="") — Comma-separated `path|component|field` triplets to snapshot after move
- `wait_after` (float, default=0.5) — Seconds to wait after arriving
- `timeout` (float, default=15.0) — Max seconds to wait for arrival

**Returns:** Structured BEFORE/MOVE/AFTER/CONSOLE report.

**Example:**

```python
# Move player and verify health unchanged
result = await test_step(
    path="Player",
    position="10,0,0",
    checks_before="Player|Health|hp",
    checks_after="Player|Health|hp"
)
```

---

## animator_intent

Natural language control for animator state machines. See [Intent Tools Guide](../features/intent-tools.md#animator_intent) for full documentation.

**Note:** `direct_only=True` — cannot be used in `batch`. Call directly.

**Parameters:**
- `target` (string, required) — GameObject path (e.g., "Player", "NPC/Animator")
- `intent` (string, required) — Natural language command (e.g., "make player jump")
- `dry_run` (bool, default=false) — Preview the batch plan without executing

**Example:**

```python
# Natural language animator control
result = await animator_intent(target="Player", intent="make player jump")
result = await animator_intent(target="Player", intent="transition to idle animation")
result = await animator_intent(target="Enemy", intent="set walk speed to 2.0")
```

---

## vfx_intent

Natural language VFX and particle control. See [Intent Tools Guide](../features/intent-tools.md#vfx_intent) for presets and examples.

**Note:** `direct_only=True` — cannot be used in `batch`. Call directly.

**Parameters:**
- `target` (string, required) — GameObject path (e.g., "Player", "Particles/Emitter")
- `intent` (string, required) — Natural language command (e.g., "create explosion effect")
- `kind` (string, default="auto") — VFX kind: "particle" | "auto" (shader effects not yet implemented)
- `dry_run` (bool, default=false) — Preview the batch plan without executing

**Example:**

```python
# Natural language VFX control
result = await vfx_intent(target="Player", intent="create explosion effect")
result = await vfx_intent(target="Scene", intent="spawn rain particles", kind="particle")
result = await vfx_intent(target="Enemy", intent="fade out particle system", dry_run=False)
```

---

## ui_intent

Natural language UI manipulation. See [UI Tools — ui_intent](ui.md#ui_intent) for full documentation and [Intent Tools Guide](../features/intent-tools.md#ui_intent) for examples.

**Note:** `direct_only=True` — cannot be used in `batch`. Call directly.

---

## debug

AI-assisted scene debug: gather diagnostic context based on symptom description (not compile/reload — use `diagnose` for that; not runtime state — use `debug_animator` or `debug_physics`).

**Parameters:**
- `symptom` (string, optional) — Natural language description ("enemy doesn't move", "button not clickable")
- `path` (string, optional) — Target object path ("/Enemy_01")
- `gather` (string, optional) — Override tool list as comma-separated names ("inspect,get_console,screenshot")

**Returns:** Structured diagnostic text for LLM analysis with context-appropriate tools.

**Example:**

```python
# Describe problem, tool gathers relevant context
result = await debug(symptom="enemy doesn't move", path="/Enemy_01")

# Custom tool selection
result = await debug(symptom="button not responding", gather="inspect,screenshot")
```

---

## debug_animator

[Play Mode] Read Animator state: layers, transitions, parameters, and current animation.

**Parameters:**
- `path` (string) — Scene path to GameObject with Animator component

**Returns:** Animator configuration and current state.

**Example:**

```python
state = await debug_animator(path="Player")
# → Shows layers, transitions, active parameters, current clip
```

---

## debug_physics

[Play Mode] Read Rigidbody state, colliders, contacts, and nearby objects.

**Parameters:**
- `path` (string) — Scene path to GameObject with Rigidbody
- `radius` (float, default=5.0) — Overlap sphere radius (meters) for nearby object detection

**Returns:** Rigidbody state, active colliders, contact list, nearby bodies.

**Example:**

```python
state = await debug_physics(path="Player")
state = await debug_physics(path="Enemy", radius=10.0)
```

---

## profile

Profile CPU/GPU/memory over time. Session-based with compare and focus options.

**Parameters:**
- `action` (string) — `start` | `stop` | `status` | `analyze` | `compare` | `list_sessions`
- `duration` (float, default=5.0) — Seconds to capture (for `start` with `mode="burst"`)
- `session` (string, optional) — Session ID or name
- `compare_with` (string, optional) — Session ID to diff against
- `focus` (string, optional) — Narrow analysis to `gc` | `rendering` | `physics` | `cpu`
- `mode` (string, default="burst") — `burst` (auto-stop after duration) | `manual` (explicit stop) | `triggered` (on spike)
- `threshold_ms` (float, default=33.3) — Frame time threshold for spike detection (mode="triggered")

**Example:**

```python
# Start burst profiling (auto-stops after 5s)
await profile(action="start", duration=5.0)
await profile(action="analyze", focus="gc")

# Manual session
await profile(action="start", mode="manual")
# ... run gameplay ...
await profile(action="stop")
await profile(action="compare", compare_with="reference_session")
```

---

## get_frame_stats

Current frame performance snapshot: FPS, CPU, GPU, memory, draw calls. No session needed.

**Parameters:**
- `include` (string, optional) — Narrow output (e.g., `"gc"` for GC stats only)

**Returns:** Frame time, FPS, GPU time, draw calls, memory usage.

**Example:**

```python
stats = await get_frame_stats()
gc_only = await get_frame_stats(include="gc")
```

---

## get_memory

Memory snapshot with asset-type breakdown.

**Parameters:**
- `include` (string, default="all") — `all` | `textures` | `meshes` | `audio` | `gc` — narrow the asset-type breakdown

**Returns:** Total memory and per-type allocation.

**Example:**

```python
# Full breakdown
total = await get_memory()

# Textures only
textures = await get_memory(include="textures")
```

---

## get_metrics

Telemetry snapshot: uptime, command counts, timing statistics.

**Parameters:**
- `format` (string, default="text") — `text` | `json`
- `reset` (bool, default=false) — Clear counters atomically after snapshot

**Returns:** Metrics in requested format.

**Example:**

```python
metrics = await get_metrics()
metrics_json = await get_metrics(format="json")
metrics = await get_metrics(reset=True)  # Snapshot and clear
```

---

## watch

[Play Mode] Manage field watches with conditional triggers.

**Parameters:**
- `action` (string) — `add` | `remove` | `clear` | `reset`
- `watch_id` (string, optional) — Watch identifier (required for `remove`/`reset`)
- `path` (string, optional) — GameObject path (required for `add`)
- `component` (string, optional) — Component type (required for `add`)
- `field` (string, optional) — Field name (required for `add`)
- `condition` (string, optional) — Comparison (`"< 10"`, `"> 0"`, `"== null"`)
- `trigger_action` (string, default="log") — `log` | `pause` — action when condition met
- `interval_ms` (int, default=500) — Poll interval in milliseconds

**Returns:** Watch ID on add, status on other actions.

**Example:**

```python
# Add watch with trigger condition
id = await watch(action="add", path="Player", component="Health", 
                field="hp", condition="< 20", trigger_action="pause")

# View all watches
await get_watches()

# Remove watch
await watch(action="remove", watch_id=id)

# Clear all
await watch(action="clear")
```

---

## get_watches

Get all active watches and recent log entries.

**Parameters:** None

**Returns:** Compact list of watch definitions and triggered events.

**Example:**

```python
watches = await get_watches()
```

---

## snapshot

Capture or compare object state snapshots.

**Parameters:**
- `path` (string) — Object path ("/Enemy_01")
- `label` (string, default="default") — Snapshot label ("before", "after")
- `compare` (string, optional) — Label to diff against (empty = capture only)

**Returns:** Capture: `"snapshot 'label' saved (N fields)"`. Compare: structured diff or error.

**Example:**

```python
# Capture before state
await snapshot(path="/Player", label="before_attack")

# ... trigger attack ...

# Compare before and after
diff = await snapshot(path="/Player", label="after_attack", compare="before_attack")
# → Shows field changes: ~ hp: 100 → 85
```

---

## console_mark

See [Diagnostics: console_mark](diagnostics.md#console_mark).

---

## get_console_since

See [Diagnostics: get_console_since](diagnostics.md#get_console_since).

---

## Common Patterns

| Task | Tool | Example |
|------|------|---------|
| Run a deterministic scenario | run_playtest | See the [Playtest Guide](../features/playtest.md) |
| Lint before run | lint_playtest | `await lint_playtest(path="Playtests/combat.playtest")` |
| Suite run | run_playtest_suite | `await run_playtest_suite(pattern="Playtests/*.playtest")` |
| Method invocation | invoke_method | `await invoke_method("Enemy", "HealthComponent", "TakeDamage", args="10")` |
| Runtime modification | invoke_method + batch | `await invoke_method("Player", "Health", "SetHp", args="50")` |

**See also:** [Scene Tools](scene.md) for Play Mode control (editor play/stop), [Objects](objects.md) for component access, [Diagnostics](diagnostics.md) for compile gates.
