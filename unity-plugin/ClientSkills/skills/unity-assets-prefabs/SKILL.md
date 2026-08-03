---
name: unity-assets-prefabs
description: Use for Unity asset database operations, prefab workflows, ScriptableObjects, dependency checks, or project settings; not materials or shaders.
---

# Unity Assets And Prefabs

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Enable `ASSETS`, resolve
the current tool schema, and inspect dependencies before moves, deletes, or
prefab-wide changes.

## Asset Workflow

```text
asset(action="find", name="Player", type="Prefab", folder="Assets/Prefabs")
asset(action="get_dependencies", path="Assets/Prefabs/Player.prefab")
asset(
  action="validate_move",
  source="Assets/Prefabs/Player.prefab",
  dest="Assets/Characters/Player.prefab"
)
asset(
  action="move",
  source="Assets/Prefabs/Player.prefab",
  dest="Assets/Characters/Player.prefab"
)
```

Use `name`, `type`, and `folder` for search. Do not invent a `query` argument.

## Prefabs

```text
batch(
  commands="""
prefab action=save path=/Player asset_path=Assets/Prefabs/Player.prefab mode=new
prefab action=get_overrides path=/Player format=structured
""",
  on_error="stop"
)
```

The prefab contract does not provide generic `parent` or `position` arguments.
Instantiate or position scene objects with the scene-authoring tools.

## ScriptableObjects

```text
scriptable_object(action="list_types", filter="Settings")
scriptable_object(
  action="create",
  type="GameSettings",
  path="Assets/Config/GameSettings.asset",
  fields="difficulty=Normal\nvolume=0.8"
)
scriptable_object(
  action="get",
  path="Assets/Config/GameSettings.asset",
  fields="difficulty,volume"
)
```

## Project Settings

Read the target before setting it:

```text
project_settings(action="get", target="layers")
project_settings(
  action="set",
  target="layers",
  index=8,
  value="Interactable"
)
```

Use only the supported targets `tags`, `layers`, `sorting_layers`, `quality`,
`physics`, `time`, `player`, `graphics`, `audio`, and `input`. Resolve the live
schema before changing quality, physics, time, player, or graphics
properties. `sorting_layers`, `audio`, and `input` are read-only; inspect them
with `action="get"` and do not attempt to set them. For ScriptingBackend configuration, use the `build_target`
parameter when supported by the backend variant.

## Build, Package, And Bake

```text
build(action="build", target="StandaloneOSX")
package(action="list")
bake(target="lighting", action="start")
```

Use `build` for player builds with explicit target platform. Use `package` for
UPM package operations. Use `bake` for lightmap and occlusion culling baking.

## Rules

- Use `validate_move` before moving referenced assets.
- Inspect dependents before deletion.
- Preserve `.meta` identity by using the asset tool, not filesystem moves.
- Read prefab overrides before apply or revert.
- Verify the final asset path and relevant references after mutation.
- Treat project settings as project-wide changes and report the exact target,
  property or index, old value, and new value.
- Keep material and shader work in `unity-materials-shaders`.
