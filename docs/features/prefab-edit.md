# Edit prefab assets

Use `prefab(action="edit")` to change a prefab asset without unpacking an
instance into the open scene. The change affects future instances and existing
instances that have not overridden the edited property.

## Choose the target

| Goal | Tool |
|---|---|
| Change the prefab asset | `prefab(action="edit")` |
| Change one scene instance | `set_property` or `manage_component` |
| Push instance overrides to its prefab | `prefab(action="apply")` |
| Discard instance overrides | `prefab(action="revert")` |

Keep prefab assets under version control. Asset editing is durable and does not
have the same recovery guarantees as a scene-only Unity Undo operation.

## Change a serialized property

```python
result = await prefab(
    action="edit",
    asset_path="Assets/Prefabs/Player.prefab",
    component="Health",
    prop="MaxHP",
    value="150",
)
```

The component must exist on the selected prefab object and `prop` must resolve
to a Unity-serialized property. Use the serialized property name shown by the
component/schema tools; ordinary C# reflection-only properties are not edited.

To target a child, pass its path relative to the prefab root through `path`:

```python
await prefab(
    action="edit",
    asset_path="Assets/Prefabs/Player.prefab",
    path="Visual/Nameplate",
    component="TextMeshProUGUI",
    prop="m_text",
    value="Player",
)
```

## Add or remove a component

```python
await prefab(
    action="edit",
    asset_path="Assets/Prefabs/Bomb.prefab",
    add_component="Rigidbody",
)

await prefab(
    action="edit",
    asset_path="Assets/Prefabs/SilentEnemy.prefab",
    remove_component="AudioSource",
)
```

The operation is idempotent when the requested component is already present or
absent. Pass `path` as well to operate on a prefab child.

## Create, save, and instantiate

A common workflow is:

```python
# 1. Design a scene object.
await create_object(name="EnemyTemplate", components="Health")
await set_property(
    path="/EnemyTemplate",
    component="Health",
    prop="MaxHP",
    value="100",
)

# 2. Save and connect it as a prefab asset.
await prefab(
    action="save",
    path="/EnemyTemplate",
    asset_path="Assets/Prefabs/Enemy.prefab",
    mode="new",
)

# 3. Edit the asset without opening Prefab Mode.
await prefab(
    action="edit",
    asset_path="Assets/Prefabs/Enemy.prefab",
    component="Health",
    prop="MaxHP",
    value="150",
)

# 4. Instantiate with an explicit scene name, parent, or object name.
await create_object(
    name="Enemy_01",
    parent="/Enemies",
    prefab_path="Assets/Prefabs/Enemy.prefab",
    scene="Level01",
)
```

`prefab(action="instantiate", asset_path=...)` is the minimal shortcut for an
unrenamed instance in the active scene. Its public wrapper does not accept
`name`, `parent`, or `scene`; use `create_object(prefab_path=...)` when any of
those controls is required.

## Verify the asset change

Instantiate a disposable scene copy and read the exact property:

```python
await create_object(
    name="PrefabVerification",
    prefab_path="Assets/Prefabs/Player.prefab",
)
actual = await get_component(
    path="/PrefabVerification",
    type="Health",
    fields="MaxHP",
)
```

Delete the verification object after checking the result. For instance
overrides, use `prefab(action="get_overrides", path=..., format="structured")`
before applying or reverting them.

See [Asset Tools](../tools/assets.md#prefab) for the full prefab action overview
and [Object Tools](../tools/objects.md) for scene-object editing.
