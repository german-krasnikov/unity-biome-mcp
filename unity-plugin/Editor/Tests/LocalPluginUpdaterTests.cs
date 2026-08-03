// TDD: LocalPluginUpdater — mock IProcessRunner injection.
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class LocalPluginUpdaterTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        class FakeRunner : LocalPluginUpdater.IProcessRunner
        {
            public List<(string exe, string args, string cwd)> Calls = new();
            public int ExitCode = 0;

            public int Run(string exe, string args, string workingDir)
            {
                Calls.Add((exe, args, workingDir));
                return ExitCode;
            }
        }

        [Test]
        public void UpdateAsync_CallsGitPull_WithRepoRoot()
        {
            var fake = new FakeRunner();
            var messages = new List<string>();
            bool completed = false;
            bool success = false;

            LocalPluginUpdater.UpdateAsync(
                repoRoot: "/fake/repo",
                runner: fake,
                onProgress: m => messages.Add(m),
                onComplete: s => { completed = true; success = s; }
            );

            Assert.AreEqual(1, fake.Calls.Count);
            Assert.AreEqual("git", fake.Calls[0].exe);
            StringAssert.Contains("pull", fake.Calls[0].args);
            Assert.AreEqual("/fake/repo", fake.Calls[0].cwd);
            Assert.IsTrue(completed);
            Assert.IsTrue(success);
        }

        [Test]
        public void UpdateAsync_GitFails_CallsOnCompleteFalse()
        {
            var fake = new FakeRunner { ExitCode = 1 };
            bool success = true;

            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("git pull failed"));

            LocalPluginUpdater.UpdateAsync(
                repoRoot: "/fake/repo",
                runner: fake,
                onProgress: _ => { },
                onComplete: s => success = s
            );

            Assert.IsFalse(success);
        }

        [Test]
        public void UpdateAsync_NullRepoRoot_DoesNotCallRunner()
        {
            var fake = new FakeRunner();
            bool completed = false;

            LocalPluginUpdater.UpdateAsync(
                repoRoot: null,
                runner: fake,
                onProgress: _ => { },
                onComplete: _ => completed = true
            );

            Assert.AreEqual(0, fake.Calls.Count);
            // completed still fires so UI can show a message
            Assert.IsTrue(completed);
        }

        [Test]
        public void UpdateAsync_PullIncludesTagsAndAutostash()
        {
            var fake = new FakeRunner();
            LocalPluginUpdater.UpdateAsync("/repo", fake, _ => { }, _ => { });
            StringAssert.Contains("--tags", fake.Calls[0].args);
            StringAssert.Contains("--autostash", fake.Calls[0].args);
        }

        [Test]
        public void UpdateAsync_GitFails_ErrorIncludesManualCommand()
        {
            var fake = new FakeRunner { ExitCode = 1 };
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("git stash && git pull"));

            LocalPluginUpdater.UpdateAsync(
                repoRoot: "/my/repo",
                runner: fake,
                onProgress: _ => { },
                onComplete: _ => { }
            );
        }
    }
}
