# Batch Command Reference

Combine two or more compatible operations into a single MCP call and avoid
repeating typed-call envelopes.

## Overview

Prefer `batch()` for two or more compatible reads or writes. Direct-only and
Python-expanded tools must be called through their typed MCP wrappers.

Instead of:
```python
await create_object("Enemy")          # 1 call
await set_property("Enemy", ...)      # 2nd call
await manage_component("Enemy", ...)  # 3rd call
```

Use:
```python
await batch("""
create_object name=Enemy
set_property path=Enemy component=Transform prop=position value=0,1,0
manage_component path=Enemy type=Health action=add
""")
```

## batch

Execute multiple commands in one call.

**Parameters:**
- `commands` (string) — Text format: one command per line, `cmd key=value key=value`
- `on_error` (string, default="continue") — "continue" or "stop"
- `timeout` (float, default=75.0) — Client-side wait budget. Unity's outer batch execution deadline is 65 seconds.
- `atomic` (bool, default=False) — On failure, revert changes recorded in the Unity Undo group. File, process, and other external side effects are not guaranteed to roll back.
- `validate_aliases` (bool, default=False) — Dry-run alias validation before any mutations execute

**Command Format:**
```
command_name key=value key=value key=value
another_command key=value key=value
```

**Example: Simple Batch**

```python
result = await batch("""
create_object name=Player
set_property path=Player component=Transform prop=position value=0,1,0
manage_component path=Player type=Health action=add
""")
```

## Supported Commands (35+)

| Category | Commands |
|----------|----------|
| **Object CRUD** | create_object, delete_object, set_active, set_parent, transfer_object |
| **Components** | manage_component, wire_event, unwire_event, set_material |
| **Properties** | set_property, set_property_delta |
| **Reads** | get_component, get_components_list, get_object_detail, find_objects, inspect |
| **Scene** | search_scene, get_hierarchy |
| **Assets** | asset, material, prefab, scriptable_object, project_settings |
| **Advanced** | object_diff, validate_references, references |

**Constraints:**
- ❌ No async commands (wait_until, move_to, run_tests, test_step, run_playtest)
- ❌ No inter-command references (each op is independent)
- ❌ No DSL-expansion tools (animator_intent, vfx_intent, ui_intent must be typed MCP calls)
- ✅ Tool enable/disable checks apply to each command
- ✅ Play Mode guard: mutating ops blocked (BLOCKED response)
- ✅ Compile guard: mutations blocked during compilation
- ✅ Main thread processing only

**Key parameter names:**
- `prop` (not `property`) — for set_property
- `type` (not `component`) — for manage_component action

## Command Reference

### Object Creation & Deletion

**create_object:**
```
create_object name=Enemy primitive=Cube parent=Enemies
```

**delete_object:**
```
delete_object path=Temp
```

**set_active:**
```
set_active path=UI/PauseMenu active=false
```

**set_parent:**
```
set_parent path=Sword parent=Player/Hand
```

### Component Management

**manage_component:**
```
manage_component path=Player type=Rigidbody action=add
manage_component path=Player type=AudioSource action=remove
```

**wire_event:**
```
wire_event path=UI/Button component=Button event=onClick target=GameManager method=OnClick arg_type=void
```

**unwire_event:**
```
unwire_event path=UI/Button component=Button event=onClick
```

**set_material:**
```
set_material path=Player color=#FF0000
```

### Property Modification

**set_property (Edit Mode; Play Mode writes are lost on stop):**
```
set_property path=Player component=Transform prop=position value=10,5,0
set_property path=Enemy component=Health prop=maxHp value=100
set_property path=Light component=Light prop=intensity value=1.5
```

**set_property_delta:**
```
set_property_delta path=Player component=Health prop=hp delta=10
```

### Reading Data

**get_component:**
```
get_component path=Player type=Transform
```

**inspect:**
```
inspect paths=Player,Enemy components=Transform,Health
```

**get_components_list:**
```
get_components_list id=12345
```

**get_hierarchy:**
```
get_hierarchy depth=3 components=true
```

**search_scene:**
```
search_scene query=Enemy
```

**find_objects:**
```
find_objects component=Rigidbody
```

### Asset Operations

**asset:**
```
asset action=find type=Material folder=Assets/UI
asset action=move source=Assets/Old.mat dest=Assets/Materials/Old.mat
asset action=delete path=Assets/Temp.prefab
```

**material:**
```
material action=set path=Assets/Mat.mat prop=_Color value=1,0,0,1
```

**prefab:**
```
prefab action=save path=MyPrefab asset_path=Assets/Prefabs/MyPrefab.prefab
prefab action=edit asset_path=Assets/Prefabs/Player.prefab component=Health prop=MaxHP value=200
```

**scriptable_object:**
```
scriptable_object action=set path=Assets/GameConfig.asset prop=maxLevel value=50
```

### Advanced Operations

**object_diff:**
```
object_diff path_a=PlayerTemplate path_b=Player
```

**validate_references:**

See [Diagnostics: validate_references](diagnostics.md#validate_references).

```
validate_references path=Player depth=3
```

**references:**
```
references action=get path=Assets/Prefabs/Player.prefab
```

## Real-World Examples

### Example 1: Setup Scene with Multiple Objects

```python
await batch("""
create_object name=Player primitive=Cube
set_property path=Player component=Transform prop=position value=0,1,0
manage_component path=Player type=Rigidbody action=add
set_property path=Player component=Rigidbody prop=mass value=80
create_object name=Enemy primitive=Cube parent=Enemies
set_property path=Enemy component=Transform prop=position value=5,1,0
manage_component path=Enemy type=Health action=add
set_property path=Enemy component=Health prop=maxHp value=50
""")
```

### Example 2: Modify Multiple UI Elements

```python
await batch("""
set_active path=UI/MainMenu active=false
set_active path=UI/GameUI active=true
set_property path=UI/HealthBar component=Image prop=fillAmount value=1
wire_event path=UI/PlayButton component=Button event=onClick target=GameManager method=StartGame arg_type=void
""")
```

### Example 3: Inspect Multiple Objects

```python
result = await batch("""
inspect paths=Player components=Transform,Health,Rigidbody
inspect paths=Enemy components=Transform,Health
get_components_list id=<boss_instance_id>
""")
```

### Example 4: Asset & Prefab Operations

```python
await batch("""
asset action=find type=Material folder=Assets/Materials
material action=set path=Assets/NewMat.mat prop=_Color value=1,0,0,1
prefab action=edit asset_path=Assets/Prefabs/Player.prefab component=Health prop=MaxHP value=200
""")
```

## Batch Behavior

**Sequential execution:** Commands run in order on the Unity main thread.

**Error handling:**
- **continue** (default) — Skip failed operation, continue to next
- **stop** — Halt batch on first failure (set in tool params)

**Atomicity:** Entire batch fails if compile in progress or Play Mode guard triggered.

**Performance:** Single round-trip TCP call, ~5-50ms total (vs 50-500ms for 10 individual calls).

## Typed vs. Batch Commands

**Some tools MUST be called as typed MCP tools (not batch):**
- `batch` — Can't batch a batch
- `do`, `ask`, `ask_user` — NL intent tools
- `run_playtest` — DSL-based testing
- `run_tests` — NUnit execution
- `wait_until`, `move_to`, `test_step` — Async operations
- `animator_intent`, `vfx_intent`, `ui_intent` — DSL-expansion tools

**All others are batchable** (reads, writes, asset ops, etc.)

## Common Mistakes

❌ **WRONG:** 10 separate calls
```python
await create_object("Enemy1")
await create_object("Enemy2")
await set_property("Enemy1", ...)
# ... 7 more calls
```

✅ **RIGHT:** 1 batch call
```python
await batch("""
create_object name=Enemy1
create_object name=Enemy2
set_property path=Enemy1 component=Transform prop=position value=0,1,0
""")
```

❌ **WRONG:** Async in batch
```python
await batch("""
wait_until path=Player component=Health field=hp value=100
""")  # ❌ ERROR
```

✅ **RIGHT:** Typed async call
```python
await wait_until(path="Player", component="Health", field="hp", value="100")  # ✅ CORRECT
```

## Output Format

**Success:**
```
ok: 5
  [0] create_object "Player" → ok
  [1] set_property "Player" → ok
  [2] manage_component "Player" → ok
  [3] get_component "Player" → Transform { position=(0,1,0), ... }
  [4] search_scene "Enemy" → [Enemy $a, Enemy2 $b]
```

**Partial failure (continue mode):**
```
ok: 3, err: 2
  [0] create_object "Player" → ok
  [1] set_property "Player" → err: Component 'Invalid' not found
  [2] manage_component "Player" → ok
  [3] get_component "Player" → ok
  [4] find_objects "Missing" → err: No objects found
```

## Batch-First Decision Tree

```
1+ operations needed?
├─ YES
│  ├─ All 2+ can be batched? (no async, no intent)
│  │  ├─ YES → use batch()
│  │  └─ NO → split into batch + typed calls
│  └─ Single operation → direct call (ok, no penalty)
└─ NO → single direct call
```

## Patterns & Idioms

| Pattern | When | Example |
|---------|------|---------|
| **Inspect All** | Snapshot multiple objects | Text DSL: multi-line `inspect` commands |
| **Setup Scene** | Initialize with multiple objects | `create_object`, `manage_component`, `set_property` in one batch |
| **Bulk Modify** | Change same property on many objects | Multiple `set_property` lines with different paths |
| **Read Before/After** | Compare state | `get_component`, mutations, `get_component` in sequence |
| **Asset Organization** | Move & create folders | `asset` commands in one batch |

---

**See also:** Token Optimization Skills for usage patterns and the
[Batch guide index](index.md#batch-combine-operations-for-token-savings).
