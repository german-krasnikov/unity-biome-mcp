using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class FingerprintHelper
    {
        public static string Fingerprint(string path, int depth)
        {
            var go = !string.IsNullOrEmpty(path) ? ComponentSerializer.FindObject(path) : null;
            GameObject[] roots;
            if (go != null)
                roots = new[] { go };
            else
            {
                var allRoots = new System.Collections.Generic.List<GameObject>();
                foreach (var (_, sceneRoots) in HierarchySerializer.GetAllLoadedSceneRoots())
                    allRoots.AddRange(sceneRoots);
                roots = allRoots.ToArray();
            }

            var sb = new StringBuilder();
            foreach (var root in roots)
                AppendFingerprint(sb, root.transform, depth, 0);

            return $"fp:{Fnv1a(sb.ToString()):X8}";
        }

        private static uint Fnv1a(string s)
        {
            uint h = 2166136261u;
            foreach (char c in s) { h ^= c; h *= 16777619u; }
            return h;
        }

        // Volatile Unity bookkeeping properties that must not influence the hash.
        private static readonly System.Collections.Generic.HashSet<string> _volatileProps =
            new System.Collections.Generic.HashSet<string>
            {
                "m_ObjectHideFlags", "m_CorrespondingSourceObject",
                "m_PrefabInstance", "m_PrefabAsset"
            };

        // P-108: stable serialization for ObjectReference — uses GUID instead of volatile instanceID.
        private static string StablePropertyValueString(SerializedProperty prop)
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference)
                return ComponentSerializer.GetPropertyValueString(prop);

            var obj = prop.objectReferenceValue;
            if (obj == null) return "null";

            // Asset objects: use stable GUID
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(obj, out var guid, out long _))
                return guid;

            // Scene objects: type + name is stable within a session (no instance ID)
            return obj.GetType().Name + ":" + obj.name;
        }

        private static void AppendFingerprint(StringBuilder sb, Transform t, int maxDepth, int depth)
        {
            sb.Append(t.gameObject.name).Append(':');
            foreach (var comp in t.gameObject.GetComponents<Component>())
            {
                if (comp == null) continue;
                var so = new SerializedObject(comp);
                var prop = so.GetIterator();
                while (prop.NextVisible(true))
                {
                    if (_volatileProps.Contains(prop.name)) continue;
                    sb.Append(prop.name).Append('=')
                      .Append(StablePropertyValueString(prop)).Append(';');
                }
            }
            if (depth < maxDepth)
                for (int i = 0; i < t.childCount; i++)
                    AppendFingerprint(sb, t.GetChild(i), maxDepth, depth + 1);
        }
    }
}
