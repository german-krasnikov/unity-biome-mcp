using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    public static class HierarchySerializer
    {
        public const int MAX_NODES = 3000;

        private static string _lastHierarchy;
        public static void ResetIncrementalCache() => _lastHierarchy = null;

        public static string SerializeIncremental(int depth, string root, string filter, bool components,
                                                   string scene = null)
        {
            var current = Serialize(depth, root, filter, components, scene);
            if (_lastHierarchy != null && current == _lastHierarchy) return "NO_CHANGE";
            _lastHierarchy = current;
            return current;
        }

        public static string Serialize(int depth = 99, string root = null, string filter = null,
                                        bool components = false, string scene = null)
        {
            var sb = new StringBuilder();
            int nodeCount = 0;

            if (string.IsNullOrEmpty(root))
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                {
                    SerializeObject(sb, stage.prefabContentsRoot, depth, 0, new List<bool>(), true, filter, ref nodeCount, components);
                }
                else
                {
                    var ctx = SceneContext.Current;
                    var scenes = string.IsNullOrEmpty(scene) ? ctx.Scenes : ctx.FilterByScene(scene);
                    bool multi = scenes.Count > 1;
                    foreach (var (name, roots) in scenes)
                    {
                        if (nodeCount >= MAX_NODES) break;
                        if (multi)
                        {
                            int headerPos = sb.Length;
                            sb.AppendLine($"[{name}]");
                            int beforeCount = nodeCount;
                            for (int i = 0; i < roots.Length && nodeCount < MAX_NODES; i++)
                                SerializeObject(sb, roots[i], depth, 0, new List<bool>(), i == roots.Length - 1, filter, ref nodeCount, components);
                            if (nodeCount == beforeCount) sb.Length = headerPos; // remove phantom header
                        }
                        else
                        {
                            for (int i = 0; i < roots.Length && nodeCount < MAX_NODES; i++)
                                SerializeObject(sb, roots[i], depth, 0, new List<bool>(), i == roots.Length - 1, filter, ref nodeCount, components);
                        }
                    }
                }
            }
            else
            {
                // C3: qualify root with scene param so multi-scene root lookup is scene-scoped
                var qualifiedRoot = (!string.IsNullOrEmpty(scene) && !root.Contains(":/"))
                    ? $"{scene}:/{root.TrimStart('/')}"
                    : root;
                var roots = GetSubtreeRoots(qualifiedRoot);
                for (int i = 0; i < roots.Length && nodeCount < MAX_NODES; i++)
                    SerializeObject(sb, roots[i], depth, 0, new List<bool>(), i == roots.Length - 1, filter, ref nodeCount, components);
            }

            if (nodeCount >= MAX_NODES)
                sb.AppendLine($"... truncated at {MAX_NODES} nodes. Use filter/root/depth to narrow.");

            return sb.ToString();
        }

        public static string SerializeSummary(string root = null)
        {
            var sb = new StringBuilder();
            if (string.IsNullOrEmpty(root))
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage != null)
                {
                    var r = stage.prefabContentsRoot;
                    int desc = CountDescendants(r.transform);
                    var inactive = r.activeSelf ? "" : " !";
                    sb.AppendLine(desc > 0 ? $"{r.name} ({desc + 1} nodes)" : $"{r.name} (1 nodes)");
                    sb.AppendLine($"  {r.name}{inactive}");
                    return sb.ToString();
                }
                var ctx = SceneContext.Current;
                var scenes = ctx.Scenes;
                bool multi = ctx.IsMulti;
                foreach (var (name, roots) in scenes)
                {
                    int total = 0;
                    foreach (var r in roots) total += 1 + CountDescendants(r.transform);
                    if (multi) sb.AppendLine($"[{name}] ({total} nodes)");
                    else sb.AppendLine($"{name} ({total} nodes)");
                    foreach (var r in roots)
                    {
                        int desc = CountDescendants(r.transform);
                        var inactive = r.activeSelf ? "" : " !";
                        sb.AppendLine(desc > 0 ? $"  {r.name} ({desc} children){inactive}" : $"  {r.name}{inactive}");
                    }
                }
            }
            else
            {
                var roots = GetSubtreeRoots(root);
                if (roots.Length == 0) { sb.AppendLine($"Not found: {root}"); return sb.ToString(); }
                var r = roots[0];
                int desc = CountDescendants(r.transform);
                var inactive = r.activeSelf ? "" : " !";
                sb.AppendLine(desc > 0 ? $"{r.name} ({desc} children){inactive}" : $"{r.name}{inactive}");
            }
            return sb.ToString();
        }

        public static string SerializeSubtree(GameObject root, int depth = 1)
        {
            if (root == null) return "";
            var sb = new StringBuilder();
            int nodeCount = 0;
            SerializeObject(sb, root, depth, 0, new List<bool>(), true, null, ref nodeCount);
            return sb.ToString().TrimEnd();
        }

        internal static List<(string name, GameObject[] roots)> GetAllLoadedSceneRoots()
        {
            var raw = new List<(Scene scene, GameObject[] roots)>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                // Exclude DontDestroyOnLoad virtual scene (runtime only, has no path)
                if (!s.isLoaded || s.name == "DontDestroyOnLoad") continue;
                raw.Add((s, s.GetRootGameObjects()));
            }
            if (raw.Count == 0)
            {
                var active = SceneManager.GetActiveScene();
                return new List<(string, GameObject[])> { (active.name, active.GetRootGameObjects()) };
            }
            // Detect duplicate names — disambiguate with directory path
            var nameCount = new Dictionary<string, int>();
            foreach (var (s, _) in raw)
            {
                nameCount.TryGetValue(s.name, out var c);
                nameCount[s.name] = c + 1;
            }
            var result = new List<(string, GameObject[])>();
            foreach (var (s, roots) in raw)
            {
                string label;
                if (nameCount[s.name] > 1)
                    label = string.IsNullOrEmpty(s.path)
                        ? $"{s.name} (unsaved)"
                        : $"{s.name} ({System.IO.Path.GetDirectoryName(s.path)})";
                else
                    label = s.name;
                result.Add((label, roots));
            }
            return result;
        }

        private static GameObject[] GetSubtreeRoots(string rootPath)
        {
            var root = ComponentSerializer.FindObject(rootPath);
            return root != null ? new[] { root } : new GameObject[0];
        }

        internal static void SerializeObject(
            StringBuilder sb, GameObject go, int maxDepth, int currentDepth,
            List<bool> parentIsLast, bool isLast, string filter, ref int nodeCount, bool components = false)
        {
            if (nodeCount >= MAX_NODES) return;
            if (!string.IsNullOrEmpty(filter) && !MatchesFilter(go, filter, maxDepth, currentDepth)) return;
            nodeCount++;
            AppendIndent(sb, parentIsLast, isLast);
            sb.Append(go.name);
            if (components)
            {
                sb.Append(" [");
                bool first = true;
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c == null || c is Transform) continue;
                    if (!first) sb.Append(',');
                    sb.Append(c.GetType().Name);
                    first = false;
                }
                sb.Append(']');
            }
            sb.Append(' ').Append(TransientObjectId.GetHexRef(go));
            if (!go.activeSelf) sb.Append(" !");
            if (currentDepth >= maxDepth && go.transform.childCount > 0)
                sb.Append(" +").Append(CountDescendants(go.transform));
            sb.AppendLine();
            if (currentDepth >= maxDepth) return;
            var transform = go.transform;
            for (int i = 0; i < transform.childCount; i++)
            {
                if (nodeCount >= MAX_NODES)
                {
                    AppendIndent(sb, parentIsLast, true);
                    sb.AppendLine($"... +{transform.childCount - i} siblings");
                    break;
                }
                parentIsLast.Add(isLast);
                SerializeObject(sb, transform.GetChild(i).gameObject, maxDepth, currentDepth + 1, parentIsLast, i == transform.childCount - 1, filter, ref nodeCount, components);
                parentIsLast.RemoveAt(parentIsLast.Count - 1);
            }
        }

        private static void AppendIndent(StringBuilder sb, List<bool> parentIsLast, bool isLast)
        {
            for (int i = 0; i < parentIsLast.Count; i++)
                sb.Append(parentIsLast[i] ? "   " : "│  ");
            if (parentIsLast.Count > 0)
                sb.Append(isLast ? "└─ " : "├─ ");
        }

        private static bool MatchesFilter(GameObject go, string filter, int maxDepth, int currentDepth)
        {
            if (go.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (currentDepth < maxDepth)
                for (int i = 0; i < go.transform.childCount; i++)
                    if (MatchesFilter(go.transform.GetChild(i).gameObject, filter, maxDepth, currentDepth + 1))
                        return true;
            return false;
        }

        private static int CountDescendants(Transform t, int budget = MAX_NODES)
        {
            int count = t.childCount;
            for (int i = 0; i < t.childCount && count < budget; i++)
                count += CountDescendants(t.GetChild(i), budget - count);
            return count;
        }
    }
}
