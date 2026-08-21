using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace UnityMCP.Editor
{
    public static partial class ComponentSerializer
    {
        public static GameObject FindObjectOrThrow(string path)
        {
            var go = FindObject(path);
            if (go == null) throw new System.ArgumentException(ErrorHelper.ObjectNotFound(path));
            return go;
        }

        public static GameObject FindObject(string path, bool strict = false)
        {
            if (string.IsNullOrEmpty(path)) return null;

            if (RefManager.IsRef(path))
            {
                var resolved = RefManager.Resolve(path);
                if (resolved != null) return resolved;
                throw new StaleCacheException($"Stale ref: {path}. Call get_hierarchy to refresh.");
            }

            // New: $HEX format (e.g. $3E8) — tightened IsRef already rejects these
            if (path.StartsWith("$"))
            {
                if (TransientObjectId.TryParse(path, out var hexId))
                {
                    var byHexId = FindObjectById(hexId.WireValue);
                    if (byHexId != null) return byHexId;
                }
                throw new System.ArgumentException($"Entity ID {path} not found");
            }

            // Legacy: #decimal format (backward compat)
            if (path.StartsWith("#") && TransientObjectId.TryParse(path, out var objectId))
            {
                var byId = FindObjectById(objectId.WireValue);
                if (byId != null) return byId;
                throw new System.ArgumentException($"Entity ID {path} not found");
            }

            // Scene-qualified path: "SceneName:/RootObj/Child"
            var parsed = ScenePathParser.Parse(path);
            if (parsed.SceneName != null)
            {
                var sceneParts = SplitPathSegments(parsed.LocalPath);
                var sceneRoot = FindRootInScene(parsed.SceneName, sceneParts[0]);
                if (sceneRoot == null) return null;  // strict scene boundary — no fuzzy cross-scene
                GameObject current = sceneRoot;
                for (int i = 1; i < sceneParts.Length; i++)
                {
                    Transform child = FindChildByName(current.transform, sceneParts[i]);
                    if (child == null) return null;  // strict scene boundary — no fuzzy cross-scene
                    current = child.gameObject;
                }
                return current;
            }

            if (path.StartsWith("/")) path = path.Substring(1);
            if (string.IsNullOrEmpty(path)) return null;

            var parts = SplitPathSegments(path);
            var root = FindRoot(parts[0]);
            if (root == null)
            {
                // Fallback: try whole path as root name (handles slashes in names)
                root = FindRoot(path);
                if (root != null) return root;
                return strict ? null : TryFuzzyFind(path, parts);
            }

            GameObject cur = root;
            for (int i = 1; i < parts.Length; i++)
            {
                Transform child = FindChildByName(cur.transform, parts[i]);
                if (child == null) return strict ? null : TryFuzzyFind(path, parts);
                cur = child.gameObject;
            }
            return cur;
        }

        // Bracket-aware + backslash-aware path split.
        // "a/[b/c]/d"   → ["a","[b/c]","d"]   (bracket protection for embedded '/')
        // "Day\/Night"  → ["Day/Night"]         (\/ = literal '/' in segment name)
        // "path\\thing" → ["path\thing"]         (\\ = literal '\' in segment name)
        // internal: also used by SceneObjectFinder (C4 fix).
        internal static string[] SplitPathSegments(string path)
        {
            var parts = new List<string>();
            var seg = new System.Text.StringBuilder();
            int depth = 0;
            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];
                // Backslash escape: \/ = literal '/', \\ = literal '\'
                if (c == '\\' && i + 1 < path.Length)
                {
                    char next = path[i + 1];
                    if (next == '/' || next == '\\') { seg.Append(next); i++; continue; }
                }
                if      (c == '[') { depth++; seg.Append(c); }
                else if (c == ']') { depth--; seg.Append(c); }
                else if (c == '/' && depth == 0) { parts.Add(seg.ToString()); seg.Clear(); }
                else seg.Append(c);
            }
            parts.Add(seg.ToString());
            return parts.ToArray();
        }

        // Escape '/' and '\' in a GO name for use as a path segment.
        // '/' inside [...] brackets is NOT escaped (brackets already protect it).
        // Fast-path when no special chars (99%+ of names).
        private static string EscapeSegment(string name)
        {
            if (!name.Contains('\\') && !name.Contains('/')) return name;
            var sb = new System.Text.StringBuilder(name.Length + 4);
            int depth = 0;
            foreach (char c in name)
            {
                if      (c == '[') { depth++; sb.Append(c); }
                else if (c == ']') { depth--; sb.Append(c); }
                else if (c == '\\') { sb.Append("\\\\"); }          // always escape backslash
                else if (c == '/' && depth == 0) { sb.Append("\\/"); }  // escape '/' outside brackets
                else sb.Append(c);
            }
            return sb.ToString();
        }

        // Transform.Find() interprets brackets as selectors — manual traversal avoids that.
        private static Transform FindChildByName(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var c = parent.GetChild(i);
                if (c.name == name) return c;
            }
            return null;
        }

        internal static GameObject FindObjectById(string objectId)
        {
            var obj = TransientObjectId.Resolve(objectId);
            return obj as GameObject;
        }

        internal static GameObject FindObjectById(int legacyId)
            => FindObjectById(legacyId.ToString(System.Globalization.CultureInfo.InvariantCulture));

        private static GameObject FindRoot(string name)
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.prefabContentsRoot.name == name)
                return stage.prefabContentsRoot;

            GameObject found = null;
            string foundScene = null;
            var ambiguous = new List<string>();
            var ambiguousScenes = new System.Collections.Generic.HashSet<string>();

            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == name)
                    {
                        if (found == null)
                        {
                            found = root;
                            foundScene = scene.name;
                        }
                        else
                        {
                            if (ambiguous.Count == 0)
                            {
                                ambiguous.Add($"{foundScene}:/{name} ({RefManager.AssignAny(found)})");
                                ambiguousScenes.Add(foundScene);
                            }
                            ambiguous.Add($"{scene.name}:/{name} ({RefManager.AssignAny(root)})");
                            ambiguousScenes.Add(scene.name);
                        }
                    }
                }
            }
            if (ambiguous.Count > 0)
            {
                bool multiScene = ambiguousScenes.Count > 1;
                string desc = multiScene
                    ? $"found in {ambiguousScenes.Count} scenes"
                    : $"matches {ambiguous.Count} objects";
                throw new System.ArgumentException(
                    $"Ambiguous: '{name}' {desc}. Use entity ID: " + string.Join(" or ", ambiguous));
            }
            return found;
        }

        private static GameObject FindRootInScene(string sceneName, string rootName)
        {
            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                if (scene.name != sceneName) continue;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == rootName) return root;
            }
            return null;
        }

        private static GameObject TryFuzzyFind(string path, string[] parts)
        {
            var lastName = parts[parts.Length - 1];
            var candidates = new List<GameObject>();

            for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (var root in scene.GetRootGameObjects())
                    FindByName(root, lastName, candidates, 5);
            }

            if (parts.Length > 1)
            {
                // GetPath() escapes segment names; re-escape parts so suffix matches GetPath output
                var suffix = "/" + string.Join("/", System.Array.ConvertAll(parts, EscapeSegment));
                candidates.RemoveAll(c => !GetPath(c).EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase));
            }

            if (candidates.Count == 1)
                return candidates[0];
            if (candidates.Count > 1)
                throw new System.ArgumentException(
                    $"Ambiguous: '{path}'. Did you mean: " +
                    string.Join(", ", candidates.ConvertAll(c => GetPath(c))));
            return null;
        }

        private static void FindByName(GameObject root, string name, List<GameObject> results, int max)
        {
            var queue = new Queue<GameObject>();
            queue.Enqueue(root);
            while (queue.Count > 0 && results.Count < max)
            {
                var go = queue.Dequeue();
                if (go.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    results.Add(go);
                for (int i = 0; i < go.transform.childCount; i++)
                    queue.Enqueue(go.transform.GetChild(i).gameObject);
            }
        }

        /// <summary>Strip namespace prefix: "UnityEngine.UI.Button" → "Button"</summary>
        internal static string StripNamespace(string typeName)
        {
            if (typeName == null) return null;
            var dot = typeName.LastIndexOf('.');
            return dot >= 0 ? typeName.Substring(dot + 1) : typeName;
        }

        /// <summary>Resolves "path::TypeName &amp;ref" (new), "path::TypeName $HEX", or "path::TypeName #id" (legacy) wire ref to Component.</summary>
        internal static Component FindComponentByRef(string wireRef)
        {
            if (string.IsNullOrEmpty(wireRef)) return null;
            // New: &ref format — last token starting with & and valid base62
            int lastSpace = wireRef.LastIndexOf(' ');
            if (lastSpace >= 0)
            {
                var tail = wireRef.Substring(lastSpace + 1);
                if (RefManager.IsRef(tail))
                    return RefManager.ResolveAny(tail) as Component;
            }
            // Legacy: #decimal and $HEX
            var hashIdx = wireRef.LastIndexOf('#');
            var dollarIdx = wireRef.LastIndexOf('$');
            var sepIdx = System.Math.Max(hashIdx, dollarIdx);
            if (sepIdx < 0) return null;
            var idStr = wireRef.Substring(sepIdx).Trim(); // includes # or $ prefix
            if (!TransientObjectId.TryParse(idStr, out var objectId)) return null;
            return objectId.Resolve() as Component;
        }

        internal static Component FindComponent(GameObject go, string typeName)
        {
            var shortName = InputNormalizer.NormalizeComponent(StripNamespace(typeName), go);
            foreach (var comp in go.GetComponents<Component>())
            {
                if (comp == null) continue;
                var t = comp.GetType();
                if (t.Name == shortName || t.FullName == typeName)
                    return comp;
                var bt = t.Name.IndexOf('`');
                if (bt > 0 && t.Name.Substring(0, bt).Equals(shortName, System.StringComparison.OrdinalIgnoreCase))
                    return comp;
            }
            if (shortName == "Transform")
            {
                var rt = go.GetComponent<RectTransform>();
                if (rt != null) return rt;
            }
            return null;
        }

        public static string GetPath(GameObject go)
        {
            var path = EscapeSegment(go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                path = EscapeSegment(t.name) + "/" + path;
                t = t.parent;
            }
            return SceneContext.Current.QualifyPath(go, path);
        }
    }

    /// <summary>Parses "SceneName:/local/path" → (sceneName, localPath). Shared between finders.</summary>
    internal readonly struct ScenePathParser
    {
        public readonly string SceneName;   // null if not scene-qualified
        public readonly string LocalPath;   // path without scene prefix, leading '/' stripped

        private ScenePathParser(string scene, string local) { SceneName = scene; LocalPath = local; }

        internal static ScenePathParser Parse(string path)
        {
            if (string.IsNullOrEmpty(path)) return new ScenePathParser(null, path);
            int sep = path.IndexOf(":/", System.StringComparison.Ordinal);
            // Reject if no separator, or the "scene" part starts with '[' (bracket object name),
            // or contains '/' (already a path segment, not a scene name).
            if (sep <= 0 || path[0] == '[' || path.Substring(0, sep).Contains('/'))
                return new ScenePathParser(null, path);
            var scene = path.Substring(0, sep);
            var local = path.Substring(sep + 2);  // skip ":/"
            if (local.StartsWith("/")) local = local.Substring(1);
            return new ScenePathParser(scene, local);
        }
    }
}
