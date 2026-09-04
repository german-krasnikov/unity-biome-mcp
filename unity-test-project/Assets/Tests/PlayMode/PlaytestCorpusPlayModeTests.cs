// TDD: B22 — corpus carrier split (R-01/standards#2): the 3 Play-bound MCPFeedbackFixture
// .playtest files (C_shared_finish, DSL_types, I3_independent_pass) rely on coroutine-driven
// MonoBehaviours (FixtureState.CompleteAfterSeconds, FixtureAsyncState.StartOperation) that only
// tick while EditorApplication.isPlaying is true. B21's EditMode carrier cannot run them; an
// EditMode test cannot enter Play Mode either (that would require a domain reload mid-test,
// which UTF does not support). So this is a genuine PlayMode NUnit fixture, in its own PlayMode
// assembly (Tests.PlayMode.asmdef — the first one in this repo).
//
// Design call (orchestrator-approved 2026-09-04): this fixture is a plain [TestFixture], NOT
// UnityMcpTestBase/SceneTestBase. Those bases live in Editor-only assemblies and their [SetUp]
// isolation machinery (RequireDisposableWorkerBoundary, UnityMcpManagedSceneSafety, chat-window/
// reload-guard repair) was written and only ever exercised for EditMode execution — this is the
// first PlayMode fixture in the repo, so there is no precedent that machinery behaves correctly
// mid-Play. There is also nothing to leak here: the fixture scene is loaded via runtime
// SceneManager.LoadSceneAsync (not EditorSceneManager), and Play Mode's own exit tears down all
// runtime scene state automatically.
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityMCP.Editor;

namespace UnityMCP.TestProject
{
    [TestFixture]
    public class PlaytestCorpusPlayModeTests
    {
        private const string FixtureDir = "Assets/MCPFeedbackFixture/PlayTests";
        private const string FixtureSceneName = "McpFeedbackFixture";

        // ── Shared script-loading + PlaytestRunner harness (mirrors PlaytestCorpusEditModeTests) ──

        private static string ReadFixtureScript(string fileName)
        {
            var fullPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", FixtureDir, fileName));
            return File.ReadAllText(fullPath, Encoding.UTF8);
        }

        private static async Task<string> AwaitBoundedAsync(TaskCompletionSource<string> tcs, double timeoutSeconds)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        private static async Task<string> RunFixtureAsync(
            string fileName, float globalTimeout = 5f, double outerTimeoutSeconds = 5.0)
        {
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(ReadFixtureScript(fileName), globalTimeout, tcs, requiresPlayMode: true);
            return await AwaitBoundedAsync(tcs, outerTimeoutSeconds);
        }

        // Single-load fresh-scene helper. Called once at the start of every [Test] method — this
        // IS the isolation boundary between tests, not a Play Mode transition (Play stays active
        // for the whole PlayMode run; only the scene instance is reloaded via runtime SceneManager).
        private static async Task LoadFixtureSceneAsync()
        {
            var op = SceneManager.LoadSceneAsync(FixtureSceneName, LoadSceneMode.Single);
            Assert.IsNotNull(op, $"Scene '{FixtureSceneName}' must be enabled in Build Settings");
            while (!op.isDone) await Awaitable.NextFrameAsync();
            await Awaitable.NextFrameAsync(); // let Awake/Start/OnEnable run before DSL touches objects
        }

        // ── ABC chain: loaded once, run sequentially, no reset between links ────────────────────

        [Test]
        public async Task Run_AbcChain_CompletesInOneSession()
        {
            await LoadFixtureSceneAsync();

            var resultA = await RunFixtureAsync("A_shared_setup.playtest");
            StringAssert.Contains("PLAYTEST: 4/4", resultA);
            StringAssert.Contains(" OK", resultA);

            // B's own first step is "ASSERT $state == 101" — only passes if A's Increment
            // persisted with no reset/reload in between. That is the effect assertion.
            var resultB = await RunFixtureAsync("B_shared_continue.playtest");
            StringAssert.Contains("PLAYTEST: 3/3", resultB);
            StringAssert.Contains(" OK", resultB);

            // C's own first step is "ASSERT $state == 102" — cumulative across A+B; C then
            // exercises the coroutine-driven CompleteAfterSeconds, which needs real Play Mode.
            var resultC = await RunFixtureAsync("C_shared_finish.playtest", globalTimeout: 8f, outerTimeoutSeconds: 10.0);
            StringAssert.Contains("PLAYTEST: 4/4", resultC);
            StringAssert.Contains(" OK", resultC);
        }

        [Test]
        public async Task Run_DslTypes_AllTypeAssertionsPass()
        {
            await LoadFixtureSceneAsync();
            var result = await RunFixtureAsync("DSL_types.playtest", globalTimeout: 8f, outerTimeoutSeconds: 10.0);
            StringAssert.Contains("PLAYTEST: 7/7", result);
            StringAssert.Contains(" OK", result);
        }

        [Test]
        public async Task Run_I3IndependentPass_IsolatedFromPriorFiles()
        {
            // Fresh scene load, not a mode transition — this is what isolates I3 from the ABC
            // chain and from DSL_types above, all within the same continuous Play session.
            await LoadFixtureSceneAsync();
            var result = await RunFixtureAsync("I3_independent_pass.playtest", globalTimeout: 8f, outerTimeoutSeconds: 10.0);
            StringAssert.Contains("PLAYTEST: 4/4", result);
            StringAssert.Contains(" OK", result);
        }
    }
}
