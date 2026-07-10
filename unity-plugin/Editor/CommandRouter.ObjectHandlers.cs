using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor
{
    // Object/read command handlers (split from CommandRouter.cs for <200-line focus).
    public static partial class CommandRouter
    {
        private static string ExecInspect(string args)
        {
            var pathsStr = JsonHelper.ExtractString(args, "paths");
            if (string.IsNullOrEmpty(pathsStr))
                throw new ArgumentException("paths is required");

            var componentsFilter = JsonHelper.ExtractString(args, "components");
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
                    sb.AppendLine(ComponentSerializer.SerializeAll(go.GetInstanceID()));
                }
            }
            return ApplyFieldsCompress(args, sb.ToString().TrimEnd());
        }

        // Cache for fast-path get_enabled_tools (bypasses main thread dispatch).
        // Always kept WARM so the TCP read thread never computes it (no EditorPrefs off-thread).
        // Writes: InvalidateEnabledToolsCache (settings UI, main thread) + end of RegisterAll
        //         (post-registration, main thread). Read thread uses ?? "" safety fallback only.
        private static volatile string _enabledToolsCache;

        // Internal accessor for tests — never null after first populate.
        internal static string PeekEnabledToolsCache => _enabledToolsCache;

        /// <summary>Thread-safe fast-path — never computes on the read thread (no EditorPrefs off-thread).</summary>
        internal static string ExecGetEnabledToolsCached() => _enabledToolsCache ?? "";

        // Called from Settings UI (always main thread) — REPOPULATES instead of nulling
        // so the read thread always sees a warm non-null value.
        internal static void InvalidateEnabledToolsCache() => _enabledToolsCache = ExecGetEnabledTools();

        private static string ExecGetEnabledTools()  => BuildToolList(enabled: true);
        private static string ExecGetDisabledTools() => BuildToolList(enabled: false);

        private static string BuildToolList(bool enabled)
        {
            var allTools = new System.Collections.Generic.HashSet<string>(MCPSettings.GetToolNames());
            foreach (var cmd in CommandRegistry.GetAllCommands())
                allTools.Add(cmd);
            var sb = new StringBuilder();
            bool first = true;
            foreach (var tool in allTools)
            {
                if (MCPSettings.IsToolEnabled(tool) == enabled)
                {
                    if (!first) sb.Append(",");
                    sb.Append(tool);
                    first = false;
                }
            }
            return sb.ToString();
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
            return incremental
                ? HierarchySerializer.SerializeIncremental(depth, root, filter, components, scene)
                : HierarchySerializer.Serialize(depth, root, filter, components, scene);
        }

        // Returns "--- ALIASES ---\nname=path|comp|field\n---" from PlaytestConfig,
        // or null when no config / no aliases. Called on main thread (AssetDatabase safe).
        internal static string BuildAliasSection(PlaytestConfig config = null)
        {
            if (config == null)
            {
                foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:PlaytestConfig"))
                {
                    var c = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestConfig>(
                        UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                    if (c?.aliases?.Count > 0) { config = c; break; }
                }
                if (config == null) return null;
            }
            if (config?.aliases == null || config.aliases.Count == 0) return null;
            var sb = new StringBuilder("--- ALIASES ---\n");
            bool any = false;
            foreach (var a in config.aliases)
            {
                if (a.type == AliasType.VarRuntime) continue;
                any = true;
                sb.Append(a.alias).Append('=');
                if (a.type == AliasType.ValConst)
                    sb.Append(a.constValue);
                else
                    sb.Append(a.path).Append('|').Append(a.component).Append('|').Append(a.field);
                sb.Append('\n');
            }
            if (!any) return null;
            sb.Append("---");
            return sb.ToString();
        }

        // Strips the --- ALIASES --- header and --- footer, returns bare name=value lines.
        private static string GetAliasesText()
        {
            var section = BuildAliasSection();
            if (section == null) return "no aliases";
            var sb = new StringBuilder();
            foreach (var raw in section.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (!line.StartsWith("---") && line.Length > 0)
                {
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(line);
                }
            }
            return sb.Length > 0 ? sb.ToString() : "no aliases";
        }

        private static string ExecAliasStatus(string _)
        {
            var sources = new List<string>();
            int count = 0;
            foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:PlaytestConfig"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var cfg = UnityEditor.AssetDatabase.LoadAssetAtPath<PlaytestConfig>(path);
                if (cfg?.aliases == null) continue;
                sources.Add(path);
                count += cfg.aliases.Count;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"loaded: {(AliasExpander.IsStale ? "stale" : count > 0 ? "true" : "empty")}");
            foreach (var s in sources) sb.AppendLine($"source: {s}");
            sb.AppendLine($"count: {count}");
            sb.Append($"stale: {AliasExpander.IsStale}");
            return sb.ToString();
        }

        private static string ExecGetComponent(string args)
        {
            var path = JsonHelper.ExtractString(args, "path");
            var type = JsonHelper.ExtractString(args, "type");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(type))
                throw new ArgumentException("path and type are required");

            var go = ComponentSerializer.FindObject(path);
            if (go == null)
                throw new InvalidOperationException(ErrorHelper.ObjectNotFound(path));

            var result = ComponentSerializer.Serialize(path, type);
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
            var id = ExtractInt(args, "id", -1);
            if (id == -1)
                throw new ArgumentException("id is required");
            var result = ComponentSerializer.ListComponents(id);
            if (result == null) throw new InvalidOperationException($"Object not found: #{id}");
            return result;
        }

        private static string ExecGetObjectDetail(string args)
        {
            var id = ExtractInt(args, "id", -1);
            if (id == -1)
                throw new ArgumentException("id is required");
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
            var path = JsonHelper.ExtractString(args, "path");
            var component = JsonHelper.ExtractString(args, "component");
            var prop = JsonHelper.ExtractString(args, "prop");
            var value = JsonHelper.ExtractString(args, "value");
            var dryRun = JsonHelper.ExtractString(args, "dry_run") == "true";
            var actual = ObjectManager.SetProperty(path, component, prop, value, dryRun);
            if (dryRun) return actual;
            // F11: skip snapshot serialization inside batch (deferred Physics.Sync handles it)
            if (BatchHelper.InBatch) return $"{prop} = {actual}";
            var go = ComponentSerializer.FindObject(path);
            if (go != null)
            {
                var normComp = InputNormalizer.NormalizeComponent(
                    ComponentSerializer.StripNamespace(component), go);
                var snapshot = ComponentSerializer.Serialize(path, normComp);
                if (snapshot != null)
                    return $"{prop} = {actual}\n---\n{snapshot}";
            }
            return $"{prop} = {actual}";
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
            var id = ExtractInt(args, "id", -1);
            var path = JsonHelper.ExtractString(args, "path");
            var force = JsonHelper.ExtractString(args, "force") == "true";
            GameObject go;
            if (id != -1) go = ComponentSerializer.FindObjectById(id);
            else if (!string.IsNullOrEmpty(path)) go = ComponentSerializer.FindObject(path, strict: true);
            else throw new ArgumentException("id or path required");
            if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path ?? $"#{id}"));
            var parentGo = go.transform.parent?.gameObject;
            var label = id != -1 ? $"#{id}" : path;
            if (id != -1) ObjectManager.DeleteObject(id, force);
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
            var list = ComponentSerializer.ListComponents(go.GetInstanceID());
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

        private static string BuildScreenshotResponse(string id, string args)
        {
            var camera = JsonHelper.ExtractString(args, "camera");

            if (camera == "annotation_frame")
            {
                var annotId = JsonHelper.ExtractString(args, "annotation_id");
                if (!string.IsNullOrEmpty(annotId))
                {
                    var snap = SceneRegionState.GetById(annotId);
                    if (snap != null) SceneRegionState.FrameRegion(snap.Id);
                }
                camera = "scene_view";
            }

            if (camera == "overview" || camera == "overview_game")
            {
                var w = ExtractInt(args, "width", 1280);
                var h = ExtractInt(args, "height", 720);
                var fp = MultiViewCapture.CaptureSceneOverview(w, h, topDown: camera == "overview");
                return JsonHelper.FormatFileResponse(id, fp);
            }

            if (camera == "multi_view")
            {
                var path = JsonHelper.ExtractString(args, "path");
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException("multi_view requires 'path' — the object to capture");
                var go = ComponentSerializer.FindObject(path);
                if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));
                var cellSize    = ExtractInt(args, "width", 512);
                var supersample = ExtractInt(args, "supersample", 2);
                var angles      = JsonHelper.ExtractString(args, "angles");
                float zoom = ExtractFloat(args, "zoom", 1f);
                Vector3 offset = ExtractVector3(args, "offset", Vector3.zero);
                float fixedSize = ExtractFloat(args, "fixed_size", 0f);
                var highlight = JsonHelper.ExtractString(args, "highlight");
                var showColliders = JsonHelper.ExtractString(args, "show_colliders") == "true";
                var filePath = MultiViewCapture.CaptureWithManifest(go, cellSize, supersample,
                    angles, zoom, offset, fixedSize, highlight, showColliders, out var manifest);
                if (!string.IsNullOrEmpty(manifest))
                    return JsonHelper.FormatFileResponseWithData(id, filePath, manifest);
                return JsonHelper.FormatFileResponse(id, filePath);
            }

            if (camera == "single_view")
            {
                var path = JsonHelper.ExtractString(args, "path");
                if (string.IsNullOrEmpty(path))
                    throw new ArgumentException("single_view requires 'path' — the object to capture");
                var go = ComponentSerializer.FindObject(path);
                if (go == null) throw new ArgumentException(ErrorHelper.ObjectNotFound(path));
                var size        = ExtractInt(args, "width", 512);
                var supersample = ExtractInt(args, "supersample", 2);
                var angle       = JsonHelper.ExtractString(args, "angle") ?? "front";
                float zoom = ExtractFloat(args, "zoom", 1f);
                Vector3 offset = ExtractVector3(args, "offset", Vector3.zero);
                float fixedSize = ExtractFloat(args, "fixed_size", 0f);
                var highlight = JsonHelper.ExtractString(args, "highlight");
                var showColliders = JsonHelper.ExtractString(args, "show_colliders") == "true";
                var filePath = MultiViewCapture.CaptureSingleView(go, size, supersample,
                    angle, zoom, offset, fixedSize, highlight, showColliders, out var manifest);
                if (!string.IsNullOrEmpty(manifest))
                    return JsonHelper.FormatFileResponseWithData(id, filePath, manifest);
                return JsonHelper.FormatFileResponse(id, filePath);
            }

            var width  = ExtractInt(args, "width", 640);
            var height = ExtractInt(args, "height", 480);
            var fpath = ScreenshotCapture.CaptureToFile(width, height, camera);
            return JsonHelper.FormatFileResponse(id, fpath);
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
    }
}
