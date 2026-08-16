# Scene Regions and Annotations

The Region Tool is an Editor-only Scene View feature for selecting XZ polygons
and attaching compact spatial context to chat. The same persistence model also
stores point, polyline, and measurement annotations.

## Entry Points

| Tool | Shortcut | Modes |
|---|---|---|
| `SceneRegionTool` | Shift+R | Lasso, Rectangle, Circle, PointByPoint |
| `SceneAnnotationTool` | Shift+A | Point, Polyline, Measurement |

Both tools are disabled while Unity is entering or running Play Mode. The Scene
View overlay exposes mode, grid-snap, label, confirm, commit, and cancel controls.

Region mode keys are `Q`, `W`, `E`, and `R`; `G` toggles grid snap. Annotation
mode keys are `1`, `2`, and `3`; `G` toggles grid snap. Enter commits a completed
shape and Escape cancels. Mode-specific mouse and point-confirm behavior lives
in the classes under `RegionTool/Drawing/` and should not be reimplemented by
the overlay.

## Drawing Contracts

Closed polygon modes implement `IDrawingMode`:

```csharp
internal interface IDrawingMode
{
    DrawingModeId Id { get; }
    void Begin(Vector2 startXZ, bool gridSnap);
    bool OnEvent(Event e, Vector2 currentXZ);
    IReadOnlyList<Vector2> PreviewVertices { get; }
    bool IsComplete { get; }
    bool IsActive { get; }
    Polygon2D? Finalize();
    void Reset();
    bool CanConfirm { get; }
    void ConfirmPending();
}
```

Annotation modes use the parallel `IAnnotationMode` contract and expose
`FinalizedPoints` rather than a closed polygon. Point requires one point;
Polyline and Measurement require at least two.

`SceneRegionTool` preserves the raw polygon, applies the configured
`PolygonDetailLevel`, rejects fewer than three vertices or area below `0.01`,
and queries at most 50 matching GameObjects for the stored snapshot.

## Polygon Geometry

`Polygon2D` is an immutable defensive copy of at least three XZ vertices.

- World X maps to `Vector2.x`; world Z maps to `Vector2.y`.
- A repeated closing vertex is stripped.
- `Contains` uses a non-zero winding-number test.
- `ContainsBatch` performs an AABB rejection before point-in-polygon tests.
- `Area`, `Centroid`, `ComputeBounds`, `Simplify`, and CSV parsing share the
  same invariant-culture representation.

Do not refer to the algorithm as ray casting: the implementation is winding
number and intentionally supports concave and self-intersecting input.

## Persistence

`SceneRegionState` keeps snapshots in memory and writes
`Library/MCP_Regions.json`. It also maintains a SessionState shadow so a domain
reload can recover the current set.

- Maximum retained snapshots: 20 by default.
- Load discards entries older than 24 hours.
- Insertion above the limit evicts the oldest snapshots.
- `EditorApplication.hierarchyChanged` increments a process-local version;
  `IsStale(id)` compares that version with the snapshot version.
- `FrameRegion` frames the stored bounds in the last active Scene View.
- All access is main-thread only.

The current persisted schema is `SchemaVersion = 2`:

```text
Id, SchemaVersion
VerticesFlat                         # [x0,z0,x1,z1,...]
Area, CenterX, CenterZ
MinX, MinZ, MaxX, MaxZ
SceneName, PlaneY
ObjectPaths, ObjectIds, TotalCount, Truncated
DetailLevel
SnapshotVersion, CreatedTicks
AnnotationType, Label, LengthOrDistance, Direction
```

`ObjectPaths` and `ObjectIds` are parallel and capped at 50; `TotalCount` is the
pre-cap count. `CreatedTicks` is Unix time in seconds. There is no
`ModifiedTicks` field and no separate `VerticesX`/`VerticesZ` arrays.

## Spatial Query Contract

The public entry point is:

```python
await spatial_query(
    action="objects_in_polygon",
    region_id="a1b2c3d4",
    component="Collider",
    cap=50,
)
```

Callers provide either:

- `vertices="x1,z1;x2,z2;x3,z3"`; or
- `region_id` for a persisted polygon region.

When both are present, supplied vertices are used and `region_id` supplies the
result label. The Python wrapper validates supplied vertices (3-256 pairs,
numeric coordinates within the supported range) but does not require
`vertices` when `region_id` is present. The Unity query clamps `cap` to 1-200;
the default is 50.

The query pipeline is:

```text
resolve polygon -> AABB filter -> optional component-name filter
                -> winding-number containment -> cap and format
```

Objects are tested by their Transform pivot projected onto XZ, not by renderer
or collider bounds. Component matching is a type-name substring comparison.
Output paths come from `ComponentSerializer.GetPath` and include a transient
object identity.

`region_clear` is a separate mutation tool. It always requires inline vertices
and defaults to `dry_run=True`; it does not accept `region_id`.

## Chat Integration

`RegionChipProvider` is registered under the `region` chip kind. Its wire
markup is a single token:

```text
[region:a1b2c3d4]
```

There is no closing `[/region]` tag. A click frames the region; the provider
formats stored region or annotation context for the model. Missing IDs remain
representable so reload and stale-reference behavior can be diagnosed.

`SceneRegionTool.OnRegionCommitted` and
`SceneAnnotationTool.OnAnnotationCommitted` pass `(id, shortLabel)` to the chat
window. Event handlers must be detached during window/test cleanup.

## Playtest Preamble

`GdSnapshotSerializer` converts stored geometry to `VAL` lines:

| Snapshot type | Output |
|---|---|
| `region` or `point` | one value at the center |
| `polyline` | one value per vertex with `_0`, `_1`, ... suffixes |
| `measurement` | `_start` and `_end` values |

```text
VAL $spawn_zone 5.00,0.00,3.00
VAL $patrol_0 1.00,0.00,0.00
VAL $patrol_1 10.00,0.00,0.00
```

Labels are lowercase, spaces become underscores, and other non-ASCII-letter or
non-digit characters are stripped. An empty label falls back to `gd_<id>`.

## Primary Files

- `unity-plugin/Editor/RegionTool/SceneRegionTool.cs`
- `unity-plugin/Editor/RegionTool/SceneAnnotationTool.cs`
- `unity-plugin/Editor/RegionTool/SceneRegionState.cs`
- `unity-plugin/Editor/RegionTool/RegionSnapshot.cs`
- `unity-plugin/Editor/RegionTool/Polygon2D.cs`
- `unity-plugin/Editor/RegionTool/SceneRegionQuery.cs`
- `unity-plugin/Editor/RegionTool/GdSnapshotSerializer.cs`
- `unity-plugin/Editor/Chat/CLI/RegionChipProvider.cs`
- `server/src/unity_mcp/tools/spatial.py`

## Verification

Region fixtures must use isolated persistence paths and restore SessionState;
they must never clear the production region store. Follow `AI/testing.md`.
Coverage lives under `unity-plugin/Editor/Tests/RegionTool/`, chat CLI tests,
and `server/tests/test_region.py` / `test_spatial_polygon.py`.

Review changes for:

- XZ rather than XY/XYZ containment semantics;
- `vertices`-or-`region_id` wrapper behavior;
- schema-v2 field names and caps;
- 24-hour load expiry and deterministic eviction;
- handler cleanup across domain reload and fixture teardown;
- exact `[region:id]` chip markup;
- safe dry-run defaults for `region_clear`.

## Related

- `AI/spatial.md`
- `AI/chat-view.md`
- `AI/playtest-dsl.md`
- `AI/testing.md`
- `unity-plugin/ClientSkills/skills/unity-physics-spatial/SKILL.md`
