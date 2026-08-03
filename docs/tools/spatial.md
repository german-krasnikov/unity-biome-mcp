# Spatial Tools

Analyze level geometry, collider layout, pathfinding, and spatial queries. Essential for level design verification, AI navigation setup, and physics-based scene layout.

## get_spatial_context

Analyze collider configuration around an object. Returns collider info, approach vectors, and nearby objects within radius.

**Parameters:**
- `path` (string) — Scene path to target object
- `radius` (float, default=5.0) — Search radius in meters

**Output:** Collider bounds, approach vectors, nearby objects within radius. Raycast available in Play Mode only.

**Example:**

```python
# Check spatial context around enemy
context = await get_spatial_context(path="Enemy", radius=5.0)

# Verify player has clear approach
context = await get_spatial_context(path="Player", radius=10.0)
```

---

## spatial_query

Flexible spatial queries: nearest object, raycasting, polygon containment, grid mapping.

**Parameters:**
- `action` (string, required) — Query type: "nearest" | "in_front_of" | "objects_in_radius" | "bounds_info" | "raycast" | "spatial_map" | "objects_in_polygon"
- `path` (string, optional) — Scene path to query origin (not required if `center` is given)
- `target` (string, optional) — Target path for raycast or in_front_of
- `distance` (float, optional) — Distance for in_front_of or raycast travel distance
- `radius` (float, optional) — Search radius for objects_in_radius
- `center` (string, optional) — Position override: "x,y,z" (when no path given)
- `component` (string, optional) — Filter by component type (e.g., "Health")
- `cell_size` (float, optional) — Grid cell size for spatial_map
- `layer_mask` (string, optional) — Layer filter (e.g., "Default|Enemy")
- `vertices` (string, optional) — Polygon vertices for objects_in_polygon: "x1,z1;x2,z2;..." (3+ pairs)
- `region_id` (string, optional) — Optional tag for objects_in_polygon results
- `cap` (int, optional) — Max results cap (default 50)

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| nearest | Find closest object (optionally filtered by component) | `path`, `component` optional | `spatial_query("nearest", path="Player", component="Health")` |
| in_front_of | Position in front of object | `path`, `distance` | `spatial_query("in_front_of", path="Player", distance=3.0)` |
| objects_in_radius | All objects within radius | `path` or `center`, `radius` | `spatial_query("objects_in_radius", path="Player", radius=5.0)` |
| bounds_info | Detailed bounds/dimensions | `path` | `spatial_query("bounds_info", path="Enemy")` |
| raycast | Cast ray, returns hits sorted by distance | `path`, `target`, `distance` | `spatial_query("raycast", path="Player", target="Enemy", distance=50)` |
| spatial_map | ASCII grid map XZ plane | `path` or `center`, `cell_size` | `spatial_query("spatial_map", path="Player", cell_size=1.0)` |
| objects_in_polygon | Objects with XZ pivot inside polygon | `vertices`, `cap` optional | `spatial_query("objects_in_polygon", vertices="0,0;10,0;10,10", cap=20)` |

**Example:**

```python
# Find nearest enemy to player
result = await spatial_query("nearest", path="Player", component="Health")

# Get position 3m ahead
ahead = await spatial_query("in_front_of", path="Player", distance=3.0)

# All objects within 5m radius
nearby = await spatial_query("objects_in_radius", path="Player", radius=5.0)

# Raycast check line of sight
hits = await spatial_query("raycast", path="Player", target="Enemy", distance=50)

# Map of objects on ground plane (1m cells)
grid = await spatial_query("spatial_map", path="Player", cell_size=1.0)

# All objects in a zone (triangle or quad)
in_zone = await spatial_query("objects_in_polygon", 
    vertices="0,0;20,0;20,20", cap=50)
```

---

## navmesh_query

NavMesh sampling, pathfinding, and baking control. Requires AI Navigation package.

**Parameters:**
- `action` (string, required) — Query type: "sample" | "path" | "raycast" | "bake" | "status" | "clear" | "get_settings" | "set_settings"
- `center` (string, optional) — Position to sample: "x,y,z"
- `from_pos` (string, optional) — Start position for path/raycast: "x,y,z"
- `to` (string, optional) — Goal position: "x,y,z"
- `max_distance` (float, default=5.0) — Max distance for sample query
- `area_mask` (int, default=-1) — NavMesh area mask filter (0=all)
- `agentRadius`, `agentHeight`, `agentClimb`, `agentSlope` (float, optional) — Agent params for set_settings

**Actions:**

| Action | Purpose | Required Params | Example |
|--------|---------|-----------------|---------|
| sample | Find nearest walkable point | `center`, `max_distance` | `navmesh_query("sample", center="5,0,5", max_distance=3.0)` |
| path | Calculate path from→to | `from_pos`, `to` | `navmesh_query("path", from_pos="0,0,0", to="10,0,0")` |
| raycast | NavMesh raycast | `from_pos`, `to` | `navmesh_query("raycast", from_pos="0,0,0", to="5,0,5")` |
| bake | Build NavMesh | — | `navmesh_query("bake")` |
| status | Triangulation stats | — | `navmesh_query("status")` |
| clear | Remove all baked data | — | `navmesh_query("clear")` |
| get_settings | List all NavMesh agent types | — | `navmesh_query("get_settings")` |
| set_settings | Update NavMeshSurface agent params | `agentRadius`, etc. | `navmesh_query("set_settings", agentRadius=0.5, agentHeight=2.0)` |

**Example:**

```python
# Find walkable point near position
sample = await navmesh_query("sample", center="5,0,5", max_distance=3.0)

# Calculate path
path = await navmesh_query("path", from_pos="0,0,0", to="10,0,0")

# Bake NavMesh
await navmesh_query("bake")

# Get triangulation stats
stats = await navmesh_query("status")

# Update agent settings
await navmesh_query("set_settings", agentRadius=0.5, agentHeight=2.0)
```

**Note:** Returns "NavMesh unavailable: AI Navigation package not installed" if the package is missing.

---

## check_colliders

Detect collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given.

**Parameters:**
- `path` (string, optional) — Scene path to check (None = scan whole scene)

**Output:** List of issues found, or "OK" if clean.

**Example:**

```python
# Check whole scene for collider problems
issues = await check_colliders()

# Check specific subtree
issues = await check_colliders(path="Level/Obstacles")
```

---

## autofit_collider

Auto-fit collider bounds to mesh or renderer. Useful for manual collider setup and tight-fitting geometry.

**Parameters:**
- `path` (string, required) — Scene path to object with collider
- `type` (string, default="box") — Collider type: "box" | "sphere" | "capsule"

**Output:** New collider bounds or error.

**Example:**

```python
# Fit box collider to mesh
await autofit_collider(path="Rock", type="box")

# Fit sphere collider to renderer
await autofit_collider(path="Ball", type="sphere")

# Fit capsule to character
await autofit_collider(path="Player", type="capsule")
```

---

## region_clear

Delete (or preview) all objects whose XZ pivot is inside a polygon region. Safe dry-run by default.

**Parameters:**
- `vertices` (string, required) — Polygon vertices: "x1,z1;x2,z2;..." (3+ pairs, max 256)
- `dry_run` (bool, default=true) — True = list objects WOULD be deleted. False = delete them.
- `filter` (string, optional) — Name-pattern substring; only matching objects affected
- `cap` (int, default=50) — Max objects processed (hard max 200)

**Output:** List of objects in region. With `dry_run=false`, objects deleted.

**Example:**

```python
# Preview deletion (safe)
preview = await region_clear(
    vertices="0,0;20,0;20,20;0,20",
    dry_run=True)

# Delete all objects in zone
result = await region_clear(
    vertices="0,0;20,0;20,20;0,20",
    dry_run=False)

# Delete only enemies in zone
result = await region_clear(
    vertices="0,0;20,0;20,20;0,20",
    filter="Enemy",
    dry_run=False,
    cap=30)
```

**Warning:** `dry_run=False` deletes objects. Always preview first with `dry_run=True`.

---

## validate_layout

Check for trigger overlaps. Warns if triggers are closer than minimum distance.

**Parameters:**
- `root` (string, default="/") — Root path to scan (default: whole scene)
- `min_distance` (float, default=3.0) — Minimum distance between triggers in meters

**Output:** List of trigger overlaps or "OK".

**Example:**

```python
# Validate whole scene triggers (3m minimum spacing)
result = await validate_layout()

# Check specific subtree with 5m minimum
result = await validate_layout(root="Dungeon", min_distance=5.0)
```

---

## scan_scene

Infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Returns coverage stats.

**Parameters:** None

**Output:** Full audit of scene components with counts.

**Example:**

```python
# Full scene infrastructure audit
audit = await scan_scene()
# -> Colliders: 42, Triggers: 8, Audio: 3, Lights: 12, ...
```

---

## analyze_lod_culling

LOD group coverage and occlusion culling analysis.

**Parameters:**
- `focus` (string, optional) — Analysis scope: "lod" | "culling" | "occlusion" | null (all)

**Output:** LOD stats, culling coverage, occlusion analysis.

**Example:**

```python
# Full LOD + culling analysis
result = await analyze_lod_culling()

# LOD coverage only
lod_info = await analyze_lod_culling(focus="lod")

# Occlusion culling analysis
occlusion = await analyze_lod_culling(focus="occlusion")
```

---

## Common Patterns

| Task | Tools | Example |
|------|-------|---------|
| Find nearest interactable | spatial_query("nearest") | `result = await spatial_query("nearest", path="Player", component="Interactable")` |
| Check line of sight | spatial_query("raycast") | `hits = await spatial_query("raycast", path="Player", target="Enemy", distance=50)` |
| Validate trigger spacing | validate_layout | `await validate_layout(min_distance=3.0)` |
| Check collider health | check_colliders | `issues = await check_colliders()` |
| Clear zone of objects | region_clear | Preview with `dry_run=True`, then delete with `dry_run=False` |
| Verify NavMesh | navmesh_query | `stats = await navmesh_query("status")` then `await navmesh_query("bake")` |
| Map scene layout | spatial_query("spatial_map") | `grid = await spatial_query("spatial_map", path="Player", cell_size=1.0)` |
| Check object bounds | spatial_query("bounds_info") | `bounds = await spatial_query("bounds_info", path="Enemy")` |

---

**See also:** [Scene Tools](scene.md) for hierarchy inspection, [Objects Tools](objects.md) for component queries, [Batch Operations](batch.md) for multi-object modifications.
