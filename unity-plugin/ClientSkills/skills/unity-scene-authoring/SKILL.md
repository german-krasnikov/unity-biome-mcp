---
name: unity-scene-authoring
description: Use when reading or changing Unity scenes, hierarchy, GameObjects, components, references, events, transforms, or object lifecycle.
---

# Unity Scene Authoring

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Enable `SCENE` or
`COMPONENTS` only when a required tool is gated.

## Workflow

1. Read a hierarchy summary, then inspect only the target subtree.
2. Resolve ambiguous names to exact hierarchy paths.
3. Mark the console.
4. Use one aggregate operation where possible.
5. Read back changed components and validate references.
6. Check the console delta and save the scene.

## Scene Lifecycle And Environment

```text
scene(action="list")
scene(action="open_additive", path="Assets/Scenes/Lighting.unity")
scene(action="set_active", path="Assets/Scenes/Lighting.unity")
scene_environment(action="get")
scene_environment(action="set", prop="fog", value="true")
scene_environment(action="set", prop="fogDensity", value="0.1")
```

Use explicit scene paths in multi-scene work. Inspect the live tool schema
before transferring objects between scenes, and save or discard each loaded
scene deliberately. `scene_environment` manages fog, skybox, and ambient
lighting settings.

## Canonical Operations

```text
setup_objects(specs="""
Marker primitive=Sphere parent=/Environment pos=(0,1,0)
Anchor parent=/Environment pos=(2,0,0)
""")
```

```text
configure_objects(config="""
/Environment/Marker Transform.m_LocalScale=(0.25,0.25,0.25)
/Environment/Anchor Transform.m_LocalPosition=(2,0,0)
""")
```

Both are standalone typed tools. Their arguments are multiline strings named
`specs` and `config`.

Component lifecycle supports `manage_component(action="add"|"remove")`.
To enable or disable a component, set its serialized enabled field after
confirming the property name with `get_component`.

```text
manage_component(path="/Lamp", type="Light", action="add")
set_property(
  path="/Lamp",
  component="Light",
  prop="m_Enabled",
  value="false"
)
```

Use `ping_object(path="/Target")` to highlight and select an object in the
Editor hierarchy. Use `get_selection()` to read the currently selected object.

## References And Events

Inspect before remapping or wiring:

```text
references(action="get", path="/HUD", children=True, depth=2)
auto_wire(path="/HUD/Controller", dry_run=True)
wire_event(
  path="/HUD/StartButton",
  component="Button",
  event="onClick",
  target="/Game",
  method="StartGame"
)
list_events(
  path="/HUD/StartButton",
  component="Button",
  event="onClick"
)
validate_references(path="/HUD", depth=3)
```

Apply `auto_wire(..., dry_run=False)` only when every proposed match is
unambiguous. Use `unwire_event` with an explicit listener index unless clearing
the entire event is intended. Verify persistent listeners with `list_events`;
do not infer them from a screenshot or an enabled component.

## Transactional Scene Edits

For multi-object mutations that must succeed or revert together, use atomic
transactional edits:

```text
scene_change_plan(goal="Add spawner", targets="/Checkpoint")
apply_scene_change(
  plan_id="abc123",
  commands="""
create_object name=Spawner parent=/Checkpoint
manage_component path=/Checkpoint/Spawner type=ParticleSystem action=add
"""
)
```

`scene_change_plan` preflights the targets, creates a checkpoint, and returns a
plan ID. `apply_scene_change` runs compatible synchronous commands as an atomic,
stop-on-error batch. It does not verify or save after batch failure. With
verification enabled, broken references, console errors, or an unavailable
verification result also prevent saving.

Use only the apply tool's documented Unity-Undo-backed scene commands. Keep
filesystem and asset operations, `execute_code`, nested batches, plugins, and
unknown commands outside the transaction; the tool rejects them before dispatch.
Verify their partial-failure behavior separately.

## Safety Rules

- Use leading `/` for ordinary hierarchy paths.
- Read available properties before setting an unfamiliar field.
- Pass `set_property.value` as a string.
- Use dry-run forms for destructive spatial operations when available.
- Do not use a screenshot to prove component values or references.
- A checkpoint alone is not rollback; use `apply_scene_change` or an explicit
  atomic batch for compatible destructive commands.
- In multi-scene workflows, verify the target object's owning scene after
  transfer and before saving.

## Good And Bad

Bad: pass invented `targets` and `properties` arguments to `configure_objects`,
or use an unsupported `disable` action on `manage_component`.

Good:

```text
configure_objects(config="/Lamp Light.intensity=1.5")
set_property(path="/Lamp", component="Light", prop="m_Enabled", value="false")
```
