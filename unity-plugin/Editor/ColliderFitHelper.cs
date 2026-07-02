using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ColliderFitHelper
    {
        internal static string Execute(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var type = JsonHelper.ExtractString(argsJson, "type") ?? "box";
            var go = ComponentSerializer.FindObjectOrThrow(path);

            var bounds = GetLocalBounds(go);
            if (!bounds.HasValue)
                throw new System.ArgumentException($"no Renderer or MeshFilter on '{path}'");

            return type.ToLowerInvariant() switch
            {
                "box"     => FitBox(go, bounds.Value),
                "sphere"  => FitSphere(go, bounds.Value),
                "capsule" => FitCapsule(go, bounds.Value),
                _         => throw new System.ArgumentException($"unknown collider type '{type}'. Valid: box, sphere, capsule")
            };
        }

        /// <summary>
        /// Returns mesh bounds in local space.
        /// Priority: SkinnedMeshRenderer.localBounds > MeshFilter.sharedMesh.bounds > Renderer.bounds (world→local).
        /// </summary>
        internal static Bounds? GetLocalBounds(GameObject go)
        {
            var smr = go.GetComponent<SkinnedMeshRenderer>();
            if (smr != null) return smr.localBounds;

            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.bounds;

            var r = go.GetComponent<Renderer>();
            if (r == null) return null;

            // Renderer.bounds is world-space — convert to local
            var worldBounds = r.bounds;
            var localCenter = go.transform.InverseTransformPoint(worldBounds.center);
            var localSize = new Vector3(
                Mathf.Abs(go.transform.InverseTransformVector(worldBounds.size.x, 0, 0).magnitude),
                Mathf.Abs(go.transform.InverseTransformVector(0, worldBounds.size.y, 0).magnitude),
                Mathf.Abs(go.transform.InverseTransformVector(0, 0, worldBounds.size.z).magnitude));
            return new Bounds(localCenter, localSize);
        }

        static string FitBox(GameObject go, Bounds b)
        {
            var col = go.GetComponent<BoxCollider>();
            if (col == null) col = Undo.AddComponent<BoxCollider>(go);
            Undo.RecordObject(col, "MCP AutoFit BoxCollider");
            col.center = b.center;
            col.size = b.size;
            return $"BoxCollider fitted: center={V(b.center)}, size={V(b.size)}";
        }

        static string FitSphere(GameObject go, Bounds b)
        {
            var col = go.GetComponent<SphereCollider>();
            if (col == null) col = Undo.AddComponent<SphereCollider>(go);
            Undo.RecordObject(col, "MCP AutoFit SphereCollider");
            col.center = b.center;
            col.radius = b.extents.magnitude;
            return $"SphereCollider fitted: center={V(b.center)}, radius={col.radius}";
        }

        static string FitCapsule(GameObject go, Bounds b)
        {
            var col = go.GetComponent<CapsuleCollider>();
            if (col == null) col = Undo.AddComponent<CapsuleCollider>(go);
            Undo.RecordObject(col, "MCP AutoFit CapsuleCollider");
            col.center = b.center;

            var ext = b.extents;
            // direction: 0=X, 1=Y, 2=Z — pick the longest axis
            if (ext.y >= ext.x && ext.y >= ext.z)
            {
                col.direction = 1;
                col.height = ext.y * 2f;
                col.radius = new Vector2(ext.x, ext.z).magnitude;
            }
            else if (ext.x >= ext.y && ext.x >= ext.z)
            {
                col.direction = 0;
                col.height = ext.x * 2f;
                col.radius = new Vector2(ext.y, ext.z).magnitude;
            }
            else
            {
                col.direction = 2;
                col.height = ext.z * 2f;
                col.radius = new Vector2(ext.x, ext.y).magnitude;
            }
            return $"CapsuleCollider fitted: center={V(b.center)}, height={col.height}, radius={col.radius}, dir={col.direction}";
        }

        static string V(Vector3 v) => $"({v.x:F3}, {v.y:F3}, {v.z:F3})";
    }
}
