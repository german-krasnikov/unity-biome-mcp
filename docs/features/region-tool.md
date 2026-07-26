# Region Selection Tool Guide

Draw regions in Scene View to query objects spatially.

## Activation

**Keyboard shortcut:** `Shift+R` in Scene View

**Status:** Mode label appears at top-left of viewport. Press `Escape` to cancel/deactivate.

## Drawing Modes (Q/W/E/R)

| Mode | Key | How It Works |
|------|-----|--------------|
| Lasso | Q | Free-hand polygon (any shape) |
| Rectangle | W | Click two corners; draws axis-aligned box |
| Circle | E | Click center, drag to set radius |
| PointByPoint | R | Click each vertex; Space to confirm; Enter to close |

### Lasso Mode (Q)
- Click repeatedly to trace outline
- Preview line follows cursor
- Enter to close and confirm

### Rectangle Mode (W)
- Click first corner
- Move mouse to second corner (preview shows quad)
- Click second corner to finalize
- Always axis-aligned (no rotation)

### Circle Mode (E)
- Click once for center
- Drag outward to set radius
- Release mouse to confirm

### PointByPoint Mode (R)
- Click to add vertex
- **Space** to confirm point (visual feedback)
- Click again for next vertex
- **Enter** to close polygon
- **Escape** to cancel

## Modifiers

| Key | Effect |
|-----|--------|
| G | Toggle grid snap (0.5 unit grid) |
| Shift | Hold for finer control (points snap to 0.1m) |
| Escape | Cancel current selection and deactivate |
| Enter | Commit polygon to cache |

## Visual Feedback

**Drawing:**
- White lines show polygon edges
- Blue circles mark vertices
- Red cross at center

**Objects inside:**
- Green quad highlights objects within region
- Count shown: "[region_id or inline] (X objects)"

## Querying Regions

### Query from Code

```python
# Get all objects in polygon region (inline vertices)
result = await spatial_query(action="objects_in_polygon", vertices="x1,z1;x2,z2;x3,z3", cap=50)

# Or use saved region ID
result = await spatial_query(action="objects_in_polygon", region_id="region-uuid-123", component="Collider", cap=50)
```

**Parameters:**
- `action` — must be `"objects_in_polygon"`
- `vertices` — semicolon-separated pairs "x1,z1;x2,z2;..." (>=3 pairs; optional if using region_id)
- `region_id` — UUID to load from cache
- `component` — filter by component type name (empty = all)
- `cap` — result limit (default 50, max 200)

### From Chat

Region chips work in the LLM prompt:

```
[region:region-id-123]Selected Area[/region]

Query this region for all Colliders:
→ LLM calls query_state internally
```

Click the chip to frame the region in Scene View.

## Persistence

Regions are automatically saved to `Library/MCP_Regions.json` (gitignored):

- **Max regions:** 20 (older ones auto-evict when limit exceeded)
- **Survives:** Domain reload, editor restart
- **Auto-delete:** Regions older than 24h (on next load)

## Common Patterns

**Select enemies for batch edit:**
```python
# 1. Draw region around enemies with Shift+R
# 2. Query region
objects = await spatial_query(action="objects_in_polygon", region_id="<saved-id>", component="Enemy")
# 3. Batch update health (for each object path in results)
# Use batch for efficiency
await batch("""
set_property path=Enemy1 component=Health prop=MaxHP value=100
set_property path=Enemy2 component=Health prop=MaxHP value=100
""")
```

**Spatial assertions in playtest:**
```
VAL $boss_arena 100,0,0
# (region drawn previously with Shift+R)
ASSERT_NEAR Player Boss 10.0  # Within arena bounds
```

**Visual selection from chat:**
- Draw region with Shift+R
- Chat references region chip
- Click chip to focus in viewport

## Limitations

- Regions are 2D (XZ plane only; Y-independent)
- Point-in-polygon uses ray-casting (O(n) per query)
- Component filter is exact match (case-sensitive)
- Regions are Edit Mode only (Play Mode queries rejected)

## Troubleshooting

| Issue | Solution |
|-------|----------|
| "Region not found" | Region evicted (>20 regions); redraw |
| Polygon won't close | Make sure you press **Enter**, not Escape |
| Query returns 0 objects | Expand region; lower cap; remove component filter |
| Stale region | Hierarchy changed; re-query or delete region |
| Can't draw in Play Mode | Pause/stop; draw regions in Edit Mode |

---

**See also:** [Scene Tools](../tools/scene.md) for spatial queries, [Batch Reference](../tools/batch.md) for batch operations.
