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
- `scene` (string, optional) — Save/discard target when multiple scenes loaded (identifies by name)

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
- `paths` (string, optional) — Comma-separated paths for multi-select (e.g., "/Player,/Enemy,/NPC")

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

## scene_change_plan

Pre-flight checklist for safe scene editing. Validates compile, console, and target references before mutations.

**Parameters:**
- `goal` (string) — Description of the edit (e.g., "Add enemy spawner")
- `targets` (string, optional) — Comma-separated object paths to resolve (e.g., "/Player,/Enemy")

**Returns:**
- Success: `plan_id=abc123\ngoal=...\ncompile=clean\nconsole_errors=0`
- Failure: `FAIL: ...` (compile error, console errors, or broken refs)

**Example:**

```python
# Create a pre-flight plan
plan = await scene_change_plan(
    goal="Add spawner at checkpoint",
    targets="/Player,/Checkpoint")
# -> plan_id=abc123
# -> goal=Add spawner at checkpoint
# -> compile=clean
# -> console_errors=0

# Use plan_id with apply_scene_change
result = await apply_scene_change(
    plan_id="abc123",
    commands="create_object name=Spawner parent=/Checkpoint")
```

---

## apply_scene_change

Execute scene mutations with pre-check, post-verify, and save. **Must use with `scene_change_plan()`.**

**Parameters:**
- `plan_id` (string, required) — ID from `scene_change_plan()`
- `commands` (string) — Batch commands to execute (one per line)
- `verify` (bool, default=true) — Check references and console after mutations
- `save` (bool, default=true) — Save scene after mutations

**Returns:** Mutation summary: `mutations=ok\nrefs=ok (0 broken)\nconsole=clean\nsaved=true`

**Example:**

```python
# Execute mutations with verification
result = await apply_scene_change(
    plan_id="abc123",
    commands="create_object name=Spawner parent=/Checkpoint\nset_property path=/Checkpoint/Spawner component=Transform prop=m_LocalPosition value=(0,0,5)")
# -> mutations=ok
# -> refs=ok (0 broken)
# -> console=clean
# -> saved=true
```

**Workflow:**

```python
# 1. Create plan (gates on compile + console)
plan = await scene_change_plan(goal="Add spawner", targets="/Checkpoint")
if "FAIL" in plan:
    return  # Fix compile/console first

# 2. Execute mutations with plan ID
result = await apply_scene_change(
    plan_id=plan.split("=")[1],  # extract plan_id
    commands="create_object name=Spawner parent=/Checkpoint")
```

---

## ping_object

Highlight an object in Hierarchy and Project, and select it.

**Parameters:**
- `path` (string, required) — Scene path to object

**Returns:** Confirmation and object info.

**Example:**

```python
# Ping Player in hierarchy
await ping_object(path="Player")

# Ping nested object
await ping_object(path="Level/Enemies/Boss")
```

---

## get_selection

Currently selected GameObject: path and component list.

**Parameters:** None

**Returns:** Selected object path and component types, or "none selected".

**Example:**

```python
# Check what's selected
selection = await get_selection()
# -> /Player [Transform,Rigidbody,PlayerController]
```

---

## setup_objects

Create and configure multiple objects in one call (autobatch macro).

**Parameters:**
- `specs` (string, required) — One object per line: `name [primitive=X] [parent=Y] [pos=(x,y,z)] [components=A,B]`

**Example:**

```python
# Create multiple NPCs with components
specs = """
NPC1 primitive=Capsule parent=/Level pos=(0,0,0) components=Health,AI
NPC2 primitive=Capsule parent=/Level pos=(5,0,0) components=Health,AI
Boss primitive=Capsule parent=/Level pos=(10,0,0) components=Health,AI,BossAI
"""
result = await setup_objects(specs)
```

---

## set_properties

Set multiple properties on ONE object (autobatch macro).

**Parameters:**
- `path` (string, required) — Scene path to object
- `props` (string, required) — Properties to set: `component.prop=value` per line or semicolon-separated

**Example:**

```python
# Set multiple component properties
result = await set_properties(
    path="/Player",
    props="Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5;Health.maxHp=100")
```

---

## configure_objects

Configure multiple objects at once (autobatch macro).

**Parameters:**
- `config` (string, required) — One object per line: `/Path component.prop=value [component2.prop2=value2] ...`

**Example:**

```python
# Configure multiple objects
config = """
/NPC1 Transform.m_LocalPosition=(1,0,0) Health.maxHp=100
/NPC2 Transform.m_LocalPosition=(3,0,0) Health.maxHp=80
/Boss Transform.m_LocalPosition=(10,0,0) Health.maxHp=500 BossAI.difficulty=hard
"""
result = await configure_objects(config)
```

---

## scene_environment

Read/write scene environment: ambient light, fog, skybox, reflections.

**Parameters:**
- `action` (string, default="get") — "get" | "set"
- `prop` (string, optional) — Property name (required for set)
- `value` (string, optional) — Property value (required for set)

**Properties:**
ambientMode, ambientLight, ambientIntensity, ambientSkyColor, ambientEquatorColor, ambientGroundColor, fog, fogColor, fogMode, fogDensity, fogStartDistance, fogEndDistance, reflectionIntensity, reflectionBounces, subtractiveShadowColor, defaultReflectionResolution

**Example:**

```python
# Get current ambient light
env = await scene_environment(action="get")

# Set ambient light to white
await scene_environment(action="set", prop="ambientLight", value="1,1,1")

# Enable fog
await scene_environment(action="set", prop="fog", value="true")

# Set fog color
await scene_environment(action="set", prop="fogColor", value="0.5,0.5,0.5")
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
| Safe scene editing | scene_change_plan + apply_scene_change | Plan first, execute with verification |
| Create multiple objects | setup_objects | `await setup_objects("NPC1 primitive=Capsule pos=(0,0,0)")` |
| Configure bulk objects | configure_objects | Multi-line path + properties format |
| Load scenes additively | scene("open_additive") | `await scene("open_additive", path="AdditiveScene")` |
| Adjust lighting/fog | scene_environment | `await scene_environment(action="set", prop="fog", value="true")` |

---

**See also:** [Testing Tools](tests.md) for test execution, [Runtime Tools](runtime.md) for Play Mode operations, [Batch](batch.md) for multi-operation efficiency, [Spatial Tools](spatial.md) for collider and layout verification.
