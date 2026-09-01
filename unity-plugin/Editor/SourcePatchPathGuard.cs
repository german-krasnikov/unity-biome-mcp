using System;
using System.IO;

namespace UnityMCP.Editor
{
    /// <summary>Pre-effect boundary check for source_patch_write (ROI
    /// Top-5 #1). Pure string/Path logic, no Unity API — projectRoot is
    /// injected so this stays fully unit-testable without a live Editor
    /// domain. Called once, from SourcePatchModePolicy.TryApplyWrite,
    /// before any Read/AcquireLease/Write effect.</summary>
    internal static class SourcePatchPathGuard
    {
        internal static void Validate(string path, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("source patch path rejected: path is required");
            if (!path.EndsWith(".cs", StringComparison.Ordinal))
                throw new ArgumentException($"source patch path rejected: must end with .cs: {path}");
            if (Path.IsPathRooted(path))
                throw new ArgumentException($"source patch path rejected: must be project-relative, not absolute: {path}");
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                throw new ArgumentException($"source patch path rejected: must start with Assets/: {path}");

            foreach (var segment in path.Replace('\\', '/').Split('/'))
            {
                if (segment == "..")
                    throw new ArgumentException($"source patch path rejected: must not contain '..' segments: {path}");
            }

            var assetsRoot = Path.GetFullPath(Path.Combine(projectRoot, "Assets")) + Path.DirectorySeparatorChar;
            var resolved = Path.GetFullPath(Path.Combine(projectRoot, path));
            if (!resolved.StartsWith(assetsRoot, StringComparison.Ordinal))
                throw new ArgumentException($"source patch path rejected: resolves outside Assets/: {path}");
        }
    }
}
