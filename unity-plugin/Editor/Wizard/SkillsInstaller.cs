using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.PackageManager;

namespace UnityMCP.Editor.Wizard
{
    public static class SkillsInstaller
    {
        public static string FindSource()
        {
            var info = PackageInfo.FindForAssembly(typeof(SkillsInstaller).Assembly);
            if (info == null) return null;
            var path = Path.Combine(info.resolvedPath, "ClientSkills");
            return Directory.Exists(path) ? path : null;
        }

        public static string[] ListFiles(string sourceDir)
        {
            var result = new List<string>();
            foreach (var f in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                var ext = Path.GetExtension(f).ToLowerInvariant();
                if (ext != ".md" && ext != ".py") continue;
                result.Add(f.Substring(sourceDir.Length + 1).Replace('\\', '/'));
            }
            return result.ToArray();
        }

        public static string MapDestination(string projectRoot, string relativePath)
        {
            var slash = relativePath.IndexOf('/');
            if (slash < 0) return null;
            var folder = relativePath.Substring(0, slash);
            var file   = relativePath.Substring(slash + 1);
            switch (folder)
            {
                case "skills":  return Path.Combine(projectRoot, ".claude", "skills", file);
                case "agents":  return Path.Combine(projectRoot, ".claude", "agents", file);
                case "scripts": return Path.Combine(projectRoot, ".codex", "scripts", file);
                default:        return null;
            }
        }

        public static InstallResult Install(string sourceDir, string projectRoot, bool overwrite = false)
        {
            var errors = new List<string>();
            int copied = 0, skipped = 0;

            foreach (var rel in ListFiles(sourceDir))
            {
                var src = Path.Combine(sourceDir, rel.Replace('/', Path.DirectorySeparatorChar));
                var dst = MapDestination(projectRoot, rel);
                if (dst == null) { errors.Add($"No mapping for {rel}"); continue; }

                if (new FileInfo(src).Length == 0) { errors.Add($"Empty: {rel}"); continue; }

                if (!overwrite && File.Exists(dst)) { skipped++; continue; }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(src, dst, true);
                    copied++;
                }
                catch (Exception ex) { errors.Add($"{rel}: {ex.Message}"); }
            }

            return new InstallResult(copied, skipped, errors.ToArray());
        }

        public static bool HasCodexDir(string projectRoot) =>
            Directory.Exists(Path.Combine(projectRoot, ".codex"));

        public static string ReadVersionFile(string projectRoot)
        {
            var path = VersionFilePath(projectRoot);
            return File.Exists(path) ? File.ReadAllText(path).Trim() : null;
        }

        public static void WriteVersionFile(string projectRoot, string version)
        {
            var path = VersionFilePath(projectRoot);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, version);
        }

        private static string VersionFilePath(string projectRoot) =>
            Path.Combine(projectRoot, ".claude", ".unity-mcp-skills-version");
    }

    public readonly struct InstallResult
    {
        public readonly int Copied;
        public readonly int Skipped;
        public readonly string[] Errors;
        public bool IsSuccess => Errors.Length == 0;

        public InstallResult(int copied, int skipped, string[] errors)
        {
            Copied = copied; Skipped = skipped; Errors = errors;
        }
    }
}
