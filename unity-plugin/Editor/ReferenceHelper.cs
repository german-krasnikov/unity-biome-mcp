using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class ReferenceHelper
    {
        internal const int MAX_ARRAY = 100;
        private const int MAX_SCAN = 5000;

        private struct RefEntry
        {
            public string ComponentType;
            public string PropertyPath;
            public string ReferencedPath;
            public string Relation; // "child" | "external" | "asset" | "null"
            public string ReferencedId;
            public GameObject ReferencedObject;
        }

        // --- Public API ---

        public static string GetReferences(string path, bool includeChildren, int depth)
        {
            var go = ComponentSerializer.FindObject(path);
            if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var sb = new StringBuilder();
            var visited = new HashSet<GameObject>();
            AppendReferences(sb, go, path, depth, visited);

            if (includeChildren)
            {
                foreach (Transform child in go.transform)
                    AppendReferencesRecursive(sb, child.gameObject, path, depth, visited);
            }

            return sb.Length == 0 ? "no references" : sb.ToString().TrimEnd('\n');
        }

        public static string FindReferencesTo(string path)
        {
            var targetGo = ComponentSerializer.FindObject(path);
            if (targetGo == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));

            var sb = new StringBuilder();
            int count = 0;
            int scanned = 0;

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded || scene.name == "DontDestroyOnLoad") continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    ScanForReferencesTo(root.transform, targetGo, sb, ref count, ref scanned);
                    if (scanned >= MAX_SCAN)
                    {
                        sb.AppendLine($"(scan limit {MAX_SCAN} reached)");
                        break;
                    }
                }
                if (scanned >= MAX_SCAN) break;
            }

            sb.Append("found: ").Append(count);
            return sb.ToString();
        }

        // --- Internal helpers ---

        private static void AppendReferences(StringBuilder sb, GameObject go, string rootPath, int depth, HashSet<GameObject> visited)
        {
            if (!visited.Add(go)) return;

            var goPath = ComponentSerializer.GetPath(go);
            var refs = CollectRefs(go);

            if (refs.Count == 0) return;

            string lastComp = null;
            foreach (var r in refs)
            {
                if (r.ComponentType != lastComp)
                {
                    sb.Append(goPath).Append(" [").Append(r.ComponentType).AppendLine("]");
                    lastComp = r.ComponentType;
                }
                sb.Append("  ").Append(r.PropertyPath).Append(": ");
                if (r.Relation == "null")
                    sb.AppendLine("null");
                else
                    sb.Append(r.ReferencedPath).Append(" #").Append(r.ReferencedId).Append(' ').AppendLine(r.Relation);
            }

            if (depth > 1)
            {
                foreach (var r in refs)
                {
                    if (r.ReferencedObject != null && r.Relation != "asset" && r.Relation != "null")
                    {
                        sb.Append("-- ").Append(r.ReferencedPath).AppendLine(" --");
                        AppendReferences(sb, r.ReferencedObject, rootPath, depth - 1, visited);
                    }
                }
            }
        }

        private static void AppendReferencesRecursive(StringBuilder sb, GameObject go, string rootPath, int depth, HashSet<GameObject> visited)
        {
            AppendReferences(sb, go, rootPath, depth, visited);
            foreach (Transform child in go.transform)
                AppendReferencesRecursive(sb, child.gameObject, rootPath, depth, visited);
        }

        private static List<RefEntry> CollectRefs(GameObject go)
        {
            var refs = new List<RefEntry>();
            var goPath = ComponentSerializer.GetPath(go);

            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null || comp is Transform) continue;
                WalkProperties(new SerializedObject(comp), comp.GetType().Name, goPath, refs);
            }
            return refs;
        }

        internal static void WalkObjectRefs(SerializedObject so, Action<SerializedProperty, string> onRef,
            bool skipBuiltins = true)
        {
            // Traverse all properties depth-first via enterChildren control:
            //   - Arrays: handle elements explicitly with friendly "name[i]" labels,
            //             then skip into children so iterator doesn't double-visit them.
            //   - Generic (non-array struct): enter children to find nested refs (G18 fix).
            //   - ObjectReference: call onRef, no children to enter.
            //   - Primitives: skip children.
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.Next(enterChildren))
            {
                enterChildren = false; // default: don't enter unless we decide to below
                if (skipBuiltins && (prop.name == "m_Script" || prop.name == "m_GameObject"))
                    continue;

                if (prop.isArray && prop.propertyType == SerializedPropertyType.Generic)
                {
                    // Explicit array iteration preserves friendly "fieldName[i]" labels
                    int cap = Math.Min(prop.arraySize, MAX_ARRAY);
                    for (int i = 0; i < cap; i++)
                    {
                        var elem = prop.GetArrayElementAtIndex(i);
                        if (elem.propertyType == SerializedPropertyType.ObjectReference)
                            onRef(elem, $"{prop.name}[{i}]");
                    }
                    // Don't enter: iterator would visit array children as "data[i]" — already handled
                }
                else if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    onRef(prop, prop.propertyPath);
                }
                else if (prop.propertyType == SerializedPropertyType.Generic)
                {
                    // Non-array Generic = nested struct — enter to find refs inside (G18 fix)
                    enterChildren = true;
                }
            }
        }

        private static void WalkProperties(SerializedObject so, string compType, string goPath, List<RefEntry> refs)
        {
            WalkObjectRefs(so, (prop, label) => AddRefEntry(refs, compType, label, prop, goPath));
        }

        private static void AddRefEntry(List<RefEntry> refs, string compType, string propPath, SerializedProperty prop, string goPath)
        {
            var entry = new RefEntry { ComponentType = compType, PropertyPath = propPath };

            if (prop.objectReferenceValue == null)
            {
                entry.Relation = "null";
                refs.Add(entry);
                return;
            }

            GameObject refGo = null;
            if (prop.objectReferenceValue is GameObject g) refGo = g;
            else if (prop.objectReferenceValue is Component c) refGo = c.gameObject;

            if (refGo != null)
            {
                entry.ReferencedPath = ComponentSerializer.GetPath(refGo);
                entry.ReferencedId = TransientObjectId.GetWireValue(refGo);
                entry.ReferencedObject = refGo;
                entry.Relation = ClassifyRef(goPath, refGo);
            }
            else
            {
                entry.ReferencedPath = prop.objectReferenceValue.name;
                entry.ReferencedId = TransientObjectId.GetWireValue(prop.objectReferenceValue);
                entry.Relation = "asset";
            }

            refs.Add(entry);
        }

        internal static string ClassifyRef(string ownerPath, GameObject referenced)
        {
            var refPath = ComponentSerializer.GetPath(referenced);
            if (refPath == ownerPath) return "self";
            if (refPath.StartsWith(ownerPath + "/")) return "child";
            if (ownerPath.StartsWith(refPath + "/")) return "parent";

            // Same root object = sibling; skip scene-qualifier prefix "SceneName:/" first
            var ownerSearchFrom = SkipScenePrefix(ownerPath);
            var refSearchFrom   = SkipScenePrefix(refPath);
            var ownerRoot = ownerPath.IndexOf('/', ownerSearchFrom);
            var refRoot   = refPath.IndexOf('/', refSearchFrom);
            var ownerRootPart = ownerRoot > 0 ? ownerPath.Substring(0, ownerRoot) : ownerPath;
            var refRootPart   = refRoot   > 0 ? refPath.Substring(0, refRoot)     : refPath;
            if (ownerRootPart == refRootPart) return "sibling";

            return "external";
        }

        /// <summary>Returns the index after "SceneName:/" if present, otherwise 1.</summary>
        private static int SkipScenePrefix(string path)
        {
            var sep = path.IndexOf(":/", StringComparison.Ordinal);
            return sep >= 0 ? sep + 2 : 1;
        }

        private static void ScanForReferencesTo(Transform t, GameObject targetGo, StringBuilder sb, ref int count, ref int scanned)
        {
            if (scanned >= MAX_SCAN) return;
            scanned++;

            int localCount = count;
            var go = t.gameObject;
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null || comp is Transform) continue;
                var so = new SerializedObject(comp);
                WalkObjectRefs(so, (p, label) =>
                {
                    if (MatchesTarget(p, targetGo))
                    {
                        sb.Append(ComponentSerializer.GetPath(go))
                          .Append(" [").Append(comp.GetType().Name).Append("].").AppendLine(label);
                        localCount++;
                    }
                }, skipBuiltins: false);
            }
            count = localCount;

            foreach (Transform child in t)
                ScanForReferencesTo(child, targetGo, sb, ref count, ref scanned);
        }

        private static bool MatchesTarget(SerializedProperty prop, GameObject targetGo)
        {
            if (prop.propertyType != SerializedPropertyType.ObjectReference || prop.objectReferenceValue == null)
                return false;
            if (prop.objectReferenceValue is GameObject refGo) return refGo == targetGo;
            if (prop.objectReferenceValue is Component refComp) return refComp.gameObject == targetGo;
            return false;
        }

    }
}
