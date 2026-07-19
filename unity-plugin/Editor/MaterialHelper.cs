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
                "get_errors" => GetShaderErrors(argsJson),
                "list_shaders" => ListShaders(argsJson),
                "set_fields" => SetFields(argsJson),
                _ => throw new ArgumentException(ErrorHelper.InvalidAction(action,
                    new[] { "create", "get", "set", "copy", "list_properties", "list_slots", "get_errors", "list_shaders", "set_fields" }))
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
            sb.AppendLine($"renderQueue: {mat.renderQueue}");

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
            var target = JsonHelper.ExtractString(args, "target") ?? "shared";
            var slot = JsonHelper.ExtractInt(args, "slot");
            Material mat;
            if (target == "instance")
            {
                var objectPath = JsonHelper.ExtractString(args, "object_path")
                    ?? throw new ArgumentException("object_path is required for target=instance");
                var go = ComponentSerializer.FindObject(objectPath)
                    ?? throw new InvalidOperationException(ErrorHelper.ObjectNotFound(objectPath));
                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) throw new InvalidOperationException($"No Renderer on: {objectPath}");
                var mats = renderer.materials;
                if (slot < 0 || slot >= mats.Length)
                    throw new ArgumentException($"Slot {slot} out of range (0-{mats.Length - 1})");
                mat = mats[slot];
            }
            else
            {
                mat = ResolveMaterial(args, slot);
            }
            var prop = JsonHelper.ExtractString(args, "prop")
                ?? throw new ArgumentException("prop is required");
            var value = JsonHelper.ExtractString(args, "value")
                ?? throw new ArgumentException("value is required");

            // Special case: renderQueue
            if (prop == "renderQueue")
            {
                if (!int.TryParse(value, out int rq))
                    throw new ArgumentException($"renderQueue must be an integer, got: {value}");
                Undo.RecordObject(mat, "Set Render Queue");
                mat.renderQueue = rq;
                EditorUtility.SetDirty(mat);
                return $"ok: renderQueue={rq}";
            }

            // Special case: change shader
            if (prop == "shader")
            {
                var sh = Shader.Find(value)
                    ?? throw new InvalidOperationException($"Shader not found: {value}");
                Undo.RecordObject(mat, "Set Shader");
                mat.shader = sh;
                EditorUtility.SetDirty(mat);
                return $"ok: shader={value}";
            }

            int idx = mat.shader.FindPropertyIndex(prop);

            // Keyword (e.g. _EMISSION) — not in shader property block
            if (idx < 0)
            {
                if (value == "true") { Undo.RecordObject(mat, "Enable Keyword"); mat.EnableKeyword(prop); EditorUtility.SetDirty(mat); return $"ok: {prop}=enabled"; }
                if (value == "false") { Undo.RecordObject(mat, "Disable Keyword"); mat.DisableKeyword(prop); EditorUtility.SetDirty(mat); return $"ok: {prop}=disabled"; }
                throw new InvalidOperationException($"Property not found: {prop}");
            }

            Undo.RecordObject(mat, "Set Material Property");
            ShaderHelper.ApplyProperty(mat, prop, mat.shader.GetPropertyType(idx), value);

            EditorUtility.SetDirty(mat);
            return $"ok: {prop}={value}\nmutated: {(target == "instance" ? "renderer_instance" : "shared_asset")}\nasset_modified: {(target != "instance" ? "true" : "false")}";
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
        private static string GetShaderErrors(string args)
        {
            var path = JsonHelper.ExtractString(args, "path")
                ?? throw new ArgumentException("path (shader asset path) is required");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            if (shader == null) throw new InvalidOperationException($"Shader not found: {path}");
            if (!ShaderUtil.ShaderHasError(shader)) return "ok: no errors";
            var msgs = ShaderUtil.GetShaderMessages(shader);
            var sb = new StringBuilder();
            sb.AppendLine($"errors: {msgs.Length}");
            foreach (var m in msgs)
                sb.AppendLine($"L{m.line}: {m.message} [{m.severity}]");
            return sb.ToString().TrimEnd();
        }

        private static string ListShaders(string args)
        {
            var filter = JsonHelper.ExtractString(args, "filter") ?? "";
            var sb = new StringBuilder();
            var guids = AssetDatabase.FindAssets("t:Shader" + (filter.Length > 0 ? " " + filter : ""));
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var s = AssetDatabase.LoadAssetAtPath<Shader>(p);
                sb.AppendLine(s != null ? $"{p} [{s.name}]" : p);
            }
            return sb.Length == 0 ? "(none)" : sb.ToString().TrimEnd();
        }

        private static string SetFields(string args)
        {
            var slot = JsonHelper.ExtractInt(args, "slot");
            var mat = ResolveMaterial(args, slot);
            var fieldsStr = JsonHelper.ExtractString(args, "value")
                ?? throw new ArgumentException("value (newline-separated prop=val) is required");
            Undo.RecordObject(mat, "Set Material Fields");
            int count = 0;
            foreach (var line in fieldsStr.Split('\n'))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;
                var eqIdx = trimmed.IndexOf('=');
                if (eqIdx < 0) continue;
                var pName = trimmed.Substring(0, eqIdx).Trim();
                var pVal = trimmed.Substring(eqIdx + 1).Trim();

                if (pName == "renderQueue")
                {
                    mat.renderQueue = int.Parse(pVal, CultureInfo.InvariantCulture);
                    count++;
                    continue;
                }
                if (pName == "shader")
                {
                    var sh = Shader.Find(pVal);
                    if (sh != null) { mat.shader = sh; count++; }
                    continue;
                }

                int idx = mat.shader.FindPropertyIndex(pName);
                if (idx < 0) continue;
                ShaderHelper.ApplyProperty(mat, pName, mat.shader.GetPropertyType(idx), pVal);
                count++;
            }
            EditorUtility.SetDirty(mat);
            return $"ok: {count} properties set";
        }
    }
}
