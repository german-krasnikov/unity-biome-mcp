using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor
{
    internal static class SceneRefResolver
    {
        internal struct RefResult
        {
            public string Input;
            public string Status;   // "OK", "MISS", "AMB"
            public string Path;
            public string Reason;
            public bool Active;
            public int InstanceId;
            public string SceneName;
            public List<(string field, string status)> Fields;
        }

        internal static List<RefResult> ResolveMany(string refs, string fields)
        {
            var tokens = refs.Split(',');
            var fieldArr = string.IsNullOrEmpty(fields)
                ? System.Array.Empty<string>()
                : fields.Split(',');
            var results = new List<RefResult>(tokens.Length);
            foreach (var t in tokens)
                results.Add(ResolveOne(t.Trim(), fieldArr));
            return results;
        }

        internal static RefResult ResolveOne(string token, string[] fields)
        {
            var r = new RefResult { Input = token };

            // Expand $alias → may produce "path|comp|field" or just a path
            string expanded = token.StartsWith("$")
                ? AliasExpander.ExpandText(token)
                : token;

            if (expanded == token && token.StartsWith("$"))
            {
                r.Status = "MISS";
                r.Reason = "unknown alias";
                return r;
            }

            // Parse pipe components: path|comp|field
            var pipes = expanded.Split('|');
            string pathPart = pipes[0].Trim();
            string compPart = pipes.Length >= 2 ? pipes[1].Trim() : null;

            if (pathPart.StartsWith("t:", System.StringComparison.OrdinalIgnoreCase))
                return ResolveByType(r, pathPart.Substring(2), compPart, fields);

            var go = ComponentSerializer.FindObject(pathPart);
            if (go == null)
            {
                r.Status = "MISS";
                r.Reason = "not found";
                return r;
            }

            FillOk(ref r, go);
            if (fields.Length > 0)
                r.Fields = CheckFields(go, compPart, fields);
            return r;
        }

        private static RefResult ResolveByType(RefResult r, string typeName, string compHint, string[] fields)
        {
            var shortName = ComponentSerializer.StripNamespace(typeName);
            var matches = new List<GameObject>();

            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                var scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    CollectByType(root.transform, shortName, matches);
            }

            if (matches.Count == 0)
            {
                r.Status = "MISS";
                r.Reason = "no objects with t:" + typeName;
                return r;
            }

            if (matches.Count > 1)
            {
                r.Status = "AMB";
                r.Reason = $"{matches.Count} matches";
                var sb = new StringBuilder();
                int cap = System.Math.Min(matches.Count, 5);
                for (int i = 0; i < cap; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(ComponentSerializer.GetPath(matches[i]));
                }
                if (matches.Count > 5) sb.Append($", +{matches.Count - 5} more");
                r.Path = sb.ToString();
                return r;
            }

            var go = matches[0];
            FillOk(ref r, go);
            if (fields.Length > 0)
                r.Fields = CheckFields(go, compHint ?? typeName, fields);
            return r;
        }

        private static void CollectByType(Transform t, string typeName, List<GameObject> results)
        {
            foreach (var c in t.gameObject.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c.GetType().Name.Equals(typeName, System.StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(t.gameObject);
                    break;
                }
            }
            foreach (Transform child in t)
                CollectByType(child, typeName, results);
        }

        private static List<(string, string)> CheckFields(GameObject go, string compName, string[] fields)
        {
            var list = new List<(string, string)>(fields.Length);
            var comp = string.IsNullOrEmpty(compName)
                ? null
                : ComponentSerializer.FindComponent(go, compName);

            foreach (var field in fields)
            {
                var f = field.Trim();
                if (string.IsNullOrEmpty(f)) continue;
                string status = "MISS";
                if (comp != null)
                {
                    var type = comp.GetType();
                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                    if (type.GetField(f, flags) != null || type.GetProperty(f, flags) != null)
                        status = "OK";
                }
                list.Add((f, status));
            }
            return list;
        }

        private static void FillOk(ref RefResult r, GameObject go)
        {
            r.Status = "OK";
            r.Path = ComponentSerializer.GetPath(go);
            r.Active = go.activeSelf;
            r.InstanceId = go.GetInstanceID();
            r.SceneName = go.scene.name;
        }

        internal static string FormatResults(List<RefResult> results)
        {
            var sb = new StringBuilder();
            foreach (var r in results)
            {
                sb.Append(r.Status).Append('\t').Append(r.Input);
                if (!string.IsNullOrEmpty(r.Path)) sb.Append('\t').Append(r.Path);
                if (r.Status == "OK")
                {
                    sb.Append('\t').Append(r.Active ? "active" : "inactive");
                    sb.Append("\tiid=").Append(r.InstanceId);
                    if (!string.IsNullOrEmpty(r.SceneName)) sb.Append("\tscene=").Append(r.SceneName);
                }
                if (!string.IsNullOrEmpty(r.Reason)) sb.Append('\t').Append(r.Reason);
                if (r.Fields != null)
                    foreach (var (field, status) in r.Fields)
                        sb.Append('\t').Append(field).Append('=').Append(status);
                sb.AppendLine();
            }
            return sb.ToString().TrimEnd();
        }
    }
}
