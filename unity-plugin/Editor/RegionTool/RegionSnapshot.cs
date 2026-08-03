using System;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Immutable snapshot of a drawn scene region or annotation.
    /// Persisted to Library/MCP_Regions.json.
    /// VerticesFlat = [x0,z0, x1,z1, ...] — flat pairs for JsonUtility compatibility.
    /// ObjectPaths capped at 50; TotalCount holds true count.
    /// AnnotationType = "region"|"point"|"polyline"|"measurement" (null = legacy "region").
    /// </summary>
    [Serializable]
    internal sealed class RegionSnapshot
    {
        // Identity
        public string Id;
        public int    SchemaVersion = 2;

        // Geometry (XZ plane — Y always 0 in MVP)
        public float[] VerticesFlat;  // [x0,z0, x1,z1, ...]
        public float   Area;          // m², Shoelace
        public float   CenterX;
        public float   CenterZ;
        public float   MinX, MinZ, MaxX, MaxZ; // AABB

        // Scene context
        public string  SceneName;
        public float   PlaneY = 0f;

        // Objects (capped at 50)
        public string[] ObjectPaths;  // ComponentSerializer.GetPath() results
        public string[] ObjectIds;    // EntityId raw values as decimal strings; parallel with ObjectPaths
        public int      TotalCount;   // true count before cap
        public bool     Truncated;    // true if TotalCount > 50

        // Polygon detail level: -1 = use global default; 0–3 = PolygonDetailLevel override
        public int DetailLevel = -1;

        // Staleness
        public int  SnapshotVersion;  // set to SceneRegionState.CurrentVersion on creation
        public long CreatedTicks;     // DateTimeOffset.UtcNow.ToUnixTimeSeconds()

        // Annotation type (Phase 1A). null = legacy "region" (JsonUtility default).
        public string AnnotationType = "region";
        public string Label;
        public float  LengthOrDistance;
        public string Direction; // "dx,dz" normalized (polyline first→last)

        public string ShortLabel
        {
            get
            {
                var type = AnnotationType ?? "region";
                switch (type)
                {
                    case "point":
                        return string.IsNullOrEmpty(Label)
                            ? $"pos=({CenterX:F1},{CenterZ:F1})"
                            : $"{Label} pos=({CenterX:F1},{CenterZ:F1})";
                    case "polyline":
                    {
                        int pts = VerticesFlat != null ? VerticesFlat.Length / 2 : 0;
                        return string.IsNullOrEmpty(Label)
                            ? $"{pts}pts {LengthOrDistance:F1}m"
                            : $"{Label} {pts}pts {LengthOrDistance:F1}m";
                    }
                    case "measurement":
                        return string.IsNullOrEmpty(Label)
                            ? $"{LengthOrDistance:F1}m"
                            : $"{Label} {LengthOrDistance:F1}m";
                    default:
                        return $"{(Truncated ? TotalCount + "+" : (ObjectPaths?.Length.ToString() ?? "0"))}obj {Area:F0}m²";
                }
            }
        }

        public static RegionSnapshot Create(
            string id,
            Polygon2D polygon,
            GameObject[] objects,
            string sceneName)
        {
            var verts = polygon.Vertices;
            var flat = new float[verts.Length * 2];
            for (int i = 0; i < verts.Length; i++)
            {
                flat[i * 2]     = verts[i].x;
                flat[i * 2 + 1] = verts[i].y;  // y = world Z in XZ plane
            }

            int cap   = Math.Min(objects.Length, 50);
            var paths = new string[cap];
            var ids   = new string[cap];
            for (int i = 0; i < cap; i++)
            {
                paths[i] = ComponentSerializer.GetPath(objects[i]);
                ids[i]   = TransientObjectId.GetWireValue(objects[i]);
            }

            var centroid = polygon.Centroid();
            var bounds   = polygon.ComputeBounds();

            return new RegionSnapshot
            {
                Id              = id,
                SchemaVersion   = 2,
                VerticesFlat    = flat,
                Area            = polygon.Area(),
                CenterX         = centroid.x,
                CenterZ         = centroid.y,
                MinX            = bounds.xMin, MinZ = bounds.yMin,
                MaxX            = bounds.xMax, MaxZ = bounds.yMax,
                SceneName       = sceneName ?? "",
                PlaneY          = 0f,
                ObjectPaths     = paths,
                ObjectIds       = ids,
                TotalCount      = objects.Length,
                Truncated       = objects.Length > 50,
                CreatedTicks    = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }

        /// <summary>Reconstruct Polygon2D from flat array (for Navigate/FrameRegion).</summary>
        public Polygon2D ToPolygon2D()
        {
            if (VerticesFlat == null || VerticesFlat.Length < 6) return default;
            var verts = new Vector2[VerticesFlat.Length / 2];
            for (int i = 0; i < verts.Length; i++)
                verts[i] = new Vector2(VerticesFlat[i * 2], VerticesFlat[i * 2 + 1]);
            return new Polygon2D(verts);
        }

        // ── Annotation factory methods ─────────────────────────────────────────

        /// <summary>Create a point annotation at the given XZ position.</summary>
        public static RegionSnapshot CreatePoint(
            string id, Vector2 xz, string[] nearestPaths, string sceneName, string label = "")
        {
            var paths = nearestPaths ?? Array.Empty<string>();
            return new RegionSnapshot
            {
                Id             = id,
                SchemaVersion  = 2,
                AnnotationType = "point",
                Label          = label,
                VerticesFlat   = new[] { xz.x, xz.y },
                Area           = 0f,
                CenterX        = xz.x,
                CenterZ        = xz.y,
                MinX = xz.x, MinZ = xz.y, MaxX = xz.x, MaxZ = xz.y,
                SceneName      = sceneName ?? "",
                ObjectPaths    = paths,
                ObjectIds      = new string[paths.Length], // null = not populated (annotations use path-based lookup)
                TotalCount     = paths.Length,
                CreatedTicks   = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }

        /// <summary>Create a polyline annotation from a sequence of XZ points.</summary>
        public static RegionSnapshot CreatePolyline(
            string id, Vector2[] points, string[] nearPaths, string sceneName, string label = "")
        {
            var flat = new float[points.Length * 2];
            float minX = float.MaxValue, minZ = float.MaxValue;
            float maxX = float.MinValue, maxZ = float.MinValue;
            float len  = 0f;

            for (int i = 0; i < points.Length; i++)
            {
                flat[i * 2]     = points[i].x;
                flat[i * 2 + 1] = points[i].y;
                if (points[i].x < minX) minX = points[i].x;
                if (points[i].y < minZ) minZ = points[i].y;
                if (points[i].x > maxX) maxX = points[i].x;
                if (points[i].y > maxZ) maxZ = points[i].y;
                if (i > 0) len += Vector2.Distance(points[i - 1], points[i]);
            }

            // direction: normalized vector from first to last
            var dir = points.Length >= 2
                ? (points[points.Length - 1] - points[0]).normalized
                : Vector2.right;

            var cx = (minX + maxX) * 0.5f;
            var cz = (minZ + maxZ) * 0.5f;

            return new RegionSnapshot
            {
                Id               = id,
                SchemaVersion    = 2,
                AnnotationType   = "polyline",
                Label            = label,
                VerticesFlat     = flat,
                Area             = 0f,
                CenterX          = cx,
                CenterZ          = cz,
                MinX = minX, MinZ = minZ, MaxX = maxX, MaxZ = maxZ,
                LengthOrDistance = len,
                Direction        = dir.x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
                                   + "," + dir.y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                SceneName        = sceneName ?? "",
                ObjectPaths      = nearPaths ?? Array.Empty<string>(),
                ObjectIds        = new string[(nearPaths?.Length ?? 0)], // parallel; null = not populated
                TotalCount       = nearPaths?.Length ?? 0,
                CreatedTicks     = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }

        /// <summary>Create a measurement annotation between two XZ points.</summary>
        public static RegionSnapshot CreateMeasurement(
            string id, Vector2 a, Vector2 b, string sceneName, string label = "")
        {
            float dist = Vector2.Distance(a, b);
            return new RegionSnapshot
            {
                Id               = id,
                SchemaVersion    = 2,
                AnnotationType   = "measurement",
                Label            = label,
                VerticesFlat     = new[] { a.x, a.y, b.x, b.y },
                Area             = 0f,
                CenterX          = (a.x + b.x) * 0.5f,
                CenterZ          = (a.y + b.y) * 0.5f,
                MinX = Mathf.Min(a.x, b.x), MinZ = Mathf.Min(a.y, b.y),
                MaxX = Mathf.Max(a.x, b.x), MaxZ = Mathf.Max(a.y, b.y),
                LengthOrDistance = dist,
                SceneName        = sceneName ?? "",
                ObjectPaths      = Array.Empty<string>(),
                ObjectIds        = Array.Empty<string>(),
                TotalCount       = 0,
                CreatedTicks     = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }
    }
}
