---
name: unity-physics-spatial
description: Use for Rigidbody or collider setup, collider validation, physics debugging, raycasts, proximity, or physics-related spatial analysis.
---

# Unity Physics And Spatial Analysis

If it is not already loaded, read
`.claude/skills/unity-mcp-operations/SKILL.md` once. Use scene-authoring tools
for component mutation. Enable `SCENE` for spatial analysis and `RUNTIME` for
Play Mode diagnostics.

## Setup

```text
batch(
  commands="""
manage_component path=/Crate type=Rigidbody action=add
manage_component path=/Crate type=BoxCollider action=add
set_property path=/Crate component=Rigidbody prop=mass value=5
set_property path=/Crate component=Rigidbody prop=useGravity value=true
get_component path=/Crate type=Rigidbody
get_component path=/Crate type=BoxCollider
""",
  on_error="stop",
  atomic=True
)
check_colliders(path="/Crate")
```

For several objects:

```text
configure_objects(config="""
/CrateA Rigidbody.mass=5
/CrateB Rigidbody.mass=8
""")
```

## Spatial Checks

```text
get_spatial_context(path="/Crate", radius=5)
spatial_query(action="nearest", path="/Crate", component="Collider")
spatial_query(action="raycast", path="/Camera", target="/Crate")
validate_layout(root="/Gameplay", min_distance=2)
```

## NavMesh

`navmesh_query` is a standalone tool and may report that AI Navigation is not
installed:

```text
navmesh_query(action="status")
navmesh_query(action="get_settings")
navmesh_query(action="set_settings", agent_type="Humanoid", speed=5.0)
navmesh_query(
  action="path",
  from_pos="(0,0,0)",
  to="(12,0,8)"
)
```

Use `sample` before path claims when endpoints may be off-mesh. Use
`get_settings` and `set_settings` to inspect and configure agent types.
Treat `bake` and `clear` as explicit project mutations, not read-only
analysis.

For bounded cleanup or placement regions, preview first:

```text
region_clear(
  vertices="0,0;10,0;10,10;0,10",
  filter="Temporary",
  dry_run=True,
  cap=50
)
```

Apply only after the preview count and object list match the requested scope.

## Rules

- Read Transform, Rigidbody, and collider data before editing.
- Confirm Edit Mode versus Play Mode before interpreting physics state.
- Use bounded runtime waits; never sleep and assume settling completed.
- Validate negative scale, trigger/Rigidbody relationships, and collider size.
- Use `autofit_collider` only after confirming the intended renderer bounds.
- Treat screenshots as spatial presentation evidence, not collision proof.
- Read back mass, constraints, trigger state, and bounds after changes.
