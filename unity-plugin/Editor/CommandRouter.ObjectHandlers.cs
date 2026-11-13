using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Object/read command handlers (split from CommandRouter.cs for <200-line focus).
    public static partial class CommandRouter
    {
        internal static string ExecInspect(string args)
        {
            var pathsStr = InspectArgs.Paths(args);
            var findType = InspectArgs.FindType(args);
            if (string.IsNullOrEmpty(pathsStr) && !string.IsNullOrEmpty(findType))
            {
                var found = ObjectManager.FindObjects(null, null, null, findType);
                if (string.IsNullOrEmpty(found)) return "none";
                if (found.StartsWith("err:") || found == "none") return found;
                pathsStr = string.Join(",", found.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            }
            if (string.IsNullOrEmpty(pathsStr))
                throw new ArgumentException("paths or find_type is required");

            var componentsFilter = InspectArgs.Components(args)
                                ?? JsonHelper.ExtractString(args, "type"); // "type" is not a declared inspect ARG
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
                        var result = ComponentSerializer.Serialize(go, typeName);
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


        internal static string ExecGetHierarchy(string args)
        {
            var summary = GetHierarchyArgs.Summary(args);
            var scene = GetHierarchyArgs.Scene(args);
            if (summary)
            {
                var summaryRoot = GetHierarchyArgs.Root(args);
                return HierarchySerializer.SerializeSummary(summaryRoot, scene);
            }
            var depth = ExtractInt(args, "depth", 99);
            var root = GetHierarchyArgs.Root(args);
            var filter = GetHierarchyArgs.Filter(args);
            var components = GetHierarchyArgs.Components(args);
            var incremental = GetHierarchyArgs.Incremental(args);
            var raw = incremental
                ? HierarchySerializer.SerializeIncremental(depth, root, filter, components, scene)
                : HierarchySerializer.Serialize(depth, root, filter, components, scene);
            return ApplyFieldsCompress(args, raw);
        }


        internal static string ExecGetComponent(string args)
        {
            var path = GetComponentArgs.Path(args);
            var type = GetComponentArgs.Type(args);
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(type))
                throw new ArgumentException("path and type are required");

            var go = ComponentSerializer.FindObject(path);
            if (go == null)
            {
                // Ref and transient-ID paths throw inside FindObject when stale.
                // Defensive check: if somehow a ref-like path slips through null, surface as STALE_CACHE.
                if (path.StartsWith("&") || path.StartsWith("#") || path.StartsWith("$"))
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

        internal static string ExecGetComponentsList(string args)
        {
            var id = JsonHelper.ExtractString(args, "id");
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id is required");
            var result = ComponentSerializer.ListComponentsById(id);
            if (result == null) throw new InvalidOperationException($"Object not found: #{id}");
            return result;
        }

        internal static string ExecGetObjectDetail(string args)
        {
            var id = JsonHelper.ExtractString(args, "id");
            if (string.IsNullOrEmpty(id))
                throw new ArgumentException("id is required ('$3E8' hex or '#123' legacy decimal, or scene path like '/Parent/Object')");
            var result = ComponentSerializer.SerializeAll(id);
            if (result == null) throw new InvalidOperationException($"Object not found: #{id}");
            return result;
        }

        internal static string ExecFindObjects(string args)
        {
            var name = FindObjectsArgs.Name(args);
            var tag = FindObjectsArgs.Tag(args);
            var layer = FindObjectsArgs.Layer(args);
            var component = FindObjectsArgs.Component(args);
            return ObjectManager.FindObjects(name, tag, layer, component);
        }

        internal static string ExecSetProperty(string args)
        {
            var dryRun = SetPropertyArgs.DryRun(args);
            var findType = SetPropertyArgs.FindType(args);
            if (!string.IsNullOrEmpty(findType))
            {
                var found = ObjectManager.FindObjects(null, null, null, findType);
                if (string.IsNullOrEmpty(found)) return "none";
                if (found.StartsWith("err:") || found == "none") return found;
                var paths = found.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                var component = SetPropertyArgs.Component(args);
                var prop = SetPropertyArgs.Prop(args);
                var value = SetPropertyArgs.Value(args);
                int ok = 0; int fail = 0;
                foreach (var p in paths)
                {
                    try
                    {
                        var itemResult = ObjectManager.SetProperty(
                            p.Trim(), component, prop, value, dryRun);
                        if (BatchHelper.IsFailureResult(itemResult)) fail++;
                        else ok++;
                    }
                    catch { fail++; }
                }
                var prefix = dryRun ? "DRY-RUN bulk" : "bulk set";
                if (fail > 0)
                {
                    var errorPrefix = dryRun
                        ? "DRY-RUN BULK ERROR"
                        : "BULK SET ERROR";
                    return $"{errorPrefix}: {prop}={value}: {ok} ok, {fail} failed / {paths.Length} {findType}";
                }
                return $"{prefix} {prop}={value}: {ok} ok, {fail} failed / {paths.Length} {findType}";
            }
            var path = SetPropertyArgs.Path(args);
            var comp = SetPropertyArgs.Component(args);
            var prp = SetPropertyArgs.Prop(args);
            var val = SetPropertyArgs.Value(args);
            string oldStr = null;
            if (!dryRun)
                try { oldStr = ObjectManager.ReadPropertyValue(path, comp, prp); } catch (ArgumentException) { }
            var actual = ObjectManager.SetProperty(path, comp, prp, val, dryRun);
            if (dryRun) return actual;
            // T15: pend receipt for single-object set_property (bulk path skips — no single receipt)
            CommandRouter.PendReceipt(new MutationReceipt {
                Path = path, Op = "modify", TargetType = "property",
                Prop = prp, Before = oldStr, After = actual, Reversible = true
            });
            // F11: skip snapshot serialization inside batch (deferred Physics.Sync handles it)
            if (BatchHelper.InBatch) return FormatPropResult(prp, actual, oldStr);
            var go = ComponentSerializer.FindObject(path);
            if (go != null)
            {
                var normComp = InputNormalizer.NormalizeComponent(
                    ComponentSerializer.StripNamespace(comp), go);
                var snapshot = ComponentSerializer.Serialize(path, normComp);
                if (snapshot != null)
                    return $"{FormatPropResult(prp, actual, oldStr)}\n---\n{snapshot}";
            }
            return FormatPropResult(prp, actual, oldStr);
        }

        internal static string ExecTransferObject(string args)
        {
            var path = TransferObjectArgs.Path(args);
            var action = TransferObjectArgs.Action(args);
            var targetScene = TransferObjectArgs.TargetScene(args);
            var parent = TransferObjectArgs.Parent(args);
            // NOT TransferObjectArgs.WorldPositionStays: that accessor defaults false
            // (bare `== "true"`), but DSL declares `DEFAULT True` -- generator gap, keep raw.
            var wps = JsonHelper.ExtractString(args, "world_position_stays") != "false";
            return ObjectManager.TransferObject(path, action, targetScene, parent, wps);
        }

        internal static string ExecSetPropertyDelta(string args)
        {
            var path = SetPropertyDeltaArgs.Path(args);
            var component = SetPropertyDeltaArgs.Component(args);
            var prop = SetPropertyDeltaArgs.Prop(args);
            var delta = SetPropertyDeltaArgs.Delta(args);
            return ObjectManager.SetPropertyDelta(path, component, prop, delta);
        }

        internal static string ExecCreateObject(string args)
        {
            var name = CreateObjectArgs.Name(args);
            var parent = CreateObjectArgs.Parent(args);
            var components = CreateObjectArgs.Components(args);
            var primitive = CreateObjectArgs.Primitive(args);
            var prefabPath = CreateObjectArgs.PrefabPath(args);
            var scene = CreateObjectArgs.Scene(args);
            var path = ObjectManager.CreateObject(name, parent, components, primitive, prefabPath, scene);
            // T15: pend receipt for create_object
            CommandRouter.PendReceipt(new MutationReceipt {
                Path = path, Op = "create", TargetType = "scene_object",
                Before = null, After = null, Reversible = true
            });
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

        internal static string ExecDeleteObject(string args)
        {
            // NOT DeleteObjectArgs.Id: DSL declares `id` as int but FindObjectById/DeleteObjectById
            // take the hex ($..)/legacy-decimal string id, not an int -- generator type mismatch.
            var id = JsonHelper.ExtractString(args, "id");
            var path = DeleteObjectArgs.Path(args);
            var force = DeleteObjectArgs.Force(args);
            GameObject go;
            if (!string.IsNullOrEmpty(id)) go = ComponentSerializer.FindObjectById(id);
            else if (!string.IsNullOrEmpty(path)) go = ComponentSerializer.FindObject(path, strict: true);
            else throw new ArgumentException("id or path required");
            if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path ?? $"#{id}"));
            var parentGo = go.transform.parent?.gameObject;
            var label = !string.IsNullOrEmpty(id) ? $"#{id}" : path;
            // T15: pend receipt before deletion (after resolving label)
            CommandRouter.PendReceipt(new MutationReceipt {
                Path = label, Op = "delete", TargetType = "scene_object",
                Before = null, After = null, Reversible = true
            });
            if (!string.IsNullOrEmpty(id)) ObjectManager.DeleteObjectById(id, force);
            else ObjectManager.DeleteObject(path, force);
            if (parentGo != null)
                return $"Deleted {label}\n--- parent ---\n{HierarchySerializer.SerializeSubtree(parentGo)}";
            return $"Deleted {label}";
        }

        internal static string ExecManageComponent(string args)
        {
            var path   = ManageComponentArgs.Path(args);
            var type   = ManageComponentArgs.Type(args);
            var action = ManageComponentArgs.Action(args);
            ObjectManager.ManageComponent(path, type, action);
            // T15: pend receipt for manage_component
            CommandRouter.PendReceipt(new MutationReceipt {
                Path = path, Op = "modify", TargetType = "component",
                Prop = type, Before = null, After = action, Reversible = true
            });
            var go = ComponentSerializer.FindObject(path);
            if (go == null) return "ok";
            var list = ComponentSerializer.ListComponentsById(TransientObjectId.GetWireValue(go));
            var csv = list.Replace('\n', ',').TrimEnd(',');
            return action == "add"
                ? $"Added: {type}. Components: {csv}"
                : $"Removed: {type}. Remaining: {csv}";
        }

        internal static string ExecGetConsole(string args)
        {
            var count     = ExtractInt(args, "count", -1);
            var level     = GetConsoleArgs.Level(args);
            var first     = ExtractInt(args, "first", 0);
            var keyword   = GetConsoleArgs.Keyword(args);
            var countOnly = GetConsoleArgs.CountOnly(args);
            var since     = ExtractFloat(args, "since", 0f);
            return ConsoleCapture.GetLogs(count, level, first, keyword, countOnly, since);
        }


        internal static string ExecAutoWire(string args)
        {
            var path   = AutoWireArgs.Path(args);
            var dryRun = AutoWireArgs.DryRun(args);
            var go     = ComponentSerializer.FindObjectOrThrow(path);
            var (wired, skipped) = AutoWiringHelper.Scan(go);
            if (!dryRun) AutoWiringHelper.Apply(wired);
            return AutoWiringHelper.Format(wired, skipped, dryRun);
        }

        internal static string ExecGetUnityEvents(string args)
        {
            var pathFilter = GetUnityEventsArgs.Path(args);
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

        internal static string ExecRuntimeSnapshot(string args)
        {
            var typeName  = RuntimeSnapshotArgs.Type(args);
            var nameMatch = RuntimeSnapshotArgs.Name(args);
            var component = RuntimeSnapshotArgs.Component(args) ?? typeName;
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

        private static string FormatPropResult(string prop, string actual, string old)
            => old != null ? $"{prop} = {actual} (was {old})" : $"{prop} = {actual}";
    }
}
