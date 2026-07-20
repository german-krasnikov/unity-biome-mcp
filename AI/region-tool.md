# Region Tool & Scene Selection

Multi-mode region selection in Scene View: draw polygons (Lasso/Rectangle/Circle/PointByPoint), query objects inside, persist regions, integrate with Chat.

**Activation:** Shift+R in Scene View. **Mode switches:** Q/W/E/R. **Grid snap:** G. **Confirm point:** Space. **Commit:** Enter. **Cancel:** Escape.

## SceneRegionTool (Main Tool)

**Purpose:** EditorTool for polygon-based scene selection with multiple drawing modes.

**Lifecycle:**
1. OnActivated() — Register shortcuts, init state (Idle)
2. OnToolGUI() — Handle input, dispatch to drawing mode
3. OnWillBeDeactivated() — Cleanup, unregister handlers

**State Machine:**
- `Idle` — No selection in progress
- `Drawing` — User adding points (calls IDrawingMode.OnMouseDown/Move)
- `Preview` — Polygon complete, user can confirm or adjust

**Drawing Modes:**
- `Lasso` — Free-hand polygon (any point order)
- `Rectangle` — Axis-aligned bounding box (2 corners)
- `Circle` — Click center, drag radius
- `PointByPoint` — Click each vertex; Space to confirm point; Enter to close

**Mode Switching:** Q/W/E/R keybinds → SetModeAction() → recreate _activeMode via DrawingModeFactory.

**Grid Snap:** Toggle G → snaps drawn points to 0.5 grid (editor pref persisted).

## Drawing Modes (IDrawingMode)

**Interface:**
```csharp
interface IDrawingMode
{
    DrawingModeId Id { get; }
    bool IsComplete { get; }
    bool CanConfirm { get; }
    void OnMouseDown(Vector2 cursorXZ);
    void OnMouseMove(Vector2 cursorXZ);
    void ConfirmPending();
    Polygon2D GetPolygon(bool simplified);
}
```

**Lasso Mode:**
- Tracks mouse position continuously
- Each MouseDown adds point to array
- MouseMove extends line
- Simplified via Douglas-Peucker (fewer vertices for display)

**Rectangle Mode:**
- First click = corner 1
- Mouse move shows preview quad
- Second click = corner 2 (finalize)
- IsComplete → true after corner 2

**Circle Mode:**
- Click center
- Drag defines radius (visual feedback)
- MouseUp finalizes
- IsComplete → true after release

**PointByPoint Mode:**
- Click to add vertex
- Space key → Confirm this point (visual feedback)
- Click again to add next
- Enter → close polygon (IsComplete)
- Escape → cancel (revert to Idle)

## SceneRegionState (Persistence)

**Purpose:** In-memory + file-persisted region registry.

**Storage:** `Library/MCP_Regions.json` (gitignored, survives domain reload + restart).

**Data Structure:**
```csharp
RegionSnapshot
{
    string Id;              // UUID
    float CenterX, CenterZ;
    float MinX, MaxX, MinZ, MaxZ;  // Bounding box
    float[] VerticesX, VerticesZ;  // Raw polygon points
    int SnapshotVersion;           // For staleness detection
    long CreatedTicks, ModifiedTicks;
}
```

**API:**
- `SetRegion(snap)` → Add/replace; auto-evict oldest if > MaxRegions (default 20)
- `GetById(id)` → Retrieve snapshot
- `Remove(id)` → Delete
- `All` → IReadOnlyCollection<RegionSnapshot>
- `IsStale(id)` → SnapshotVersion != _globalVersion (hierarchy changed)

**Eviction Policy:** FIFO when count > MaxRegions (20 default). Max age: 24h (implementation deferred).

**Thread Safety:** Main thread only (Editor API).

**Version Tracking:** _globalVersion increments on hierarchy change (EditorApplication.hierarchyChanged) — detects stale regions.

**Navigation:**
- `FrameRegion(id)` → SceneView.Frame(bounds)
- `HighlightRegion(id)` → Alias for FrameRegion (visual feedback)

## SceneRegionQuery (Polygon Queries)

**Purpose:** Find GameObjects inside polygon (spatial query).

**Query Execution:** `Execute(args)` → JSON string with:
- `vertices` — CSV "x1,z1;x2,z2;..." (optional; use region_id instead)
- `region_id` — UUID to load from cache (optional)
- `component` — Filter by component type (optional; empty = all)
- `cap` — Result limit (default 50, hard max 200)

**Query Pipeline:**

```
1. AABB pre-filter (cheap)
   ↓
2. Component filter (if specified)
   ↓
3. Point-in-polygon (Polygon2D.Contains)
   ↓
4. Cap at result limit
   ↓
5. Format + return
```

**Output Format:**
```
[region_id or inline] (X objects)
  /GameObject1 (Component1)
  /GameObject2 (Component2)
```

**Point-in-Polygon:** Uses Polygon2D.Contains (ray-casting algorithm, O(n) per point, O(m*n) total).

**Optimization:**
- AABB bounds check first (early exit for distant objects)
- Component filter reduces search space
- Cap=50 prevents huge result sets (user can paginate)

**Error Cases:**
- region_id not found → "Region 'X' not found. Draw with Shift+R first."
- Polygon < 3 vertices → ArgumentException
- No vertices + no region_id → ArgumentException

## Polygon2D Utilities

**Methods:**
- `FromCsv(string)` — Parse "x1,z1;x2,z2;..." → Polygon2D
- `Contains(Vector2)` → bool (ray-casting PIP)
- `Area()` → float (shoelace formula)
- `ComputeBounds()` → Rect (min/max x/z)
- `Simplify(epsilon)` → Polygon2D (Douglas-Peucker)

**Precision:** Uses Vector2 (float, ~6 decimal places). Sufficient for scene queries.

## SceneMcpOverlay (Visual Feedback)

**Purpose:** Draw region polygons in Scene View (gizmos).

**Elements:**
- Region outline (white lines)
- Selected points (blue circles)
- Center point (red cross)
- Grid overlay (if snap enabled)
- Mode name label (top-left)

**Gizmo Drawing:**
- OnSceneGUI → check _state
- Drawing → draw partial polygon + cursor line
- Preview → draw complete polygon + matched objects (green quads)

**Performance:** Cull to visible viewport (SceneView frustum).

## Integration with Chat

**Chip Provider:** RegionChipProvider registers via ChipKindRegistry.

**Chip Markup:** `[region:id]Label[/region]` in LLM responses.

**Chip Behavior:**
- Click → FrameRegion(id) in Scene View
- Right-click → Show region context menu (navigate, delete, export)

**Query from Chat:**
```
@region query_state(region_id, component="Collider", cap=50)
# → LLM calls SceneRegionQuery.Execute internally
```

## Common Patterns

| Pattern | Method | Why |
|---------|--------|-----|
| Draw region | Shift+R + Q/W/E/R + Enter | Multi-mode; persistent cache |
| Query inside | query_state with region_id | One call; efficient AABB+PIP |
| Load last region | SceneRegionState.GetById(id) | Survives domain reload |
| Check staleness | IsStale(id) | Hierarchy changed; re-query if true |
| Frame in viewport | FrameRegion(id) | Visual focus from Chat |

## Errors & Recovery

| Error | Cause | Fix |
|-------|-------|-----|
| "Region not found" | ID typo or evicted (>20 regions) | Redraw region; check SceneRegionState.All |
| Polygon corrupt (< 3 verts) | Interrupted draw (Escape before Enter) | Redraw; confirm with Enter, not Escape |
| Query returns 0 objects | No GameObjects in polygon | Expand region; lower cap; remove component filter |
| "Cannot query in Play Mode" | Called during play session | Pause/stop; then draw region in Edit Mode |
| Stale region (hierarchy changed) | Objects added/removed/moved | IsStale() returns true; re-query or delete region |

## Performance Characteristics

| Operation | Time | Notes |
|-----------|------|-------|
| Draw polygon (Lasso, 50 pts) | Instant (< 16ms) | Real-time gizmo draw |
| Simplify (Douglas-Peucker ε=0.1) | <1ms | 50 pts → ~20 pts |
| Query 100 GameObjects | ~5ms | AABB + PIP |
| Persist to JSON | <1ms | 20 regions, ~40KB |
| Load from JSON | <1ms | Deserialization |

## Data Format (MCP_Regions.json)

```json
{
  "Regions": [
    {
      "Id": "region-uuid-1",
      "CenterX": 5.0, "CenterZ": -3.0,
      "MinX": 0.0, "MaxX": 10.0, "MinZ": -8.0, "MaxZ": 2.0,
      "VerticesX": [0, 5, 10, 5],
      "VerticesZ": [-8, -8, 2, 2],
      "SnapshotVersion": 42,
      "CreatedTicks": 1234567890,
      "ModifiedTicks": 1234567891
    }
  ]
}
```

## GdSnapshotSerializer (Playtest Integration)

**Purpose:** Convert `RegionSnapshot` instances to DSL `VAL $label` preamble lines for playtest scripts (updated v0.92.x from ALIAS to VAL format). Bridges the region annotation system with the playtest DSL.

**API:**
- `ToAliasLines(snap)` → one or more `VAL $label x,y,z` lines (no trailing newline)
- `ToPlaytestPreamble(snapshots)` → full preamble block from a collection

**Label format:** `$<sanitized_label>` — lowercase, underscores, stripped special chars; fallback to `gd_<id>` if label empty.

**Annotation type mapping:**

| AnnotationType | Output |
|----------------|--------|
| `point` / `region` | `VAL $label cx,cy,cz` (center) |
| `polyline` | `VAL $label_0 x,y,z` … `VAL $label_N x,y,z` (per vertex) |
| `measurement` | `VAL $label_start x,y,z` + `VAL $label_end x,y,z` |

**Example output:**
```
VAL $spawn_zone 5.00,0.00,3.00
VAL $patrol_0 1.00,0.00,0.00
VAL $patrol_1 10.00,0.00,0.00
```

**Usage in playtest scripts:**
```
# Include preamble from GdSnapshotSerializer.ToPlaytestPreamble(regions)
TELEPORT Player $spawn_zone
MOVE_PATH $patrol_0 > $patrol_1
```

**File:** `unity-plugin/Editor/RegionTool/GdSnapshotSerializer.cs`  
**Tests:** `unity-plugin/Editor/Tests/RegionTool/GdSnapshotSerializerTests.cs`

---

**Related:** `AI/chat-view.md` (chip registration), `AI/playtest-dsl.md` (GD Integration section), `CLAUDE.md` § verification-gates (validate spatial queries), `.claude/skills/token-optimization.md` (query batching).
