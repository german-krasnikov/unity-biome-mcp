"""Spatial / scene-analysis queries: layout, context, scan, raycast, colliders."""
from ._annotations import RO as _RO, RW as _RW
from ._common import bind

_send = None
_args = None


async def validate_layout(root: str = "/", min_distance: float = 3.0) -> str:
    """Check trigger overlaps. Warns if triggers closer than min_distance meters."""
    return await _send("validate_layout", _args(root=root, min_distance=str(min_distance)))


async def get_spatial_context(path: str, radius: float = 5.0) -> str:
    """Collider info + approach vectors + nearby objects within radius. Raycast in Play Mode only."""
    return await _send("get_spatial_context", _args(path=path, radius=str(radius)))


async def scan_scene() -> str:
    """Scene infrastructure scan: colliders, triggers, audio, lights, rigidbody, canvas, nav. Coverage stats."""
    return await _send("scan_scene", {})


async def check_colliders(path: str | None = None) -> str:
    """Check collider issues: triggers without Rigidbody, negative scale, micro colliders. Scans whole scene if no path given."""
    return await _send("check_colliders", _args(path=path))


async def autofit_collider(path: str, type: str = "box") -> str:
    """Auto-fit collider to mesh/renderer bounds. type: box|sphere|capsule."""
    return await _send("autofit_collider", _args(path=path, type=type))


def _validate_polygon(vertices: str | None) -> None:
    """Fast-fail validation for polygon CSV. Raises ToolError on bad input."""
    from mcp.server.fastmcp.exceptions import ToolError
    if not vertices:
        raise ToolError("vertices required for objects_in_polygon")
    pairs = vertices.split(";")
    if len(pairs) < 3:
        raise ToolError(f"polygon needs >=3 vertices, got {len(pairs)}")
    if len(pairs) > 256:
        raise ToolError(f"polygon max 256 vertices, got {len(pairs)}. Simplify contour.")
    for i, pair in enumerate(pairs):
        parts = pair.strip().split(",")
        if len(parts) != 2:
            raise ToolError(f"vertex {i}: expected 'x,z', got '{pair.strip()}'")
        try:
            x, z = float(parts[0]), float(parts[1])
        except ValueError:
            raise ToolError(f"vertex {i}: non-numeric '{pair.strip()}'")
        if abs(x) > 100_000 or abs(z) > 100_000:
            raise ToolError(f"vertex {i}: coordinates out of range (max 100000)")


async def spatial_query(action: str, path: str | None = None, target: str | None = None,
                        distance: float | None = None, radius: float | None = None,
                        component: str | None = None, cell_size: float | None = None,
                        layer_mask: str | None = None,
                        center: str | None = None,
                        vertices: str | None = None,
                        region_id: str | None = None,
                        cap: int | None = None) -> str:
    """Spatial queries. action: nearest|in_front_of|objects_in_radius|bounds_info|raycast|spatial_map|objects_in_polygon.
    nearest: find closest object (optionally filtered by component name).
    in_front_of: position in front of object at distance.
    objects_in_radius: list all objects within radius. path is optional when center='x,y,z' is given.
    bounds_info: detailed bounds/dimensions of object.
    raycast: cast ray from path/pos to target, returns hits sorted by distance.
    spatial_map: ASCII grid map of objects in XZ plane. cell_size in meters.
    objects_in_polygon: objects whose XZ pivot is inside polygon. vertices='x1,z1;x2,z2;...' (>=3 pairs). cap=max results (default 50). region_id=optional tag forwarded to Unity (e.g. for named zones)."""
    if action == "objects_in_polygon":
        _validate_polygon(vertices)
    return await _send("spatial_query", _args(
        action=action, path=path, target=target,
        distance=str(distance) if distance is not None else None,
        radius=str(radius) if radius is not None else None,
        component=component,
        cell_size=str(cell_size) if cell_size is not None else None,
        layer_mask=layer_mask,
        center=center,
        vertices=vertices,
        region_id=region_id,
        cap=str(cap) if cap is not None else None))


async def region_clear(vertices: str, dry_run: bool = True,
                       filter: str | None = None, cap: int = 50) -> str:
    """Delete (or preview) all objects whose XZ pivot is inside the polygon region.

    vertices: CSV polygon 'x1,z1;x2,z2;...' (>=3 pairs).
    dry_run: True = list objects that WOULD be deleted (safe default). False = delete them.
    filter: optional name-pattern substring; only matching objects are affected.
    cap: max objects processed (default 50, hard max 200).
    """
    _validate_polygon(vertices)
    return await _send("region_clear", _args(
        vertices=vertices,
        dry_run="true" if dry_run else "false",
        filter=filter,
        cap=str(cap)))


async def navmesh_query(action: str, center: str | None = None,
                        from_pos: str | None = None, to: str | None = None,
                        max_distance: float = 5.0, area_mask: int = -1,
                        agentRadius: float | None = None,
                        agentHeight: float | None = None,
                        agentClimb: float | None = None,
                        agentSlope: float | None = None) -> str:
    """NavMesh queries and management. action: sample|path|raycast|bake|status|clear|get_settings|set_settings.
    sample: find nearest walkable point to center.
    path: calculate path from from_pos to to.
    raycast: NavMesh raycast from from_pos toward to.
    bake: build NavMesh (NavMeshSurface components or legacy NavMeshBuilder).
    status: triangulation stats (triangles, vertices, areas).
    clear: remove all baked NavMesh data.
    get_settings: list all NavMesh agent type settings.
    set_settings: update NavMeshSurface agent params (agentRadius/agentHeight/agentClimb/agentSlope)."""
    d: dict = {"action": action, "area_mask": str(area_mask)}
    if center: d["center"] = center
    if from_pos: d["from"] = from_pos
    if to: d["to"] = to
    if action == "sample": d["max_distance"] = str(max_distance)
    if agentRadius is not None: d["agentRadius"] = agentRadius
    if agentHeight is not None: d["agentHeight"] = agentHeight
    if agentClimb is not None: d["agentClimb"] = agentClimb
    if agentSlope is not None: d["agentSlope"] = agentSlope
    result = await _send("navmesh", _args(**d))
    if "Command not registered: navmesh" in result:
        return "NavMesh unavailable: AI Navigation package not installed in this project."
    return result


async def analyze_lod_culling(focus: str | None = None) -> str:
    """LOD group coverage + occlusion culling analysis.
    focus: lod|culling|occlusion|null=all."""
    return await _send("analyze_lod_culling", _args(focus=focus))


def register(mcp, send, args):
    bind(globals(), send, args)
    mcp.tool(annotations=_RO)(validate_layout)
    mcp.tool(annotations=_RO)(get_spatial_context)
    mcp.tool(annotations=_RO)(scan_scene)
    mcp.tool(annotations=_RO)(check_colliders)
    mcp.tool(annotations=_RW)(autofit_collider)
    mcp.tool(annotations=_RO)(spatial_query)
    mcp.tool(annotations=_RW)(region_clear)
    mcp.tool(annotations=_RW)(navmesh_query)
    mcp.tool(annotations=_RO)(analyze_lod_culling)
