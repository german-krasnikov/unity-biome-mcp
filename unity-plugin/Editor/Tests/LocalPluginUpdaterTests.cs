// TDD: LocalPluginUpdater — mock IProcessRunner injection.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

        // ── DefaultRunner branch: MainThreadDispatcher, not delayCall (DEV-66 Part C3) ──

        // Subclassing (rather than a fake IProcessRunner) is required here: UpdateAsync
        // gates the background Task.Run + main-thread-marshal branch on `runner is
        // DefaultRunner`, and a subclass satisfies that check via polymorphism without
        // spawning a real git process.
        private sealed class TestableDefaultRunner : LocalPluginUpdater.DefaultRunner
        {
            public int ExitCode;
            public override int Run(string exe, string args, string workingDir) => ExitCode;
        }

        private static async Task<bool?> DrainUntilAsync(Func<bool?> read, int timeoutMs = 2000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                MainThreadDispatcher.Drain();
                var value = read();
                if (value.HasValue) return value;
                await Task.Delay(10);
            }
            return read();
        }

        [Test]
        public async Task UpdateAsync_DefaultRunnerBranch_MarshalsCompletionThroughMainThreadDispatcher_NotDelayCall()
        {
            // ExitCode=1 deliberately — the success path calls AssetDatabase.Refresh for
            // real, which this test must not trigger against a shared Editor worker.
            var fake = new TestableDefaultRunner { ExitCode = 1 };
            bool? success = null;
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("git pull failed"));

            LocalPluginUpdater.UpdateAsync("/fake/repo", fake, _ => { }, s => success = s);

            var result = await DrainUntilAsync(() => success);

            Assert.IsTrue(result.HasValue,
                "the DefaultRunner branch must marshal git-pull completion onto the main thread " +
                "via MainThreadDispatcher");
            Assert.IsFalse(result.Value, "a non-zero git exit code must callback with false");
        }

        [Test]
        public void UpdateAsync_DefaultRunnerBranch_DoesNotDependOnDelayCall()
        {
            var src = ReadRequiredPackageSource(typeof(LocalPluginUpdater), "Editor/Updates/LocalPluginUpdater.cs");
            var start = src.IndexOf("if (runner is DefaultRunner)");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "DefaultRunner branch not found");
            var end = src.IndexOf("// Tests inject synchronous FakeRunner", start);
            Assert.That(end, Is.GreaterThan(start), "test-only branch marker not found after the DefaultRunner branch");
            var body = src.Substring(start, end - start);

            StringAssert.Contains("MainThreadDispatcher.Enqueue", body,
                "git-pull completion must marshal onto the main thread via MainThreadDispatcher — " +
                "Task.Run's continuation runs on a background thread, and EditorApplication.delayCall " +
                "+= is not thread-safe from there");
            StringAssert.DoesNotContain("delayCall", body,
                "the DefaultRunner branch must not depend on delayCall — a backgrounded Editor does " +
                "not reliably drain it (RELAY-FIX, commit 1bcc90b7)");
        }
    }
}
