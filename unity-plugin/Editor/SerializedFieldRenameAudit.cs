using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine.Serialization;

namespace UnityMCP.Editor
{
    internal static class SerializedFieldRenameAudit
    {
        internal static string Execute(string argsJson)
        {
            var typeName = JsonHelper.ExtractString(argsJson, "type")
                ?? throw new ArgumentException("'type' is required");
            var oldField = JsonHelper.ExtractString(argsJson, "old_field")
                ?? throw new ArgumentException("'old_field' is required");
            var newField = JsonHelper.ExtractString(argsJson, "new_field")
                ?? throw new ArgumentException("'new_field' is required");
            var include = JsonHelper.ExtractString(argsJson, "include") ?? "prefabs,scenes,scriptable_objects";

            var type = FindType(typeName)
                ?? throw new ArgumentException($"Type not found: '{typeName}'");

            bool hasFSA = CheckFormerlySerializedAs(type, newField, oldField);
            var staleAssets = FindStaleAssets(type, oldField, include);

            var sb = new StringBuilder();
            sb.AppendLine($"has_formerly_serialized_as: {(hasFSA ? "true" : "false")}");
            sb.AppendLine($"stale_assets: {(staleAssets.Count == 0 ? "none" : staleAssets.Count.ToString())}");
            foreach (var a in staleAssets.Take(100))
                sb.AppendLine($"  {a}");
            if (staleAssets.Count >= 100)
                sb.AppendLine("  (scan capped at 100; more may exist)");
            sb.AppendLine($"safe_to_remove_attribute: {(hasFSA && staleAssets.Count == 0 ? "true" : "false")}");
            sb.AppendLine("recommended_actions:");
            if (!hasFSA)
                sb.AppendLine($"  1. Add [FormerlySerializedAs(\"{oldField}\")] to {newField} on {typeName}");
            if (staleAssets.Count > 0)
                sb.AppendLine($"  {(hasFSA ? "1" : "2")}. Open & re-save {staleAssets.Count} stale assets to migrate data");
            if (hasFSA && staleAssets.Count == 0)
                sb.AppendLine("  1. [FormerlySerializedAs] attribute can now be removed safely");
            return sb.ToString().TrimEnd();
        }

        private static Type FindType(string typeName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName, throwOnError: false, ignoreCase: true);
                if (t != null) return t;
            }
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetTypes().FirstOrDefault(x =>
                    x.Name == typeName || x.FullName?.EndsWith("." + typeName) == true);
                if (t != null) return t;
            }
            return null;
        }

        private static bool CheckFormerlySerializedAs(Type type, string newField, string oldField)
        {
            var field = type.GetField(newField,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) return false;
            return field.GetCustomAttributes<FormerlySerializedAsAttribute>()
                        .Any(a => a.oldName == oldField);
        }

        private static List<string> FindStaleAssets(Type type, string oldField, string include)
        {
            var result = new List<string>();
            var parts = include.Split(',').Select(s => s.Trim().ToLower()).ToHashSet();

            if (parts.Contains("prefabs"))        ScanPrefabs(type, oldField, result);
            if (parts.Contains("scriptable_objects")) ScanScriptableObjects(type, oldField, result);
            if (parts.Contains("scenes"))         ScanSceneFiles(type, oldField, result);
            return result;
        }

        private static void ScanPrefabs(Type type, string oldField, List<string> result)
        {
            var fieldPattern = new Regex($@"^\s+{Regex.Escape(oldField)}:", RegexOptions.Multiline);
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                if (result.Count >= 100) break;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                try
                {
                    if (fieldPattern.IsMatch(File.ReadAllText(path)))
                        result.Add(path);
                }
                catch { /* skip unreadable */ }
            }
        }

        private static void ScanScriptableObjects(Type type, string oldField, List<string> result)
        {
            var fieldPattern = new Regex($@"^\s+{Regex.Escape(oldField)}:", RegexOptions.Multiline);
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject"))
            {
                if (result.Count >= 100) break;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                try
                {
                    if (fieldPattern.IsMatch(File.ReadAllText(path)))
                        result.Add(path);
                }
                catch { /* skip unreadable */ }
            }
        }

        // Scene scan is type-approximate (no type filter) — false positives are acceptable
        // for a diagnostic tool; false negatives (missed stale data) are not.
        private static void ScanSceneFiles(Type type, string oldField, List<string> result)
        {
            var fieldPattern = new Regex($@"^\s+{Regex.Escape(oldField)}:", RegexOptions.Multiline);
            foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
            {
                if (result.Count >= 100) break;
                var path = AssetDatabase.GUIDToAssetPath(guid);
                try
                {
                    if (fieldPattern.IsMatch(File.ReadAllText(path)))
                        result.Add(path);
                }
                catch { /* skip unreadable */ }
            }
        }
    }
}
