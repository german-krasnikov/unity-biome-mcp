using System.Text;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Execute polygon-based spatial queries against the current Unity scene.
    /// Reads from scene via FindObjectsByType + Polygon2D.Contains.
    /// Output is plain text matching existing SpatialHelper patterns.
    /// </summary>
    internal static class SceneRegionQuery
    {
        private const int DefaultCap = 50;
        private const int HardMaxCap = 200;

        /// <summary>
        /// Find all GameObjects whose XZ pivot position is inside the polygon.
        /// args: JSON string with keys: vertices, region_id, component, cap
        /// </summary>
        public static string Execute(string args)
        {
            var vertices  = JsonHelper.ExtractString(args, "vertices");
            var regionId  = JsonHelper.ExtractString(args, "region_id");
            var component = JsonHelper.ExtractString(args, "component");
            var capStr    = JsonHelper.ExtractString(args, "cap");

            int cap = DefaultCap;
            if (capStr != null && int.TryParse(capStr, out var parsedCap))
                cap = System.Math.Min(System.Math.Max(1, parsedCap), HardMaxCap);

            Polygon2D poly;
            string regionLabel;

            if (!string.IsNullOrEmpty(regionId))
            {
                if (!string.IsNullOrEmpty(vertices))
                {
                    poly = Polygon2D.FromCsv(vertices);
                }
                else
                {
                    var snap = SceneRegionState.GetById(regionId);
                    if (snap == null)
                        throw new System.ArgumentException($"Region '{regionId}' not found. Draw a region with Shift+R first.");
                    poly = snap.ToPolygon2D();
                    if (poly.Vertices == null || poly.Vertices.Length < 3)
                        throw new System.ArgumentException($"Region '{regionId}' has corrupt geometry.");
                }
                regionLabel = regionId;
            }
            else if (!string.IsNullOrEmpty(vertices))
            {
                poly = Polygon2D.FromCsv(vertices);
                regionLabel = "inline";
            }
            else
            {
                throw new System.ArgumentException(
                    "Provide 'vertices' (CSV 'x1,z1;x2,z2;...') or 'region_id'");
            }

            return FindInside(poly, regionLabel, component, cap);
        }

        /// <summary>
        /// Find GameObjects inside polygon — returns array for tool/snapshot use.
        /// Internally used by SceneRegionTool; cap defaults to 50.
        /// </summary>
        public static GameObject[] FindInside(Polygon2D poly, int cap = DefaultCap)
        {
            var bounds = poly.ComputeBounds();
            var result = new System.Collections.Generic.List<GameObject>(cap);
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                var pos = go.transform.position;
                if (pos.x < bounds.xMin || pos.x > bounds.xMax ||
                    pos.z < bounds.yMin || pos.z > bounds.yMax) continue;
                if (!poly.Contains(new UnityEngine.Vector2(pos.x, pos.z))) continue;
                result.Add(go);
                if (result.Count >= cap) break;
            }
            return result.ToArray();
        }

        /// <summary>
        /// Core query: find GameObjects inside polygon, return formatted text.
        /// Stages: AABB pre-filter → component filter → PIP → cap+format.
        /// </summary>
        public static string FindInside(Polygon2D poly, string label, string componentFilter, int cap = DefaultCap)
        {
            var bounds = poly.ComputeBounds();
            var area   = poly.Area();
            var sb     = new StringBuilder();
            int count  = 0;
            int total  = 0;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                // Stage 1: AABB pre-filter (cheap)
                var pos = go.transform.position;
                float xz_x = pos.x, xz_z = pos.z;
                if (xz_x < bounds.xMin || xz_x > bounds.xMax ||
                    xz_z < bounds.yMin || xz_z > bounds.yMax) continue;

                // Stage 2: Component filter (before PIP — cheap when component absent)
                if (!string.IsNullOrEmpty(componentFilter))
                {
                    bool has = false;
                    foreach (var c in go.GetComponents<Component>())
                        if (c != null && c.GetType().Name.Contains(componentFilter))
                        { has = true; break; }
                    if (!has) continue;
                }

                // Stage 3: Winding-number PIP (most expensive per-object)
                if (!poly.Contains(new UnityEngine.Vector2(xz_x, xz_z))) continue;

                total++;
                if (count < cap)
                {
                    // Format matches ObjectsInRadius: path #id (x,z)
                    sb.AppendLine($"  {ComponentSerializer.GetPath(go)} #{TransientObjectId.GetWireValue(go)} ({F(xz_x)},{F(xz_z)})");
                    count++;
                }
            }

            string Region() => string.IsNullOrEmpty(label) ? "polygon" : $"'{label}'";
            if (total == 0) return $"0 objects in {Region()} (area={F(area, "F1")}m2)";

            var header = $"{total} objects in {Region()} (area={F(area, "F1")}m2):";
            if (total > cap)
            {
                sb.AppendLine($"  ...+{total - cap} more");
                header = $"{cap}+ objects in {Region()} (area={F(area, "F1")}m2):";
            }
            return header + "\n" + sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Find GameObjects whose XZ position is within <paramref name="radius"/> of any segment
        /// of the polyline defined by <paramref name="points"/>.
        /// AABB pre-filter runs first; per-segment distance only for objects inside expanded AABB.
        /// </summary>
        public static GameObject[] FindNearPolyline(Vector2[] points, float radius, int cap = DefaultCap)
        {
            if (points == null || points.Length < 2)
                return System.Array.Empty<GameObject>();

            cap = System.Math.Min(System.Math.Max(1, cap), HardMaxCap);

            // Build AABB over all polyline points, expanded by radius
            float minX = points[0].x, maxX = points[0].x;
            float minZ = points[0].y, maxZ = points[0].y;
            for (int i = 1; i < points.Length; i++)
            {
                if (points[i].x < minX) minX = points[i].x;
                if (points[i].x > maxX) maxX = points[i].x;
                if (points[i].y < minZ) minZ = points[i].y;
                if (points[i].y > maxZ) maxZ = points[i].y;
            }
            minX -= radius; maxX += radius;
            minZ -= radius; maxZ += radius;

            var result = new System.Collections.Generic.List<GameObject>(cap);
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                var pos = go.transform.position;
                // Stage 1: AABB pre-filter
                if (pos.x < minX || pos.x > maxX || pos.z < minZ || pos.z > maxZ) continue;

                // Stage 2: min distance to any segment
                var xz = new Vector2(pos.x, pos.z);
                float minDist = float.MaxValue;
                for (int i = 0; i < points.Length - 1; i++)
                {
                    float d = PointToSegmentDist(xz, points[i], points[i + 1]);
                    if (d < minDist) minDist = d;
                }
                if (minDist > radius) continue;

                result.Add(go);
                if (result.Count >= cap) break;
            }
            return result.ToArray();
        }

        /// <summary>Distance from point P to segment AB.</summary>
        internal static float PointToSegmentDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-10f) return (p - a).magnitude;
            float t = Vector2.Dot(p - a, ab) / lenSq;
            t = System.Math.Max(0f, System.Math.Min(1f, t));
            return (p - (a + t * ab)).magnitude;
        }

        private static string F(float v, string fmt = "F2")
            => v.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture);
    }
}
