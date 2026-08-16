# Asset and Project Tools

Manage Unity assets, prefabs, project settings, packages, builds, baking, and
rendering analysis. Use `Assets/...` paths for project assets unless a tool
explicitly asks for an absolute file-system path.

Material and shader authoring has one canonical guide:
[Shaders and Materials](shaders.md).

## `asset`

`asset` wraps common `AssetDatabase` operations. Prefer it over direct file-system
moves so Unity keeps `.meta` files and GUID references intact.

### Find and inspect assets

```python
materials = await asset(
    action="find",
    type="Material",
    name="UI",
    folder="Assets/Art",
    labels="approved",
)

info = await asset(
    action="get_info",
    path="Assets/Models/Player.fbx",
)
```

`find` accepts any combination of `type`, `name`, `folder`, and comma-separated
`labels`; at least one is required. Results are capped at 200. `get_info` returns
the type, GUID, file size, and direct dependencies.

### Create, duplicate, move, and delete

```python
await asset(action="create", type="Folder", path="Assets/Game/Generated")

await asset(
    action="duplicate",
    source="Assets/Game/Base.asset",
    dest="Assets/Game/Generated/Variant.asset",
)

check = await asset(
    action="validate_move",
    source="Assets/Game/Old.asset",
    dest="Assets/Game/New.asset",
)
await asset(
    action="move",
    source="Assets/Game/Old.asset",
    dest="Assets/Game/New.asset",
)
```

`validate_move` uses Unity's move validation without changing the asset. Use
`path_only=True` only for a syntax check before the destination folder exists.

`create` supports `Folder`, `Material`, `PhysicMaterial`, `AnimatorController`, and
`ScriptableObject`. Creating a ScriptableObject through this generic action also
requires `class_name`; the dedicated `scriptable_object` tool below is clearer for
initial field values.

```python
# Destructive: removes the asset through AssetDatabase, including its .meta file.
await asset(action="delete", path="Assets/Game/Generated/Variant.asset")
```

Check reverse dependencies before deleting or moving a widely used asset.

### Dependencies and importer settings

```python
direct = await asset(
    action="get_dependencies",
    path="Assets/Scenes/Main.unity",
)
transitive = await asset(
    action="get_dependencies",
    path="Assets/Scenes/Main.unity",
    recursive=True,
)
users = await asset(
    action="find_dependents",
    path="Assets/Materials/Shared.mat",
)
```

`find_dependents` scans project assets and caps the result at 100. It is more
expensive than a forward dependency read.

Read an importer property by omitting `value`, or write a public writable importer
property and let Unity reimport the asset:

```python
current = await asset(
    action="import_settings",
    path="Assets/Textures/Icon.png",
    prop="isReadable",
)
await asset(
    action="import_settings",
    path="Assets/Textures/Icon.png",
    prop="isReadable",
    value="true",
)
```

Importer properties depend on the asset type. An omitted `prop` and `value` dumps
the public readable properties for discovery.

### Text, packages, and reimport

```python
text = await asset(action="read_text", path="Assets/Config/game.json")
await asset(
    action="write_text",
    path="Assets/Config/game.json",
    content='{"difficulty":"normal"}',
)
await asset(action="reimport", path="Assets/Config/game.json")
```

`write_text` participates in the Python change-capture pipeline, but it still
overwrites the target text file. Read the current file first when ownership is
uncertain.

`export_package` requires an asset `path` and an output file-system `output`.
`include_deps` defaults to true. `import_package` takes the absolute or accessible
file-system path to a `.unitypackage`.

## `prefab`

Use the canonical [Prefab Workflow](../features/prefab-edit.md) for save,
instantiate, variant, override, edit, apply, revert, and unpack examples.

The public actions are `save`, `instantiate`, `create_variant`, `apply`, `revert`,
`get_overrides`, `unpack`, and `edit`. A reliable creation flow is:

```python
await create_object(name="Pickup", primitive="Sphere")
await manage_component(path="/Pickup", type="Pickup", action="add")
await prefab(
    action="save",
    path="/Pickup",
    asset_path="Assets/Prefabs/Pickup.prefab",
    mode="new",
)
```

Direct prefab editing changes the asset and therefore every instance that inherits
the value. Inspect overrides before applying or reverting instance changes.

## `scriptable_object`

Create and maintain ScriptableObject configuration assets.

```python
await scriptable_object(
    action="create",
    type="GameSettings",
    path="Assets/Config/GameSettings.asset",
    fields="maxLevel=50\nstartingGold=100",
)

settings = await scriptable_object(
    action="get",
    path="Assets/Config/GameSettings.asset",
    filter="maxLevel,startingGold",
)

await scriptable_object(
    action="set",
    path="Assets/Config/GameSettings.asset",
    prop="maxLevel",
    value="60",
)
```

Other actions are `list_types` (optional name `filter`) and `find` (required
`type`). For a multi-field update, pass newline-separated `fields` instead of
`prop` and `value`.

## `project_settings`

Read or change supported project-wide settings. Always read the target first and
record the original value; these changes can affect every scene and build.

```python
physics = await project_settings(action="get", target="physics")
await project_settings(
    action="set",
    target="physics",
    prop="gravity",
    value="0,-15,0",
)

tags = await project_settings(action="get", target="tags")
await project_settings(action="set", target="tags", prop="Enemy")
await project_settings(
    action="set",
    target="tags",
    prop="remove",
    value="Obsolete",
)

await project_settings(
    action="set",
    target="layers",
    index=8,
    value="Enemy",
)
```

Targets are `tags`, `layers`, `sorting_layers`, `quality`, `physics`, `time`,
`player`, `graphics`, `audio`, and `input`. Sorting layers, audio, and legacy input
are read-only in the current implementation.

Specify the scripting backend's build target explicitly; otherwise Unity defaults
this operation to the Standalone target group:

```python
await project_settings(
    action="set",
    target="player",
    prop="ScriptingBackend",
    value="IL2CPP",
    build_target="Standalone",
)
```

## `build`

Builds a player with the current project configuration.

```python
result = await build(
    action="build",
    target="StandaloneOSX",
    scenes="Assets/Scenes/Boot.unity,Assets/Scenes/Main.unity",
    path="Builds/macOS/Game.app",
    dev=True,
)
```

`target`, `scenes`, and `path` are optional; Unity uses the active target, enabled
Build Settings scenes, and `Builds/<target>` defaults when omitted. Build output is
not automatically acceptance evidence—read the result and run the built player or
project-specific smoke checks.

## `package`

Wraps Unity Package Manager list, search, add, and remove operations.

```python
installed = await package(action="list")
matches = await package(action="search", query="cinemachine")
await package(action="add", name="com.unity.cinemachine")
```

Use `version` with `add` only when the project intentionally pins a compatible
version. Adding or removing a package can trigger downloads, asset imports, and a
domain reload; wait for compilation before continuing.

## `bake`

Controls lighting and occlusion baking.

```python
await bake(target="lighting", action="start")
status = await bake(target="lighting", action="status")
```

Lighting actions are `start`, `status`, `cancel`, `clear`, and `settings`. Lighting
start is asynchronous, so poll `status` until it becomes idle and check the
Console. Occlusion actions are `start`, `status`, and `clear`; occlusion start is a
synchronous Unity operation.

## Analyze rendering {#analyze-rendering}

`render_analyze` reads scene rendering health:

```python
stats = await render_analyze(action="stats", detail="full")
batching = await render_analyze(action="batching", path="/Level")
lights = await render_analyze(action="lights")
audit = await render_analyze(action="audit", path="/Level")
```

Actions are `stats`, `materials`, `shaders`, `lights`, `batching`, `overdraw`,
`audit`, `compare`, `frame_debug`, `shadow_audit`, `probe_audit`, and
`light_optimize`. `detail` is `brief` or `full` where the action supports it.
`frame_debug` uses Unity's Frame Debugger data and briefly pauses rendering.

Calling `stats` saves one in-memory rendering baseline. A later `compare` compares
current draw calls, batches, and set-pass calls with that last baseline:

```python
await render_analyze(action="stats")
# ...make a rendering change...
delta = await render_analyze(action="compare")
```

The current implementation does not select named baselines; `baseline_id` is
accepted by the wrapper but comparison still uses the most recent `stats` sample.
Live render counters can be zero when no Scene View is open, and analysis is not a
substitute for profiling the target platform.

## `material` {#material}

Material creation, values, renderer slots, shared-vs-instance behavior, shader
errors, and audits are documented in [Shaders and Materials](shaders.md).

## `material_audit`

See [Audit materials and textures](shaders.md#audit-materials-and-textures).

## `shader`

See [Shaders and Materials](shaders.md#create-a-shader-asset) and
[Shader Graph](shaders.md#work-with-shader-graph).

## `references`

The `references` tool reads and remaps **scene-object** references, not asset-file
dependencies. See [Object references](objects.md#references). For asset GUID
dependencies, use `asset(action="get_dependencies")` or
`asset(action="find_dependents")` above.

## Related workflows

- [Shaders and Materials](shaders.md) — canonical material/shader guide.
- [Prefab Workflow](../features/prefab-edit.md) — safe prefab asset operations.
- [Object Tools](objects.md) — scene objects and serialized references.
- [Batch Operations](batch.md) — combine compatible operations.
- [Generated Tool Schema](../tools-schema/index.md) — exhaustive parameters.
