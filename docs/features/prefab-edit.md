# Prefab Editing Guide

Modify prefab assets directly without unpacking to scene.

## Overview

The `edit` action lets you modify prefab component properties, add/remove components, all without instantiating the prefab in the scene. Available since v0.56.0.

## When to Use Prefab Edit vs set_property

| Task | Use | Why |
|------|-----|-----|
| Modify prefab template | `prefab(action=edit)` | Changes all future instances |
| Modify scene instance | `set_property` | Only affects that object |
| Add component to prefab | `prefab(action=edit, add_component=...)` | All instances inherit |
| Add to scene object | `manage_component` | Only that object |

## Syntax

```python
# Edit property
await prefab("edit", 
             asset_path="Assets/Prefabs/Player.prefab",
             component="Health",
             prop="MaxHP",
             value="200")

# Add component
await prefab("edit",
             asset_path="Assets/Prefabs/Enemy.prefab",
             add_component="Rigidbody")

# Remove component
await prefab("edit",
             asset_path="Assets/Prefabs/Projectile.prefab",
             remove_component="AudioSource")
```

## Examples

**Change max health for all Player instances:**
```python
await prefab("edit",
             asset_path="Assets/Prefabs/Player.prefab",
             component="Health",
             prop="MaxHP",
             value="150")
# → All spawned Player prefabs now have MaxHP=150
```

**Add rigid body to bomb prefab:**
```python
await prefab("edit",
             asset_path="Assets/Prefabs/Bomb.prefab",
             add_component="Rigidbody")
```

**Remove audio from silent enemy variant:**
```python
await prefab("edit",
             asset_path="Assets/Prefabs/SilentEnemy.prefab",
             remove_component="AudioSource")
```

## Workflow: Design → Save → Edit

1. **Design in scene first:**
   ```python
   await create_object(name="MyPrefab", parent="")
   await manage_component(path="MyPrefab", type="Health", action="add")
   await set_property(path="MyPrefab", component="Health", prop="MaxHP", value="100")
   ```

2. **Save as prefab:**
   ```python
   await prefab("save", path="MyPrefab", asset_path="Assets/Prefabs/MyPrefab.prefab")
   ```

3. **Edit prefab directly (no unpacking):**
   ```python
   await prefab("edit",
                asset_path="Assets/Prefabs/MyPrefab.prefab",
                component="Health",
                prop="MaxHP",
                value="150")
   ```

4. **Spawn instances:**
   ```python
   await create_object(name="Enemy1", parent="", prefab_path="Assets/Prefabs/MyPrefab.prefab")
   # → Enemy1 inherits MaxHP=150 from prefab
   ```

## instantiate

Instantiate a prefab as a scene object with optional parent assignment.

**Parameters:**
- `asset_path` (string) — Path to prefab asset (e.g., "Assets/Prefabs/Enemy.prefab")
- `name` (string, optional) — Name for the spawned object; defaults to prefab name
- `parent` (string, optional) — Parent GameObject path; if omitted, spawns at scene root
- `scene` (string, optional) — Scene name (for multi-scene projects)

**Example:**

```python
# Spawn at scene root
await prefab("instantiate", asset_path="Assets/Prefabs/Enemy.prefab")

# Spawn under a parent with custom name
await prefab("instantiate",
             asset_path="Assets/Prefabs/Enemy.prefab",
             name="Enemy1",
             parent="Enemies")

# Equivalent to:
await create_object(name="Enemy1", parent="Enemies", prefab_path="Assets/Prefabs/Enemy.prefab")
```

**Difference from create_object:** The `prefab` instantiate action is optimized for prefab workflows; `create_object` with `prefab_path` is equivalent and more general-purpose.

---

## Supported Operations (edit)

✅ **Can edit:**
- Public properties (int, float, string, bool, Vector3, Quaternion)
- ObjectReferences (assign by path or GUID)
- Color, Bounds, Rect
- Collections (arrays, lists)

❌ **Cannot edit:**
- Private fields (reflection not allowed)
- Non-serializable types
- Nested ScriptableObjects (use scriptable_object tool instead)

## Verification

After editing, verify the change persisted:

```python
await prefab("edit",
             asset_path="Assets/Prefabs/Player.prefab",
             component="Health",
             prop="MaxHP",
             value="200")

# Verify by spawning instance
await create_object(name="TestPlayer", prefab_path="Assets/Prefabs/Player.prefab")
health = await get_component("TestPlayer", "Health")
# → Should show MaxHP: 200
```

---

**See also:** [Asset Management](../tools/assets.md) for asset operations, [Object Tools](../tools/objects.md) for scene instances.
