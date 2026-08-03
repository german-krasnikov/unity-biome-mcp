using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.TestRuns
{
    public sealed class TestRunAssetCleanupReport
    {
        public string Warning = "";
        public string QuarantinedLedgerPath = "";

        public bool HasWarning => !string.IsNullOrEmpty(Warning);
    }

    /// <summary>
    /// Durable ownership boundary for test-created assets. Fixture-local delegates
    /// are only an optimization; this ledger and the reserved root survive reload.
    /// </summary>
    public static class TestRunAssetOwnership
    {
        public const string Root = "Assets/TestsTemp";
        public const string OwnedRunIdSessionKey = "UnityMCP_active_test_run_id_v1";

        private static readonly object Gate = new object();
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false, true);
        private static readonly HashSet<string> CompileAffectingExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".asmdef", ".asmref", ".rsp", ".dll", ".mdb", ".pdb",
                ".aar", ".jar"
            };

        public static string RegisterForActiveRun(string assetPath)
        {
            var canonical = RequireOwnedAssetPath(assetPath);
            RequireNonCompilingAssetPath(canonical);
            var runId = RequireActiveRunId();
            var ledgerPath = LedgerPath(runId);
            lock (Gate)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ledgerPath));
                using (var stream = new FileStream(
                           ledgerPath, FileMode.Append, FileAccess.Write, FileShare.Read,
                           4096, FileOptions.WriteThrough))
                using (var writer = new StreamWriter(stream, Utf8NoBom, 1024, true))
                {
                    writer.WriteLine(Convert.ToBase64String(Utf8NoBom.GetBytes(canonical)));
                    writer.Flush();
                    stream.Flush(true);
                }
            }
            return canonical;
        }

        /// <summary>
        /// Ensures the reserved ownership container exists. The container itself
        /// is never fixture-owned, registered in a ledger, or deletable through
        /// <see cref="DeleteOwnedAsset"/>.
        /// </summary>
        public static string EnsureRoot()
        {
            MigrateLegacyAutoSuffixedRoots();
            if (AssetDatabase.IsValidFolder(Root)) return Root;

            var absoluteRoot = Path.Combine(ProjectRoot(), Root);
            if (File.Exists(absoluteRoot))
                throw new IOException(
                    $"The test ownership root is occupied by a file: '{Root}'.");
            if (Directory.Exists(absoluteRoot))
            {
                AssetDatabase.ImportAsset(Root, ImportAssetOptions.ForceSynchronousImport);
                if (AssetDatabase.IsValidFolder(Root)) return Root;
                throw new IOException(
                    $"The existing test ownership root could not be imported: '{Root}'.");
            }

            var guid = AssetDatabase.CreateFolder("Assets", "TestsTemp");
            if (string.IsNullOrEmpty(guid) || !AssetDatabase.IsValidFolder(Root))
                throw new IOException($"Could not create test ownership root '{Root}'.");
            return Root;
        }

        public static TestRunAssetCleanupReport CleanupForActiveRun(string preserveAssetPath)
        {
            return CleanupForRun(RequireActiveRunId(), preserveAssetPath);
        }

        public static TestRunAssetCleanupReport CleanupForRun(
            string runId,
            string preserveAssetPath,
            bool allowCompileAffectingAssets = false)
        {
            RequireSafeRunId(runId);
            var preserve = string.IsNullOrEmpty(preserveAssetPath)
                ? ""
                : RequireOwnedAssetPath(preserveAssetPath, allowRunScene: true);
            if (!string.IsNullOrEmpty(preserve) && !string.Equals(
                    preserve, ExpectedRunScenePath(runId), StringComparison.Ordinal))
                throw new ArgumentException(
                    "A run-level sweep may preserve only that run's exact owned scene.",
                    nameof(preserveAssetPath));
            if (!allowCompileAffectingAssets)
                RequireNoCompileAffectingAssetsInRoot();
            MigrateLegacyAutoSuffixedRoots();

            var report = new TestRunAssetCleanupReport();
            IReadOnlyList<string> registered;
            try
            {
                registered = ReadLedger(runId);
            }
            catch (Exception error)
            {
                report.Warning =
                    $"Test asset ownership ledger was corrupt for run '{runId}': {error.Message}";
                report.QuarantinedLedgerPath = QuarantineLedger(runId, ref report.Warning);
                registered = Array.Empty<string>();
            }

            SweepReservedRoot(preserve);

            var survivors = registered
                .Where(path => !IsPreserved(path, preserve) && AssetExists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (survivors.Length > 0)
                throw new IOException(
                    "Test-owned assets survived cleanup: " + string.Join(", ", survivors));

            RequireCleanReservedRoot(preserve);

            DeleteLedger(runId);
            return report;
        }

        public static string RequireOwnedAssetPath(string assetPath) =>
            RequireOwnedAssetPath(assetPath, allowRunScene: false);

        public static string RequireActiveRunScenePath(string assetPath)
        {
            var runId = RequireActiveRunId();
            var canonical = RequireOwnedAssetPath(assetPath, allowRunScene: true);
            if (!string.Equals(canonical, ExpectedRunScenePath(runId),
                    StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "The active run-owned scene hint does not match the active run identity.");
            return canonical;
        }

        public static string ExpectedRunScenePath(string runId)
        {
            RequireSafeRunId(runId);
            return Root + "/__mcp_test_run_" + runId + ".unity";
        }

        public static void DeleteOwnedAsset(string assetPath)
        {
            var canonical = RequireOwnedAssetPath(assetPath);
            RequireNonCompilingAssetPath(canonical);
            DeleteAssetOrFile(canonical);
        }

        public static string RequireOwnedAssetPath(
            string assetPath,
            bool allowRunScene)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                throw new ArgumentException("A test-owned asset path is required.", nameof(assetPath));

            var canonical = assetPath.Replace('\\', '/');
            var segments = canonical.Split('/');
            var invalid = !string.Equals(canonical, canonical.Trim(), StringComparison.Ordinal) ||
                          Path.IsPathRooted(canonical) ||
                          segments.Any(segment => string.IsNullOrEmpty(segment) ||
                                                  segment == "." || segment == "..") ||
                          canonical.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) ||
                          string.Equals(canonical, Root, StringComparison.OrdinalIgnoreCase) ||
                          !canonical.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase);
            if (invalid)
                throw new ArgumentException(
                    $"Test cleanup may own only concrete assets below '{Root}'.",
                    nameof(assetPath));

            if (!canonical.StartsWith(Root + "/", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Test-owned paths must use canonical casing below '{Root}'.",
                    nameof(assetPath));

            if (!allowRunScene)
            {
                var runScene = SessionState.GetString(
                    "UnityMCP_active_owned_test_scene_v1", "").Replace('\\', '/');
                if (!string.IsNullOrEmpty(runScene) &&
                    (string.Equals(canonical, runScene, StringComparison.OrdinalIgnoreCase) ||
                     runScene.StartsWith(canonical.TrimEnd('/') + "/",
                         StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException(
                        "A fixture cannot own the run-level scene or one of its ancestors.",
                        nameof(assetPath));
            }

            return canonical;
        }

        private static string RequireActiveRunId()
        {
            var runId = SessionState.GetString(OwnedRunIdSessionKey, "");
            if (string.IsNullOrEmpty(runId))
                throw new InvalidOperationException(
                    "The UTF run has no durable UnityMCP asset ownership ledger.");
            RequireSafeRunId(runId);
            return runId;
        }

        private static void SweepReservedRoot(string preserve)
        {
            var absoluteRoot = Path.Combine(ProjectRoot(), Root);
            if (!Directory.Exists(absoluteRoot)) return;

            foreach (var entry in Directory.EnumerateFileSystemEntries(absoluteRoot).ToArray())
            {
                var relative = Root + "/" + Path.GetFileName(entry);
                if (IsPreserved(relative, preserve) || IsPreservedMeta(relative, preserve))
                    continue;
                if (relative.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(entry);
                    continue;
                }
                DeleteAssetOrFile(relative);
            }
        }

        private static void RequireNoCompileAffectingAssetsInRoot()
        {
            var absoluteRoot = Path.Combine(ProjectRoot(), Root);
            if (!Directory.Exists(absoluteRoot)) return;
            var offender = Directory.EnumerateFiles(
                    absoluteRoot, "*", SearchOption.AllDirectories)
                .FirstOrDefault(IsCompileAffectingPath);
            if (string.IsNullOrEmpty(offender)) return;
            throw new InvalidOperationException(
                "Compile-affecting test asset requires disposable-worker finalization " +
                "and cannot be deleted between ordinary tests: " + offender);
        }

        private static void RequireNonCompilingAssetPath(string assetPath)
        {
            if (IsCompileAffectingPath(assetPath))
                throw new ArgumentException(
                    "Compile-affecting assets cannot be owned by an ordinary fixture; " +
                    "use a disposable worker orchestration lane.", nameof(assetPath));
        }

        private static bool IsCompileAffectingPath(string path) =>
            CompileAffectingExtensions.Contains(Path.GetExtension(path ?? ""));

        internal static bool IsLegacyAutoSuffixedRootPath(string assetPath)
        {
            const string prefix = Root + " ";
            if (string.IsNullOrEmpty(assetPath) ||
                !assetPath.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            var suffix = assetPath.Substring(prefix.Length);
            return suffix.Length > 0 && suffix[0] >= '1' && suffix[0] <= '9' &&
                   suffix.All(value => value >= '0' && value <= '9');
        }

        private static int MigrateLegacyAutoSuffixedRoots()
        {
            return MigrateLegacyAutoSuffixedRoots(
                Path.Combine(ProjectRoot(), "Assets"),
                assetPath => AssetDatabase.IsValidFolder(assetPath) &&
                             !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath)),
                AssetDatabase.DeleteAsset);
        }

        internal static int MigrateLegacyAutoSuffixedRoots(
            string assetsDirectory,
            Func<string, bool> isImportedFolder,
            Func<string, bool> deleteAsset)
        {
            if (string.IsNullOrWhiteSpace(assetsDirectory))
                throw new ArgumentException("An Assets directory is required.",
                    nameof(assetsDirectory));
            if (isImportedFolder == null)
                throw new ArgumentNullException(nameof(isImportedFolder));
            if (deleteAsset == null)
                throw new ArgumentNullException(nameof(deleteAsset));
            if (!Directory.Exists(assetsDirectory)) return 0;

            var candidates = Directory.EnumerateDirectories(
                    assetsDirectory, "TestsTemp *", SearchOption.TopDirectoryOnly)
                .Select(absolutePath => new
                {
                    AbsolutePath = absolutePath,
                    AssetPath = "Assets/" + Path.GetFileName(absolutePath)
                })
                .Where(candidate => IsLegacyAutoSuffixedRootPath(candidate.AssetPath))
                .OrderBy(candidate => candidate.AssetPath, StringComparer.Ordinal)
                .ToArray();

            // Validate every candidate before the first deletion. A non-empty or
            // unimported lookalike is evidence, not disposable test ownership.
            foreach (var candidate in candidates)
            {
                if (!isImportedFolder(candidate.AssetPath))
                    throw new InvalidOperationException(
                        "Legacy test-root candidate is not an imported folder asset: " +
                        candidate.AssetPath);
                if (Directory.EnumerateFileSystemEntries(candidate.AbsolutePath).Any())
                    throw new IOException(
                        "Legacy test-root candidate is non-empty and was not deleted: " +
                        candidate.AssetPath);
            }

            foreach (var candidate in candidates)
            {
                if (!deleteAsset(candidate.AssetPath) ||
                    Directory.Exists(candidate.AbsolutePath))
                    throw new IOException(
                        "Could not delete empty legacy test-root folder asset: " +
                        candidate.AssetPath);
            }
            return candidates.Length;
        }

        private static void RequireCleanReservedRoot(string preserve)
        {
            var absoluteRoot = Path.Combine(ProjectRoot(), Root);
            if (!Directory.Exists(absoluteRoot)) return;
            var survivors = Directory.EnumerateFileSystemEntries(absoluteRoot)
                .Where(entry =>
                {
                    var relative = Root + "/" + Path.GetFileName(entry);
                    return !IsPreserved(relative, preserve) &&
                           !IsPreservedMeta(relative, preserve);
                })
                .Select(Path.GetFileName)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (survivors.Length > 0)
                throw new IOException(
                    "Unexpected entries survived the test ownership sweep: " +
                    string.Join(", ", survivors));
        }

        private static void DeleteAssetOrFile(string assetPath)
        {
            var absolute = Path.Combine(ProjectRoot(), assetPath);
            var imported = AssetDatabase.IsValidFolder(assetPath) ||
                           AssetDatabase.LoadMainAssetAtPath(assetPath) != null ||
                           !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(assetPath));
            if (imported)
            {
                if (!AssetDatabase.DeleteAsset(assetPath) && AssetExists(assetPath))
                    throw new IOException($"Could not delete test-owned asset '{assetPath}'.");
                return;
            }

            if (Directory.Exists(absolute))
                Directory.Delete(absolute, true);
            else if (File.Exists(absolute))
                File.Delete(absolute);
            var meta = absolute + ".meta";
            if (File.Exists(meta)) File.Delete(meta);
        }

        private static bool AssetExists(string assetPath)
        {
            var absolute = Path.Combine(ProjectRoot(), assetPath);
            return AssetDatabase.IsValidFolder(assetPath) ||
                   AssetDatabase.LoadMainAssetAtPath(assetPath) != null ||
                   File.Exists(absolute) || Directory.Exists(absolute);
        }

        private static bool IsPreserved(string candidate, string preserve)
        {
            if (string.IsNullOrEmpty(preserve)) return false;
            return string.Equals(candidate, preserve, StringComparison.OrdinalIgnoreCase) ||
                   preserve.StartsWith(candidate.TrimEnd('/') + "/",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPreservedMeta(string candidate, string preserve) =>
            !string.IsNullOrEmpty(preserve) &&
            string.Equals(candidate, preserve + ".meta", StringComparison.OrdinalIgnoreCase);

        private static IReadOnlyList<string> ReadLedger(string runId)
        {
            var path = LedgerPath(runId);
            if (!File.Exists(path)) return Array.Empty<string>();
            var result = new List<string>();
            foreach (var line in File.ReadAllLines(path, Utf8NoBom))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var decoded = Utf8NoBom.GetString(Convert.FromBase64String(line));
                    result.Add(RequireOwnedAssetPath(decoded, allowRunScene: true));
                }
                catch (Exception error)
                {
                    throw new InvalidDataException(
                        $"Test asset ownership ledger is corrupt for run '{runId}'.", error);
                }
            }
            return result;
        }

        private static void DeleteLedger(string runId)
        {
            var path = LedgerPath(runId);
            if (File.Exists(path)) File.Delete(path);
        }

        private static string QuarantineLedger(string runId, ref string warning)
        {
            var path = LedgerPath(runId);
            if (!File.Exists(path)) return "";
            var quarantine = path + ".corrupt-" + Guid.NewGuid().ToString("N");
            try
            {
                File.Move(path, quarantine);
                return quarantine;
            }
            catch (Exception moveError)
            {
                warning += "; quarantine rename failed: " + moveError.Message;
                try
                {
                    File.Copy(path, quarantine, false);
                    File.Delete(path);
                    return quarantine;
                }
                catch (Exception copyError)
                {
                    warning += "; quarantine copy failed: " + copyError.Message;
                    try
                    {
                        File.Delete(path);
                    }
                    catch (Exception deleteError)
                    {
                        warning += "; corrupt ledger delete failed: " + deleteError.Message;
                    }
                    return "";
                }
            }
        }

        private static string LedgerPath(string runId) => Path.Combine(
            ProjectRoot(), "Library", "UnityMCP", "TestOwnership", runId + ".assets");

        private static string ProjectRoot() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private static void RequireSafeRunId(string runId)
        {
            if (string.IsNullOrWhiteSpace(runId) ||
                !string.Equals(Path.GetFileName(runId), runId, StringComparison.Ordinal) ||
                runId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new ArgumentException("A safe run id is required.", nameof(runId));
        }
    }
}
