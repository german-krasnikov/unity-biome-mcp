# Object Tools

Read and author GameObjects, components, serialized properties, hierarchy
relationships, and scene-object references. Prefer full scene paths such as
`/Level/Player`; qualify them as `SceneName:/Level/Player` when loaded scenes
contain ambiguous paths.

Most authoring tools on this page operate on serialized Edit Mode state. Use the
[Runtime Tools](runtime.md) or Playtest DSL for intentional Play Mode changes.

## `get_component` {#get_component}

Read one component when you know its object path and type:

```python
transform = await get_component(
    path="/Player",
    type="Transform",
    fields="position,rotation",
)
health = await get_component(path="/Player", type="Health")
```

`fields` projects the returned text to the requested names. Use `compress=True` to
strip defaults from a large result, or `full=True` only when diagnostic detail was
removed by response distillation.

## `inspect` {#inspect}

Use one `inspect` call for a related multi-object or multi-component snapshot:

```python
state = await inspect(
    paths="/Player,/Enemies/GuardA,/Enemies/GuardB",
    components="Transform,Health",
    fields="position,currentHealth",
)
```

Alternatively, `find_type` discovers objects with one component and inspects them:

```python
lights = await inspect(find_type="Light", components="Transform,Light")
```

Do not combine `find_type` with a separately maintained path list; choose one scope
so the result is reproducible.

## `find_objects` {#find_objects}

`find_objects` is a simple scene-wide filter by name substring, exact tag, exact
layer name, and/or component type:

```python
enemies = await find_objects(name="Enemy", component="Health")
```

It does not support regex, hierarchy-root, or active-state filters. Use
[`search_scene`](scene.md#search_scene) for those queries.

## `get_object_detail` {#get_object_detail}

`get_object_detail` serializes every component and value on one GameObject. It is
heavy; prefer `get_component` for normal reads.

```python
# Replace this process-local decimal instance ID with one returned by Unity.
detail = await get_object_detail(id=123456)
```

Instance IDs are valid only in the current Editor process. They are not asset GUIDs
or the compact hierarchy references returned by `get_hierarchy`.

## `get_components_list` {#get_components_list}

This lower-level reader lists component names for a decimal Unity instance ID:

```python
components = await get_components_list(id=123456)
```

When you already have a path, `inspect(paths="/Player")` is usually clearer.

## `create_object` {#create_object}

Create an empty object, a Unity primitive, or a prefab instance:

```python
await create_object(name="SpawnPoints", parent="/Level")
await create_object(
    name="Player",
    primitive="Capsule",
    components="Rigidbody,PlayerController",
)
await create_object(
    name="GuardA",
    parent="/Enemies",
    prefab_path="Assets/Prefabs/Guard.prefab",
)
```

`parent` must already exist. Use `scene` when the new object belongs in a specific
loaded scene.

## `delete_object` {#delete_object}

Delete by path and verify that the target disappeared:

```python
await delete_object(path="/TemporaryPreview")
missing = await search_scene(query="TemporaryPreview")
```

Deleting a non-empty container requires `force=True` and removes its descendants.
Use a checkpoint or source control before broad destructive changes.

## `manage_component` {#manage_component}

Add or remove a component by short name or full namespace:

```python
await manage_component(path="/Player", type="Rigidbody", action="add")
rigidbody = await get_component(path="/Player", type="Rigidbody")
```

Only `add` and `remove` are supported. To enable or disable a component, set its
serialized enabled property where the component exposes one.

## `set_property` {#set_property}

Change one serialized field or property and read it back:

```python
await set_property(
    path="/Player",
    component="Rigidbody",
    prop="mass",
    value="2",
)
mass = await get_component(path="/Player", type="Rigidbody", fields="mass")
```

Values are strings on the wire and are converted according to the serialized
field: booleans, numbers, vectors, colors, enums, strings, and object references
are supported. Use `dry_run=True` to preview a supported change.

For a scene-object reference, pass a scene path; use `ref_component_type` when the
field expects a particular Component rather than a GameObject:

```python
await set_property(
    path="/Spawner",
    component="Spawner",
    prop="playerCollider",
    value="/Player",
    ref_component_type="CapsuleCollider",
)
```

Asset references use `Assets/...`; `"null"` clears an object-reference field.
Use [`material`](shaders.md#create-and-configure-a-material) for shader properties
instead of treating `Material` as a component.

## `set_property_delta` {#set_property_delta}

Apply an intentional relative numeric or vector change:

```python
await set_property_delta(
    path="/Player",
    component="Health",
    prop="maxHealth",
    delta="+10",
)
```

Read the value before and after; repeated delta calls are not idempotent.

## Hierarchy relationships

### `set_active` {#set_active}

```python
await set_active(path="/UI/PauseMenu", active=False)
```

### `set_parent` {#set_parent}

```python
await set_parent(
    path="/Sword",
    parent="/Player/WeaponSocket",
    world_position_stays=True,
)
```

Pass `parent=None` to move an object to the scene root. Set
`world_position_stays=False` only when retaining its local transform is intended.

### `rename_object` {#rename_object}

```python
new_path = await rename_object(path="/Enemy", name="Guard")
```

Subsequent calls must use the returned path.

### `set_sibling_index` {#set_sibling_index}

```python
await set_sibling_index(path="/UI/Buttons/Play", index=0)
```

The index is zero-based within the current parent.

### `transfer_object` {#transfer_object}

Move an object to another loaded scene, or copy it in the current or target scene:

```python
await transfer_object(
    path="Main:/Player",
    action="move",
    target_scene="Gameplay",
)
copy = await transfer_object(path="Gameplay:/Player", action="copy")
```

When `parent` is supplied, it must resolve in the destination scene.

## `object_diff` {#object_diff}

Compare components, properties, and children on two scene objects:

```python
diff = await object_diff(
    path_a="/Enemies/GuardTemplate",
    path_b="/Enemies/GuardA",
)
```

Scene-qualified paths allow cross-scene comparison.

## Events

### `wire_event` {#wire_event}

Create a persistent UnityEvent listener, then verify it with `list_events`:

```python
await wire_event(
    path="/UI/StartButton",
    component="Button",
    event="onClick",
    target="/GameManager",
    method="StartGame",
    target_component_type="GameManager",
)
listeners = await list_events(
    path="/UI/StartButton",
    component="Button",
    event="onClick",
)
```

See [Component Events](components.md#wire_event) for overload selection and
argument types.

### `unwire_event` {#unwire_event}

`unwire_event` removes one zero-based persistent listener with `index`, or all
persistent listeners on the named event when `index` is omitted. Inspect first;
the operation is destructive.

### `get_unity_events` {#get_unity_events}

Use `get_unity_events(path="/UI")` to audit persistent listeners across a subtree.
Use `list_events` for exact verification of one event field.

## `set_material` {#set_material}

`set_material` is a small scene helper that creates a new in-memory Material,
assigns it to a Renderer, and sets its color:

```python
await set_material(path="/Marker", color="#FF4D4D")
```

It does not create a reusable `.mat` asset. For renderer slots, shared-vs-instance
control, textures, and durable material assets, use
[Shaders and Materials](shaders.md).

## `references` {#references}

`references` inspects serialized **scene-object** references. It is not an asset
dependency tool.

```python
outgoing = await references(
    action="get",
    path="/Player",
    children=True,
    depth=2,
)
incoming = await references(action="find_to", path="/Player/Weapon")
```

`get` lists outgoing references and can recursively follow them to `depth`.
`children=True` also scans the source hierarchy. `find_to` searches loaded scene
components for references to the target.

`remap` is intended for a duplicated root whose components still reference an
original hierarchy. It scans components on `target` and maps references under
`source` to the analogous paths under `target`:

```python
result = await references(
    action="remap",
    path="/Enemies/GuardCopy",
    source="/Enemies/GuardTemplate",
    target="/Enemies/GuardCopy",
)
```

The Python wrapper currently requires `path` for every action; for `remap`, pass
the same target path even though the Unity remapper uses `source` and `target`.
Explicit newline-separated `oldPath=newPath` entries in `mappings` override the
automatic subtree mapping. Inspect the result and references after remapping.

For asset dependencies use
[`asset(action="get_dependencies")`](assets.md#dependencies-and-importer-settings).

## Related workflows

- [Scene Tools](scene.md) — hierarchy discovery, scenes, and guarded changes.
- [Component Tools](components.md) — component references and event details.
- [Batch Operations](batch.md) — combine compatible object operations.
- [Generated Tool Schema](../tools-schema/index.md) — exhaustive signatures and defaults.
