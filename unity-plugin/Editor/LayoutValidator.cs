using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class LayoutValidator
    {
        private static readonly CultureInfo IC = CultureInfo.InvariantCulture;

        public static string Validate(string root, float minDistance)
        {
            var rootGO = ComponentSerializer.FindObject(root);
            if (rootGO == null) return ErrorHelper.ObjectNotFound(root);

            var triggers = new List<(Transform t, string name)>();
            var solids = new List<(Transform t, string name)>();

            foreach (var col in rootGO.GetComponentsInChildren<Collider>(true))
            {
                var path = GetRelativePath(col.transform, rootGO.transform);
                if (col.isTrigger) triggers.Add((col.transform, path));
                else solids.Add((col.transform, path));
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Layout: {triggers.Count} triggers, {solids.Count} solids");

            int warnings = 0;
            for (int i = 0; i < triggers.Count; i++)
                for (int j = i + 1; j < triggers.Count; j++)
                {
                    var dist = Vector3.Distance(triggers[i].t.position, triggers[j].t.position);
                    if (dist < minDistance)
                    {
                        sb.AppendLine($"WARNING: {triggers[i].name} <-> {triggers[j].name} dist={dist.ToString("F1", IC)}m < {minDistance}m");
                        warnings++;
                    }
                }

            sb.Append(warnings == 0 ? "OK: no trigger overlaps" : $"{warnings} warning(s)");
            return sb.ToString();
        }

        public static string GetSpatialContext(string path, float radius)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) return ErrorHelper.ObjectNotFound(path);

            var pos = go.transform.position;
            var sb = new StringBuilder();
            sb.AppendLine($"Position: ({pos.x.ToString("F1", IC)},{pos.y.ToString("F1", IC)},{pos.z.ToString("F1", IC)})");

            foreach (var col in go.GetComponentsInChildren<Collider>())
            {
                var type = col.isTrigger ? "TRIGGER" : "SOLID";
                var bounds = col.bounds;
                sb.AppendLine($"  {col.gameObject.name} [{type}] center=({bounds.center.x.ToString("F1", IC)},{bounds.center.y.ToString("F1", IC)},{bounds.center.z.ToString("F1", IC)}) size=({bounds.size.x.ToString("F1", IC)},{bounds.size.y.ToString("F1", IC)},{bounds.size.z.ToString("F1", IC)})");
            }

            Physics.SyncTransforms();
            sb.AppendLine("Approach vectors:");
            var dirs = new (string name, Vector3 dir)[] {
                ("N", Vector3.forward), ("S", Vector3.back),
                ("E", Vector3.right), ("W", Vector3.left),
                ("NE", (Vector3.forward+Vector3.right).normalized),
                ("NW", (Vector3.forward+Vector3.left).normalized),
                ("SE", (Vector3.back+Vector3.right).normalized),
                ("SW", (Vector3.back+Vector3.left).normalized)
            };
            foreach (var (name, dir) in dirs)
            {
                var testPoint = pos + dir * radius;
                var blocked = Physics.Linecast(testPoint, pos, out _);
                sb.AppendLine($"  {name}: ({testPoint.x.ToString("F1", IC)},{testPoint.y.ToString("F1", IC)},{testPoint.z.ToString("F1", IC)}) {(blocked ? "BLOCKED" : "CLEAR")}");
            }

            var nearby = Physics.OverlapSphere(pos, radius);
            if (nearby.Length > 0)
            {
                sb.AppendLine($"Nearby ({radius}m radius):");
                foreach (var col in nearby)
                {
                    if (col.transform.root == go.transform.root) continue;
                    var dist = Vector3.Distance(col.transform.position, pos);
                    sb.AppendLine($"  {col.gameObject.name} dist={dist.ToString("F1", IC)}m {(col.isTrigger ? "TRIGGER" : "SOLID")}");
                }
            }

            return sb.ToString().TrimEnd();
        }

        private static string GetRelativePath(Transform child, Transform root)
        {
            if (child == root) return root.name;
            var parts = new List<string>();
            var current = child;
            while (current != null && current != root)
            {
                parts.Add(current.name);
                current = current.parent;
            }
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
