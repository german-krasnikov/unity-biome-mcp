using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class ScriptableObjectHelper
    {
        static readonly string[] ValidActions = { "create", "get", "set", "list_types", "find" };

        internal static string Execute(string action, string argsJson)
        {
            switch (action)
            {
                case "create": return Create(argsJson);
                case "get":    return Get(argsJson);
                case "set":    return Set(argsJson);
                case "list_types": return ListTypes(argsJson);
                case "find":   return Find(argsJson);
                default:       throw new ArgumentException(ErrorHelper.InvalidAction(action, ValidActions));
            }
        }

        // ── create ────────────────────────────────────────────────────────────

        private static string Create(string args)
        {
            var typeName = JsonHelper.ExtractString(args, "type");
            var path     = JsonHelper.ExtractString(args, "path");
            var fields   = JsonHelper.ExtractString(args, "fields");
            if (string.IsNullOrEmpty(typeName)) throw new ArgumentException("type is required");
            if (string.IsNullOrEmpty(path))     throw new ArgumentException("path is required");

            var type = FindSOType(typeName);
            if (type == null) throw new ArgumentException($"ScriptableObject type not found: {typeName}");

            AssetHelper.EnsureDirectory(path);
            var asset = ScriptableObject.CreateInstance(type);
            UndoGroupStack.StageAsset(path);
            AssetDatabase.CreateAsset(asset, path);
            try
            {
                if (!string.IsNullOrEmpty(fields))
                {
                    var so = new SerializedObject(asset);
                    SetMultipleFields(so, fields);
                    so.ApplyModifiedProperties();
                    EditorUtility.SetDirty(asset);
                }
                AssetDatabase.SaveAssets();
                return $"Created: {path}";
            }
            catch
            {
                AssetDatabase.DeleteAsset(path);
                throw;
            }
        }

        // ── get ───────────────────────────────────────────────────────────────

        private static string Get(string args)
        {
            var asset = LoadAsset(JsonHelper.ExtractString(args, "path"));
            var fieldsFilter = JsonHelper.ExtractString(args, "fields");
            HashSet<string> wanted = fieldsFilter != null
                ? new HashSet<string>(fieldsFilter.Split(',').Select(f => f.Trim()))
                : null;
            var so = new SerializedObject(asset);
            var prop = so.GetIterator();
            prop.Next(true);
            var sb = new StringBuilder();
            while (prop.NextVisible(false))
            {
                if (prop.name == "m_Script") continue;
                if (wanted != null && !wanted.Contains(prop.name)) continue;
                sb.AppendLine($"{prop.name}: {ComponentSerializer.GetPropertyValueString(prop)}");
            }
            return sb.ToString().TrimEnd('\n', '\r');
        }

        // ── set ───────────────────────────────────────────────────────────────

        private static string Set(string args)
        {
            var path   = JsonHelper.ExtractString(args, "path");
            var prop   = JsonHelper.ExtractString(args, "prop");
            var value  = JsonHelper.ExtractString(args, "value");
            var fields = JsonHelper.ExtractString(args, "fields");

            bool hasSingle = !string.IsNullOrEmpty(prop) && !string.IsNullOrEmpty(value);
            bool hasMulti  = !string.IsNullOrEmpty(fields);

            if (!hasSingle && !hasMulti)
                throw new ArgumentException("prop+value or fields is required");
            if (hasSingle && hasMulti)
                throw new ArgumentException("prop+value and fields are mutually exclusive");

            var asset = LoadAsset(path);
            var so    = new SerializedObject(asset);

            string result;
            if (hasSingle)
                result = SetSingleField(so, prop, value);
            else
            {
                result = SetMultipleFields(so, fields);
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return result;
        }

        private static string SetSingleField(SerializedObject so, string prop, string value)
        {
            var property = so.FindProperty(prop);
            if (property == null)
            {
                var allowed = ListFieldNames(so);
                throw new ArgumentException($"Property not found: '{prop}'. Allowed: {allowed}");
            }
            var oldVal = property.hasMultipleDifferentValues ? "<mixed>" : ComponentSerializer.GetPropertyValueString(property);
            ValueParser.SetPropertyValue(property, value);
            return $"ok: {prop} = {oldVal} → {value}";
        }

        private static string ListFieldNames(SerializedObject so)
        {
            var it = so.GetIterator(); it.Next(true);
            var names = new List<string>();
            while (it.NextVisible(false))
                if (it.name != "m_Script") names.Add(it.name);
            return string.Join(", ", names);
        }

        private static string SetMultipleFields(SerializedObject so, string fields)
        {
            var sb = new StringBuilder();
            foreach (var line in fields.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0)
                    throw new ArgumentException($"Invalid format (expected prop=value): {trimmed}");

                var fieldProp = trimmed.Substring(0, eqIdx).Trim();
                var fieldVal  = trimmed.Substring(eqIdx + 1).Trim();

                var property = so.FindProperty(fieldProp);
                if (property == null)
                {
                    var allowed = ListFieldNames(so);
                    throw new ArgumentException($"Property not found: '{fieldProp}'. Allowed: {allowed}");
                }

                var oldVal = property.hasMultipleDifferentValues ? "<mixed>" : ComponentSerializer.GetPropertyValueString(property);
                ValueParser.SetPropertyValue(property, fieldVal);
                sb.AppendLine($"ok: {fieldProp} = {oldVal} → {fieldVal}");
            }
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "ok";
        }

        // ── list_types ────────────────────────────────────────────────────────

        private static string ListTypes(string args)
        {
            var filter = JsonHelper.ExtractString(args, "filter");
            var types = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .Where(t => !t.IsAbstract && !t.IsGenericType)
                .Where(t => string.IsNullOrEmpty(filter) || t.Name.Contains(filter))
                .Take(100)
                .Select(t => t.Name);
            return string.Join("\n", types);
        }

        // ── find ──────────────────────────────────────────────────────────────

        private static string Find(string args)
        {
            var typeName = JsonHelper.ExtractString(args, "type");
            if (string.IsNullOrEmpty(typeName)) throw new ArgumentException("type is required");

            var guids = AssetDatabase.FindAssets($"t:{typeName}");
            if (guids.Length == 0) return "(none)";
            return string.Join("\n", guids.Select(AssetDatabase.GUIDToAssetPath));
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static ScriptableObject LoadAsset(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is required");
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) throw new ArgumentException($"ScriptableObject not found: {path}");
            return asset;
        }

        private static Type FindSOType(string name)
        {
            return TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                .FirstOrDefault(t => t.Name == name || t.FullName == name);
        }

    }
}
