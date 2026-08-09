// TDD: run_playtest path= parameter — load script from file instead of inline.
// Error cases only (synchronous TCS set), regression for inline script.
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestPathTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _subDir;
        private string _tempAbsDir;
        private string _externalDir;
        private Func<bool> _savedIsPlayMode;
        private Func<bool> _savedIsCompiling;

        [SetUp]
        public void SetUp()
        {
            _subDir = "PlaytestPathTests_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            _tempAbsDir = Path.Combine(Application.dataPath, "TestsTemp", _subDir);
            Directory.CreateDirectory(_tempAbsDir);

            _savedIsPlayMode = CommandRouter.IsPlayMode;
            _savedIsCompiling = CommandRouter.IsCompiling;
            CommandRouter.IsPlayMode = () => true;
            CommandRouter.IsCompiling = () => false;
        }

        [TearDown]
        public void TearDown()
        {
            CommandRouter.IsPlayMode = _savedIsPlayMode;
            CommandRouter.IsCompiling = _savedIsCompiling;

            if (Directory.Exists(_tempAbsDir))
                Directory.Delete(_tempAbsDir, true);
            if (_externalDir != null && Directory.Exists(_externalDir))
                Directory.Delete(_externalDir, true);
        }

        // Relative path from project root for a file inside Assets/TestsTemp/{_subDir}/
        private string RelPath(string filename) =>
            $"Assets/TestsTemp/{_subDir}/{filename}";

        private static async Task<string> GetResultAsync(string argsJson)
        {
            var json = $"{{\"id\":\"t\",\"cmd\":\"run_playtest\",\"args\":{{{argsJson}}}}}";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CommandRouter.ProcessAsync(json, tcs);
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        // ── Error cases (TCS set immediately, safe in EditMode) ──────────────

        [Test]
        public async Task Path_FileNotFound_ReturnsError()
        {
            var result = await GetResultAsync("\"path\":\"Assets/nonexistent_xyz.playtest\"");
            StringAssert.Contains("file not found", result);
        }

        [Test]
        public async Task Path_EmptyString_ReturnsError()
        {
            var result = await GetResultAsync("\"path\":\"\"");
            // empty path → "file not found" (Path.GetFullPath("") resolves to project root, which is a dir not a file)
            StringAssert.Contains("file not found", result);
        }

        [Test]
        public async Task Path_AndScript_BothPresent_ReturnsError()
        {
            var result = await GetResultAsync("\"path\":\"foo.playtest\",\"script\":\"# test\"");
            StringAssert.Contains("not both", result);
        }

        [Test]
        public async Task NeitherPathNorScript_ReturnsError()
        {
            var result = await GetResultAsync("\"timeout\":\"5\"");
            StringAssert.Contains("script or path required", result);
        }

        // ── Success paths ─────────────────────────────────────────────────────

        [Test]
        public async Task Path_ValidFile_NoFileNotFoundError()
        {
            File.WriteAllText(Path.Combine(_tempAbsDir, "t.playtest"), "# empty script");
            var result = await GetResultAsync($"\"path\":\"{RelPath("t.playtest")}\"");
            StringAssert.DoesNotContain("file not found", result);
        }

        [Test]
        public async Task Path_WithDefs_PrependedBeforeScript()
        {
            File.WriteAllText(Path.Combine(_tempAbsDir, "d.playtest"), "# uses $hero");
            var result = await GetResultAsync($"\"path\":\"{RelPath("d.playtest")}\",\"defs\":\"VAL $hero /Player\"");
            StringAssert.DoesNotContain("file not found", result);
        }

        [Test]
        public async Task Path_OutsideAssets_InsideProjectRoot_NoError()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var externalName = "Playtests_Temp_" + _subDir;
            _externalDir = Path.Combine(projectRoot, externalName);
            Directory.CreateDirectory(_externalDir);
            File.WriteAllText(Path.Combine(_externalDir, "test_temp.playtest"), "# temp");

            var result = await GetResultAsync($"\"path\":\"{externalName}/test_temp.playtest\"");
            StringAssert.DoesNotContain("file not found", result);
            StringAssert.DoesNotContain("path must be inside project", result);
        }

        [Test]
        public async Task Script_InlineMode_StillWorks_NoPathError()
        {
            // regression: inline script must not return file-loading errors
            var result = await GetResultAsync("\"script\":\"# comment only\"");
            StringAssert.DoesNotContain("file not found", result);
            StringAssert.DoesNotContain("script or path required", result);
        }
    }
}
