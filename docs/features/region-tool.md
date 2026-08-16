# Scene Region Selection

Draw a polygon on the Scene View XZ plane, then reuse it in Chat or a
`spatial_query`. Regions are an Editor aid: drawing is disabled while Unity is
entering or running Play Mode.

## Draw and save a region

1. Focus the Scene View and press **Shift+R**.
2. Choose a drawing mode with **Q**, **W**, **E**, or **R**, or use the MCP Scene
   overlay.
3. Draw the shape.
4. Review the highlighted objects and area in the preview.
5. Press **Enter** to save the region, or **Escape** to discard it.

| Key | Mode | Gesture |
|---|---|---|
| **Q** | Lasso | Press and drag a freehand contour; release to preview |
| **W** | Rectangle | Press at one corner, drag, and release |
| **E** | Circle | Press at the center, drag the radius, and release |
| **R** | Point by point | Click vertices; double-click or click near the first vertex to close |

Press **G** to toggle the 0.5-unit grid snap. In point-by-point mode, **Escape**
removes the most recent vertex; use it again at the first vertex to cancel.

The preview highlights up to 50 matching scene objects. Saving creates an
eight-character region ID and, when automatic context is enabled, adds a Region
chip to MCP Chat.

## Query the saved geometry

Use the region ID returned by the chip or saved snapshot:

```python
objects = await spatial_query(
    action="objects_in_polygon",
    region_id="a1b2c3d4",
    component="Collider",
    cap=50,
)
```

`component` is an optional, case-sensitive component-name substring filter. `cap`
defaults to 50 and is clamped to 200.

You can run the same query without using the Scene View by supplying at least
three `x,z` pairs:

```python
objects = await spatial_query(
    action="objects_in_polygon",
    vertices="0,0;10,0;10,8;0,8",
    cap=50,
)
```

If both `vertices` and `region_id` are supplied, the inline vertices are used and
the ID labels the result. See [Spatial Tools](../tools/spatial.md#spatial_query) for
other spatial actions.

## Use a region in MCP Chat

A saved region is represented in the prompt as:

```text
[region:a1b2c3d4]
```

The resolved context includes its area, bounds, center, object count, scene name,
and a bounded list of object paths. Set the chip depth to `full` when the polygon
vertices or the complete stored path list are needed. A `STALE` marker means the
hierarchy changed after the region was captured; query the region again before
making decisions from its object list.

The Region chip menu can frame the region, copy stored object paths, or remove the
region.

## Persistence and limits

Regions are stored in `Library/MCP_Regions.json`, which is local to the Unity
project and normally ignored by version control.

- At most 20 regions are retained; the oldest is evicted when the limit is
  exceeded.
- Regions survive a domain reload and Editor restart.
- Entries older than 24 hours are dropped the next time the store loads.
- Geometry uses world XZ coordinates and ignores object height.
- Containment tests each GameObject's transform pivot, not its renderer or collider
  bounds.
- The stored object-path snapshot is capped at 50, while a later
  `spatial_query(..., cap=...)` can return up to 200 current matches.

## Troubleshooting

| Symptom | What to do |
|---|---|
| The shortcut does nothing | Focus the Scene View and stop Play Mode |
| A point-by-point polygon will not preview | Add at least three vertices, then double-click or click near the first vertex |
| `Region not found` | The entry expired or was evicted; draw and save it again |
| The chip is marked `STALE` | The hierarchy changed; rerun `spatial_query` before editing objects |
| A visible object is missing | Its transform pivot may be outside the XZ polygon; use a larger region or an inline polygon |
| Too few results are returned | Increase `cap` (up to 200) or remove the component filter |

For destructive cleanup, use [`region_clear`](../tools/spatial.md#region_clear) with
its default `dry_run=True`, review the paths, and only then repeat with
`dry_run=False`.
