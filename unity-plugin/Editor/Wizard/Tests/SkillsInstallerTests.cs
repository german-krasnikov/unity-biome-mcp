// Red phase: SkillsInstaller logic tests — pure I/O, no Unity API required
using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SkillsInstallerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tmp;

        [SetUp]
        public void SetUp()
        {
            _tmp = Path.Combine(Path.GetTempPath(), "SkillsInstallerTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tmp);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true);
        }

        // ── MapDestination ────────────────────────────────────────────────────

        [Test]
        public void MapDestination_Skills_MapsToClaude()
        {
            var dst = SkillsInstaller.MapDestination("/root", "skills/foo.md");
            Assert.AreEqual(Path.Combine("/root", ".claude", "skills", "foo.md"), dst);
        }

        [Test]
        public void MapDestination_Agents_MapsToClaude()
        {
            var dst = SkillsInstaller.MapDestination("/root", "agents/bar.md");
            Assert.AreEqual(Path.Combine("/root", ".claude", "agents", "bar.md"), dst);
        }

        [Test]
        public void MapDestination_Scripts_MapsToCodex()
        {
            var dst = SkillsInstaller.MapDestination("/root", "scripts/foo.py");
            Assert.AreEqual(Path.Combine("/root", ".codex", "scripts", "foo.py"), dst);
        }

        [Test]
        public void MapDestination_Unknown_ReturnsNull()
        {
            var dst = SkillsInstaller.MapDestination("/root", "unknown/foo.md");
            Assert.IsNull(dst);
        }

        [Test]
        public void MapDestination_NoSlash_ReturnsNull()
        {
            var dst = SkillsInstaller.MapDestination("/root", "noslash.md");
            Assert.IsNull(dst);
        }

        [Test]
        public void MapDestination_PathTraversal_ReturnsNull()
        {
            var dst = SkillsInstaller.MapDestination("/root", "skills/../../outside.md");
            Assert.IsNull(dst);
        }

        // ── ListFiles ─────────────────────────────────────────────────────────

        [Test]
        public void ListFiles_ReturnsNestedSkillArtifacts()
        {
            var src = Path.Combine(_tmp, "src");
            Directory.CreateDirectory(Path.Combine(src, "skills", "sample", "references"));
            Directory.CreateDirectory(Path.Combine(src, "scripts"));
            File.WriteAllText(Path.Combine(src, "skills", "sample", "SKILL.md"), "x");
            File.WriteAllText(Path.Combine(src, "skills", "sample", "references", "guide.txt"), "x");
            File.WriteAllText(Path.Combine(src, "scripts", "c.py"), "x");

            var files = SkillsInstaller.ListFiles(src);

            Assert.AreEqual(3, files.Length);
            Assert.Contains("skills/sample/SKILL.md", files);
            Assert.Contains("skills/sample/references/guide.txt", files);
            Assert.Contains("scripts/c.py", files);
        }

        [Test]
        public void ListFiles_ExcludesUnityAndPythonMetadata()
        {
            var src = Path.Combine(_tmp, "metadata");
            Directory.CreateDirectory(Path.Combine(src, "skills", "sample", "__pycache__"));
            File.WriteAllText(Path.Combine(src, "skills", "sample", "SKILL.md"), "x");
            File.WriteAllText(Path.Combine(src, "skills", "sample", "SKILL.md.meta"), "x");
            File.WriteAllText(Path.Combine(src, "skills", "sample", ".DS_Store"), "x");
            File.WriteAllText(Path.Combine(src, "skills", "sample", "__pycache__", "helper.pyc"), "x");

            var files = SkillsInstaller.ListFiles(src);

            CollectionAssert.AreEqual(new[] { "skills/sample/SKILL.md" }, files);
        }

        [Test]
        public void ListFiles_UsesForwardSlashes()
        {
            var src = Path.Combine(_tmp, "src2");
            Directory.CreateDirectory(Path.Combine(src, "agents"));
            File.WriteAllText(Path.Combine(src, "agents", "x.md"), "y");

            var files = SkillsInstaller.ListFiles(src);

            StringAssert.DoesNotContain("\\", files[0]);
        }

        [Test]
        public void ListFiles_AcceptsTrailingDirectorySeparator()
        {
            var src = Path.Combine(_tmp, "trailing");
            Directory.CreateDirectory(Path.Combine(src, "agents"));
            File.WriteAllText(Path.Combine(src, "agents", "x.md"), "y");

            var files = SkillsInstaller.ListFiles(src + Path.DirectorySeparatorChar);

            CollectionAssert.AreEqual(new[] { "agents/x.md" }, files);
        }

        // ── Install ───────────────────────────────────────────────────────────

        [Test]
        public void Install_CopiesFiles_ToCorrectDestinations()
        {
            var src = Path.Combine(_tmp, "src3");
            var dst = Path.Combine(_tmp, "dst3");
            Directory.CreateDirectory(Path.Combine(src, "skills", "test", "references"));
            File.WriteAllText(Path.Combine(src, "skills", "test", "SKILL.md"), "content");
            File.WriteAllText(Path.Combine(src, "skills", "test", "references", "example.cs"), "code");

            var result = SkillsInstaller.Install(src, dst);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(2, result.Copied);
            Assert.IsTrue(File.Exists(Path.Combine(dst, ".claude", "skills", "test", "SKILL.md")));
            Assert.IsTrue(File.Exists(Path.Combine(dst, ".claude", "skills", "test", "references", "example.cs")));
        }

        [Test]
        public void Install_ReportsChangedExistingFileWhenNoOverwrite()
        {
            var src = Path.Combine(_tmp, "src4");
            var dst = Path.Combine(_tmp, "dst4");
            Directory.CreateDirectory(Path.Combine(src, "skills"));
            File.WriteAllText(Path.Combine(src, "skills", "test.md"), "new");
            Directory.CreateDirectory(Path.Combine(dst, ".claude", "skills"));
            File.WriteAllText(Path.Combine(dst, ".claude", "skills", "test.md"), "old");

            var result = SkillsInstaller.Install(src, dst, overwrite: false);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(0, result.Copied);
            Assert.AreEqual(0, result.Skipped);
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(dst, ".claude", "skills", "test.md")));
        }

        [Test]
        public void Install_SkipsIdenticalExistingFile()
        {
            var src = Path.Combine(_tmp, "same-src");
            var dst = Path.Combine(_tmp, "same-dst");
            Directory.CreateDirectory(Path.Combine(src, "skills"));
            File.WriteAllText(Path.Combine(src, "skills", "test.md"), "same");
            Directory.CreateDirectory(Path.Combine(dst, ".claude", "skills"));
            File.WriteAllText(Path.Combine(dst, ".claude", "skills", "test.md"), "same");

            var result = SkillsInstaller.Install(src, dst);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(0, result.Copied);
            Assert.AreEqual(1, result.Skipped);
        }

        [Test]
        public void Install_OverwritesWhenFlagSet()
        {
            var src = Path.Combine(_tmp, "src5");
            var dst = Path.Combine(_tmp, "dst5");
            Directory.CreateDirectory(Path.Combine(src, "skills"));
            File.WriteAllText(Path.Combine(src, "skills", "test.md"), "new");
            Directory.CreateDirectory(Path.Combine(dst, ".claude", "skills"));
            File.WriteAllText(Path.Combine(dst, ".claude", "skills", "test.md"), "old");

            var result = SkillsInstaller.Install(src, dst, overwrite: true);

            Assert.AreEqual(1, result.Copied);
            Assert.AreEqual("new", File.ReadAllText(Path.Combine(dst, ".claude", "skills", "test.md")));
        }

        [Test]
        public void Install_ReportsErrorForEmptyFile()
        {
            var src = Path.Combine(_tmp, "src6");
            var dst = Path.Combine(_tmp, "dst6");
            Directory.CreateDirectory(Path.Combine(src, "skills"));
            File.WriteAllText(Path.Combine(src, "skills", "empty.md"), "");

            var result = SkillsInstaller.Install(src, dst);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual(1, result.Errors.Length);
        }

        [Test]
        public void Install_PreflightIOException_ReturnsRestoredFailure()
        {
            var result = SkillsInstaller.Install(
                Path.Combine(_tmp, "missing-source"),
                Path.Combine(_tmp, "destination"));

            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.StateRestored);
            Assert.IsNull(result.RecoveryPath);
            StringAssert.Contains("preflight failed", result.Errors[0]);
        }

        [Test]
        public void Install_RemovesOnlyMatchingLegacyFiles()
        {
            var src = Path.Combine(_tmp, "migration-src");
            var dst = Path.Combine(_tmp, "migration-dst");
            Directory.CreateDirectory(Path.Combine(src, "skills", "new-skill"));
            File.WriteAllText(Path.Combine(src, "skills", "new-skill", "SKILL.md"), "new");
            var legacy = Path.Combine(dst, ".claude", "skills", "old.md");
            Directory.CreateDirectory(Path.GetDirectoryName(legacy));
            File.WriteAllText(legacy, "old managed content");
            var legacyFiles =
                new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyCollection<string>>
            {
                { "skills/old.md", new[] { SkillsInstaller.ComputeGitBlobSha1(legacy) } }
            };

            var result = SkillsInstaller.Install(src, dst, false, legacyFiles);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Removed);
            Assert.IsFalse(File.Exists(legacy));
            Assert.IsTrue(File.Exists(Path.Combine(
                dst, ".claude", "skills", "new-skill", "SKILL.md")));
        }

        [Test]
        public void Install_PreservesModifiedLegacyFileAndMakesNoChanges()
        {
            var src = Path.Combine(_tmp, "conflict-src");
            var dst = Path.Combine(_tmp, "conflict-dst");
            Directory.CreateDirectory(Path.Combine(src, "skills", "new-skill"));
            File.WriteAllText(Path.Combine(src, "skills", "new-skill", "SKILL.md"), "new");
            var legacy = Path.Combine(dst, ".claude", "skills", "old.md");
            Directory.CreateDirectory(Path.GetDirectoryName(legacy));
            File.WriteAllText(legacy, "original");
            var expectedHash = SkillsInstaller.ComputeGitBlobSha1(legacy);
            File.WriteAllText(legacy, "user edit");
            var legacyFiles =
                new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IReadOnlyCollection<string>>
            {
                { "skills/old.md", new[] { expectedHash } }
            };

            var result = SkillsInstaller.Install(src, dst, false, legacyFiles);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("user edit", File.ReadAllText(legacy));
            Assert.IsFalse(File.Exists(Path.Combine(
                dst, ".claude", "skills", "new-skill", "SKILL.md")));
        }

        // ── Version file ──────────────────────────────────────────────────────

        [Test]
        public void ReadVersionFile_ReturnsNullWhenMissing()
        {
            Assert.IsNull(SkillsInstaller.ReadVersionFile(_tmp));
        }

        [Test]
        public void WriteAndRead_VersionFile_RoundTrip()
        {
            SkillsInstaller.WriteVersionFile(_tmp, "1.2.3");
            Assert.AreEqual("1.2.3", SkillsInstaller.ReadVersionFile(_tmp));
        }

        [Test]
        public void WriteVersionFile_AtomicallyReplacesExistingMarker()
        {
            SkillsInstaller.WriteVersionFile(_tmp, "1.2.3");

            SkillsInstaller.WriteVersionFile(_tmp, "1.2.4");

            Assert.AreEqual("1.2.4", SkillsInstaller.ReadVersionFile(_tmp));
        }

        [Test]
        public void ReadVersionFile_TreatsEmptyMarkerAsMissing()
        {
            var marker = Path.Combine(_tmp, ".claude", ".unity-biome-mcp-skills-version");
            Directory.CreateDirectory(Path.GetDirectoryName(marker));
            File.WriteAllText(marker, "");

            Assert.IsNull(SkillsInstaller.ReadVersionFile(_tmp));
        }

        [Test]
        public void WriteVersionFile_RejectsEmptyVersion()
        {
            Assert.Throws<ArgumentException>(() => SkillsInstaller.WriteVersionFile(_tmp, " "));
            Assert.IsNull(SkillsInstaller.ReadVersionFile(_tmp));
        }

        [Test]
        public void DeleteVersionFile_RemovesExistingMarker()
        {
            SkillsInstaller.WriteVersionFile(_tmp, "1.2.3");

            SkillsInstaller.DeleteVersionFile(_tmp);

            Assert.IsNull(SkillsInstaller.ReadVersionFile(_tmp));
        }

        // ── HasCodexDir ───────────────────────────────────────────────────────

        [Test]
        public void HasCodexDir_ReturnsFalseWhenAbsent()
        {
            Assert.IsFalse(SkillsInstaller.HasCodexDir(_tmp));
        }

        [Test]
        public void HasCodexDir_ReturnsTrueWhenPresent()
        {
            Directory.CreateDirectory(Path.Combine(_tmp, ".codex"));
            Assert.IsTrue(SkillsInstaller.HasCodexDir(_tmp));
        }
    }
}
