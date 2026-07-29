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
- `paths` (string, optional) — Glob pattern (e.g. `Playtests/*.playtest`), comma-separated, or newline-separated list of project-relative paths (mutually exclusive with `suite_path`)
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
result = await run_playtest_suite(paths="Playtests/*.playtest")

# Run specific files with restart between each
result = await run_playtest_suite(
    paths="Playtests/combat.playtest,Playtests/movement.playtest",
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
- `paths` (string, optional) — Glob pattern (e.g. `Playtests/*.playtest`) or comma-separated list (mutually exclusive with `suite_path`)
- `suite_path` (string, optional) — Absolute path to a `.suite` file

**Returns:** Aggregated lint report with `LINT: X/Y clean` header, one block per file.

**Example:**

```python
result = await lint_playtest_suite(paths="Playtests/*.playtest")
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

Read-only scene reference resolver. Resolves `$alias`, `/path`, or `t:Type` tokens against the live scene.

**Parameters:**
- `refs` (string) — Comma-separated list of `$alias`, `/path`, or `t:Type` tokens
- `fields` (string, optional) — Comma-separated field names to check existence on matched component

**Returns:** One tab-aligned line per ref: `OK`|`MISS`|`AMB` + path + details.

**Example:**

```python
result = await resolve_scene_refs(refs="$player,/Enemy,t:Camera")
result = await resolve_scene_refs(refs="$player", fields="hp,maxHp")
```

---

## lint_scene_refs

Read-only linter for scene references in DSL scripts or batch commands.

**Parameters:**
- `path` (string, optional) — Project-relative path to `.playtest` file (mutually exclusive with `snippet`)
- `snippet` (string, optional) — Inline DSL or batch commands to lint (mutually exclusive with `path`)

**Checks:** Unresolved aliases, embedded aliases, missing objects, ambiguous names.

**Returns:** `OK: no issues` or severity-tagged issues (`ERROR`/`WARN`) with `file:line:token`.

**Example:**

```python
result = await lint_scene_refs(path="Playtests/combat.playtest")
result = await lint_scene_refs(snippet="ASSERT /Player|Health|hp == 100")
```

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

## set_runtime_property

Modify a component field in Play Mode via reflection (runtime-only).

**Parameters:**
- `path` (string) — GameObject path
- `component` (string) — Component name
- `field` (string) — Field name (public field or property)
- `value` (string) — New value (inferred type)

**Note:** Only works in Play Mode. Use `set_property` in Edit Mode.

**Example:**

```python
# Modify health at runtime
await set_runtime_property("Player", "Health", "hp", "50")

# Modify velocity
await set_runtime_property("Player", "Rigidbody", "velocity", "0,10,0")

# Batch modifications
await batch("""
set_runtime_property path=Player component=Health field=hp value=100
set_runtime_property path=Enemy component=Health field=hp value=50
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

Natural language control for animator state machines (Category: Intent).

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

Natural language VFX and particle control (Category: Intent).

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

Natural language UI manipulation. See [UI Tools — ui_intent](ui.md#ui_intent) for full documentation.

---

## Common Patterns

| Task | Tool | Example |
|------|------|---------|
| Run a deterministic scenario | run_playtest | See the [Playtest Guide](../features/playtest.md) |
| Lint before run | lint_playtest | `await lint_playtest(path="Playtests/combat.playtest")` |
| Suite run | run_playtest_suite | `await run_playtest_suite(paths="Playtests/*.playtest")` |
| Method invocation | invoke_method | `await invoke_method("Enemy", "HealthComponent", "TakeDamage", args="10")` |
| Runtime modification | set_runtime_property + batch | `await batch("set_runtime_property path=Player component=Health field=hp value=50")` |

**See also:** [Scene Tools](scene.md) for Play Mode control (editor play/stop), [Objects](objects.md) for component access, [Diagnostics](diagnostics.md) for compile gates.
