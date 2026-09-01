# Scene Tools

Use scene tools to inspect the hierarchy, manage loaded scenes, and apply verified
Edit Mode changes. Start with a narrow read, make the change, and verify the result
before saving.

For object-level reads and edits, see [Object Tools](objects.md); persistent
events are covered in [Component and Event Tools](components.md).
For screenshots, checkpoints, session recovery, and other cross-cutting helpers,
see [System Tools](system.md).

## Inspect a scene

### `get_hierarchy`

Returns the loaded scene hierarchy as a compact text tree. Without a `scene`
filter, it includes every loaded scene and adds scene headers when needed.

```python
# Begin with a shallow overview.
hierarchy = await get_hierarchy(depth=2)

# Include component names only when they help answer the question.
player = await get_hierarchy(root="/Player", depth=3, components=True)
```

Useful parameters:

- `root` scopes the read to one subtree.
- `filter` keeps objects whose names contain the supplied text.
- `scene` selects one loaded scene by name; omitting it includes all loaded
  scenes.
- `summary=True` returns scene/root counts and direct root objects without
  compact object references.
- `incremental=True` returns `NO_CHANGE` when the scene has not changed since the
  previous incremental read.
- `compress=True` groups repeated sibling slots, points, and visual meshes.
- `full=True` bypasses response distillation.

Object references are shown as compact `&...` identifiers. They are
process-local: never reuse them after an Editor restart, and call
`get_hierarchy` again when one becomes stale after a connection lifecycle
change. Prefer a stable scene path in calls that accept one, and use an
identifier when duplicate names make the path ambiguous.

### `search_scene`

Searches by name and optional filters without reading the entire hierarchy.

```python
players = await search_scene(query="tag=Player active=true")
rigidbodies = await search_scene(query="t:Rigidbody", root="/Gameplay")
bosses = await search_scene(query="Boss", scene="Dungeon", limit=20)
```

Supported query terms are plain name text, `t:Component`, `tag=Tag`, `layer=N`, and
`active=true|false`. Terms can be combined. `limit=0` removes the result limit; use
it only when the result set is known to be small.

### `scene_diff`

Compares serialized hierarchy lines with the previous snapshot. The first call
creates the snapshot.

```python
await scene_diff()
await create_object(name="SpawnPoint", parent="/Level")
changes = await scene_diff()
```

This compares hierarchy lines—object names, structure, and active markers. It
does not compare component fields or runtime values. Use component reads or
[runtime reads and waits](runtime.md) for those checks.

## Manage loaded scenes

### `scene`

The `scene` tool supports `new`, `open`, `save`, `discard`, `open_additive`, `close`,
`set_active`, `list`, and `save_copy`.

```python
loaded = await scene(action="list")
await scene(action="open_additive", path="Assets/Scenes/UI.unity")
await scene(action="set_active", path="Gameplay")
await scene(action="close", path="UI")
```

`path` is required for `open`, `open_additive`, `close`, `set_active`, and
`save_copy`. For `save`, omit it to use the scene's current path; an untitled
scene requires a path. In a multi-scene setup, `scene` selects the target for
`save`, `discard`, or `save_copy`. `save_copy` writes the current in-memory
state to another path without changing the active scene reference.

Unsaved work can be lost by `open`, `close`, or `discard`. Inspect `scene(action="list")`
first when scene ownership is uncertain.

### `editor`

Reads editor state, controls Play Mode, or changes the current selection.

```python
state = await editor(action="state")
await editor(action="select", path="/Player")
await editor(action="play")
# ...run runtime checks...
await editor(action="stop")
```

Actions are `state`, `play`, `pause`, `stop`, `select`, `project_path`, and
`mutation_mode`. For multi-selection, pass comma-separated paths through `paths`.

For `mutation_mode`, omit `enable` to query current intent; pass `enable=true` or
`enable=false` to set the preference. Mutation Mode is an optional feature; check
`mcp_status()` for availability and operational state before relying on it.

## Apply a guarded scene change

Use `scene_change_plan` with `apply_scene_change` when a change should be gated on
compile state, console errors, target resolution, and post-change verification.

```python
plan = await scene_change_plan(
    goal="Add a spawn point",
    targets="/Level",
)
if plan.startswith("FAIL:"):
    raise RuntimeError(plan)

plan_id = plan.splitlines()[0].removeprefix("plan_id=")
result = await apply_scene_change(
    plan_id=plan_id,
    commands=(
        "create_object name=SpawnPoint parent=/Level\n"
        "set_property path=/Level/SpawnPoint component=Transform "
        "prop=m_LocalPosition value=(0,0,5)"
    ),
    verify=True,
    save=True,
)
```

The apply step accepts only scene operations with proven Unity Undo coverage:
object lifecycle and hierarchy changes, serialized property/component/event
changes, collider fitting, uGUI creation/layout, and `attach_uitk`. It rejects an
empty plan, nested batches, unknown/plugin commands, and asset, file, package, or
code-execution operations before sending anything to Unity.

Accepted commands run with `atomic=true` and `on_error=stop`. If a handler
returns an error or Unity reports `ATOMIC_ROLLBACK`, the tool stops without
verification or saving and reports `mutations=failed`. Keep file and asset work
outside this transaction and verify its partial-failure behavior separately.

With `verify=True`, saving occurs only after reference and console checks succeed.
Read the returned `state`, `verified`, and `saved` fields instead of assuming that a
completed call saved the scene. Plans expire after ten minutes and cannot be used
while Unity is in Play Mode.

## Create or configure several objects

The macro tools below expand to batch commands and append lightweight inspection.
They use `on_error=continue`; use the guarded workflow above when all-or-nothing
behavior is required.

### `setup_objects`

Creates objects from one specification per line:

```python
result = await setup_objects("""
GuardA primitive=Capsule parent=/Enemies pos=(0,0,0) components=Health,GuardAI
GuardB primitive=Capsule parent=/Enemies pos=(4,0,0) components=Health,GuardAI
""")
```

Each line starts with the object name and can include `primitive`, `parent`, `pos`,
and a comma-separated `components` list.

### `set_properties`

Sets several serialized properties on one object:

```python
result = await set_properties(
    path="/Player",
    props="Transform.m_LocalPosition=(1,0,0);Rigidbody.mass=5",
)
```

### `configure_objects`

Sets properties across several existing objects:

```python
result = await configure_objects("""
/GuardA Transform.m_LocalPosition=(1,0,0) Health.maxHp=100
/GuardB Transform.m_LocalPosition=(3,0,0) Health.maxHp=80
""")
```

Only scene paths beginning with `/` (or a qualified multi-scene path) are accepted
by this macro.

## Scene environment

### `scene_environment`

Reads or writes ambient light, fog, reflections, and related render settings.

```python
environment = await scene_environment(action="get")
await scene_environment(action="set", prop="fog", value="true")
await scene_environment(action="set", prop="fogColor", value="0.5,0.5,0.5")
```

Common properties include `ambientMode`, `ambientLight`, `ambientIntensity`,
`ambientSkyColor`, `ambientEquatorColor`, `ambientGroundColor`, `fog`, `fogColor`,
`fogMode`, `fogDensity`, `fogStartDistance`, `fogEndDistance`,
`reflectionIntensity`, `reflectionBounces`, `subtractiveShadowColor`, and
`defaultReflectionResolution`.

## Selection helpers

Use `get_selection()` to read the selected GameObject and its component list. Use
`ping_object(path="/Player")` to select and highlight a known object. These helpers
are convenient for interactive work, but verification should use hierarchy,
component, or runtime reads rather than selection state.

## Related workflows

- [Object tools](objects.md) — create, inspect, and modify GameObjects and components.
- [Component and event tools](components.md) — inspect references and wire persistent events.
- [Batch operations](batch.md) — command syntax, failure policy, and atomic batches.
- [Runtime tools](runtime.md) — Play Mode reads, waits, and method calls.
- [Screenshots and visual comparison](screenshots.md) — visual evidence and baselines.
- [Diagnostics](diagnostics.md) — compile, console, and reference recovery.
- [Generated tool schema](../tools-schema/index.md) — exhaustive signatures and defaults.
