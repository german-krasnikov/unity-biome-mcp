using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    public static class SearchHelper
    {
        public static string Search(string query, string root = null, int limit = 50, string scene = null)
        {
            if (string.IsNullOrEmpty(query))
                throw new ArgumentException("query is required");

            var q = ParseQuery(query);
            var results = new List<GameObject>();
            if (!WalkScene(query, root, q, results, limit, scene, out var notFound, out int totalCount))
                return notFound;

            if (results.Count == 0)
                return BuildEmptyHint(query, root);

            int overflow = (limit > 0 && results.Count >= limit) ? totalCount - results.Count : 0;

            var sb = new StringBuilder();
            foreach (var go in results)
            {
                sb.Append(ComponentSerializer.GetPath(go)).Append(' ').Append(RefManager.Assign(go));
                var compNames = new List<string>();
                foreach (var c in go.GetComponents<Component>())
                    if (c != null && !(c is Transform)) compNames.Add(c.GetType().Name);
                if (compNames.Count > 0)
                    sb.Append(" [").Append(string.Join(",", compNames)).Append("]");
                if (!go.activeSelf) sb.Append(" !");
                sb.AppendLine();
            }
            if (overflow > 0)
                sb.AppendLine($"...+{overflow} more (limit={limit})");
            return sb.ToString().TrimEnd('\n');
        }

        // Returns false + sets notFound hint when root is not found; otherwise populates results.
        // totalCount = all matches found (including those beyond limit).
        private static bool WalkScene(string query, string root, SearchQuery q,
            List<GameObject> results, int limit, string scene,
            out string notFound, out int totalCount)
        {
            notFound = null;
            totalCount = 0;
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null)
            {
                CollectMatches(stage.prefabContentsRoot.transform, q, results, limit, ref totalCount);
            }
            else if (!string.IsNullOrEmpty(root) && root != "/")
            {
                var rootGO = ComponentSerializer.FindObject(root);
                if (rootGO == null) { notFound = BuildEmptyHint(query); return false; }
                CollectMatches(rootGO.transform, q, results, limit, ref totalCount);
            }
            else
            {
                var ctx = SceneContext.Current;
                var sceneList = string.IsNullOrEmpty(scene) ? ctx.Scenes : ctx.FilterByScene(scene);
                foreach (var (_, roots) in sceneList)
                    foreach (var r in roots)
                        CollectMatches(r.transform, q, results, limit, ref totalCount);
            }
            return true;
        }

        // Struct for parsed query
        private struct SearchQuery
        {
            public string Name;       // plain text → substring match (case-insensitive)
            public string Component;  // t:XXX
            public string Tag;        // tag=XXX
            public int? Layer;        // layer=N
            public bool? Active;      // active=true/false
        }

        // Parse "t:Light tag=Player active=false Main Camera" into SearchQuery
        private static SearchQuery ParseQuery(string query)
        {
            var q = new SearchQuery();
            var nameParts = new List<string>();

            foreach (var token in query.Split(' '))
            {
                if (string.IsNullOrEmpty(token)) continue;

                if (token.StartsWith("t:", StringComparison.OrdinalIgnoreCase))
                    q.Component = ComponentSerializer.StripNamespace(token.Substring(2));
                else if (token.StartsWith("tag=", StringComparison.OrdinalIgnoreCase))
                    q.Tag = token.Substring(4);
                else if (token.StartsWith("layer=", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(token.Substring(6), out var layer))
                        q.Layer = layer;
                }
                else if (token.StartsWith("active=", StringComparison.OrdinalIgnoreCase))
                    q.Active = token.Substring(7).ToLower() == "true";
                else
                    nameParts.Add(token);
            }

            if (nameParts.Count > 0)
                q.Name = string.Join(" ", nameParts);

            return q;
        }

        private static bool Matches(GameObject go, SearchQuery q)
        {
            // Name: case-insensitive substring
            if (!string.IsNullOrEmpty(q.Name) &&
                go.name.IndexOf(q.Name, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            // Component: check by type name
            if (!string.IsNullOrEmpty(q.Component))
            {
                bool found = false;
                foreach (var c in go.GetComponents<Component>())
                {
                    if (c != null && c.GetType().Name.Equals(q.Component, StringComparison.OrdinalIgnoreCase))
                    {
                        found = true;
                        break;
                    }
                }
                if (!found) return false;
            }

            // Tag
            if (!string.IsNullOrEmpty(q.Tag) && go.tag != q.Tag)
                return false;

            // Layer
            if (q.Layer.HasValue && go.layer != q.Layer.Value)
                return false;

            // Active
            if (q.Active.HasValue && go.activeSelf != q.Active.Value)
                return false;

            return true;
        }

        private static void CollectMatches(Transform t, SearchQuery q,
            List<GameObject> results, int limit, ref int totalCount)
        {
            if (Matches(t.gameObject, q))
            {
                totalCount++;
                if (limit <= 0 || results.Count < limit)
                    results.Add(t.gameObject);
            }

            foreach (Transform child in t)
                CollectMatches(child, q, results, limit, ref totalCount);
        }

        // ── search_context ────────────────────────────────────────────────────────
        // Delegate injected by SearchContextPlugin ([InitializeOnLoad] in Chat.CLI).
        // Decouples the assembly boundary: Editor doesn't reference Chat.CLI.
        public static Func<string, int, string, string> SearchContextProvider { get; internal set; }

        /// <summary>
        /// Returns tab-separated lines: type\tpath\tname.
        /// Delegate implementation provided by SearchContextPlugin (Chat.CLI assembly).
        /// </summary>
        public static string SearchContext(string query = "", int limit = 30, string types = null)
        {
            if (SearchContextProvider == null)
                throw new InvalidOperationException("SearchContextProvider not initialized (Chat.CLI not loaded)");
            return SearchContextProvider(query, limit, types);
        }

        private static string BuildEmptyHint(string query, string rootPath = null)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            string ctxName; int total; var tops = new List<string>();
            if (stage != null)
            {
                ctxName = stage.prefabContentsRoot.name;
                total = stage.prefabContentsRoot.GetComponentsInChildren<Transform>(true).Length;
                foreach (Transform c in stage.prefabContentsRoot.transform) tops.Add(c.name);
            }
            else if (!string.IsNullOrEmpty(rootPath) && rootPath != "/")
            {
                var rootGO = ComponentSerializer.FindObject(rootPath);
                ctxName = rootGO != null ? rootGO.name : rootPath;
                total = rootGO != null ? rootGO.GetComponentsInChildren<Transform>(true).Length : 0;
                if (rootGO != null) foreach (Transform c in rootGO.transform) tops.Add(c.name);
            }
            else
            {
                var scenes = HierarchySerializer.GetAllLoadedSceneRoots();
                ctxName = string.Join("+", scenes.ConvertAll(s => s.name));
                total = 0;
                foreach (var (_, roots) in scenes)
                    foreach (var r in roots)
                    {
                        total += r.GetComponentsInChildren<Transform>(true).Length;
                        tops.Add(r.name);
                    }
            }
            var topStr = tops.Count <= 8 ? string.Join(", ", tops)
                : string.Join(", ", tops.GetRange(0, 8)) + $", +{tops.Count - 8} more";
            var topLine = topStr.Length > 0 ? $"\ntop: {topStr}" : "";
            return $"no matches in '{ctxName}' ({total} objects){topLine}\nhint: t:Type | tag=X | layer=N | active=true | name-substring";
        }
    }
}
