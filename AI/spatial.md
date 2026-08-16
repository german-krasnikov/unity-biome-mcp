# Feature: Spatial Queries

## Overview

Spatial queries for proximity, overlap, raycasting, and scene visualization. Tools: `spatial_query` (action-based routing: `nearest`, `in_front_of`, `objects_in_radius`, `bounds_info`, `raycast`, `spatial_map`, `objects_in_polygon`), `region_clear` (mutating region operations), `navmesh_query` (NavMesh queries and management).

## Architecture

```
Claude Code ←─stdio─→ Python MCP Server ←─TCP:9500─→ Unity Editor Plugin
                            │                              │
                     ├─ spatial_query             CommandRouter (3 cases)
                     ├─ region_clear                   │
                     └─ navmesh_query          ├─ SpatialHelper.cs
                                              │   (Physics casts, grid, distance)
                                              ├─ RegionTool.Polygon2D (region ops)
                                              └─ NavMeshHelper.cs (NavMesh API)
```

## Implementation Notes

### Actions

- `nearest` — find closest object (optionally filtered by component name)
- `in_front_of` — position in front of object at distance (returns world position)
- `objects_in_radius` — list all objects within radius (around path OR center); results sorted nearest-first, then truncated to `cap`
- `bounds_info` — detailed bounds/dimensions of object
- `raycast` — cast ray from path/pos to target, returns hits sorted by distance
- `spatial_map` — ASCII grid map of objects in XZ plane (cell_size in meters)
- `objects_in_polygon` — objects whose XZ pivot is inside polygon; vertices='x1,z1;x2,z2;...' (semicolon-separated pairs, min 3, max 256)

### Parameters

**Core:**
- `action` (required) — one of the seven actions above
- `path` (required for most actions, optional for `objects_in_radius`) — object path or scene path
- `center` (optional) — world-position origin as `"x,y,z"`. For `objects_in_radius`, it is an alternative to `path` and wins when both are given

**Action-specific:**
- `nearest`: `component` (filter by component type)
- `in_front_of`: `distance` (how far in front, in meters)
- `objects_in_radius`: `radius` (search radius in meters), `cap` (default 20,
  clamped to 1-200)
- `raycast`: `target` (destination path or parenthesized position), `layer_mask` (optional physics mask)
- `spatial_map`: `cell_size` (grid cell size in meters)
- `objects_in_polygon`: either `vertices` (semicolon-separated x,z pairs; min 3, max 256) or a stored `region_id`, plus optional `component` and `cap`

### Output Format

The C# helpers return compact text rather than a common JSON envelope:

- `nearest`: `/Enemy dist=5.23 pos=(10.00,0.00,15.00)`
- `in_front_of`: `(10.50,0.00,15.20)`
- `objects_in_radius`: a count header followed by indented `path dist=N` lines
- `bounds_info`: one line with `center`, `size`, `min`, and `max`
- `raycast`: `PATH`, ordered `HIT` lines, then `CLEAR` or `BLOCKED`
- `spatial_map`: a bounded XZ grid preceded by a legend
- `objects_in_polygon`: see [`AI/region-tool.md`](region-tool.md#spatial-query-contract)

## Code Locations

- Python wrappers covered here: `server/src/unity_mcp/tools/spatial.py`
  (`spatial_query`, `region_clear`, and `navmesh_query`; the module also owns
  other spatial diagnostics documented through their public schemas)
- C# helpers: 
  - `unity-plugin/Editor/SpatialHelper.cs` (Physics.Raycast, bounds, grid, region_clear)
  - `unity-plugin/Editor/RegionTool/Polygon2D.cs` and `SceneRegionQuery.cs`
  - `unity-plugin/Editor/NavMeshHelper.cs` (NavMesh API wrapper)
- C# registration: `unity-plugin/Editor/CommandRouter.Registration.cs`
- Tests: see the source-backed list in [Tests](#tests)

## MCP Tool

### `spatial_query`

**Parameters:**
- `action` (required) — nearest | in_front_of | objects_in_radius | bounds_info | raycast | spatial_map | objects_in_polygon
- `path` (required for most actions, optional for objects_in_radius)
- `center` (optional) — `"x,y,z"` world position (used for objects_in_radius; overrides path when both given)
- `vertices` or `region_id` — one is required for objects_in_polygon; vertices win when both are supplied
- `region_id` identifies a snapshot stored by the Scene View Region Tool
- `cap` (optional) — max results returned; defaults to 20 for
  `objects_in_radius` and 50 for `objects_in_polygon`, with a hard maximum of
  200 in both handlers
- `target`, `distance`, `radius`, `component`, `cell_size`, `layer_mask` — action-specific

```
# Find closest Rigidbody
spatial_query(action="nearest", path="/Player", component="Rigidbody")
→ /Enemy dist=5.23 pos=(10.00,0.00,15.00)

# Position in front of object
spatial_query(action="in_front_of", path="/Player", distance=3.0)
→ (10.50,0.00,15.20)

# Objects within radius around world position
spatial_query(action="objects_in_radius", center="10,1,20", radius=5.0)
→ 2 objects within 5m (showing 2):
    /Rock_2 dist=4.80
    /Rock_1 dist=5.10

# Raycast from/to objects
spatial_query(action="raycast", path="/Player", target="/Enemy_Boss")
→ PATH: (0.0,0.0,0.0) -> (10.0,0.0,0.0) dist=10.00
  HIT 1: /Rock_Barrier at (8.2,0.0,0.0) dist=8.20 [BoxCollider]
  BLOCKED: 1 hit

# Scene grid map
spatial_query(action="spatial_map", path="/Level", cell_size=1.0)
→ # Map: XZ, cell=1m, ...
  ...ASCII grid...
```

### `region_clear`

Delete (or preview) all objects whose XZ pivot is inside a polygon region.

**Parameters:**
- `vertices` (required) — semicolon-separated x,z pairs: `"0,0;10,0;10,10;0,10"` (min 3, max 256)
- `dry_run` (optional, default True) — True = list without deleting, False = delete immediately
- `filter` (optional) — name substring pattern; only matching objects affected
- `cap` (optional, default 50, max 200) — max objects processed

**Returns:**
- Dry run: `"DRY: N objects would be deleted: [list paths]"`
- Live: `"DELETED: N object(s)"`

**Examples:**
```python
# Preview objects inside triangle (safe)
region_clear(vertices="0,0;10,0;5,10", dry_run=True)
# → DRY: 2 objects would be deleted:
#   /Level/Rock_1
#   /Level/Debris_2

# Delete only objects matching "Debris"
region_clear(vertices="0,0;10,0;5,10", dry_run=False, filter="Debris")
# → DELETED: 1 object

# Delete up to 10 objects
region_clear(vertices="0,0;10,0;5,10", dry_run=False, cap=10)
# → DELETED: 10 objects
```

**Verification:**
After deletion, use `get_hierarchy` to confirm objects gone from scene.

**Edge Cases:**
- Missing or malformed `vertices` is rejected by the Python wrapper before the
  command is sent; the C# handler validates the same contract defensively
- `dry_run` defaults to True (safe if omitted)
- Invalid polygon → delegates to `Polygon2D.FromCsv()` (format validation)
- Objects destroyed during iteration safely skipped

**Notes:**
- Uses `Undo.DestroyObjectImmediate` (can be undone)
- Filters by XZ pivot position only (ignores Y)
- Token efficient: plain text response, no JSON

### `navmesh_query`

Query NavMesh for walkability, path-finding, and line-of-sight checks.

**Parameters:**
- `action` (required) — sample | path | raycast | bake | status | clear | get_settings | set_settings
- `center` (action-specific) — query center as `"x,y,z"` (for sample)
- `from_pos` (action-specific) — start point as `"x,y,z"` (for path, raycast)
- `to` (action-specific) — destination as `"x,y,z"` (for path, raycast)
- `max_distance` (optional, default 5.0) — search radius for sample
- `area_mask` (optional, default -1 all areas) — NavMesh area filter (int bitmask)
- `agentRadius`, `agentHeight`, `agentClimb`, `agentSlope` — optional
  `set_settings` values for installed `NavMeshSurface` components

**Returns:**

**sample action:**
```
walkable: true
position: (5.2, 0.1, 3.4)
distance: 0.347
```

**path action:**
```
status: PathComplete
corners: 4
  (0, 0, 0)
  (5, 0, 5)
  (10, 0, 8)
  (10, 0, 10)
```

**raycast action:**
```
hit: true
position: (7.2, 0.1, 6.5)
distance: 9.234
mask: 1
```
or if clear:
```
hit: false
position: (10, 0, 10)
distance: 14.142
```

**Examples:**
```python
# Find nearest walkable point to player position
navmesh_query(action="sample", center="0,0,0", max_distance=10.0)
# → walkable: true
#   position: (0.1, 0.0, 0.2)
#   distance: 0.283

# Plan AI path from point A to B
navmesh_query(action="path", from_pos="0,0,0", to="10,0,10")
# → status: PathComplete
#   corners: 3
#     (0, 0, 0)
#     (5, 0, 5)
#     (10, 0, 10)

# Check line-of-sight between enemy and player
navmesh_query(action="raycast", from_pos="5,0,0", to="5,0,10")
# → hit: false
#   position: (5, 0, 10)
#   distance: 10
```

**Verification:**
- `sample` → walkable=true confirms point is on NavMesh
- `path` → status=PathComplete confirms connectivity
- `raycast` → hit=false confirms no obstacles
- `status` reports current triangulation counts after a bake or clear

**Edge Cases:**
- No NavMesh in scene → `sample` returns `walkable: false`
- `area_mask=0` → auto-converted to -1 (all areas)
- Large scenes → may timeout if NavMesh is complex

**Requirements:**
- Query actions require baked NavMesh data; `bake` creates or rebuilds it
- `bake`, `clear`, and `set_settings` mutate NavMesh/editor state; the query
  actions are read-only
- Play Mode queries allowed (Editor mode sampling safe)

**Notes:**
- Token efficient: newline-separated key:value format
- Returns float coordinates with 4 significant digits (G4 format)
- `area_mask` bitmask follows NavMesh.GetAreaCost() indexing

## Tests

- Python: `server/tests/test_spatial_center.py`, `test_spatial_polygon.py`,
  `test_spatial_scan.py`, `test_region_clear.py`, and `test_navmesh.py`.
- C#: `unity-plugin/Editor/Tests/SpatialHelperCenterTests.cs`,
  `SpatialHelperSyncTests.cs`, `RegionClearTests.cs`, and `NavMeshHelperTests.cs`.
- Use [`AI/testing.md`](testing.md) for current commands and acceptance policy.
## Review Checklist

### spatial_query
- [ ] Security: Physics.Raycast safe, no eval, correct layer mask handling
- [ ] Performance: radius queries use Physics.OverlapSphere (not O(n)), grid bounded
- [ ] Token efficiency: compact text format avoids repeated JSON structure
- [ ] Edge cases: no objects found, invalid path/center handled

### region_clear
- [ ] Security: Polygon2D validates vertices (no eval), cap limits processing
- [ ] Safety: dry_run=True default (opt-in delete)
- [ ] Undo: uses Undo.DestroyObjectImmediate (user can undo)
- [ ] Performance: cap=200 hard limit, polygon containment check O(n) per object
- [ ] Token efficiency: plain text list format
- [ ] Edge cases: destroyed objects skipped, filter=None handled

### navmesh_query
- [ ] Security: NavMesh API safe, no eval, area_mask=0 auto-converted
- [ ] Performance: avoid repeating expensive bake operations unnecessarily
- [ ] Correctness: query, bake/status/clear, and settings actions match
  `NavMeshHelper.Execute`
- [ ] Edge cases: no NavMesh returns graceful failure, path status handled

## Related

- Consumer workflow: `unity-plugin/ClientSkills/skills/unity-physics-spatial/SKILL.md`
- Knowledge: [`AI/hierarchy-serializer.md`](hierarchy-serializer.md) (object formatting)
- Knowledge: [`AI/region-tool.md`](region-tool.md) (region management; pairs with objects_in_polygon)
- Tool: `search_scene` (complementary name/tag/component search)
