using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor.PackageManager;

namespace UnityMCP.Editor.Wizard
{
    public static class SkillsInstaller
    {
        private static readonly IReadOnlyDictionary<string, IReadOnlyCollection<string>> LegacyFileBlobs =
            new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
            {
                { "agents/playmode-tester.md", Hashes(
                    "30eb33c0e069a4afb6388b435cfb35cbfbaaa37f",
                    "38da369c451e03133780689152f436e2c4b74f5c") },
                { "agents/unity-editor-developer.md", Hashes(
                    "cffe4d1b9cfdd586d86db7ef62c8defebc688327",
                    "038c467734bd44fbcba23774d091d3d287fb4250") },
                { "agents/unity-test-reviewer.md", Hashes(
                    "0988c9a8ca580951c7fbe70695b15b59a15fc9af") },
                { "skills/csharp-unity.md", Hashes(
                    "0bcb76fa205de60ccfdb9abe79bbd58363f856df",
                    "656a29142dee25c467a4a146eaaff6bdd7bc7cad") },
                { "skills/playmode-verification.md", Hashes("44a63921f280d536b2f4a418cb3c5f9a5798dc7c") },
                { "skills/playtest-dsl.md", Hashes("f66e511229aa0f1bf810a8eb594359463906d8a8") },
                { "skills/testing-tdd.md", Hashes("376fe65dc36f48242108c86802ce1a239ac20074") },
                { "skills/token-optimization.md", Hashes(
                    "28d63739dbeddf081db7f7d40050b278ef68f6aa",
                    "f65b8eb4d4911d4c30c21244aa7002f984481f2d") },
                { "skills/unity-animation.md", Hashes("44f5b569bf1ce8106e32752a3a6aa0baeab7f826") },
                { "skills/unity-animator.md", Hashes("ee72e514a6e539cda9f1b7f6e3a23783bde02da3") },
                { "skills/unity-assets.md", Hashes("7e8277689acbccc1efac1e8c609f536b59ee2f59") },
                { "skills/unity-biome-mcp-reference.md", Hashes("5c089e641dca9e15ed66c6bf65c97743487cc577") },
                { "skills/unity-mcp-reference.md", Hashes("e74f9889ca19c2534eac0bfd06e2e6d946991ef1") },
                { "skills/unity-code-intel.md", Hashes(
                    "c360d1e5abaa19425763fb85bdb3d6b5d7fc8266",
                    "aaa5f35488d914ed0afe9a282d60fc8e7170e176",
                    "6f07a6ab3d2d5902e467a7fc9e8e3ebff8e60fd7") },
                { "skills/unity-components.md", Hashes("8be2446480c7e35c7314a7b3f2115d6e56e973dc") },
                { "skills/unity-debugging.md", Hashes("748e3f006214f5363e2672fac060ea658c5dbfa1") },
                { "skills/unity-efficiency.md", Hashes("e286935ccfc6a04ca646d062237f383df4aafe6a") },
                { "skills/unity-hierarchy.md", Hashes("39f4bbf631c75e158982614f4385c5486afd2c5d") },
                { "skills/unity-intent.md", Hashes(
                    "4edae5680fd481b7aabf76a30d297044fbe8d09b",
                    "b5425ff3ca9a34032c24b36143769b63c6895252") },
                { "skills/unity-particles.md", Hashes("f9295a7d74aeb1117e3d67a810685e3a0bb2a681") },
                { "skills/unity-performance.md", Hashes("d3b1982de8d12b9470ce226ac2ee04613601ac72") },
                { "skills/unity-physics.md", Hashes(
                    "d8ce85983cff1581cd8ee10516e836cd7cb54c7e",
                    "1a09874e9927e92fda09e616951d6b28f509e303") },
                { "skills/unity-scene-ui.md", Hashes("45c1c1dde20faad53ee376f0babdcf2fd28b5dd1") },
                { "skills/unity-session.md", Hashes(
                    "77a54a284b23fab133b58a84d23ab045f415e605",
                    "d285472c011e6cebc9e80ee38e7c8868ceff6191") },
                { "skills/unity-shaders.md", Hashes("0f44903d3e2045bcf822c9e6cb3f8b8ccf9900d7") },
                { "skills/unity-testing.md", Hashes(
                    "39650bc93cfd75419863eca1f7f5cd8cb025c144",
                    "2a874bc1e2daa3569d077f8c343282fb51c25a16") },
                { "skills/unity-testing-verification/references/test-authoring.md", Hashes(
                    "a06497388792403ae2e34cef65324140ebad446c",
                    "f6ed9e999cba3f8aefa653fa2013fc40ce53474a") },
                { "skills/unity-timeline.md", Hashes("622e09329fecdcebda756a7b3bc2ef726333801a") },
                { "skills/unity-ui-authoring/SKILL.md", Hashes(
                    "73848d830e8d979b333c5398cd25d57ea0b2bd1d") },
            };

        private static IReadOnlyCollection<string> Hashes(params string[] values) =>
            new HashSet<string>(values, StringComparer.Ordinal);

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
            var root = Path.GetFullPath(sourceDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var f in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                var relative = f.Substring(root.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
                if (!IsInstallable(relative)) continue;
                result.Add(relative);
            }
            result.Sort(StringComparer.Ordinal);
            return result.ToArray();
        }

        public static string MapDestination(string projectRoot, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath)) return null;
            relativePath = relativePath.Replace('\\', '/');
            var slash = relativePath.IndexOf('/');
            if (slash < 0) return null;
            var folder = relativePath.Substring(0, slash);
            var file = relativePath.Substring(slash + 1);
            if (string.IsNullOrWhiteSpace(file) || Path.IsPathRooted(file)) return null;

            string targetRoot;
            switch (folder)
            {
                case "skills":  targetRoot = Path.Combine(projectRoot, ".claude", "skills"); break;
                case "agents":  targetRoot = Path.Combine(projectRoot, ".claude", "agents"); break;
                case "scripts": targetRoot = Path.Combine(projectRoot, ".codex", "scripts"); break;
                default: return null;
            }

            var root = Path.GetFullPath(targetRoot);
            var destination = Path.GetFullPath(Path.Combine(root, file.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                         + Path.DirectorySeparatorChar;
            return destination.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                ? destination
                : null;
        }

        private static bool IsInstallable(string relativePath)
        {
            var normalized = relativePath.Replace('\\', '/');
            var slash = normalized.IndexOf('/');
            if (slash < 0) return false;

            var root = normalized.Substring(0, slash);
            if (root != "skills" && root != "agents" && root != "scripts") return false;

            var segments = normalized.Split('/');
            foreach (var segment in segments)
            {
                if (segment == "__pycache__") return false;
            }

            var name = segments[segments.Length - 1];
            if (name == ".DS_Store" || name.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                return false;
            return !name.EndsWith(".pyc", StringComparison.OrdinalIgnoreCase);
        }

        public static InstallResult Install(string sourceDir, string projectRoot, bool overwrite = false)
        {
            return Install(sourceDir, projectRoot, overwrite, LegacyFileBlobs);
        }

        internal static InstallResult Install(
            string sourceDir,
            string projectRoot,
            bool overwrite,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> legacyFileBlobs,
            Action<InstallMutation, string> afterMutation = null,
            Action<string> cleanupTransaction = null)
        {
            try
            {
                return InstallCore(
                    sourceDir,
                    projectRoot,
                    overwrite,
                    legacyFileBlobs,
                    afterMutation,
                    cleanupTransaction);
            }
            catch (Exception ex)
            {
                return new InstallResult(
                    0,
                    0,
                    0,
                    new[] { $"Installation preflight failed: {ex.Message}" },
                    stateRestored: true);
            }
        }

        private static InstallResult InstallCore(
            string sourceDir,
            string projectRoot,
            bool overwrite,
            IReadOnlyDictionary<string, IReadOnlyCollection<string>> legacyFileBlobs,
            Action<InstallMutation, string> afterMutation,
            Action<string> cleanupTransaction)
        {
            var errors = new List<string>();
            var installableFiles = ListFiles(sourceDir);
            var migratable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var destinations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var sourceRoot = Path.GetFullPath(sourceDir);
            var projectRootFull = Path.GetFullPath(projectRoot);

            foreach (var legacy in legacyFileBlobs)
            {
                var destination = MapDestination(projectRoot, legacy.Key);
                if (destination == null || !File.Exists(destination)) continue;
                if (!IsPathSafe(projectRootFull, destination))
                {
                    errors.Add($"Unsafe legacy path: {legacy.Key}");
                    continue;
                }
                if (legacy.Value.Contains(ComputeGitBlobSha1(destination)))
                    migratable.Add(destination);
                else
                    errors.Add($"Modified legacy file requires review: {legacy.Key}");
            }

            foreach (var rel in installableFiles)
            {
                var src = Path.Combine(sourceDir, rel.Replace('/', Path.DirectorySeparatorChar));
                var dst = MapDestination(projectRoot, rel);
                if (dst == null) { errors.Add($"No mapping for {rel}"); continue; }
                if (!IsPathSafe(sourceRoot, src) || !IsPathSafe(projectRootFull, dst))
                {
                    errors.Add($"Unsafe path: {rel}");
                    continue;
                }
                destinations.Add(dst);

                if (new FileInfo(src).Length == 0) { errors.Add($"Empty: {rel}"); continue; }
                if (!File.Exists(dst) || migratable.Contains(dst) || FilesEqual(src, dst)) continue;
                if (!overwrite) errors.Add($"Existing file differs; enable overwrite or review it: {rel}");
            }

            if (errors.Count > 0)
                return new InstallResult(0, 0, 0, errors.ToArray());

            int copied = 0, skipped = 0, removed = 0;
            var transactionRoot = Path.Combine(
                projectRootFull,
                ".unity-biome-skills-" + Guid.NewGuid().ToString("N"));
            var stageRoot = Path.Combine(transactionRoot, "stage");
            var backupRoot = Path.Combine(transactionRoot, "backup");
            var applied = new List<AppliedFile>();
            var createdDirectories = new List<string>();
            var preserveTransaction = false;
            var stateRestored = true;

            try
            {
                foreach (var rel in installableFiles)
                {
                    var src = Path.Combine(sourceRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    var staged = Path.Combine(stageRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(staged));
                    File.Copy(src, staged, true);
                }

                foreach (var rel in installableFiles)
                {
                    var src = Path.Combine(sourceRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    var dst = MapDestination(projectRootFull, rel);
                    if (File.Exists(dst) && FilesEqual(src, dst)) { skipped++; continue; }
                    if (!IsPathSafe(projectRootFull, dst))
                        throw new IOException($"Unsafe destination changed during install: {rel}");

                    EnsureTransactionDirectories(
                        projectRootFull,
                        Path.GetDirectoryName(dst),
                        createdDirectories);
                    var staged = Path.Combine(stageRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    var backup = Path.Combine(backupRoot, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(dst))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(backup));
                        applied.Add(new AppliedFile(dst, backup, staged));
                        afterMutation?.Invoke(InstallMutation.DestinationJournaled, dst);
                        File.Replace(staged, dst, backup);
                    }
                    else
                    {
                        applied.Add(new AppliedFile(dst, null, staged));
                        afterMutation?.Invoke(InstallMutation.DestinationJournaled, dst);
                        File.Move(staged, dst);
                    }
                    copied++;
                    afterMutation?.Invoke(InstallMutation.DestinationApplied, dst);
                }

                foreach (var path in migratable)
                {
                    if (destinations.Contains(path)) continue;
                    if (!IsPathSafe(projectRootFull, path))
                        throw new IOException($"Unsafe legacy path changed during install: {path}");
                    var relative = path.Substring(projectRootFull.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    var backup = Path.Combine(backupRoot, "removed", relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(backup));
                    applied.Add(new AppliedFile(path, backup, null));
                    afterMutation?.Invoke(InstallMutation.LegacyRemovalJournaled, path);
                    File.Move(path, backup);
                    removed++;
                    afterMutation?.Invoke(InstallMutation.LegacyRemoved, path);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Installation failed: {ex.Message}");
                for (var i = applied.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        var item = applied[i];
                        var backupExists = item.Backup != null && File.Exists(item.Backup);
                        var newDestinationApplied = item.Backup == null &&
                            item.Staged != null &&
                            !File.Exists(item.Staged);
                        if (!backupExists && !newDestinationApplied) continue;

                        if (File.Exists(item.Destination)) File.Delete(item.Destination);
                        if (backupExists)
                        {
                            Directory.CreateDirectory(Path.GetDirectoryName(item.Destination));
                            File.Move(item.Backup, item.Destination);
                        }
                    }
                    catch (Exception rollbackError)
                    {
                        preserveTransaction = true;
                        stateRestored = false;
                        errors.Add(
                            $"Rollback failed for {applied[i].Destination}: {rollbackError.Message}. " +
                            $"Recovery files: {transactionRoot}");
                    }
                }
                for (var i = createdDirectories.Count - 1; i >= 0; i--)
                {
                    var directory = createdDirectories[i];
                    try
                    {
                        if (Directory.Exists(directory) &&
                            !Directory.EnumerateFileSystemEntries(directory).Any())
                            Directory.Delete(directory);
                    }
                    catch (Exception rollbackError)
                    {
                        preserveTransaction = true;
                        stateRestored = false;
                        errors.Add(
                            $"Rollback failed for {directory}: {rollbackError.Message}. " +
                            $"Recovery files: {transactionRoot}");
                    }
                }
                if (stateRestored)
                {
                    copied = 0;
                    removed = 0;
                }
            }
            finally
            {
                if (!preserveTransaction && Directory.Exists(transactionRoot))
                {
                    try
                    {
                        if (cleanupTransaction == null)
                            Directory.Delete(transactionRoot, true);
                        else
                            cleanupTransaction(transactionRoot);
                    }
                    catch (Exception cleanupError)
                    {
                        errors.Add(
                            $"WARNING: Transaction cleanup incomplete: {cleanupError.Message}. " +
                            $"Recovery files: {transactionRoot}");
                    }
                }
            }

            return new InstallResult(
                copied,
                skipped,
                removed,
                errors.ToArray(),
                stateRestored,
                stateRestored ? null : transactionRoot);
        }

        internal enum InstallMutation
        {
            DestinationJournaled,
            DestinationApplied,
            LegacyRemovalJournaled,
            LegacyRemoved,
        }

        private static void EnsureTransactionDirectories(
            string projectRoot,
            string directory,
            List<string> createdDirectories)
        {
            var missing = new Stack<string>();
            var current = directory;
            while (!Directory.Exists(current))
            {
                if (!IsPathSafe(projectRoot, current))
                    throw new IOException($"Unsafe destination directory: {current}");
                missing.Push(current);
                var parent = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(parent) || parent == current)
                    throw new IOException($"Destination directory escapes project root: {directory}");
                current = parent;
            }

            while (missing.Count > 0)
            {
                var path = missing.Pop();
                if (Directory.Exists(path)) continue;
                createdDirectories.Add(path);
                Directory.CreateDirectory(path);
            }
        }

        private static bool IsPathSafe(string rootPath, string candidatePath)
        {
            var root = Path.GetFullPath(rootPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidate = Path.GetFullPath(candidatePath);
            var prefix = root + Path.DirectorySeparatorChar;
            if (!candidate.Equals(root, StringComparison.OrdinalIgnoreCase) &&
                !candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return false;

            var current = root;
            if (HasReparsePoint(current)) return false;
            var relative = candidate.Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            foreach (var segment in relative.Split(
                         new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if (HasReparsePoint(current)) return false;
            }
            return true;
        }

        private static bool HasReparsePoint(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return false;
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }

        internal static string ComputeGitBlobSha1(string path)
        {
            var content = File.ReadAllBytes(path);
            var prefix = Encoding.UTF8.GetBytes($"blob {content.Length}\0");
            var input = new byte[prefix.Length + content.Length];
            Buffer.BlockCopy(prefix, 0, input, 0, prefix.Length);
            Buffer.BlockCopy(content, 0, input, prefix.Length, content.Length);
            using (var sha1 = SHA1.Create())
                return BitConverter.ToString(sha1.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
        }

        private static bool FilesEqual(string left, string right)
        {
            var leftInfo = new FileInfo(left);
            var rightInfo = new FileInfo(right);
            if (leftInfo.Length != rightInfo.Length) return false;

            const int bufferSize = 8192;
            var leftBuffer = new byte[bufferSize];
            var rightBuffer = new byte[bufferSize];
            using (var leftStream = File.OpenRead(left))
            using (var rightStream = File.OpenRead(right))
            {
                while (true)
                {
                    var leftRead = leftStream.Read(leftBuffer, 0, bufferSize);
                    var rightRead = rightStream.Read(rightBuffer, 0, bufferSize);
                    if (leftRead != rightRead) return false;
                    if (leftRead == 0) return true;
                    for (var i = 0; i < leftRead; i++)
                    {
                        if (leftBuffer[i] != rightBuffer[i]) return false;
                    }
                }
            }
        }

        public static bool HasCodexDir(string projectRoot) =>
            Directory.Exists(Path.Combine(projectRoot, ".codex"));

        public static string ReadVersionFile(string projectRoot)
        {
            var path = VersionFilePath(projectRoot);
            if (!File.Exists(path)) return null;
            var value = File.ReadAllText(path, Encoding.UTF8).Trim();
            return IsValidVersionMarker(value) ? value : null;
        }

        public static void WriteVersionFile(string projectRoot, string version)
        {
            if (!IsValidVersionMarker(version))
                throw new ArgumentException("Version marker must be non-empty.", nameof(version));
            var path = VersionFilePath(projectRoot);
            if (!IsPathSafe(projectRoot, path))
                throw new IOException($"Unsafe version marker path: {path}");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(
                           temporary,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           4096,
                           FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    writer.Write(version);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(path))
                    File.Replace(temporary, path, null);
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static void DeleteVersionFile(string projectRoot)
        {
            var path = VersionFilePath(projectRoot);
            if (!IsPathSafe(projectRoot, path))
                throw new IOException($"Unsafe version marker path: {path}");
            if (File.Exists(path)) File.Delete(path);
        }

        private static bool IsValidVersionMarker(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128) return false;
            return value.All(c => !char.IsControl(c));
        }

        private static string VersionFilePath(string projectRoot) =>
            Path.Combine(projectRoot, ".claude", ".unity-biome-mcp-skills-version");

        private readonly struct AppliedFile
        {
            public readonly string Destination;
            public readonly string Backup;
            public readonly string Staged;

            public AppliedFile(string destination, string backup, string staged)
            {
                Destination = destination;
                Backup = backup;
                Staged = staged;
            }
        }
    }

    public readonly struct InstallResult
    {
        public readonly int Copied;
        public readonly int Skipped;
        public readonly int Removed;
        public readonly string[] Errors;
        public readonly bool StateRestored;
        public readonly string RecoveryPath;
        public bool IsSuccess => Errors.All(error =>
            error.StartsWith("WARNING: ", StringComparison.Ordinal));

        public InstallResult(
            int copied,
            int skipped,
            int removed,
            string[] errors,
            bool stateRestored = true,
            string recoveryPath = null)
        {
            Copied = copied;
            Skipped = skipped;
            Removed = removed;
            Errors = errors;
            StateRestored = stateRestored;
            RecoveryPath = recoveryPath;
        }
    }
}
