using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class SpatialHelper
    {
        private static string F(float v, string fmt = "F1") =>
            v.ToString(fmt, CultureInfo.InvariantCulture);

        private static float ExtractFloat(string args, string key, float def)
        {
            var val = JsonHelper.ExtractString(args, key);
            return val != null && float.TryParse(val,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : def;
        }

        private static int ExtractInt(string args, string key, int def)
        {
            var val = JsonHelper.ExtractString(args, key);
            return val != null && int.TryParse(val, out var i) ? i : def;
        }

        private static bool TryParseVec3(string s, out Vector3 v)
        {
            v = default;
            var parts = s.Trim('(', ')').Split(',');
            if (parts.Length != 3) return false;
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return false;
            if (!float.TryParse(parts[1], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) return false;
            if (!float.TryParse(parts[2], System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return false;
            v = new Vector3(x, y, z);
            return true;
        }

        private static Vector3 ParseVec3(string s)
        {
            if (!TryParseVec3(s, out var v))
                throw new System.ArgumentException($"Invalid position format '{s}', expected '(x,y,z)'");
            return v;
        }

        public static string Raycast(string fromArg, string toArg, string layerMask)
        {
            Physics.SyncTransforms();

            Vector3 origin = fromArg != null && fromArg.StartsWith("(")
                ? ParseVec3(fromArg)
                : (ComponentSerializer.FindObject(fromArg) is var fo && fo != null
                    ? fo.transform.position
                    : throw new System.ArgumentException(ErrorHelper.ObjectNotFound(fromArg)));

            Vector3 target = toArg != null && toArg.StartsWith("(")
                ? ParseVec3(toArg)
                : (ComponentSerializer.FindObject(toArg) is var to && to != null
                    ? to.transform.position
                    : throw new System.ArgumentException(ErrorHelper.ObjectNotFound(toArg)));

            var dir = target - origin;
            float dist = dir.magnitude;
            int mask = layerMask != null && int.TryParse(layerMask, out var lm) ? lm : Physics.DefaultRaycastLayers;
            var hits = Physics.RaycastAll(origin, dir.normalized, dist, mask);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            var sb = new StringBuilder();
            sb.AppendLine($"PATH: ({F(origin.x)},{F(origin.y)},{F(origin.z)}) -> ({F(target.x)},{F(target.y)},{F(target.z)}) dist={F(dist,"F2")}");

            int count = System.Math.Min(hits.Length, 20);
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                var p = h.point;
                var col = h.collider.GetType().Name;
                sb.AppendLine($"HIT {i + 1}: {ComponentSerializer.GetPath(h.collider.gameObject)} at ({F(p.x)},{F(p.y)},{F(p.z)}) dist={F(h.distance,"F2")} [{col}]");
            }

            sb.Append(count == 0 ? "CLEAR (no hits)" : $"BLOCKED: {count} hit{(count > 1 ? "s" : "")}");
            return sb.ToString();
        }

        public static string SpatialMap(string root, float cellSize)
        {
            if (cellSize <= 0f) cellSize = 2f;

            var transforms = new List<Transform>();
            if (string.IsNullOrEmpty(root) || root == "/")
            {
                foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                    if (go.transform.parent == null) transforms.Add(go.transform);
            }
            else
            {
                var rootGO = ComponentSerializer.FindObject(root);
                if (rootGO == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(root));
                foreach (Transform child in rootGO.GetComponentsInChildren<Transform>())
                    transforms.Add(child);
            }

            if (transforms.Count == 0) return "No objects found";

            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            foreach (var t in transforms)
            {
                minX = System.Math.Min(minX, t.position.x);
                maxX = System.Math.Max(maxX, t.position.x);
                minZ = System.Math.Min(minZ, t.position.z);
                maxZ = System.Math.Max(maxZ, t.position.z);
            }

            int cols = System.Math.Min(40, (int)((maxX - minX) / cellSize) + 1);
            int rows = System.Math.Min(40, (int)((maxZ - minZ) / cellSize) + 1);
            if (cols < 1) cols = 1;
            if (rows < 1) rows = 1;

            var grid = new char[rows, cols];
            var legend = new Dictionary<char, string>();
            char nextLabel = 'A';

            for (int r = 0; r < rows; r++)
                for (int c = 0; c < cols; c++)
                    grid[r, c] = '.';

            foreach (var t in transforms)
            {
                int c = System.Math.Min(cols - 1, (int)((t.position.x - minX) / cellSize));
                int r = System.Math.Min(rows - 1, (int)((maxZ - t.position.z) / cellSize));
                if (grid[r, c] == '.')
                {
                    if (nextLabel > 'Z') continue;
                    grid[r, c] = nextLabel;
                    legend[nextLabel] = t.name;
                    nextLabel++;
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"# Map: XZ, cell={cellSize}m, {cols}x{rows}");
            var legendParts = new List<string>();
            foreach (var kv in legend) legendParts.Add($"{kv.Key}={kv.Value}");
            sb.AppendLine("# " + string.Join(" ", legendParts.ToArray()));

            for (int r = 0; r < rows; r++)
            {
                float z = maxZ - r * cellSize;
                sb.Append($"{F(z,"F0"),4} ");
                for (int c = 0; c < cols; c++) sb.Append($" {grid[r, c]} ");
                sb.AppendLine();
            }

            return sb.ToString().TrimEnd();
        }


        public static string Nearest(string fromPath, string componentFilter)
        {
            var from = ComponentSerializer.FindObject(fromPath);
            if (from == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(fromPath));
            var fromPos = from.transform.position;

            GameObject best = null;
            float bestDist = float.MaxValue;

            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go == from) continue;
                if (!string.IsNullOrEmpty(componentFilter))
                {
                    bool hasComp = false;
                    foreach (var c in go.GetComponents<Component>())
                        if (c != null && c.GetType().Name.Contains(componentFilter)) { hasComp = true; break; }
                    if (!hasComp) continue;
                }
                float dist = Vector3.Distance(fromPos, go.transform.position);
                if (dist < bestDist) { bestDist = dist; best = go; }
            }

            if (best == null) return "No matching object found";
            var pos = best.transform.position;
            return $"{ComponentSerializer.GetPath(best)} dist={F(bestDist,"F2")} pos=({F(pos.x,"F2")},{F(pos.y,"F2")},{F(pos.z,"F2")})";
        }

        public static string InFrontOf(string path, float distance)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            var pos = go.transform.position + go.transform.forward * distance;
            return $"({F(pos.x,"F2")},{F(pos.y,"F2")},{F(pos.z,"F2")})";
        }

        /// <summary>
        /// List objects within radius. center wins over path when both provided.
        /// center format: "x,y,z" (comma-separated, no parens).
        /// </summary>
        public static string ObjectsInRadius(string path, float radius, string center = null, int cap = 20)
        {
            if (radius <= 0f) return "No objects within radius";
            cap = System.Math.Min(System.Math.Max(1, cap), 200);

            Vector3 fromPos;
            GameObject fromObj = null;

            if (center != null)
            {
                // center wins — parse "x,y,z" by wrapping in parens for ParseVec3
                try { fromPos = ParseVec3("(" + center + ")"); }
                catch { throw new System.ArgumentException("Invalid center format, expected 'x,y,z'"); }
            }
            else
            {
                fromObj = ComponentSerializer.FindObject(path);
                if (fromObj == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
                fromPos = fromObj.transform.position;
            }

            var sb = new StringBuilder();
            int total = 0;
            int shown = 0;
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (go == fromObj || (fromObj != null && go.transform.IsChildOf(fromObj.transform))) continue;
                float dist = Vector3.Distance(fromPos, go.transform.position);
                if (dist <= radius)
                {
                    total++;
                    if (shown < cap)
                    {
                        sb.AppendLine($"  {ComponentSerializer.GetPath(go)} dist={F(dist,"F2")}");
                        shown++;
                    }
                }
            }
            if (total == 0) return "No objects within radius";
            if (total > shown) sb.AppendLine($"  ...+{total - shown} more");
            return $"{total} objects within {radius}m (showing {shown}):\n{sb.ToString().TrimEnd()}";
        }

        public static string BoundsInfo(string path)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            var b = MultiViewCapture.ComputeBounds(go);
            return $"center=({F(b.center.x,"F2")},{F(b.center.y,"F2")},{F(b.center.z,"F2")}) " +
                   $"size=({F(b.size.x,"F2")},{F(b.size.y,"F2")},{F(b.size.z,"F2")}) " +
                   $"min=({F(b.min.x,"F2")},{F(b.min.y,"F2")},{F(b.min.z,"F2")}) " +
                   $"max=({F(b.max.x,"F2")},{F(b.max.y,"F2")},{F(b.max.z,"F2")})";
        }

        /// <summary>
        /// Delete (or preview) objects inside a polygon region.
        /// dry_run=true → list; dry_run=false → Undo.DestroyObjectImmediate each.
        /// </summary>
        public static string RegionClear(string args)
        {
            var vertices = JsonHelper.ExtractString(args, "vertices");
            var dryRunStr = JsonHelper.ExtractString(args, "dry_run") ?? "true";
            var filter = JsonHelper.ExtractString(args, "filter");
            var capStr = JsonHelper.ExtractString(args, "cap");

            bool dryRun = !string.Equals(dryRunStr, "false", System.StringComparison.OrdinalIgnoreCase);
            int cap = 50;
            if (capStr != null && int.TryParse(capStr, out var pc)) cap = System.Math.Min(System.Math.Max(1, pc), 200);

            if (string.IsNullOrEmpty(vertices))
                throw new System.ArgumentException("vertices required for region_clear");

            var poly = UnityMCP.Editor.RegionTool.Polygon2D.FromCsv(vertices);
            var objects = UnityMCP.Editor.RegionTool.SceneRegionQuery.FindInside(poly, cap);

            // Apply name filter if provided
            var targets = new System.Collections.Generic.List<GameObject>(objects.Length);
            foreach (var go in objects)
            {
                if (filter != null && !go.name.Contains(filter)) continue;
                targets.Add(go);
            }

            if (dryRun)
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"DRY: {targets.Count} object{(targets.Count == 1 ? "" : "s")} would be deleted:");
                foreach (var go in targets)
                    sb.AppendLine($"  {ComponentSerializer.GetPath(go)}");
                return sb.ToString().TrimEnd();
            }

            int deleted = 0;
            foreach (var go in targets)
            {
                if (go == null) continue;
                UnityEditor.Undo.DestroyObjectImmediate(go);
                deleted++;
            }
            return $"DELETED: {deleted} object{(deleted == 1 ? "" : "s")}";
        }

        public static string Execute(string args)
        {
            var action = JsonHelper.ExtractString(args, "action");
            return action switch
            {
                "nearest" => Nearest(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "component") ?? ""),
                "in_front_of" => InFrontOf(
                    JsonHelper.ExtractString(args, "path"),
                    float.TryParse(JsonHelper.ExtractString(args, "distance") ?? "1",
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : 1f),
                "objects_in_radius" => ObjectsInRadius(
                    JsonHelper.ExtractString(args, "path"),
                    float.TryParse(JsonHelper.ExtractString(args, "radius") ?? "5",
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 5f,
                    JsonHelper.ExtractString(args, "center"),
                    ExtractInt(args, "cap", 20)),
                "bounds_info" => BoundsInfo(JsonHelper.ExtractString(args, "path")),
                "raycast" => Raycast(
                    JsonHelper.ExtractString(args, "path"),
                    JsonHelper.ExtractString(args, "target"),
                    JsonHelper.ExtractString(args, "layer_mask")),
                "spatial_map" => SpatialMap(
                    JsonHelper.ExtractString(args, "path"),
                    ExtractFloat(args, "cell_size", 2f)),
                "objects_in_polygon" => RegionTool.SceneRegionQuery.Execute(args),
                _ => throw new System.ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "nearest", "in_front_of", "objects_in_radius", "bounds_info",
                            "raycast", "spatial_map", "objects_in_polygon" }))
            };
        }
    }
}
