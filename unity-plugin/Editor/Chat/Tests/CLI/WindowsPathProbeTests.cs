// TDD tests for WindowsPathProbe.ProbeDirs — the pure, OS-agnostic part (just File.Exists
// checks) of the Windows Registry PATH fallback used by ChatBinaryResolver.WhichViaSh() when
// where.exe returns nothing (see WindowsPathProbe.cs for why: Unity's inherited PATH usually
// lacks %APPDATA%\npm etc). Testable on any editor platform since it never touches the
// registry. WindowsPathProbe.Resolve() itself (the registry read) is Windows-only
// (#if UNITY_EDITOR_WIN) and not unit-testable here — see report for manual verification steps.
using System;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class WindowsPathProbeTests
    {
        private string _tempDir;

        [TearDown]
        public void TearDown()
        {
            if (_tempDir != null && System.IO.Directory.Exists(_tempDir))
            {
                try { System.IO.Directory.Delete(_tempDir, true); } catch { }
            }
            _tempDir = null;
        }

        private string MakeTempDir()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mcp_probe_dirs_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_tempDir);
            return _tempDir;
        }

        [Test]
        public void ProbeDirs_ExeFoundInDir_ReturnsFullPath()
        {
            var dir = MakeTempDir();
            var expected = System.IO.Path.Combine(dir, "codex.exe");
            System.IO.File.WriteAllText(expected, "");

            var result = WindowsPathProbe.ProbeDirs(new[] { dir }, "codex");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProbeDirs_CmdFoundInDir_ReturnsFullPath()
        {
            var dir = MakeTempDir();
            var expected = System.IO.Path.Combine(dir, "codex.cmd");
            System.IO.File.WriteAllText(expected, "");

            var result = WindowsPathProbe.ProbeDirs(new[] { dir }, "codex");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProbeDirs_ExeAndCmdBothPresent_PrefersExe()
        {
            var dir = MakeTempDir();
            var exePath = System.IO.Path.Combine(dir, "uv.exe");
            var cmdPath = System.IO.Path.Combine(dir, "uv.cmd");
            System.IO.File.WriteAllText(exePath, "");
            System.IO.File.WriteAllText(cmdPath, "");

            var result = WindowsPathProbe.ProbeDirs(new[] { dir }, "uv");

            Assert.AreEqual(exePath, result);
        }

        [Test]
        public void ProbeDirs_NotFoundInAnyDir_ReturnsNull()
        {
            var dir = MakeTempDir();
            var result = WindowsPathProbe.ProbeDirs(new[] { dir }, "nonexistent-binary-xyz");
            Assert.IsNull(result);
        }

        [Test]
        public void ProbeDirs_SecondDirHasMatch_SkipsFirstEmptyDir()
        {
            var dir1 = MakeTempDir();
            var dir2 = System.IO.Path.Combine(dir1, "second");
            System.IO.Directory.CreateDirectory(dir2);
            var expected = System.IO.Path.Combine(dir2, "uv.exe");
            System.IO.File.WriteAllText(expected, "");

            var result = WindowsPathProbe.ProbeDirs(new[] { dir1, dir2 }, "uv");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProbeDirs_EmptyOrNullEntriesInDirs_AreSkipped()
        {
            var dir = MakeTempDir();
            var expected = System.IO.Path.Combine(dir, "uv.exe");
            System.IO.File.WriteAllText(expected, "");

            var result = WindowsPathProbe.ProbeDirs(new[] { null, "", dir }, "uv");

            Assert.AreEqual(expected, result);
        }

        [Test]
        public void ProbeDirs_NullDirs_ReturnsNull()
        {
            Assert.IsNull(WindowsPathProbe.ProbeDirs(null, "uv"));
        }
    }
}
