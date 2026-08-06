using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class AssetDatabaseHelper
    {
        const int MaxFindResults = 200;
        static readonly string[] ValidActions = { "find", "get_info", "create", "move", "validate_move", "duplicate", "delete", "get_dependencies", "find_dependents", "import_settings", "export_package", "import_package", "read_text", "write_text", "reimport" };

        internal static string Execute(string action, string argsJson)
        {
            switch (action)
            {
                case "find":            return Find(argsJson);
                case "get_info":        return GetInfo(argsJson);
                case "create":          return Create(argsJson);
                case "move":            return Move(argsJson);
                case "validate_move":   return ValidateMove(argsJson);
                case "duplicate":       return Duplicate(argsJson);
                case "delete":          return Delete(argsJson);
                case "get_dependencies":return GetDependencies(argsJson);
                case "find_dependents": return FindDependents(argsJson);
                case "import_settings": return ImportSettings(argsJson);
                case "export_package": return ExportPackage(argsJson);
                case "import_package": return ImportPackage(argsJson);
                case "read_text":      return ReadText(argsJson);
                case "write_text":     return WriteText(argsJson);
                case "reimport":       return Reimport(argsJson);
                default:                throw new System.Exception(ErrorHelper.InvalidAction(action, ValidActions));
            }
        }

        static string Find(string argsJson)
        {
            var type    = JsonHelper.ExtractString(argsJson, "type");
            var name    = JsonHelper.ExtractString(argsJson, "name");
            var folder  = JsonHelper.ExtractString(argsJson, "folder");
            var labels  = JsonHelper.ExtractString(argsJson, "labels");

            var filter = new StringBuilder();
            if (!string.IsNullOrEmpty(type))   filter.Append("t:").Append(type);
            if (!string.IsNullOrEmpty(name))   { if (filter.Length > 0) filter.Append(' '); filter.Append(name); }
            if (!string.IsNullOrEmpty(labels))
            {
                foreach (var lbl in labels.Split(','))
                {
                    var l = lbl.Trim();
                    if (l.Length > 0) { if (filter.Length > 0) filter.Append(' '); filter.Append("l:").Append(l); }
                }
            }

            if (filter.Length == 0 && string.IsNullOrEmpty(folder))
                throw new System.Exception("At least one of type/name/folder/labels is required for find");

            var searchFolders = string.IsNullOrEmpty(folder) ? null : new[] { folder };
            var guids = AssetDatabase.FindAssets(filter.ToString(), searchFolders);

            var sb = new StringBuilder();
            int count = 0;
            foreach (var guid in guids)
            {
                if (count >= MaxFindResults) { sb.Append($"\n({guids.Length - MaxFindResults} more...)"); break; }
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(AssetDatabase.GUIDToAssetPath(guid));
                count++;
            }
            return sb.Length == 0 ? "(no results)" : sb.ToString();
        }

        static string GetInfo(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");

            var mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (mainType == null) throw new System.Exception($"Asset not found: {path}");

            var guid = AssetDatabase.AssetPathToGUID(path);
            var fullPath = Path.GetFullPath(path);
            var size = File.Exists(fullPath) ? new FileInfo(fullPath).Length : 0;
            var deps = AssetDatabase.GetDependencies(path, false);

            var sb = new StringBuilder();
            sb.Append("type: ").Append(mainType.Name).Append('\n');
            sb.Append("guid: ").Append(guid).Append('\n');
            sb.Append("size: ").Append(size).Append('\n');
            sb.Append("dependencies: ").Append(deps.Length).Append('\n');
            foreach (var d in deps) sb.Append(d).Append('\n');
            return sb.ToString().TrimEnd();
        }

        static void ValidatePath(string path)
        {
            if (!path.StartsWith("Assets/") && !path.StartsWith("Packages/"))
                throw new System.ArgumentException($"Path must start with Assets/ or Packages/: {path}");
        }

        static string Create(string argsJson)
        {
            var type = JsonHelper.ExtractString(argsJson, "type");
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");
            if (string.IsNullOrEmpty(type)) throw new System.Exception("type is required");
            ValidatePath(path);

            if (type == "Folder")
            {
                var parent = Path.GetDirectoryName(path).Replace('\\', '/');
                var folderName = Path.GetFileName(path);
                if (!AssetDatabase.IsValidFolder(path))
                    AssetDatabase.CreateFolder(parent, folderName);
                return "ok: " + path;
            }

            AssetHelper.EnsureDirectory(path);

            Object asset;
            switch (type)
            {
                case "Material":
                    var shader = Shader.Find("Standard")
                        ?? Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("HDRP/Lit")
                        ?? Shader.Find("Hidden/InternalErrorShader");
                    if (shader == null)
                        throw new System.Exception("No default shader found. Specify shader via 'shader' arg.");
                    asset = new Material(shader);
                    break;
                case "PhysicMaterial":
                    asset = new PhysicsMaterial();
                    break;
                case "AnimatorController":
                {
                    var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
                    AssetDatabase.SaveAssets();
                    return "ok: " + path;
                }
                case "ScriptableObject":
                {
                    var className = JsonHelper.ExtractString(argsJson, "class");
                    if (string.IsNullOrEmpty(className))
                        throw new System.Exception("class is required for ScriptableObject create");
                    var found = TypeCache.GetTypesDerivedFrom<ScriptableObject>()
                        .FirstOrDefault(t => t.Name == className);
                    if (found == null)
                        throw new System.Exception($"ScriptableObject type not found: {className}");
                    var so = ScriptableObject.CreateInstance(found);
                    AssetDatabase.CreateAsset(so, path);
                    AssetDatabase.SaveAssets();
                    return "ok: " + path;
                }
                default:
                    throw new System.Exception($"Unsupported create type '{type}'. Valid: Folder|Material|PhysicMaterial|AnimatorController|ScriptableObject");
            }

            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return "ok: " + path;
        }

        static string Move(string argsJson)
        {
            var source = JsonHelper.ExtractString(argsJson, "source");
            var dest   = JsonHelper.ExtractString(argsJson, "dest");
            if (string.IsNullOrEmpty(source)) throw new System.Exception("source is required");
            if (string.IsNullOrEmpty(dest))   throw new System.Exception("dest is required");
            ValidatePath(source);
            ValidatePath(dest);

            var preCheck = AssetDatabase.ValidateMoveAsset(source, dest);
            if (!string.IsNullOrEmpty(preCheck)) throw new System.Exception(preCheck);

            var error = AssetDatabase.MoveAsset(source, dest);
            if (!string.IsNullOrEmpty(error)) throw new System.Exception(error);
            return "ok: " + dest;
        }

        static string ValidateMove(string argsJson)
        {
            var source = JsonHelper.ExtractString(argsJson, "source");
            var dest   = JsonHelper.ExtractString(argsJson, "dest");
            if (string.IsNullOrEmpty(source)) throw new System.Exception("source is required");
            if (string.IsNullOrEmpty(dest))
            {
                ValidatePath(source);
                bool exists = AssetDatabase.AssetPathExists(source);
                return $"dest is absent — source '{source}' {(exists ? "exists" : "not found")}; provide dest to validate the move";
            }
            ValidatePath(source);
            ValidatePath(dest);
            var error = AssetDatabase.ValidateMoveAsset(source, dest);
            if (!string.IsNullOrEmpty(error))
                throw new System.Exception(error);
            return "ok";
        }

        static string Duplicate(string argsJson)
        {
            var source = JsonHelper.ExtractString(argsJson, "source");
            var dest   = JsonHelper.ExtractString(argsJson, "dest");
            if (string.IsNullOrEmpty(source)) throw new System.Exception("source is required");
            if (string.IsNullOrEmpty(dest))   throw new System.Exception("dest is required");
            ValidatePath(source);
            ValidatePath(dest);

            if (!AssetDatabase.CopyAsset(source, dest))
                throw new System.Exception($"CopyAsset failed: {source} → {dest}");
            return "ok";
        }

        static string Delete(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");
            ValidatePath(path);

            if (!AssetDatabase.DeleteAsset(path))
                throw new System.Exception($"DeleteAsset failed: {path}");
            return "ok";
        }

        static string GetDependencies(string argsJson)
        {
            var path      = JsonHelper.ExtractString(argsJson, "path");
            var recursive = JsonHelper.ExtractString(argsJson, "recursive");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");

            bool recurse = recursive == "true" || recursive == "True" || recursive == "1";
            var deps = AssetDatabase.GetDependencies(path, recurse);

            var sb = new StringBuilder();
            foreach (var d in deps) { if (sb.Length > 0) sb.Append('\n'); sb.Append(d); }
            return sb.Length == 0 ? "(no dependencies)" : sb.ToString();
        }

        static string ImportSettings(string argsJson)
        {
            var path  = JsonHelper.ExtractString(argsJson, "path");
            var prop  = JsonHelper.ExtractString(argsJson, "prop");
            var value = JsonHelper.ExtractString(argsJson, "value");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");

            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) throw new System.Exception($"No importer found for: {path}");

            // READ path: no value → return current setting(s)
            if (string.IsNullOrEmpty(value))
            {
                if (string.IsNullOrEmpty(prop))
                    return DumpAllImportSettings(importer);
                return ReadImportSetting(importer, prop);
            }

            // WRITE path: existing logic
            var propInfo = importer.GetType().GetProperty(prop,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (propInfo == null)
                throw new System.Exception($"Property '{prop}' not found on {importer.GetType().Name}");
            if (!propInfo.CanWrite)
                throw new System.Exception($"Property '{prop}' on {importer.GetType().Name} is read-only");

            object parsed;
            var t = propInfo.PropertyType;
            if (t == typeof(bool))
                parsed = value == "true" || value == "1";
            else if (t == typeof(int))
            {
                if (!int.TryParse(value, out int iv))
                    throw new System.Exception($"Cannot parse '{value}' as int for '{prop}'");
                parsed = iv;
            }
            else if (t == typeof(float))
            {
                if (!float.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv))
                    throw new System.Exception($"Cannot parse '{value}' as float for '{prop}'");
                parsed = fv;
            }
            else if (t.IsEnum)
            {
                try { parsed = System.Enum.Parse(t, value, ignoreCase: true); }
                catch { throw new System.Exception($"Cannot parse '{value}' as {t.Name}. Valid values: {string.Join(", ", System.Enum.GetNames(t))}"); }
            }
            else
                parsed = value;

            propInfo.SetValue(importer, parsed);
            importer.SaveAndReimport();
            return "ok";
        }

        static string ReadImportSetting(AssetImporter importer, string prop)
        {
            var propInfo = importer.GetType().GetProperty(prop,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (propInfo == null)
                throw new System.Exception($"Property '{prop}' not found on {importer.GetType().Name}");
            return $"{prop}: {propInfo.GetValue(importer)}";
        }

        static string DumpAllImportSettings(AssetImporter importer)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"type: {importer.GetType().Name}");
            foreach (var p in importer.GetType().GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!p.CanRead) continue;
                try { sb.AppendLine($"{p.Name}: {p.GetValue(importer)}"); }
                catch (System.Exception) { sb.AppendLine($"{p.Name}: (read error)"); }
            }
            return sb.ToString().TrimEnd();
        }

        static string FindDependents(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.ArgumentException("path is required");
            var guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid)) throw new System.ArgumentException($"asset not found: {path}");

            const int MAX_RESULTS = 100;
            var allPaths = AssetDatabase.GetAllAssetPaths();
            var dependents = new System.Collections.Generic.List<string>();

            foreach (var assetPath in allPaths)
            {
                if (assetPath == path) continue;
                if (!assetPath.StartsWith("Assets/")) continue;
                var deps = AssetDatabase.GetDependencies(assetPath, false);
                if (System.Array.IndexOf(deps, path) >= 0)
                {
                    dependents.Add(assetPath);
                    if (dependents.Count >= MAX_RESULTS) break;
                }
            }

            if (dependents.Count == 0) return "no dependents found";
            var sb = new StringBuilder();
            sb.AppendLine($"dependents of {path}: {dependents.Count}" + (dependents.Count >= MAX_RESULTS ? " (capped)" : ""));
            foreach (var d in dependents) sb.AppendLine(d);
            return sb.ToString().TrimEnd();
        }

        static string ExportPackage(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var output = JsonHelper.ExtractString(argsJson, "output");
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(output))
                throw new System.Exception("export_package requires 'path' and 'output'");
            var dir = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var includeDeps = JsonHelper.ExtractString(argsJson, "include_deps") != "false";
            var opts = ExportPackageOptions.Recurse;
            if (includeDeps) opts |= ExportPackageOptions.IncludeDependencies;
            AssetDatabase.ExportPackage(path, output, opts);
            return $"Exported to {output}";
        }

        static string ImportPackage(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path))
                throw new System.Exception("import_package requires 'path'");
            if (!File.Exists(path))
                throw new System.Exception($"Package not found: {path}");

            System.Collections.Generic.List<string> assetPaths;
            try { assetPaths = ReadPackageManifest(path); }
            catch { assetPaths = new System.Collections.Generic.List<string>(); }
            AssetDatabase.ImportPackage(path, false);

            var sb = new StringBuilder();
            sb.Append("ok: ").Append(assetPaths.Count).Append(" assets");
            foreach (var p in assetPaths) { sb.Append('\n'); sb.Append(p); }
            return sb.ToString();
        }

        static bool ReadExact(Stream s, byte[] buf, int offset, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = s.Read(buf, offset + total, count - total);
                if (n <= 0) return false;
                total += n;
            }
            return true;
        }

        static string ReadText(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");
            ValidatePath(path);
            var abs = Path.GetFullPath(path);
            if (!File.Exists(abs)) throw new System.Exception($"File not found: {path}");
            var content = File.ReadAllText(abs, System.Text.Encoding.UTF8);
            var size = System.Text.Encoding.UTF8.GetByteCount(content);
            const int MaxBytes = 65536;
            var truncated = size > MaxBytes;
            if (truncated) content = content.Substring(0, MaxBytes / 2) + "\n...(truncated)";
            var sb = new StringBuilder();
            sb.Append("ok:read\npath:").Append(path).Append("\nsize:").Append(size);
            if (truncated) sb.Append("\ntruncated:true");
            sb.Append("\ncontent:").Append(content);
            return sb.ToString();
        }

        static string WriteText(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            var content = JsonHelper.ExtractString(argsJson, "content") ?? "";
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");
            ValidatePath(path);
            AssetHelper.EnsureDirectory(path);
            var abs = Path.GetFullPath(path);
            File.WriteAllText(abs, content, System.Text.Encoding.UTF8);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.Default);
            var size = System.Text.Encoding.UTF8.GetByteCount(content);
            return $"ok:write\npath:{path}\nsize:{size}";
        }

        static string Reimport(string argsJson)
        {
            var path = JsonHelper.ExtractString(argsJson, "path");
            if (string.IsNullOrEmpty(path)) throw new System.Exception("path is required");
            ValidatePath(path);
            if (AssetDatabase.GetMainAssetTypeAtPath(path) == null)
                throw new System.Exception($"Asset not found: {path}");
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return $"ok:reimport\npath:{path}";
        }

        static System.Collections.Generic.List<string> ReadPackageManifest(string packagePath)
        {
            var result = new System.Collections.Generic.List<string>();
            using var fs = File.OpenRead(packagePath);
            using var gz = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress);
            var header = new byte[512];
            while (ReadExact(gz, header, 0, 512))
            {
                var name = Encoding.ASCII.GetString(header, 0, 100).TrimEnd('\0');
                if (string.IsNullOrEmpty(name)) break;
                var sizeStr = Encoding.ASCII.GetString(header, 124, 12).TrimEnd('\0').Trim();
                long size = string.IsNullOrEmpty(sizeStr) ? 0 : System.Convert.ToInt64(sizeStr, 8);

                if (name.EndsWith("/pathname") && size > 0 && size <= 65536)
                {
                    var buf = new byte[size];
                    if (!ReadExact(gz, buf, 0, (int)size)) break;
                    var assetPath = Encoding.UTF8.GetString(buf).Trim();
                    assetPath = assetPath.Replace('\\', '/');
                    if (assetPath.Contains("..")) continue;
                    if (assetPath.StartsWith("Assets/") || assetPath.StartsWith("Packages/"))
                        result.Add(assetPath);
                    long padded = ((size + 511) / 512) * 512 - size;
                    if (padded > 0) ReadExact(gz, new byte[padded], 0, (int)padded);
                }
                else if (size > 0)
                {
                    long padded = ((size + 511) / 512) * 512;
                    var skip = new byte[65536];
                    long rem = padded;
                    while (rem > 0) { int n = gz.Read(skip, 0, (int)System.Math.Min(rem, skip.Length)); if (n <= 0) break; rem -= n; }
                }
            }
            return result;
        }
    }
}
