# Asset Tools

Manage prefabs, materials, ScriptableObjects, and project settings. Control the asset pipeline without leaving chat.

## asset

Core asset database operations: search, copy, move, delete, import/export.

**Parameters:**
- `action` (string) — "find" | "get_info" | "create" | "move" | "validate_move" | "duplicate" | "delete" | "get_dependencies" | "find_dependents" | "import_settings" | "export_package" | "import_package"
- `path` (string) — Asset path (Assets-relative)
- `type` (string, optional) — Asset type filter
- `name` (string, optional) — Name for search
- `folder` (string, optional) — Folder scope for search
- `source`, `dest` (string, optional) — For move operations
- `recursive` (bool, default=false) — Include subfolders
- `output` (string, optional) — Export destination
- `include_deps` (bool, default=true) — Include dependencies

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| find | Search assets by name/type/labels | name OR type, folder (opt) | `asset("find", name="PlayerMesh", folder="Assets/Meshes")` |
| get_info | Read asset metadata | path | `asset("get_info", path="Assets/Models/Player.fbx")` |
| create | Create new asset | type, path | `asset("create", type="Folder", path="Assets/NewFolder")` |
| move | Relocate asset + .meta | source, dest | `asset("move", source="Assets/Old/X.mat", dest="Assets/Materials/X.mat")` |
| validate_move | Test move without executing | source, dest | `asset("validate_move", source="Assets/A.cs", dest="Assets/B.cs")` |
| duplicate | Copy asset | path | `asset("duplicate", path="Assets/Material.mat")` |
| delete | Remove asset + .meta | path | `asset("delete", path="Assets/Temp.prefab")` |
| get_dependencies | List forward dependencies | path | `asset("get_dependencies", path="Assets/Scene.unity", include_deps=true)` |
| find_dependents | Reverse dependencies — who references this asset | path | `asset("find_dependents", path="Assets/Materials/Shared.mat")` |
| import_settings | Configure import params | path, prop, value | `asset("import_settings", path="Assets/Mesh.fbx", prop="importer_type", value="humanoid")` |
| export_package | Create .unitypackage | path, output | `asset("export_package", path="Assets/MyFeature", output="/tmp/export.unitypackage")` |
| import_package | Load .unitypackage | path (file system) | `asset("import_package", path="/tmp/export.unitypackage")` |

**Example:**

```python
# Find materials in folder
mats = await asset("find", type="Material", folder="Assets/UI", labels="hud,animated")

# Get asset info
info = await asset("get_info", path="Assets/Models/Player.fbx")

# Create new folder
await asset("create", type="Folder", path="Assets/Materials")

# Move asset
await asset("move", source="Assets/Old/Player.mat", dest="Assets/Materials/Player.mat")

# Delete temp file
await asset("delete", path="Assets/Temp.prefab")

# Get forward dependencies
deps = await asset("get_dependencies", path="Assets/Scenes/Level1.unity", include_deps=true)

# Find reverse dependencies (who references this asset)
dependents = await asset("find_dependents", path="Assets/Materials/Shared.mat")

# Export package
await asset("export_package", path="Assets/MyFeature", output="/tmp/feature.unitypackage", include_deps=true)

# Import package
await asset("import_package", path="/tmp/feature.unitypackage")
```

---

## material

Manage materials and shaders. Create, modify, and assign materials to objects.

**Parameters:**
- `action` (string) — "create" | "get" | "set" | "copy" | "list_properties" | "list_slots" | "get_errors" | "list_shaders" | "set_fields"
- `path` (string, optional) — Material asset path
- `object_path` (string, optional) — Scene object path
- `shader` (string, optional) — Shader name (e.g., "Standard", "Unlit/Color")
- `prop` (string, optional) — Property name (e.g., "_Color", "_MainTexture")
- `value` (string, optional) — Property value (for set_fields: newline-separated prop=val pairs)
- `source` (string, optional) — Source material for copy
- `targets` (string, optional) — Comma-separated scene paths for apply
- `slot` (int, optional) — Material slot index (default 0)
- `filter` (string, optional) — Name filter for list_shaders
- `target` (string, optional) — "shared" | "instance" | "asset" (default "shared") — controls which material is modified by set

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| create | Create material with shader | path, shader | `material("create", path="Assets/NewMat.mat", shader="Standard")` |
| get | Read material properties | path OR object_path | `material("get", path="Assets/PlayerMat.mat")` |
| set | Modify material property | path OR object_path, prop, value | `material("set", path="Assets/Mat.mat", prop="_Color", value="1,0,0,1")` |
| set_fields | Set multiple properties at once | path, value (newline-separated) | `material("set_fields", path="Assets/Mat.mat", value="_Color=1,0,0,1\n_Metallic=0.8")` |
| copy | Clone + assign to objects | source, targets | `material("copy", source="Assets/Base.mat", targets="Player,Enemy")` |
| list_properties | Enumerate all properties | path OR object_path | `material("list_properties", path="Assets/Mat.mat")` |
| list_slots | List material slots on object | object_path | `material("list_slots", object_path="Player")` |
| list_shaders | List available shaders | filter (optional) | `material("list_shaders", filter="URP")` |
| get_errors | Get shader compilation errors | path | `material("get_errors", path="Assets/Shaders/Custom.shader")` |

**Color Format:** RGB or hex
- Vector: `"0.5,0.2,1,1"` (RGBA)
- Hex: `"#FF0000"` (red)
- Shorthand: `"red"` (standard colors)

**Example:**

```python
# Create material
await material("create", path="Assets/RedMat.mat", shader="Standard")

# Read properties
props = await material("get", path="Assets/RedMat.mat")

# Set color
await material("set", path="Assets/RedMat.mat", prop="_Color", value="#FF0000")

# Set texture
await material("set", path="Assets/Mat.mat", prop="_MainTexture", value="Assets/Textures/Wood.png")

# Copy material to scene objects
await material("copy", source="Assets/BaseMat.mat", targets="Player,Enemy,Boss")

# List all properties
props = await material("list_properties", path="Assets/Mat.mat")

# Modify material on scene object
await material("set", object_path="Player", prop="_Metallic", value="0.8")

# List material slots on scene object
slots = await material("list_slots", object_path="Player")

# Set multiple properties at once
await material("set_fields", path="Assets/Mat.mat", value="_Color=1,0,0,1\n_Metallic=0.8\n_Smoothness=0.5")

# List available shaders (with optional filter)
shaders = await material("list_shaders", filter="URP")

# Check shader errors
errors = await material("get_errors", path="Assets/Shaders/Custom.shader")

# Use specific material slot
await material("set", object_path="Player", prop="_Color", value="#FF0000", slot=1)

# Modify instance material (not shared)
await material("set", object_path="Player", prop="_Color", value="#FF0000", target="instance")
```

---

## prefab

Create, modify, and manage prefabs. Save instances as prefabs or edit prefabs directly (v0.56.0+).

**Parameters:**
- `action` (string) — "save" | "create_variant" | "apply" | "revert" | "get_overrides" | "unpack" | "edit"
- `path` (string, optional) — Scene instance path
- `asset_path` (string, optional) — Prefab asset path (Assets-relative)
- `base_path` (string, optional) — Base prefab for variant
- `variant_path` (string, optional) — New variant path
- `component` (string, optional) — Component for edit action
- `add_component`, `remove_component` (string, optional) — For component management
- `prop`, `value` (string, optional) — Property to modify
- `recursive` (bool, default=false) — Apply/revert recursively
- `mode` (string, optional) — For save: "new" | "overwrite" (default "overwrite")
- `scope` (string, optional) — For revert: "object" (default) | "children"
- `format` (string, optional) — For get_overrides: "text" (default) | "structured"

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| save | Scene instance → prefab asset | path, asset_path | `prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")` |
| create_variant | Make variant from base | base_path, variant_path | `prefab("create_variant", base_path="Assets/Enemy.prefab", variant_path="Assets/Variants/EnemyFast.prefab")` |
| apply | Push instance changes → base | path | `prefab("apply", path="Player")` |
| revert | Discard instance changes | path | `prefab("revert", path="Player")` |
| get_overrides | List property modifications | path | `prefab("get_overrides", path="Enemy")` |
| unpack | Convert instance → GameObject | path | `prefab("unpack", path="SpawnedPrefab")` |
| edit | Modify prefab asset directly | asset_path, component, prop, value | `prefab("edit", asset_path="Assets/Player.prefab", component="Health", prop="MaxHP", value="200")` |
| edit (add) | Add component to prefab | asset_path, add_component | `prefab("edit", asset_path="Assets/Player.prefab", add_component="Rigidbody")` |
| edit (remove) | Remove component | asset_path, remove_component | `prefab("edit", asset_path="Assets/Player.prefab", remove_component="AudioSource")` |

**Workflow: Two-step prefab creation**

```python
# 1. Design in scene
await create_object("Player")
await manage_component("Player", "add", "Health")
await set_property("Player", "Health", "maxHp", "100")

# 2. Save as prefab
await prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")
```

**Workflow: Direct prefab editing (v0.56.0+)**

```python
# Modify prefab asset without unpacking
await prefab("edit", 
  asset_path="Assets/Prefabs/Player.prefab",
  component="Health",
  prop="MaxHP",
  value="200"
)

# Add component to prefab
await prefab("edit",
  asset_path="Assets/Prefabs/Player.prefab",
  add_component="Rigidbody"
)

# Remove component
await prefab("edit",
  asset_path="Assets/Prefabs/Player.prefab",
  remove_component="AudioSource"
)
```

**Workflow: Variant management**

```python
# Create variant (inherits from base)
await prefab("create_variant",
  base_path="Assets/Prefabs/Enemy.prefab",
  variant_path="Assets/Prefabs/Variants/EnemyFast.prefab"
)

# Modify variant (doesn't affect base)
await prefab("edit",
  asset_path="Assets/Prefabs/Variants/EnemyFast.prefab",
  component="Health",
  prop="maxHp",
  value="50"
)
```

---

## scriptable_object

Manage ScriptableObject assets. Create, modify, and save ScriptableObject configurations.

**Parameters:**
- `action` (string) — "create" | "get" | "set" | "list_types" | "find"
- `path` (string, optional) — Asset path or scene instance
- `type` (string, optional) — ScriptableObject class name
- `prop` (string, optional) — Property name
- `value` (string, optional) — Property value
- `fields` (string, optional) — Newline-separated prop=value pairs (for create/set bulk)
- `filter` (string, optional) — Name filter for list_types / comma-separated field filter for get

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| create | Create new ScriptableObject | path, type [, fields] | `scriptable_object("create", path="Assets/GameConfig.asset", type="GameSettings")` |
| get | Read ScriptableObject | path [, filter] | `scriptable_object("get", path="Assets/GameConfig.asset")` |
| set | Modify property | path, prop, value OR fields | `scriptable_object("set", path="Assets/GameConfig.asset", prop="maxLevel", value="50")` |
| list_types | List available SO types | filter (optional) | `scriptable_object("list_types", filter="Game")` |
| find | Find SO assets by type | type | `scriptable_object("find", type="GameSettings")` |

**Example:**

```python
# Create new ScriptableObject
await scriptable_object("create", 
  path="Assets/GameConfig.asset",
  type="GameSettings"
)

# Create with initial fields
await scriptable_object("create",
  path="Assets/GameConfig.asset",
  type="GameSettings",
  fields="maxLevel=50\nstartGold=100"
)

# Read configuration
config = await scriptable_object("get", path="Assets/GameConfig.asset")

# Read specific fields only
config = await scriptable_object("get", path="Assets/GameConfig.asset", filter="maxLevel,startGold")

# Modify property
await scriptable_object("set",
  path="Assets/GameConfig.asset",
  prop="maxLevel",
  value="100"
)

# Set multiple fields at once
await scriptable_object("set",
  path="Assets/GameConfig.asset",
  fields="maxLevel=100\nstartGold=500"
)

# List available ScriptableObject types
types = await scriptable_object("list_types", filter="Config")

# Find all instances of a type
configs = await scriptable_object("find", type="GameSettings")
```

---

## project_settings

Access and modify project-wide settings.

**Parameters:**
- `action` (string) — "get" | "set"
- `target` (string) — Setting category: "tags" | "layers" | "sorting_layers" | "quality" | "physics" | "time" | "player"
- `prop` (string, optional) — Property name within the target category
- `value` (string, optional) — New value
- `index` (int, optional) — Index for array-based settings (e.g., layers)

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| get | Read project setting | `project_settings("get", target="physics")` |
| set | Modify setting | `project_settings("set", target="physics", prop="gravity", value="0,-15,0")` |

**Example:**

```python
# Read physics settings
physics = await project_settings("get", target="physics")

# Set gravity
await project_settings("set", target="physics", prop="gravity", value="0,-15,0")

# Read time settings
time = await project_settings("get", target="time")

# Read tags
tags = await project_settings("get", target="tags")

# Set a layer by index
await project_settings("set", target="layers", prop="layer", value="MyLayer", index=8)
```

---

## material_audit

Scene-wide material and texture audit. Finds duplicates, compression issues, and optimization opportunities.

**Parameters:**
- `action` (string, default="summary") — "summary" | "materials" | "textures" | "duplicates" | "compression" | "recommendations"
- `platform` (string, optional) — "Android" | "iOS" | "Standalone" | "Default" (for compression check)

**Actions:**

| Action | Purpose | Example |
|--------|---------|---------|
| summary | Overview of all materials/textures | `material_audit("summary")` |
| materials | List all materials in scene | `material_audit("materials")` |
| textures | List all textures in scene | `material_audit("textures")` |
| duplicates | Find duplicate materials | `material_audit("duplicates")` |
| compression | Check texture compression settings | `material_audit("compression", platform="Android")` |
| recommendations | Optimization suggestions | `material_audit("recommendations")` |

**Example:**

```python
# Quick overview
summary = await material_audit()

# Check for duplicate materials
dupes = await material_audit("duplicates")

# Platform-specific compression audit
issues = await material_audit("compression", platform="Android")

# Get optimization recommendations
recs = await material_audit("recommendations")
```

---

## shader

Manage shaders and their properties. See [Shader Tools](shaders.md) for the complete reference.

---

## references

Find, search, and remap asset references (standalone MCP tool, also batchable).

**Parameters:**
- `action` (string) — "get" | "find_to" | "remap"
- `path` (string) — Asset path to analyze
- `children` (bool, default=false) — Include child objects
- `depth` (int, default=1) — Recursion depth
- `source` (string, optional) — Source asset for remap
- `target` (string, optional) — Target asset for remap / reverse search target
- `mappings` (string, optional) — Bulk remap mappings (for remap action)

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| get | Outgoing references from asset | path | `references("get", path="Assets/Prefabs/Player.prefab")` |
| find_to | Reverse search — who references this asset | path | `references("find_to", path="Assets/Materials/Shared.mat")` |
| remap | Remap references from source to target | path, source, target OR mappings | `references("remap", path="Assets/Scene.unity", source="Assets/Old.mat", target="Assets/New.mat")` |

**Example:**

```python
# Outgoing references from a prefab
refs = await references("get", path="Assets/Prefabs/Player.prefab")

# Who references this material?
users = await references("find_to", path="Assets/Materials/Shared.mat")

# Remap a reference
await references("remap", path="Assets/Scene.unity", source="Assets/Old.mat", target="Assets/New.mat")

# Deep scan with children
refs = await references("get", path="Assets/Prefabs/Player.prefab", children=True, depth=3)
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Create material + assign | material("create") + material("copy") | `await material("create", path="Assets/New.mat", shader="Standard"); await material("copy", source="Assets/New.mat", targets="Player")` |
| Save scene instance as prefab | prefab("save") | `await prefab("save", path="Player", asset_path="Assets/Prefabs/Player.prefab")` |
| Edit prefab without unpacking | prefab("edit") | `await prefab("edit", asset_path="Assets/Prefabs/Player.prefab", component="Health", prop="maxHp", value="200")` |
| Create variant | prefab("create_variant") | `await prefab("create_variant", base_path="Assets/Enemy.prefab", variant_path="Assets/Variants/EnemyFast.prefab")` |
| Organize assets | asset("move") + asset("create") | `await asset("create", type="Folder", path="Assets/Materials"); await asset("move", source="Assets/Old.mat", dest="Assets/Materials/Old.mat")` |
| Export for sharing | asset("export_package") | `await asset("export_package", path="Assets/MyFeature", output="/tmp/export.unitypackage")` |

---

**See also:** [Batch](batch.md) for combining asset operations, [Objects](objects.md) for scene-instance material assignment.
