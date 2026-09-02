using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ProjectConfigWriterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tmpDir;

        [SetUp]
        public void SetUp()
        {
            _tmpDir = Path.Combine(Path.GetTempPath(), $"ProjectConfigWriterTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_tmpDir);
            RegisterCleanup(() =>
            {
                if (Directory.Exists(_tmpDir))
                    Directory.Delete(_tmpDir, true);
            });
            // ARC-11 T2: _tmpDir is a fresh GUID per test, so this key never
            // collides across tests — but it's still a new EditorPrefs key any
            // test in this fixture may now write via ProjectConfigWriter's
            // baseline stamping. Protect it here once so every test restores it
            // (deletes it, since it never existed before) instead of leaking it,
            // per the typed EditorPrefs ownership rule this codebase requires.
            ProtectEditorPrefString(PrefKeys.LastSyncedVersionPrefix + _tmpDir);
        }

        // --- helpers ---

        private static HashSet<string> AllKeys()
        {
            var keys = new HashSet<string>();
            foreach (var t in ProjectConfigTargets.All) keys.Add(t.Key);
            return keys;
        }

        // --- filtering tests (new) ---

        [Test]
        public void Run_WithAllKeysEnabled_CreatesAllSixTargetFiles()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());

            foreach (var target in ProjectConfigTargets.All)
                Assert.IsTrue(File.Exists(Path.Combine(_tmpDir, target.RelativePath)),
                    $"{target.RelativePath} should have been created");
        }

        [Test]
        public void Run_WithNoEnabledKeys_WritesNoNewFiles()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string>());

            foreach (var target in ProjectConfigTargets.All)
                Assert.IsFalse(File.Exists(Path.Combine(_tmpDir, target.RelativePath)),
                    $"{target.RelativePath} should NOT have been created");
        }

        [Test]
        public void Run_WithEnabledKeySubset_WritesOnlyThoseFiles()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            Assert.IsTrue(File.Exists(Path.Combine(_tmpDir, ".mcp.json")));
            Assert.IsFalse(File.Exists(Path.Combine(_tmpDir, ".cursor/mcp.json")));
        }

        [Test]
        public void Run_DisabledAgent_ExistingFile_NotRewritten()
        {
            // ARC-0b T4 / ARC-14 T2 (ARC-19 §3 row 34): file-exists bypass removed —
            // a disabled agent's pre-existing file must be left byte-identical.
            // Pre-create .cursor/mcp.json with old version — not in enabled set.
            var cursorDir = Path.Combine(_tmpDir, ".cursor");
            Directory.CreateDirectory(cursorDir);
            var cursorPath = Path.Combine(cursorDir, "mcp.json");
            File.WriteAllText(cursorPath,
                "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"uvx\",\"_v\":\"0.0.1\"}}}");
            // Sentinel proves no rewrite happened — a rewrite would regenerate fresh
            // content and silently drop this appended line.
            File.AppendAllText(cursorPath, "\n// sentinel-untouched\n");
            var before = File.ReadAllText(cursorPath);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            var after = File.ReadAllText(cursorPath);
            Assert.AreEqual(before, after);
            StringAssert.Contains("sentinel-untouched", after);
            StringAssert.Contains("\"_v\":\"0.0.1\"", after);
        }

        [Test]
        public void GetActiveTargets_FileExistsKeyNotEnabled_ExcludesTarget()
        {
            // ARC-0b T4 / ARC-14 T2 (ARC-19 §3 row 34): red-flip of
            // GetActiveTargets_FileExistsNotEnabled_IncludesTarget — file existence must
            // no longer bypass the enabledKeys filter.
            var cursorDir = Path.Combine(_tmpDir, ".cursor");
            Directory.CreateDirectory(cursorDir);
            File.WriteAllText(Path.Combine(cursorDir, "mcp.json"), "{}");

            var enabled = new HashSet<string>(); // cursor not enabled
            var active = new List<ProjectConfigTarget>(
                ProjectConfigWriter.GetActiveTargets(_tmpDir, enabled));

            Assert.IsFalse(active.Exists(t => t.Key == "cursor"),
                "cursor should be excluded — key not enabled, even though file exists");
        }

        [Test]
        public void GetActiveTargets_FileAbsentNotEnabled_ExcludesTarget()
        {
            var enabled = new HashSet<string>(); // nothing enabled, no files
            var active = new List<ProjectConfigTarget>(
                ProjectConfigWriter.GetActiveTargets(_tmpDir, enabled));

            Assert.IsFalse(active.Exists(t => t.Key == "cursor"),
                "cursor should be excluded — file absent, not enabled");
        }

        [Test]
        public void Run_UpdatesGitignore_WithOnlyActiveTargetPaths()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            var gitignore = File.ReadAllText(Path.Combine(_tmpDir, ".gitignore"));
            StringAssert.Contains(".mcp.json", gitignore);
            StringAssert.DoesNotContain(".cursor/mcp.json", gitignore);
        }

        // --- original tests (updated to inject enabledKeys for isolation) ---

        [Test]
        public void Run_FreshProjectDir_EachFileContainsCurrentVersion()
        {
            // JSON entries no longer contain port (discovery via .port files); TOML still does.
            ProjectConfigWriter.Run(_tmpDir, 9501, "1.2.3", AllKeys());

            foreach (var target in ProjectConfigTargets.All)
            {
                var content = File.ReadAllText(Path.Combine(_tmpDir, target.RelativePath));
                if (target.IsToml)
                {
                    StringAssert.Contains("v1.2.3", content);
                    StringAssert.Contains("9501", content);
                }
                else
                {
                    StringAssert.Contains("\"_v\": \"1.2.3\"", content);
                }
            }
        }

        [Test]
        public void Run_ExistingOwnedCurrentFile_DoesNotRewrite()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });
            var path = Path.Combine(_tmpDir, ".mcp.json");
            // Sentinel proves no rewrite happened — a rewrite would regenerate fresh
            // content and silently drop this, whereas plain string equality against
            // deterministic output would pass either way (same inputs → same bytes).
            File.AppendAllText(path, "\n// sentinel-untouched\n");
            var before = File.ReadAllText(path);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });
            var after = File.ReadAllText(path);

            Assert.AreEqual(before, after);
            StringAssert.Contains("sentinel-untouched", after);
        }

        [Test]
        public void Run_PortChangedOnly_SkipsRewrite_PreservesUnrelatedMcpServers()
        {
            // Port change alone no longer triggers a rewrite (port not in JSON entries).
            // Same-version entry is OwnedCurrent → skip; sibling servers stay untouched.
            var path = Path.Combine(_tmpDir, ".mcp.json");
            File.WriteAllText(path,
                "{\"mcpServers\":{\"other-tool\":{\"command\":\"x\"},"
                + "\"unity-mcp\":{\"command\":\"uvx\",\"_v\":\"1.0.0\"}}}");

            ProjectConfigWriter.Run(_tmpDir, 9600, "1.0.0", new HashSet<string>());

            var content = File.ReadAllText(path);
            StringAssert.Contains("other-tool", content);
        }

        [Test]
        public void Run_VersionChanged_RewritesFile()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.0.0", new HashSet<string> { "claude-code" });
            ProjectConfigWriter.Run(_tmpDir, 9500, "2.0.0", new HashSet<string> { "claude-code" });

            var content = File.ReadAllText(Path.Combine(_tmpDir, ".mcp.json"));
            StringAssert.Contains("\"_v\": \"2.0.0\"", content);
        }

        [Test]
        public void Run_FileWithForeignUnityMcpEntry_AdoptsEntry_AddsVersionMarker()
        {
            // Adoption: foreign entry gets "_v" marker inserted; custom content preserved.
            // Key must be enabled (ARC-0b T4 / ARC-14 T2): GetActiveTargets no longer
            // visits a target just because its file exists — Run() only reaches Adopt()
            // for a key the caller opted into. claude-code is .mcp.json's key.
            var path = Path.Combine(_tmpDir, ".mcp.json");
            var handWritten = "{\"mcpServers\":{\"unity-mcp\":{\"command\":\"custom\"}}}";
            File.WriteAllText(path, handWritten);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            var content = File.ReadAllText(path);
            StringAssert.Contains("\"_v\": \"1.2.3\"", content);
            StringAssert.Contains("custom", content);
        }

        [Test]
        public void WriteOne_Foreign_AdoptsEntryInsteadOfSkipping()
        {
            var path = Path.Combine(_tmpDir, ".mcp.json");
            var handWritten = "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\":\"custom\"}}}";
            File.WriteAllText(path, handWritten);

            ProjectConfigWriter.WriteOne(_tmpDir, ProjectConfigTargets.All[0], 9500, "1.2.3",
                WizardConfigWriter.GitInstallUrlFor("1.2.3"));

            var content = File.ReadAllText(path);
            StringAssert.Contains("\"_v\": \"1.2.3\"", content);
            StringAssert.Contains("custom", content);
        }

        [Test]
        public void WriteOne_PinnedEntry_FileUnchanged()
        {
            // ARC-0b Task 1: a pinned entry with a stale "_v" must never be rewritten by
            // WriteOne — Classify() now returns OwnedCurrent for it, hitting the
            // existing no-op path. Sentinel technique from Run_ExistingOwnedCurrentFile_DoesNotRewrite.
            var path = Path.Combine(_tmpDir, ".mcp.json");
            File.WriteAllText(path,
                "{\"mcpServers\":{\"unity-biome-mcp\":{"
                + "\"command\": \"uvx\","
                + "\"_v\": \"1.49.0\","
                + "\"_pin\": true"
                + "}}}");
            File.AppendAllText(path, "\n// sentinel-untouched\n");
            var before = File.ReadAllText(path);

            ProjectConfigWriter.WriteOne(_tmpDir, ProjectConfigTargets.All[0], 9500, "1.50.0",
                WizardConfigWriter.GitInstallUrlFor("1.50.0"));

            var after = File.ReadAllText(path);
            Assert.AreEqual(before, after);
            StringAssert.Contains("sentinel-untouched", after);
        }

        // ── ARC-11 T2: baseline tracking + drift detection ──────────────────

        [Test]
        public void WriteOne_MarkerMatchesLastSynced_VersionBumped_OverwritesNormally()
        {
            var keys = new HashSet<string> { "claude-code" };

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.0.0", keys);
            ProjectConfigWriter.Run(_tmpDir, 9500, "2.0.0", keys);

            Assert.AreEqual("2.0.0",
                EditorPrefs.GetString(PrefKeys.LastSyncedVersionPrefix + _tmpDir, ""));
        }

        [Test]
        public void WriteOne_MarkerDiffersFromLastSynced_NoPinFlag_DoesNotOverwrite_PinsInstead()
        {
            // The P7 regression: after our own write recorded a baseline, the user
            // hand-edits "_v" outside the CLI (no "_pin" flag) — the next Run must
            // detect the drift and Pin() instead of silently reverting the edit.
            var keys = new HashSet<string> { "claude-code" };
            var path = Path.Combine(_tmpDir, ".mcp.json");

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.50.0", keys);
            Assert.AreEqual("1.50.0",
                EditorPrefs.GetString(PrefKeys.LastSyncedVersionPrefix + _tmpDir, ""),
                "precondition: our own first write must record a baseline");

            var content = File.ReadAllText(path);
            File.WriteAllText(path, content.Replace("\"_v\": \"1.50.0\"", "\"_v\": \"1.49.0\""));

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.50.0", keys); // live version unchanged

            var after = File.ReadAllText(path);
            StringAssert.Contains("\"_v\": \"1.49.0\"", after);
            StringAssert.DoesNotContain("\"_v\": \"1.50.0\"", after);
            StringAssert.Contains("\"_pin\": true", after);
        }

        [Test]
        public void WriteOne_NoBaselineYet_MarkerDiffersFromLive_OverwritesAsBefore()
        {
            // No prior Run() for this project — baseline was never recorded (e.g.
            // first launch after upgrading the plugin onto an already-synced
            // project). Pre-ARC-11 default must be preserved: an unpinned stale
            // marker still gets overwritten.
            var path = Path.Combine(_tmpDir, ".mcp.json");
            File.WriteAllText(path,
                "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\": \"uvx\",\"_v\": \"0.5.0\"}}}");

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            var content = File.ReadAllText(path);
            StringAssert.Contains("\"_v\": \"1.2.3\"", content);
            StringAssert.DoesNotContain("\"_v\": \"0.5.0\"", content);
        }

        [Test]
        public void WriteOne_OwnedCurrent_StampsBaselineOpportunistically()
        {
            // Entry already matches the live version but no baseline was ever
            // recorded. A Run() that hits the OwnedCurrent no-op path must stamp
            // the baseline immediately instead of waiting for the next real
            // version bump.
            var path = Path.Combine(_tmpDir, ".mcp.json");
            File.WriteAllText(path,
                "{\"mcpServers\":{\"unity-biome-mcp\":{\"command\": \"uvx\",\"_v\": \"1.2.3\"}}}");

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", new HashSet<string> { "claude-code" });

            Assert.AreEqual("1.2.3",
                EditorPrefs.GetString(PrefKeys.LastSyncedVersionPrefix + _tmpDir, ""));
        }

        [Test]
        [Platform(Exclude = "Win")]
        public void Run_UnwritableTargetDirectory_DoesNotThrow_LogsWarning()
        {
            var chmod = new ProcessStartInfo("chmod", "000 " + _tmpDir) { UseShellExecute = false };
            Process.Start(chmod)?.WaitForExit();

            try
            {
                Assert.DoesNotThrow(() =>
                    ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys()));
            }
            finally
            {
                var restore = new ProcessStartInfo("chmod", "755 " + _tmpDir) { UseShellExecute = false };
                Process.Start(restore)?.WaitForExit();
            }
        }

        [Test]
        public void Run_UpdatesGitignore_WithAllSixPaths()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());

            var gitignore = File.ReadAllText(Path.Combine(_tmpDir, ".gitignore"));
            foreach (var target in ProjectConfigTargets.All)
                StringAssert.Contains(target.RelativePath, gitignore);
        }

        [Test]
        public void Run_GitignoreAlreadyPatched_SecondRunNoOp()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());
            var path = Path.Combine(_tmpDir, ".gitignore");
            var before = File.ReadAllText(path);

            ProjectConfigWriter.Run(_tmpDir, 9500, "1.2.3", AllKeys());
            var after = File.ReadAllText(path);

            Assert.AreEqual(before, after);
        }

        [Test]
        public void Run_EmptyVersionString_FallsBackToUnpinnedGitInstallUrl()
        {
            ProjectConfigWriter.Run(_tmpDir, 9500, "", new HashSet<string> { "claude-code" });

            var content = File.ReadAllText(Path.Combine(_tmpDir, ".mcp.json"));
            StringAssert.Contains(WizardConfigWriter.GitInstallUrl, content);
        }
    }
}
