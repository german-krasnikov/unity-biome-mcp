using System;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityMCP.Editor
{
    internal static class MaterialHelper
    {
        internal static string Execute(string action, string argsJson)
        {
            return action switch
            {
                "create" => Create(argsJson),
                "get" => Get(argsJson),
                "set" => Set(argsJson),
                "copy" => Copy(argsJson),
                "list_properties" => ListProperties(argsJson),
                "list_slots" => ListSlots(argsJson),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "create", "get", "set", "copy", "list_properties", "list_slots" }))
            };
        }

        private static string Create(string args)
        {
            var path = JsonHelper.ExtractString(args, "path")
                ?? throw new ArgumentException("path is required");
            var shaderName = JsonHelper.ExtractString(args, "shader") ?? "Standard";

            AssetHelper.EnsureDirectory(path);
            var shader = Shader.Find(shaderName)
                ?? throw new InvalidOperationException($"Shader not found: {shaderName}");

            var mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            return $"ok: {path}";
        }

        private static Material ResolveMaterial(string args, int slot = 0)
        {
            var path = JsonHelper.ExtractString(args, "path");
            var objectPath = JsonHelper.ExtractString(args, "object_path");

            if (path != null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) throw new InvalidOperationException($"Material not found: {path}");
                return mat;
            }
            if (objectPath != null)
            {
                var go = ComponentSerializer.FindObject(objectPath);
                if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(objectPath));
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) throw new InvalidOperationException($"No Renderer on: {objectPath}");
                var mats = renderer.sharedMaterials;
                if (slot < 0 || slot >= mats.Length)
                    throw new ArgumentException($"Slot {slot} out of range (0-{mats.Length - 1})");
                if (mats[slot] == null) throw new InvalidOperationException($"Renderer on '{objectPath}' has no material assigned at slot {slot}");
                return mats[slot];
            }
            throw new ArgumentException("path or object_path is required");
        }

        private static string Get(string args)
        {
            var slot = JsonHelper.ExtractInt(args, "slot");
            var mat = ResolveMaterial(args, slot);
            var sb = new StringBuilder();
            sb.AppendLine($"Shader: {mat.shader.name}");

            int count = mat.shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                var name = mat.shader.GetPropertyName(i);
                var type = mat.shader.GetPropertyType(i);
                sb.AppendLine(FormatProperty(mat, name, type));
            }
            return sb.ToString().TrimEnd();
        }

        private static string FormatProperty(Material mat, string name, ShaderPropertyType type)
        {
            return type switch
            {
                ShaderPropertyType.Color =>
                    $"{name}: {mat.GetColor(name)} [Color]",
                ShaderPropertyType.Float or ShaderPropertyType.Range =>
                    $"{name}: {mat.GetFloat(name).ToString("G4", CultureInfo.InvariantCulture)} [Float]",
                ShaderPropertyType.Texture =>
                    $"{name}: {AssetDatabase.GetAssetPath(mat.GetTexture(name))} [Texture]",
                ShaderPropertyType.Vector =>
                    $"{name}: {mat.GetVector(name)} [Vector]",
                _ => $"{name}: ? [{type}]"
            };
        }

        private static string Set(string args)
        {
            var slot = JsonHelper.ExtractInt(args, "slot");
            var mat = ResolveMaterial(args, slot);
            var prop = JsonHelper.ExtractString(args, "prop")
                ?? throw new ArgumentException("prop is required");
            var value = JsonHelper.ExtractString(args, "value")
                ?? throw new ArgumentException("value is required");

            int idx = mat.shader.FindPropertyIndex(prop);

            // Keyword (e.g. _EMISSION) — not in shader property block
            if (idx < 0)
            {
                if (value == "true") { Undo.RecordObject(mat, "Enable Keyword"); mat.EnableKeyword(prop); EditorUtility.SetDirty(mat); return "ok"; }
                if (value == "false") { Undo.RecordObject(mat, "Disable Keyword"); mat.DisableKeyword(prop); EditorUtility.SetDirty(mat); return "ok"; }
                throw new InvalidOperationException($"Property not found: {prop}");
            }

            Undo.RecordObject(mat, "Set Material Property");
            ShaderHelper.ApplyProperty(mat, prop, mat.shader.GetPropertyType(idx), value);

            EditorUtility.SetDirty(mat);
            return "ok";
        }

        private static string Copy(string args)
        {
            var sourcePath = JsonHelper.ExtractString(args, "source")
                ?? throw new ArgumentException("source is required");
            var targets = JsonHelper.ExtractString(args, "targets")
                ?? throw new ArgumentException("targets is required");
            var slot = JsonHelper.ExtractInt(args, "slot");

            Material mat;
            if (sourcePath.StartsWith("Assets/", StringComparison.Ordinal) || sourcePath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                if (mat == null) throw new InvalidOperationException($"Material not found at: {sourcePath}");
            }
            else
            {
                var sourceGo = ComponentSerializer.FindObject(sourcePath);
                if (sourceGo == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(sourcePath));
                var sourceRenderer = sourceGo.GetComponent<Renderer>();
                if (sourceRenderer == null) throw new InvalidOperationException($"No Renderer on: {sourcePath}");
                var sourceMats = sourceRenderer.sharedMaterials;
                if (slot < 0 || slot >= sourceMats.Length)
                    throw new ArgumentException($"Source slot {slot} out of range (0-{sourceMats.Length - 1})");
                mat = sourceMats[slot];
            }

            int count = 0;
            foreach (var t in targets.Split(','))
            {
                var tPath = t.Trim();
                if (tPath.Length == 0) continue;
                var go = ComponentSerializer.FindObject(tPath);
                if (go == null) continue;
                var r = go.GetComponent<Renderer>();
                if (r == null) continue;
                Undo.RecordObject(r, "Copy Material");
                var mats = r.sharedMaterials;
                if (slot < mats.Length)
                {
                    mats[slot] = mat;
                    r.sharedMaterials = mats;
                }
                else
                {
                    r.sharedMaterial = mat;
                }
                EditorUtility.SetDirty(r);
                count++;
            }
            return $"ok: {count} copied";
        }

        private static string ListProperties(string args)
        {
            var slot = JsonHelper.ExtractInt(args, "slot");
            var mat = ResolveMaterial(args, slot);
            var sb = new StringBuilder();
            int count = mat.shader.GetPropertyCount();
            for (int i = 0; i < count; i++)
            {
                var name = mat.shader.GetPropertyName(i);
                var type = mat.shader.GetPropertyType(i);
                sb.AppendLine($"{name}: {type}");
            }
            return sb.ToString().TrimEnd();
        }

        private static string ListSlots(string args)
        {
            var objectPath = JsonHelper.ExtractString(args, "object_path")
                ?? throw new ArgumentException("object_path is required");
            var go = ComponentSerializer.FindObject(objectPath);
            if (go == null) throw new InvalidOperationException(ErrorHelper.ObjectNotFound(objectPath));
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null) throw new InvalidOperationException($"No Renderer on: {objectPath}");

            var mats = renderer.sharedMaterials;
            var sb = new StringBuilder();
            for (int i = 0; i < mats.Length; i++)
            {
                var m = mats[i];
                sb.AppendLine($"[{i}] {(m != null ? m.name : "null")} ({(m != null ? m.shader.name : "none")})");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
