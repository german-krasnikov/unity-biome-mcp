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
hier = await get_hierarchy(components=true)
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

Control Play/Pause/Stop and selection. Core editor state operations.

**Parameters:**
- `action` (string) — "play" | "pause" | "stop" | "select" | "frame" | "focus"
- `path` (string, optional) — GameObject path for select/frame/focus

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| play | Enter Play Mode | `editor("play")` |
| pause | Pause Play Mode | `editor("pause")` |
| stop | Exit Play Mode | `editor("stop")` |
| select | Highlight object in Hierarchy | `editor("select", path="Player")` |
| frame | Zoom camera on object | `editor("frame", path="Player")` |
| focus | Activate editor window | `editor("focus")` |

**Example:**

```python
# Enter play mode
await editor("play")

# Select Player in Hierarchy
await editor("select", path="Player")

# Frame camera on Player
await editor("frame", path="Player")

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

# Make changes...
await set_property("Player", "Health", "hp", "50")

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

await invoke_method("Enemy", "HealthComponent", "TakeDamage", args="10")

diff = await scene_diff()
# → "Enemy/Health == 90 (was 100)"
```

---

## run_tests

Execute NUnit tests in EditMode or PlayMode.

**Parameters:**
- `mode` (string) — "EditMode" | "PlayMode" (required)
- `filter` (string, optional) — Pipe-separated test class names for fast focused runs

**Returns immediately** with message "tests-started|{mode}|...". Poll `get_test_results()` every 5 seconds.

**Example:**

```python
# Start Edit Mode tests
result = await run_tests(mode="EditMode")
# → "tests-started|EditMode|poll get_test_results every 5s"

# Poll for results (in a loop)
import asyncio
for i in range(24):  # 2 minutes
    status = await get_test_results()
    if status not in ("pending", "none"):
        print(f"Tests complete: {status}")
        break
    await asyncio.sleep(5)

# Run only failing tests (much faster)
await run_tests(mode="EditMode", filter="HealthTest|DamageTest")
```

**Full workflow:**
1. Run all EditMode tests first (fast gate)
2. If pass, run PlayMode tests
3. PlayMode must run AFTER all MCP mutations (reconnects to Unity)

---

## run_tests_wait

Run tests and block until results arrive. Wraps the manual `run_tests` + poll loop.

**Parameters:**
- `mode` (string, default="EditMode") — "EditMode" | "PlayMode"
- `filter` (string, optional) — Pipe-separated test class names
- `timeout` (float, default=180.0) — Max seconds to wait
- `poll_interval` (float, default=5.0) — Seconds between status polls

**Returns:** Final test result summary, `"TIMEOUT: <last_status>"`, or `"BLOCKED: <reason>"`.

**Example:**

```python
# Preferred over manual poll loop
result = await run_tests_wait(mode="EditMode")

# Focused run with shorter timeout
result = await run_tests_wait(mode="EditMode", filter="HealthTest|DamageTest", timeout=60)
```

---

## get_test_results

Poll test execution status after `run_tests()`.

**Parameters:** None

**Output:** Test result summary with pass/fail counts, or "pending" if still running.

**Example:**

```python
# After run_tests()...
result = await get_test_results()
# → "EditMode: 150 passed, 0 failed (45.2s)"
# → "pending" (still running)
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

Load previous session context. Shows hierarchy diff since last save.

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

## checkpoint (continued) — Advanced Usage

Use checkpoints + scene_diff for test assertions:

```python
# Playtest sequence
await checkpoint(label="start")
await editor("play")
await wait_until(path="Player", component="Health", field="hp", value="100", timeout=10)

# Simulate combat
await invoke_method(path="Player", component="Health", method="TakeDamage", args="10")

# Verify state change
diff = await scene_diff()
assert "90" in diff, "Health should drop to 90"

await editor("stop")
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Verify scene structure | get_hierarchy + search_scene | `hier = await get_hierarchy()` |
| Track object changes | checkpoint + scene_diff | `await checkpoint(label="before"); ...; diff = await scene_diff()` |
| Visual regression testing | screenshot_baseline + screenshot_compare | `await screenshot_baseline(name="x"); diff = await screenshot_compare(name="x")` |
| Run tests after changes | run_tests + get_test_results | `await run_tests(mode="EditMode"); await asyncio.sleep(5); result = await get_test_results()` |
| Load scenes additively | scene("open_additive") | `await scene("open_additive", path="AdditiveScene")` |

---

**See also:** [Runtime Tools](runtime.md) for Play Mode operations, [Batch](batch.md) for multi-operation efficiency.
