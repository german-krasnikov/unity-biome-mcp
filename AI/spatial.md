# Feature: Spatial Queries

## Overview

Spatial queries for proximity, overlap, raycasting, and scene visualization. Tools: `spatial_query` (action-based routing: `nearest`, `in_front_of`, `objects_in_radius`, `bounds_info`, `raycast`, `spatial_map`, `objects_in_polygon`), `region_clear` (mutating region operations), `navmesh_query` (NavMesh path/sampling/raycast queries).

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
- `action` (required) — one of the 6 actions above
- `path` (required for most actions, optional for `objects_in_radius`) — object path or scene path
- `center` (optional, new in 0.5.0) — world-position origin as `"x,y,z"` string. For `objects_in_radius`, alternative to `path` (center wins when both given)

**Action-specific:**
- `nearest`: `component` (filter by component type)
- `in_front_of`: `distance` (how far in front, in meters)
- `objects_in_radius`: `radius` (search radius in meters)
- `raycast`: `target` (destination path/pos), `component` (optional filter)
- `spatial_map`: `cell_size` (grid cell size in meters)
- `objects_in_polygon`: `vertices` (required, semicolon-separated x,z pairs; min 3, max 256), `region_id` (optional region label), `cap` (optional max results, default 50)
- `raycast`, `objects_in_radius`: `layer_mask` (optional physics layer filter)

### Output Format

**nearest:**
```
Enemy #3001 [Rigidbody,EnemyAI] (distance: 5.23m)
```

**in_front_of:**
```
Position: (10.5, 0.0, 15.2)
```

**objects_in_radius:** (sorted nearest-first; cap applied after sort)
```
Rock_1 #4050 [Renderer] (5.1m)
Rock_2 #4051 [Renderer,Rigidbody] (6.8m)
```

**bounds_info:**
```
Center: (5.0, 1.0, 3.0)
Size: (2.0, 2.5, 1.5)
Extents: (1.0, 1.25, 0.75)
```

**raycast:**
```
Enemy_Boss #2000 [Rigidbody,Health,Animator] (distance: 10.5m)
Rock_Barrier #3050 [BoxCollider] (distance: 8.2m)
```

**spatial_map:**
```
XZ Plane Grid (cell=1.0m):
  10 |               * * *
  09 |             * B *
  08 |           *       *
  ...
  00 +---+---+---+---+---+
     0   5  10  15  20
  * = object, B = breadcrumb/reference
```

## Code Locations

- Python tools: `server/src/unity_mcp/tools/spatial.py` (3 tools: spatial_query, region_clear, navmesh_query)
- C# helpers: 
  - `unity-plugin/Editor/SpatialHelper.cs` (Physics.Raycast, bounds, grid, region_clear)
  - `unity-plugin/Editor/RegionTool.cs` (Polygon2D, SceneRegionQuery)
  - `unity-plugin/Editor/NavMeshHelper.cs` (NavMesh API wrapper)
- C# command: `unity-plugin/Editor/CommandRouter.cs` (3 cases: spatial_query, region_clear, navmesh)
- Python tests: `server/tests/test_spatial_center.py` (7 tests), `server/tests/test_spatial_new.py` (3 tests)
- C# tests: 
  - `unity-test-project/Assets/Tests/Editor/MCSpatialTests.cs` (16 tests)
  - `unity-test-project/Assets/Tests/Editor/RegionClearTests.cs` (7 tests)

## MCP Tool

### `spatial_query`

**Parameters:**
- `action` (required) — nearest | in_front_of | objects_in_radius | bounds_info | raycast | spatial_map | objects_in_polygon
- `path` (required for most actions, optional for objects_in_radius)
- `center` (optional, new in 0.5.0) — `"x,y,z"` world position (used for objects_in_radius; overrides path when both given)
- `vertices` (required for objects_in_polygon) — semicolon-separated x,z pairs: `"0,0;10,0;10,10;0,10"` (min 3 vertices, max 256)
- `region_id` (optional, objects_in_polygon) — label for grouped results
- `cap` (optional, objects_in_polygon, default 50) — max results returned
- `target`, `distance`, `radius`, `component`, `cell_size`, `layer_mask` — action-specific

```
# Find closest Rigidbody
spatial_query(action="nearest", path="/Player", component="Rigidbody")
→ Enemy #3001 [Rigidbody,EnemyAI] (distance: 5.23m)

# Position in front of object
spatial_query(action="in_front_of", path="/Player", distance=3.0)
→ Position: (10.5, 0.0, 15.2)

# Objects within radius around world position (new in 0.5.0)
spatial_query(action="objects_in_radius", center="10,1,20", radius=5.0)
→ Rock_1 #4050 [Renderer] (5.1m)
  Rock_2 #4051 [Renderer,Rigidbody] (6.8m)

# Raycast from/to objects
spatial_query(action="raycast", path="/Player", target="/Enemy_Boss")
→ Enemy_Boss #2000 [Rigidbody,Health,Animator] (distance: 10.5m)
  Rock_Barrier #3050 [BoxCollider] (distance: 8.2m)

# Scene grid map
spatial_query(action="spatial_map", path="/Level", cell_size=1.0)
→ XZ Plane Grid (cell=1.0m):
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
→ DRY: 2 objects would be deleted:
  /Level/Rock_1
  /Level/Debris_2

# Delete only objects matching "Debris"
region_clear(vertices="0,0;10,0;5,10", dry_run=False, filter="Debris")
→ DELETED: 1 object

# Delete up to 10 objects
region_clear(vertices="0,0;10,0;5,10", dry_run=False, cap=10)
→ DELETED: 10 objects
```

**Verification:**
After deletion, use `get_hierarchy` to confirm objects gone from scene.

**Edge Cases:**
- Missing `vertices` → raises ArgumentException
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
- `action` (required) — sample | path | raycast
- `center` (action-specific) — query center as `"x,y,z"` (for sample)
- `from_pos` (action-specific) — start point as `"x,y,z"` (for path, raycast)
- `to` (action-specific) — destination as `"x,y,z"` (for path, raycast)
- `max_distance` (optional, default 5.0) — search radius for sample
- `area_mask` (optional, default -1 all areas) — NavMesh area filter (int bitmask)

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
→ walkable: true
  position: (0.1, 0.0, 0.2)
  distance: 0.283

# Plan AI path from point A to B
navmesh_query(action="path", from_pos="0,0,0", to="10,0,10")
→ status: PathComplete
  corners: 3
    (0, 0, 0)
    (5, 0, 5)
    (10, 0, 10)

# Check line-of-sight between enemy and player
navmesh_query(action="raycast", from_pos="5,0,0", to="5,0,10")
→ hit: false
  position: (5, 0, 10)
  distance: 10
```

**Verification:**
- `sample` → walkable=true confirms point is on NavMesh
- `path` → status=PathComplete confirms connectivity
- `raycast` → hit=false confirms no obstacles

**Edge Cases:**
- No NavMesh in scene → `sample` returns `walkable: false`
- `area_mask=0` → auto-converted to -1 (all areas)
- `max_distance` clamped by Unity NavMesh API
- Large scenes → may timeout if NavMesh is complex

**Requirements:**
- NavMesh asset must exist in scene
- Agent type radius configured in NavMesh bake settings
- Play Mode queries allowed (Editor mode sampling safe)

**Notes:**
- Token efficient: newline-separated key:value format
- Returns float coordinates with 4 significant digits (G4 format)
- `area_mask` bitmask follows NavMesh.GetAreaCost() indexing

## TDD Scenarios

### spatial_query

**Red Phase (Python):**
1. **test_spatial_center_forwarded**: center → sends to bridge
2. **test_spatial_center_none_omitted**: center=None → not sent
3. **test_spatial_center_and_path_both_forwarded**: both given → both sent
4. **test_spatial_center_ignored_for_nearest**: center ignored for nearest action
5. **test_spatial_objects_in_radius_center_only_no_path**: center works without path

**Red Phase (C#):**
1. **SpatialHelper_Nearest_FindsClosest**: distance calculation
2. **SpatialHelper_ObjectsInRadius_WithCenter**: center origin
3. **SpatialHelper_Raycast_ReturnsHitsSorted**: hit order
4. **SpatialHelper_SpatialMap_GeneratesGrid**: grid generation

### region_clear

**Red Phase (Python):**
1. **test_region_clear_dry_run_lists**: dry_run=True lists without deleting
2. **test_region_clear_live_deletes**: dry_run=False deletes objects
3. **test_region_clear_filter_applied**: filter parameter matches names
4. **test_region_clear_missing_vertices_fails**: missing vertices raises error

**Red Phase (C#):**
1. **RegionClear_DryRun_ListsObjectsWithoutDeleting**: dry_run=True safe
2. **RegionClear_LiveRun_DeletesObjectsInsidePolygon**: dry_run=False mutates
3. **RegionClear_DryRun_ExcludesObjectsOutsidePolygon**: outside objects skipped
4. **RegionClear_FilterApplied_NonMatchingExcluded**: filter excludes
5. **RegionClear_FilterMatches_ObjectIncluded**: filter includes
6. **RegionClear_MissingVertices_ThrowsArgumentException**: validation
7. **RegionClear_DefaultDryRun_DoesNotDelete**: default safe

### navmesh_query

**Red Phase (Python):**
1. **test_navmesh_sample_finds_point**: sample returns walkable position
2. **test_navmesh_sample_not_walkable**: sample returns false when no mesh
3. **test_navmesh_path_complete**: path returns corners
4. **test_navmesh_path_partial**: path status=PathPartial when blocked
5. **test_navmesh_raycast_hit**: raycast detects obstruction
6. **test_navmesh_raycast_clear**: raycast returns destination when clear

**Red Phase (C#):**
NavMeshHelper.cs (NavMesh API delegates; no explicit tests — relies on Unity NavMesh correctness)

## Review Checklist

### spatial_query
- [ ] Security: Physics.Raycast safe, no eval, correct layer mask handling
- [ ] Performance: radius queries use Physics.OverlapSphere (not O(n)), grid bounded
- [ ] Token efficiency: text format ~8x smaller than JSON
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
- [ ] Performance: NavMesh queries cached by Unity
- [ ] Correctness: all 3 actions (sample, path, raycast) return consistent format
- [ ] Edge cases: no NavMesh returns graceful failure, path status handled

## Related

- Skill: `.claude/skills/csharp-unity.md` (Editor API, Physics)
- Knowledge: `AI/hierarchy-serializer.md` (object formatting)
- Knowledge: `AI/region-tool.md` (region management; pairs with objects_in_polygon)
- Tool: `search_scene` (complementary name/tag/component search)
