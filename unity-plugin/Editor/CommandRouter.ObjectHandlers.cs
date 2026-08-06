using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Object/read command handlers (split from CommandRouter.cs for <200-line focus).
    public static partial class CommandRouter
    {
        private static string ExecInspect(string args)
        {
            var pathsStr = JsonHelper.ExtractString(args, "paths");
            var findType = JsonHelper.ExtractString(args, "find_type");
            if (string.IsNullOrEmpty(pathsStr) && !string.IsNullOrEmpty(findType))
            {
                var found = ObjectManager.FindObjects(null, null, null, findType);
                if (string.IsNullOrEmpty(found)) return "none";
                if (found.StartsWith("err:") || found == "none") return found;
                pathsStr = string.Join(",", found.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            if (string.IsNullOrEmpty(pathsStr))
                throw new ArgumentException("paths or find_type is required");

            var componentsFilter = JsonHelper.ExtractString(args, "components")
                                ?? JsonHelper.ExtractString(args, "type");
            HashSet<string> filterSet = null;
            if (!string.IsNullOrEmpty(componentsFilter))
            {
                filterSet = new HashSet<string>();
                foreach (var c in componentsFilter.Split(','))
                {
                    var trimmed = c.Trim();
                    if (trimmed.Length > 0) filterSet.Add(trimmed);
                }
            }

            var sb = new StringBuilder();
            foreach (var rawPath in pathsStr.Split(','))
            {
                var path = rawPath.Trim();
                if (path.Length == 0) continue;

                if (sb.Length > 0) sb.AppendLine();
                sb.Append("--- ").Append(path).AppendLine(" ---");

                var go = ComponentSerializer.FindObject(path);
                if (go == null)
                {
                    sb.AppendLine(ErrorHelper.ObjectNotFound(path));
                    continue;
                }

                if (filterSet != null)
                {
                    foreach (var typeName in filterSet)
                    {
                        var result = ComponentSerializer.Serialize(path, typeName);
                        if (result != null)
                        {
                            sb.Append("[").Append(typeName).AppendLine("]");
                            sb.AppendLine(result);
                        }
                    }
                }
                else
                {
                    sb.AppendLine(ComponentSerializer.SerializeAll(TransientObjectId.GetWireValue(go)));
                }
            }
            return ApplyFieldsCompress(args, sb.ToString().TrimEnd());
        }


        private static string ExecGetHierarchy(string args)
        {
            var summary = JsonHelper.ExtractString(args, "summary") == "true";
            var scene = JsonHelper.ExtractString(args, "scene");
            if (summary)
            {
                var summaryRoot = JsonHelper.ExtractString(args, "root");
                return HierarchySerializer.SerializeSummary(summaryRoot);
            }
            var depth = ExtractInt(args, "depth", 99);
            var root = JsonHelper.ExtractString(args, "root");
            var filter = JsonHelper.ExtractString(args, "filter");
            var components = JsonHelper.ExtractString(args, "components") == "true";
            var incremental = JsonHelper.ExtractString(args, "incremental") == "true";
            var raw = incremental
                ? HierarchySerializer.SerializeIncremental(depth, root, filter, components, scene)
                : HierarchySerializer.Serialize(depth, root, filter, components, scene);
            return ApplyFieldsCompress(args, raw);
        }


        private static string ExecGetComponent(string args)
        {
            var path = JsonHelper.ExtractString(args, "path");
            var type = JsonHelper.ExtractString(args, "type");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(type))
                throw new ArgumentException("path and type are required");

            var go = ComponentSerializer.FindObject(path);
            if (go == null)
            {
                // Ref and transient-ID paths throw inside FindObject when stale.
                // Defensive check: if somehow a ref-like path slips through null, surface as STALE_CACHE.
                if (path.StartsWith("#") || RefManager.IsRef(path))
                    throw new StaleCacheException($"Stale cache: {path}. Call get_hierarchy to refresh.");
                throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));
            }

            // P-107: pass the already-resolved go to avoid a second FindObject traversal
            // that can return stale results for freshly-created nested objects.
            var result = ComponentSerializer.Serialize(go, type);
            if (result == null)
                throw new InvalidOperationException(ErrorHelper.ComponentNotFound(type, go));

            return ApplyFieldsCompress(args, result);
        }

        private static string ApplyFieldsCompress(string args, string result)
        {
            var fields = JsonHelper.ExtractString(args, "fields");
            if (!string.IsNullOrEmpty(fields))
                return FieldProjector.Project(result, fields);
            if (JsonHelper.ExtractString(args, "compress") == "true")
                return DefaultStripper.Strip(result);
            return result;
        }

        private static string ExecGetComponentsList(string args)
        {
            var id = JsonHelper.ExtractString(args, "id");
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id is required");
            var result = ComponentSerializer.ListComponentsById(id);
            if (result == null) throw new InvalidOperationException($"Object not found: #{id}");
            return result;
        }

        private static string ExecGetObjectDetail(string args)
        {
            var id = JsonHelper.ExtractString(args, "id");
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id is required (instanceID as '#123' or scene path like '/Parent/Object')");
            var result = ComponentSerializer.SerializeAll(id);
            if (result == null) throw new InvalidOperationException($"Object not found: #{id}");
            return result;
        }

        private static string ExecFindObjects(string args)
        {
            var name = JsonHelper.ExtractString(args, "name");
            var tag = JsonHelper.ExtractString(args, "tag");
            var layer = JsonHelper.ExtractString(args, "layer");
            var component = JsonHelper.ExtractString(args, "component");
            return ObjectManager.FindObjects(name, tag, layer, component);
        }

        private static string ExecSetProperty(string args)
        {
            var findType = JsonHelper.ExtractString(args, "find_type");
            if (!string.IsNullOrEmpty(findType))
            {
                var found = ObjectManager.FindObjects(null, null, null, findType);
                if (string.IsNullOrEmpty(found)) return "none";
                if (found.StartsWith("err:") || found == "none") return found;
                var paths = found.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var component = JsonHelper.ExtractString(args, "component");
                var prop = JsonHelper.ExtractString(args, "prop");
                var value = JsonHelper.ExtractString(args, "value");
                int ok = 0; int fail = 0;
                foreach (var p in paths)
                {
                    try { ObjectManager.SetProperty(p.Trim(), component, prop, value, false); ok++; }
                    catch { fail++; }
                }
                return $"bulk set {prop}={value}: {ok} ok, {fail} failed / {paths.Length} {findType}";
            }
            var path = JsonHelper.ExtractString(args, "path");
            var comp = JsonHelper.ExtractString(args, "component");
            var prp = JsonHelper.ExtractString(args, "prop");
            var val = JsonHelper.ExtractString(args, "value");
            var dryRun = JsonHelper.ExtractString(args, "dry_run") == "true";
            var actual = ObjectManager.SetProperty(path, comp, prp, val, dryRun);
            if (dryRun) return actual;
            // F11: skip snapshot serialization inside batch (deferred Physics.Sync handles it)
            if (BatchHelper.InBatch) return $"{prp} = {actual}";
            var go = ComponentSerializer.FindObject(path);
            if (go != null)
            {
                var normComp = InputNormalizer.NormalizeComponent(
                    ComponentSerializer.StripNamespace(comp), go);
                var snapshot = ComponentSerializer.Serialize(path, normComp);
                if (snapshot != null)
                    return $"{prp} = {actual}\n---\n{snapshot}";
            }
            return $"{prp} = {actual}";
        }

        private static string ExecTransferObject(string args)
        {
            var path = JsonHelper.ExtractString(args, "path");
            var action = JsonHelper.ExtractString(args, "action");
            var targetScene = JsonHelper.ExtractString(args, "target_scene");
            var parent = JsonHelper.ExtractString(args, "parent");
            var wps = JsonHelper.ExtractString(args, "world_position_stays") != "false";
            return ObjectManager.TransferObject(path, action, targetScene, parent, wps);
        }

        private static string ExecSetPropertyDelta(string args)
        {
            var path = JsonHelper.ExtractString(args, "path");
            var component = JsonHelper.ExtractString(args, "component");
            var prop = JsonHelper.ExtractString(args, "prop");
            var delta = JsonHelper.ExtractString(args, "delta");
            return ObjectManager.SetPropertyDelta(path, component, prop, delta);
        }

        private static string ExecCreateObject(string args)
        {
            var name = JsonHelper.ExtractString(args, "name");
            var parent = JsonHelper.ExtractString(args, "parent");
            var components = JsonHelper.ExtractString(args, "components");
            var primitive = JsonHelper.ExtractString(args, "primitive");
            var prefabPath = JsonHelper.ExtractString(args, "prefab_path");
            var scene = JsonHelper.ExtractString(args, "scene");
            var path = ObjectManager.CreateObject(name, parent, components, primitive, prefabPath, scene);
            GameObject go;
            try { go = ComponentSerializer.FindObject(path); }
            catch (System.ArgumentException) { go = null; } // duplicate name in scene — GO created, lookup ambiguous

            string warn = "";
            if (go != null && !string.IsNullOrEmpty(prefabPath))
            {
                var missing = new List<string>();
                foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t == go.transform) continue;
                    if (UnityEditor.PrefabUtility.GetPrefabInstanceStatus(t.gameObject) == UnityEditor.PrefabInstanceStatus.MissingAsset)
                        missing.Add(ComponentSerializer.GetPath(t.gameObject));
                }
                if (missing.Count > 0)
                    warn = $"\n[WARN] missing nested prefabs: {string.Join(", ", missing)}";
            }

            if (go?.transform.parent != null)
                return $"Created {path}{warn}\n--- parent ---\n{HierarchySerializer.SerializeSubtree(go.transform.parent.gameObject)}";
            return $"Created {path}{warn}";
        }

        private static string ExecDeleteObject(string args)
        {
            var id = JsonHelper.ExtractString(args, "id");
            var path = JsonHelper.ExtractString(args, "path");
            var force = JsonHelper.ExtractString(args, "force") == "true";
            GameObject go;
            if (!string.IsNullOrEmpty(id)) go = ComponentSerializer.FindObjectById(id);
            else if (!string.IsNullOrEmpty(path)) go = ComponentSerializer.FindObject(path, strict: true);
            else throw new ArgumentException("id or path required");
            if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path ?? $"#{id}"));
            var parentGo = go.transform.parent?.gameObject;
            var label = !string.IsNullOrEmpty(id) ? $"#{id}" : path;
            if (!string.IsNullOrEmpty(id)) ObjectManager.DeleteObjectById(id, force);
            else ObjectManager.DeleteObject(path, force);
            if (parentGo != null)
                return $"Deleted {label}\n--- parent ---\n{HierarchySerializer.SerializeSubtree(parentGo)}";
            return $"Deleted {label}";
        }

        private static string ExecManageComponent(string args)
        {
            var path   = JsonHelper.ExtractString(args, "path");
            var type   = JsonHelper.ExtractString(args, "type");
            var action = JsonHelper.ExtractString(args, "action");
            ObjectManager.ManageComponent(path, type, action);
            var go = ComponentSerializer.FindObject(path);
            if (go == null) return "ok";
            var list = ComponentSerializer.ListComponentsById(TransientObjectId.GetWireValue(go));
            var csv = list.Replace('\n', ',').TrimEnd(',');
            return action == "add"
                ? $"Added: {type}. Components: {csv}"
                : $"Removed: {type}. Remaining: {csv}";
        }

        private static string ExecGetConsole(string args)
        {
            var count     = ExtractInt(args, "count", -1);
            var level     = JsonHelper.ExtractString(args, "level");
            var first     = ExtractInt(args, "first", 0);
            var keyword   = JsonHelper.ExtractString(args, "keyword");
            var countOnly = JsonHelper.ExtractString(args, "count_only") == "true";
            var since     = ExtractFloat(args, "since", 0f);
            return ConsoleCapture.GetLogs(count, level, first, keyword, countOnly, since);
        }


        private static string ExecAutoWire(string args)
        {
            var path   = JsonHelper.ExtractString(args, "path");
            var dryRun = JsonHelper.ExtractString(args, "dry_run") == "true";
            var go     = ComponentSerializer.FindObjectOrThrow(path);
            var (wired, skipped) = AutoWiringHelper.Scan(go);
            if (!dryRun) AutoWiringHelper.Apply(wired);
            return AutoWiringHelper.Format(wired, skipped, dryRun);
        }

        private static string ExecGetUnityEvents(string args)
        {
            var pathFilter = JsonHelper.ExtractString(args, "path");
            var sb = new StringBuilder();
            int found = 0;
            foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                ScanForEvents(go, pathFilter, sb, ref found);
            return found == 0 ? "no UnityEvents found" : sb.ToString().TrimEnd();
        }

        private static void ScanForEvents(UnityEngine.GameObject go, string pathFilter, StringBuilder sb, ref int found)
        {
            var path = ComponentSerializer.GetPath(go);
            if (pathFilter != null
                && !path.Equals(pathFilter, StringComparison.OrdinalIgnoreCase)
                && !path.StartsWith(pathFilter + "/", StringComparison.OrdinalIgnoreCase))
            {
                foreach (UnityEngine.Transform child in go.transform)
                    ScanForEvents(child.gameObject, pathFilter, sb, ref found);
                return;
            }
            foreach (var comp in go.GetComponents<UnityEngine.Component>())
            {
                if (comp == null) continue;
                try
                {
                    var cso = new UnityEditor.SerializedObject(comp);
                    var prop = cso.GetIterator();
                    while (prop.NextVisible(true))
                    {
                        if (prop.propertyType != UnityEditor.SerializedPropertyType.Generic) continue;
                        var calls = prop.FindPropertyRelative("m_PersistentCalls.m_Calls");
                        if (calls == null || calls.arraySize == 0) continue;
                        sb.Append(path).Append("|").Append(comp.GetType().Name)
                          .Append("|").Append(prop.name).Append(": ")
                          .Append("UnityEvent[").Append(calls.arraySize).AppendLine("]");
                        found++;
                    }
                }
                catch { continue; }
            }
            foreach (UnityEngine.Transform child in go.transform)
                ScanForEvents(child.gameObject, pathFilter, sb, ref found);
        }

        private static string ExecRuntimeSnapshot(string args)
        {
            var typeName  = JsonHelper.ExtractString(args, "type");
            var nameMatch = JsonHelper.ExtractString(args, "name");
            var component = JsonHelper.ExtractString(args, "component") ?? typeName;
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentException("type is required");

            var foundPaths = ObjectManager.FindObjects(nameMatch, null, null, typeName);
            if (foundPaths == "none" || foundPaths.StartsWith("err:")) return foundPaths;

            var sb = new StringBuilder();
            sb.Append("runtime_snapshot: ").AppendLine(typeName);
            sb.AppendLine("---");
            int count = 0;
            foreach (var p in foundPaths.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                var serialized = ComponentSerializer.Serialize(p.Trim(), component);
                if (serialized == null) continue;
                sb.Append("### ").AppendLine(p.Trim());
                sb.AppendLine(serialized);
                count++;
                if (count >= 50) { sb.AppendLine("... (truncated at 50)"); break; }
            }
            sb.Append("total: ").Append(count);
            return ApplyFieldsCompress(args, sb.ToString().TrimEnd());
        }
    }
}
