# Scene Tools

Load, modify, inspect, and capture the state of your Unity scenes. Core operations for scene management and visual verification.

## get_hierarchy

Read the current scene's GameObject hierarchy as a text tree.

**Parameters:**
- `depth` (int, default=2) — Tree depth to traverse
- `root` (string, optional) — Scope to subtree (path or None for whole scene)
- `filter` (string, optional) — Filter objects by name substring
- `components` (bool, default=false) — Include component list `[Type1,Type2]` on each object
- `compress` (bool, default=false) — Group repeated slots/points/meshes
- `summary` (bool, default=false) — Compact root-only counts (60-100 tokens)
- `incremental` (bool, default=false) — Return NO_CHANGE if scene unchanged since last call
- `full` (bool, default=false) — Bypass distillation, return raw response
- `scene` (string, optional) — Filter to a single scene by name (multi-scene only)

**Output Format:**

Single scene:
```
Main Camera $a
Directional Light $b
GameManager $c
├─ UIRoot $d
│  ├─ HealthBar $e
│  └─ PauseMenu $f !
Player $g
├─ Body $h
└─ WeaponSlot $i
   └─ Sword $j
```

Multi-scene:
```
[MainScene]
Main Camera $a
Directional Light $b

[AdditiveScene]
Player $c
├─ Body $d
```

With components:
```
Main Camera [Camera,AudioListener] $a
Player [Rigidbody,PlayerController] $b
├─ Body [SkinnedMeshRenderer] $c
```

**Example:**

```python
# Basic hierarchy
hier = await get_hierarchy()
print(hier)

# With components
hier = await get_hierarchy(components=True)
```

**Use Cases:**
- Verify scene structure before running tests
- Check if objects are active/inactive
- Quick reference for object paths in batch operations

---

## search_scene

Find GameObjects by name, component type, tag, or layer.

**Parameters:**
- `query` (string) — Search term. Syntax: `name text`, `t:Component`, `tag=Tag`, `layer=N`, `active=bool`. Combine with spaces.
- `root` (string, optional) — Scope search to subtree (path or None for whole scene)
- `limit` (int, default=50) — Max results (0 = unlimited)
- `scene` (string, optional) — Filter to a single scene by name (multi-scene only)

**Output Format:**
```
Player $a (layer=Default, tag=Player, active=true)
├─ Head $b
├─ Body $c
└─ Legs $d
```

**Example:**

```python
# Find by name
results = await search_scene(query="Player")

# Find all objects with Rigidbody
results = await search_scene(query="t:Rigidbody")

# Find all enemies (tag)
results = await search_scene(query="tag=Enemy")

# Scope to subtree
results = await search_scene(query="Health", root="Player")
```

---

## scene

Open, close, or manage scenes. Control which scenes are loaded additively.

**Parameters:**
- `action` (string) — "new" | "open" | "save" | "discard" | "open_additive" | "close" | "set_active" | "list"
- `path` (string) — Scene name or path (e.g., "MainScene" or "Assets/Scenes/MainScene.unity"). Required for open/save/open_additive/close/set_active.
- `scene_name` (string, optional) — Save/discard target when multiple scenes loaded (identifies by name)

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| list | Show all loaded scenes | — | `scene("list")` |
| open_additive | Load scene without unloading current | path | `scene("open_additive", path="AdditiveLevel")` |
| close | Unload scene | path | `scene("close", path="MainScene")` |
| set_active | Make scene the active scene | path | `scene("set_active", path="MainScene")` |

**Example:**

```python
# List loaded scenes
scenes = await scene("list")

# Load additional scene
await scene("open_additive", path="UI")

# Set MainScene as active
await scene("set_active", path="MainScene")

# Unload UI scene
await scene("close", path="UI")
```

---

## screenshot

Capture the current game view as a PNG image. See [Screenshot Tools](screenshots.md#screenshot) for full parameters and examples.

---

## editor

Read editor state, control Play Mode, select a GameObject, or return the project path.

**Parameters:**
- `action` (string, default="state") — "state" | "play" | "pause" | "stop" | "select" | "project_path"
- `path` (string, optional) — GameObject path required by `select`

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| state | Return current Play Mode and editor state | `editor()` |
| play | Enter Play Mode | `editor("play")` |
| pause | Pause Play Mode | `editor("pause")` |
| stop | Exit Play Mode | `editor("stop")` |
| select | Highlight object in Hierarchy | `editor("select", path="Player")` |
| project_path | Return the current Unity project path | `editor("project_path")` |

**Example:**

```python
# Enter play mode
await editor("play")

# Select Player in Hierarchy
await editor("select", path="Player")

# Pause simulation
await editor("pause")

# Exit play mode
await editor("stop")
```

---

## checkpoint

Create a named Undo checkpoint. Allows rollback via Ctrl+Z in Unity.

**Parameters:**
- `label` (string, default="checkpoint") — Checkpoint identifier

**Example:**

```python
# Save full scene state
await checkpoint(label="before_combat")

# Make a hierarchy change...
await create_object("CombatRoot")

# Compare later via scene_diff
diff = await scene_diff()
```

---

## fingerprint

Generate hash of scene state for comparison across runs.

**Parameters:**
- `path` (string, optional) — Scope to subtree (default: whole scene)
- `depth` (int, default=3) — Depth to hash

**Output:** Single-line fingerprint (~5 tokens): `fp:XXXXXXXX`

**Example:**

```python
# Get scene fingerprint
fp1 = await fingerprint()

# Modify objects...
await set_property("Player", "Transform", "position", "10,0,0")

# Compare
fp2 = await fingerprint()
assert fp1 != fp2, "Scene state should have changed"
```

---

## scene_diff

Compare current scene state with last snapshot. First call saves the snapshot; subsequent calls return the diff.

**Parameters:** None

**Output:** Added/removed lines showing what changed.

**Example:**

```python
await scene_diff()  # saves snapshot

await create_object("Enemy")

diff = await scene_diff()
# -> DIFF: +1 -0
# -> + Enemy ...
```

---

## run_tests

Dispatch NUnit tests in EditMode or PlayMode and return the durable run identity
without waiting for completion. `run_tests_wait()` is the correlated convenience
wrapper for callers that can wait.

For an ordinary interactive MCP session, prefer `run_tests_wait()` so the agent
does not implement recovery and polling itself. This low-level dispatch API is
for callers that need nonblocking control. Unity Biome MCP repository and
disposable-worker runs use `python3 run_unity_tests.py` instead; full and release
runs must not be driven by an ad hoc MCP polling loop.

**Parameters:**
- `mode` (string, default="EditMode") — "EditMode" | "PlayMode"
- `filter` (string, optional) — Pipe-separated test class names for fast focused runs
- `request_id` (string, optional) — Stable caller identity; reuse it when resolving an uncertain start

**Returns:**
`tests-started|request_id=...|run_id=...|utf_guid=...|state=dispatched`, or
`START-UNKNOWN|request_id=...|reason=...` when dispatch may have happened but
the acknowledgment was lost.

**Example:**

```python
# Start an asynchronous Edit Mode run and retain every returned identifier.
ack = await run_tests(mode="EditMode")
# Poll get_test_run(run_id=...) for this exact run.

# Run only failing tests (much faster)
ack = await run_tests(mode="EditMode", filter="HealthTest|DamageTest")
```

If the result is `START-UNKNOWN`, call
`resolve_test_request(request_id=...)` with that same request ID. Do not dispatch
a replacement run.

**Full workflow:**
1. Run all EditMode tests first (fast gate)
2. If pass, run PlayMode tests
3. PlayMode must run AFTER all MCP mutations (reconnects to Unity)

---

## run_tests_wait

Dispatch tests and wait until that exact durable run becomes terminal. The
wrapper preserves `request_id`/`run_id` correlation across transport loss and
domain reload. This is the default NUnit entry point for consumer-project MCP
sessions. Repository/disposable-worker verification uses the standalone
`run_unity_tests.py` runner.

**Parameters:**
- `mode` (string, default="EditMode") — "EditMode" | "PlayMode"
- `filter` (string, optional) — Pipe-separated test class names
- `timeout` (float, default=900.0) — Max seconds to wait
- `poll_interval` (float, default=5.0) — Seconds between status polls
- `request_id` (string, optional) — Stable caller identity

**Returns:** A reconciled terminal JSON snapshot, `TIMEOUT|request_id=...|run_id=...`,
`START-UNKNOWN|...`, a protocol error, or `BLOCKED: <reason>`. A timeout is
observational and does not mark the Unity run complete.

**Example:**

```python
# Preferred over manual poll loop
result = await run_tests_wait(mode="EditMode")

# Focused run with shorter timeout
result = await run_tests_wait(mode="EditMode", filter="HealthTest|DamageTest", timeout=60)
```

---

## get_test_results

Legacy result facade. Pass the exact `run_id` to avoid reading a stale latest
run; uncorrelated output is diagnostic only.

**Parameters:**
- `run_id` (string, optional) — Durable run identity

**Output:** Test result summary with pass/fail counts, or "pending" if still running.

**Example:**

```python
# Prefer get_test_run for acceptance.
result = await get_test_results(run_id=run_id)
```

---

## get_test_run

Return the durable JSON snapshot for one exact run.

**Parameters:**
- `run_id` (string, required) — Value returned by `run_tests` or `resolve_test_request`

Only a reconciled `state="terminal"` snapshot is completion evidence. Check its
`outcome`, expected/missing/unexpected test sets, issues, and cleanup state; a
partial count is not success.

```python
snapshot = await get_test_run(run_id=run_id)
```

---

## resolve_test_request

Resolve a potentially lost start acknowledgment without starting another run.

```python
status = await resolve_test_request(request_id=request_id)
```

Use the returned `run_id` for all further polling.

---

## cancel_test_run

Request cancellation of one exact run. Cancellation is asynchronous, so keep
polling `get_test_run(run_id=...)` until the run reaches a reconciled terminal
state.

```python
status = await cancel_test_run(run_id=run_id)
```

---

## list_test_runs

List recent durable runs, newest first. This is a diagnostic and recovery aid;
do not substitute a "latest" entry for the `run_id` returned by your dispatch.

```python
runs = await list_test_runs(limit=20)
```

---

## get_test_progress

Legacy progress facade. Always pass `run_id` when using it; prefer
`get_test_run` for acceptance because the durable snapshot includes lifecycle,
outcome, manifest, issues, and cleanup evidence.

```python
progress = await get_test_progress(run_id=run_id)
```

---

## save_session

Save current scene state to `.claude/session-context.json` for cold-start recovery.

**Parameters:** None

**Example:**

```python
await save_session()
```

---

## load_session

Load the previous session context beside the current hierarchy.

**Parameters:** None

**Example:**

```python
await load_session()
```

---

## screenshot_baseline

Save a reference image for visual regression. See [Screenshot Tools](screenshots.md#screenshot_baseline) for full parameters and examples.

---

## screenshot_compare

Compare current view against a saved baseline. See [Screenshot Tools](screenshots.md#screenshot_compare) for full parameters and examples.

---

## get_changes

Get Unity editor changes since last call. Tracks hierarchy changes, undo/redo, play mode, scene open/save, selection.

**Parameters:**
- `clear` (bool, default=true) — Clear the change log after reading

**Output:** Chronological event list, or NO_CHANGES if nothing happened.

**Example:**

```python
await get_changes()
# → hierarchy_changed, selection_changed, ...

# Peek without clearing
await get_changes(clear=False)
```

---

## Runtime Assertion Example

Use runtime reads for component-value assertions. `scene_diff()` only compares
serialized hierarchy lines.

```python
await checkpoint(label="start")
await editor("play")
await wait_until(path="Player", component="Health", field="hp", value="100", timeout=10)

await invoke_method(path="Player", component="Health", method="TakeDamage", args="10")

health = await query_state(path="Player", component="Health", field="hp")
assert "90" in health, "Health should drop to 90"

await editor("stop")
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Verify scene structure | get_hierarchy + search_scene | `hier = await get_hierarchy()` |
| Track hierarchy changes | scene_diff | `await scene_diff(); ...; diff = await scene_diff()` |
| Visual regression testing | screenshot_baseline + screenshot_compare | `await screenshot_baseline(name="x"); diff = await screenshot_compare(name="x")` |
| Run tests after changes in a consumer project | run_tests_wait | `result = await run_tests_wait(mode="EditMode")` |
| Load scenes additively | scene("open_additive") | `await scene("open_additive", path="AdditiveScene")` |

---

**See also:** [Runtime Tools](runtime.md) for Play Mode operations, [Batch](batch.md) for multi-operation efficiency.
