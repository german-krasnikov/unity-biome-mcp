# Spatial Tools

Inspect object positions, bounds, colliders, trigger spacing, NavMesh data, and
rendering culling. Spatial membership tools use a GameObject's XZ pivot unless a
section states otherwise; they do not test whether the object's full bounds
overlap a region.

## `get_spatial_context` {#get_spatial_context}

Read collider bounds, eight approach directions, and nearby colliders around one
object:

```python
context = await get_spatial_context(path="/Enemy", radius=8)
```

The approach report uses physics linecasts from the test points toward the target.
Treat it as level-layout context, not a NavMesh path or gameplay reachability test.

## `spatial_query` {#spatial_query}

Use `spatial_query` for focused positional questions.

### Nearest object and radius

```python
nearest = await spatial_query(
    action="nearest",
    path="/Player",
    component="Interactable",
)

nearby = await spatial_query(
    action="objects_in_radius",
    center="10,0,5",
    radius=6,
    cap=30,
)
```

`nearest` requires an origin `path`. Its optional component filter is a
case-sensitive substring of the component type name. For `objects_in_radius`, an
explicit `center="x,y,z"` takes precedence over `path`; results are sorted by
distance and capped at 200.

### Position, bounds, raycast, and map

```python
ahead = await spatial_query(
    action="in_front_of",
    path="/Player",
    distance=3,
)
bounds = await spatial_query(action="bounds_info", path="/Enemy")
hits = await spatial_query(
    action="raycast",
    path="/Player",
    target="/Enemy",
    layer_mask="-1",
)
grid = await spatial_query(
    action="spatial_map",
    path="/Level",
    cell_size=2,
)
```

The physics raycast travels from `path` to `target`; either endpoint may also be a
parenthesized position such as `"(0,1,0)"`. Its length comes from those endpoints,
so the wrapper's `distance` argument does not extend or shorten this action.
`layer_mask` is a numeric physics mask. Results contain at most 20 sorted hits.

`spatial_map` maps the selected hierarchy on the XZ plane, up to 40 by 40 cells and
26 legend labels. It uses `path`; the wrapper's `center` argument is not applied to
this action.

### Objects inside a polygon

Supply inline XZ vertices or the ID of a region saved by the Scene Region Tool:

```python
inline = await spatial_query(
    action="objects_in_polygon",
    vertices="0,0;20,0;20,20;0,20",
    component="EnemyController",
    cap=50,
)

saved = await spatial_query(
    action="objects_in_polygon",
    region_id="a1b2c3d4",
)
```

The polygon needs 3–256 vertices. The component filter is a case-sensitive type
name substring, and the result cap is clamped to 1–200. `region_id` identifies
cached geometry; it is not merely an output label. See the
[Scene Region Tool](../features/region-tool.md) for drawing, persistence, and
expiry behavior.

## `region_clear` {#region_clear}

Preview objects whose XZ pivots fall inside an inline polygon, then reuse the same
vertices and filter for deletion:

```python
vertices = "0,0;20,0;20,20;0,20"
preview = await region_clear(
    vertices=vertices,
    filter="Temporary",
    dry_run=True,
    cap=50,
)

# Destructive: run only after reviewing the preview.
deleted = await region_clear(
    vertices=vertices,
    filter="Temporary",
    dry_run=False,
    cap=50,
)
```

`filter` is a case-sensitive object-name substring. The cap is applied while
collecting objects inside the polygon, before the name filter, so a narrow filter
may return fewer matches than expected in a crowded region. `region_clear` does
not accept `region_id`; copy the saved region's vertices when this workflow needs
a drawn region.

Deletion participates in Unity Undo, but a source-control checkpoint is still the
safer boundary for a large clear.

## `navmesh_query` {#navmesh_query}

Sample, trace, build, and inspect Unity NavMesh data:

```python
sample = await navmesh_query(
    action="sample",
    center="5,0,5",
    max_distance=3,
)
path = await navmesh_query(
    action="path",
    from_pos="0,0,0",
    to="10,0,0",
)
ray = await navmesh_query(
    action="raycast",
    from_pos="0,0,0",
    to="5,0,5",
)
```

`area_mask=-1` includes all NavMesh areas. The path result reports Unity's path
status and corners; inspect the status rather than assuming any non-empty response
is complete.

Project-level operations are also available:

```python
settings = await navmesh_query(action="get_settings")
await navmesh_query(
    action="set_settings",
    agentRadius=0.5,
    agentHeight=2,
)
await navmesh_query(action="bake")
status = await navmesh_query(action="status")
```

`set_settings` updates positive values on every `NavMeshSurface` it finds; it does
not edit legacy Navigation-window agent settings. `bake` builds all surfaces, or
falls back to Unity's legacy builder. `clear` removes baked NavMesh data. These are
project mutations, so do not run them merely to answer a read-only question.

If the required navigation module is unavailable, the tool reports that NavMesh
support is not installed.

## Collider checks

### `check_colliders` {#check_colliders}

```python
scene_issues = await check_colliders()
target_issues = await check_colliders(path="/Level/Obstacles/Rock")
```

The check reports 3D triggers without a Rigidbody on the object or parent, negative
scale on collider objects, and very small Box/Sphere colliders. A target path checks
that object only, not its descendants.

### `autofit_collider` {#autofit_collider}

```python
await autofit_collider(path="/Level/Obstacles/Rock", type="box")
```

`type` is `box`, `sphere`, or `capsule`. The tool reuses or adds that collider and
fits it to a local SkinnedMeshRenderer, MeshFilter mesh, or Renderer bound. Inspect
the result; automatic capsule radius and direction may not match gameplay needs.

### `validate_triggers` {#validate_triggers}

```python
spacing = await validate_triggers(root="/Level", min_distance=3)
```

This scans 3D colliders below `root` and warns when trigger **transform positions**
are closer than `min_distance`. Despite its historical wording, it does not test
collider-volume intersections.

## Scene-wide analysis

### `scan_scene` {#scan_scene}

`scan_scene()` returns scene infrastructure counts for colliders, triggers, audio,
lights, rigidbodies, Canvas objects, and navigation components.

### `analyze_lod_culling` {#analyze_lod_culling}

```python
all_findings = await analyze_lod_culling()
lod_findings = await analyze_lod_culling(focus="lod")
occlusion = await analyze_lod_culling(focus="occlusion")
```

The analysis reports LOD groups, high-poly renderers without an LODGroup, crossfade
cost warnings, and whether occlusion data is baked. `focus` accepts `lod`,
`culling`, or `occlusion`; the latter two select the same culling section.

## Related workflows

- [Scene Region Tool](../features/region-tool.md) — draw and reuse saved polygons.
- [Scene Tools](scene.md) — discover hierarchy paths and loaded scenes.
- [Object Tools](objects.md) — inspect or mutate returned GameObjects.
- [Generated Tool Schema](../tools-schema/index.md) — exhaustive signatures and defaults.
