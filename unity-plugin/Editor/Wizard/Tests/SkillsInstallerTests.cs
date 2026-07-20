// Red phase: SkillsInstaller logic tests — pure I/O, no Unity API required
using System;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Wizard;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SkillsInstallerTests
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

        // ── ListFiles ─────────────────────────────────────────────────────────

        [Test]
        public void ListFiles_ReturnsMdAndPyOnly()
        {
            var src = Path.Combine(_tmp, "src");
            Directory.CreateDirectory(Path.Combine(src, "skills"));
            File.WriteAllText(Path.Combine(src, "skills", "a.md"), "x");
            File.WriteAllText(Path.Combine(src, "skills", "b.txt"), "x");
            File.WriteAllText(Path.Combine(src, "scripts", "c.py"), "x");
            Directory.CreateDirectory(Path.Combine(src, "scripts"));
            File.WriteAllText(Path.Combine(src, "scripts", "c.py"), "x");

            var files = SkillsInstaller.ListFiles(src);

            Assert.AreEqual(2, files.Length);
            Assert.Contains("skills/a.md", files);
            Assert.Contains("scripts/c.py", files);
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

        // ── Install ───────────────────────────────────────────────────────────

        [Test]
        public void Install_CopiesFiles_ToCorrectDestinations()
        {
            var src = Path.Combine(_tmp, "src3");
            var dst = Path.Combine(_tmp, "dst3");
            Directory.CreateDirectory(Path.Combine(src, "skills"));
            File.WriteAllText(Path.Combine(src, "skills", "test.md"), "content");

            var result = SkillsInstaller.Install(src, dst);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.Copied);
            Assert.IsTrue(File.Exists(Path.Combine(dst, ".claude", "skills", "test.md")));
        }

        [Test]
        public void Install_SkipsExistingWhenNoOverwrite()
        {
            var src = Path.Combine(_tmp, "src4");
            var dst = Path.Combine(_tmp, "dst4");
            Directory.CreateDirectory(Path.Combine(src, "skills"));
            File.WriteAllText(Path.Combine(src, "skills", "test.md"), "new");
            Directory.CreateDirectory(Path.Combine(dst, ".claude", "skills"));
            File.WriteAllText(Path.Combine(dst, ".claude", "skills", "test.md"), "old");

            var result = SkillsInstaller.Install(src, dst, overwrite: false);

            Assert.AreEqual(0, result.Copied);
            Assert.AreEqual(1, result.Skipped);
            Assert.AreEqual("old", File.ReadAllText(Path.Combine(dst, ".claude", "skills", "test.md")));
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
