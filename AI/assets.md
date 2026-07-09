# Assets, Materials, Prefabs & Project Settings

Asset database CRUD, prefab lifecycle, material configuration, ScriptableObject management, project-wide settings.

## asset(action, path=None, type=None, name=None, folder=None, source=None, dest=None, prop=None, value=None, recursive=False, labels=None, output=None, include_deps=True)

**Write.** Asset database operations with import/export.

### Actions

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| find | Search assets by name/type/labels | name OR type, folder (optional) | `asset("find", name="PlayerMesh", folder="Assets/Meshes")` |
| get_info | Read asset metadata | path | `asset("get_info", path="Assets/Models/Player.fbx")` |
| create | Create new asset | type (Folder/Material/PhysicMaterial), path | `asset("create", type="Material", path="Assets/NewMat.mat")` |
| move | Relocate asset + .meta | source, dest (both Assets/ paths) | `asset("move", source="Assets/A.prefab", dest="Assets/Prefabs/A.prefab")` |
| validate_move | Test move without executing | source, dest | `asset("validate_move", source="Assets/Old/X.cs", dest="Assets/New/X.cs")` |
| duplicate | Copy asset | path | `asset("duplicate", path="Assets/Material.mat")` |
| delete | Remove asset | path | `asset("delete", path="Assets/Temp.prefab")` |
| get_dependencies | List dependencies | path, include_indirect (bool) | `asset("get_dependencies", path="Assets/Scene.unity", include_deps=True)` |
| import_settings | Configure import params | path, prop, value (varies by type) | `asset("import_settings", path="Assets/Mesh.fbx", prop="importer_type", value="humanoid")` |
| export_package | Serialize to .unitypackage | path, output (filesystem path), include_deps (bool) | `asset("export_package", path="Assets/MyFeature", output="/tmp/export.unitypackage", include_deps=True)` |
| import_package | Load .unitypackage | path (filesystem), source | `asset("import_package", path="/tmp/export.unitypackage")` |

**Batch find:**
```python
await asset("find", type="Material", folder="Assets/UI", labels="hud,animated")
```

**Recursive operations:**
```python
await asset("get_dependencies", path="Assets/", recursive=True)
```

---

## material(action, path=None, object_path=None, shader=None, prop=None, value=None, source=None, targets=None, filter=None, fields=None)

**Write.** Material asset operations + runtime material assignment.

### Actions

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| create | Create material with shader | path, shader (name) | `material("create", path="Assets/NewMat.mat", shader="Standard")` |
| get | Read material properties | path OR object_path | `material("get", path="Assets/PlayerMat.mat")` |
| set | Modify material property | path OR object_path, prop, value | `material("set", path="Assets/Mat.mat", prop="_Color", value="1,0,0,1")` |
| set_fields | Batch property setting — multiple `prop=value` pairs | path OR object_path, fields (`\n`-separated `prop=value`) | `material("set_fields", path="Assets/Mat.mat", fields="_Color=1,0,0,1\n_Glossiness=0.8")` |
| copy | Clone + assign to scene objects | source (asset), targets (comma-sep scene paths) | `material("copy", source="Assets/Base.mat", targets="Player,Enemy")` |
| list_properties | Enumerate all properties | path OR object_path | `material("list_properties", path="Assets/Mat.mat")` |
| list_shaders | List available shaders with optional name filter | filter (optional substring) | `material("list_shaders", filter="URP")` |
| get_shader_errors | Return shader compilation errors for a material | path OR object_path | `material("get_shader_errors", path="Assets/Mat.mat")` |

**Shader lookup:**
```python
# Auto-find by name (case-sensitive)
await material("create", path="Assets/CustomMat.mat", shader="Standard")
await material("create", path="Assets/Unlit.mat", shader="Unlit/Color")
```

**Scene object reference:**
```python
# Get material from Renderer on scene object
await material("get", object_path="Player")
# Set property on that material
await material("set", object_path="Player", prop="_MainColor", value="0,1,0,1")
```

---

## prefab(action, path=None, asset_path=None, base_path=None, variant_path=None, component=None, prop=None, value=None, add_component=None, remove_component=None, recursive=False)

**Write.** Prefab save, variant creation, apply/revert, component management, prefab editing (v0.56.0+).

### Actions

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| save | Save scene instance → prefab asset | path (scene), asset_path (destination) | `prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")` |
| create_variant | Make variant from base prefab | base_path, variant_path | `prefab("create_variant", base_path="Assets/Enemy.prefab", variant_path="Assets/Variants/EnemyFast.prefab")` |
| apply | Push instance changes → base prefab | path (scene instance) | `prefab("apply", path="Player")` |
| revert | Discard instance changes → base state | path (scene instance) | `prefab("revert", path="Player")` |
| get_overrides | List property modifications | path (scene instance) | `prefab("get_overrides", path="Enemy")` |
| unpack | Convert instance → normal GameObject | path (scene instance) | `prefab("unpack", path="SpawnedPrefab")` |
| edit | Modify prefab asset directly (v0.56.0+) | asset_path, component, prop, value | `prefab("edit", asset_path="Assets/Prefabs/Player.prefab", component="Health", prop="MaxHP", value="200")` |
| edit (add component) | Add component to prefab asset | asset_path, add_component | `prefab("edit", asset_path="Assets/Prefabs/Player.prefab", add_component="Rigidbody")` |
| edit (remove component) | Remove component from prefab | asset_path, remove_component | `prefab("edit", asset_path="Assets/Prefabs/Player.prefab", remove_component="AudioSource")` |

**Two-step prefab creation:**
```python
# 1. Design in scene
await set_property(path="Player", component="Health", prop="MaxHP", value="100")
# 2. Save as prefab
await prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")
```

**Prefab editing without unpacking:**
```python
# Modify prefab asset directly (v0.56.0+)
await prefab("edit", asset_path="Assets/Prefabs/Player.prefab", 
             component="Collider", prop="enabled", value="false")
```

---

## scriptable_object(action, path=None, type=None, prop=None, value=None, filter=None)

**Write.** ScriptableObject CRUD + type discovery.

### Actions

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| create | Create SO instance | type (C# class name), path (asset destination) | `scriptable_object("create", type="GameConfig", path="Assets/Config.asset")` |
| get | Read property | path | `scriptable_object("get", path="Assets/Config.asset")` |
| set | Modify property | path, prop, value | `scriptable_object("set", path="Assets/Config.asset", prop="difficulty", value="hard")` |
| list_types | Enumerate all SO types in project | [filter] | `scriptable_object("list_types", filter="Config")` |
| find | Find SOs of type | type | `scriptable_object("find", type="GameConfig")` |

**Type discovery:**
```python
# List all ScriptableObject subclasses
await scriptable_object("list_types")

# Filter by substring
await scriptable_object("list_types", filter="Preset")
```

---

## project_settings(action, target, prop=None, value=None, index=None)

**Write-idempotent.** Project-wide settings: tags, layers, quality, physics, time, player.

### Targets & Properties

| Target | Purpose | Get Example | Set Example |
|--------|---------|-------------|-------------|
| tags | Tag list | `project_settings("get", target="tags")` | `project_settings("set", target="tags", prop="add", value="Enemy")` |
| layers | Layer list | `project_settings("get", target="layers")` | `project_settings("set", target="layers", prop="add", value="UI")` |
| sorting_layers | Sorting layer list | `project_settings("get", target="sorting_layers")` | `project_settings("set", target="sorting_layers", prop="add", value="UI")` |
| quality | QualitySettings (presets) | `project_settings("get", target="quality")` | `project_settings("set", target="quality", prop="level", value="2")` |
| physics | Physics settings | `project_settings("get", target="physics")` | `project_settings("set", target="physics", prop="gravity", value="-9.81,0,0")` |
| time | Time settings | `project_settings("get", target="time")` | `project_settings("set", target="time", prop="fixed_timestep", value="0.02")` |
| player | Player settings | `project_settings("get", target="player")` | `project_settings("set", target="player", prop="icon", value="Assets/Icon.png")` |

**Batch tag/layer setup:**
```python
# Add multiple tags
for tag in ["Player", "Enemy", "Projectile"]:
    await project_settings("set", target="tags", prop="add", value=tag)
```

---

## Integration Patterns

**One-shot prefab creation:**
```python
# Design object in scene
await create_object(name="Player", parent="/", components=["Rigidbody", "Health"])
await set_property(path="Player", component="Health", prop="MaxHP", value="100")

# Save → prefab
await prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")

# Spawn clones from asset
await prefab("create_variant", base_path="Assets/Prefabs/Player.prefab", 
             variant_path="Assets/Variants/BossPlayer.prefab")
```

**Material workflow:**
```python
# Create material asset
await material("create", path="Assets/Enemy.mat", shader="Standard")

# Configure it
await material("set", path="Assets/Enemy.mat", prop="_Color", value="1,0,0,1")

# Apply to scene objects
await material("copy", source="Assets/Enemy.mat", targets="Enemy1,Enemy2")
```

**ScriptableObject config:**
```python
# Create config asset
await scriptable_object("create", type="GameConfig", path="Assets/GameConfig.asset")

# Load + modify
await scriptable_object("set", path="Assets/GameConfig.asset", prop="difficulty", value="hard")
```

---

**See also:** AI/tools-reference.md (ASSETS category), AI/batch.md (batch operations), `.claude/skills/token-optimization.md`.
