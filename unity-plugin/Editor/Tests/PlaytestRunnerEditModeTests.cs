// TDD: B05 — the Play-mode gate for run_playtest moved past parsing. Registration no longer
// flags run_playtest runtime:true (CommandRouterRegistrationTests carries that assertion);
// AsyncRunPlaytest now scans the script's `# @needs editmode` header itself and decides.
// Dispatched end-to-end through CommandRouter.ProcessAsync (same pattern as PlaytestPathTests.cs)
// since AsyncRunPlaytest is a private handler reachable only through the command dispatch path.
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestRunnerEditModeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private Func<bool> _savedIsCompiling;

        [SetUp]
        public void SetUp()
        {
            // Defensive parity with PlaytestPathTests.cs: isolate from real Editor compile state.
            _savedIsCompiling = CommandRouter.IsCompiling;
            CommandRouter.IsCompiling = () => false;
        }

        [TearDown]
        public void TearDown()
        {
            CommandRouter.IsCompiling = _savedIsCompiling;
        }

        private static async Task<string> GetResultAsync(string argsJson)
        {
            var json = $"{{\"id\":\"t\",\"cmd\":\"run_playtest\",\"args\":{{{argsJson}}}}}";
            var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CommandRouter.ProcessAsync(json, tcs);
            return await AwaitBoundedAsync(tcs);
        }

        // Bounded wait shared by every test in this fixture: races the TCS against a fixed
        // timeout rather than an unbounded Task.Delay spin (B06 — the first tests to ever
        // exercise Tick() outside Play Mode, where completion rides EditorApplication.update).
        private static async Task<string> AwaitBoundedAsync(TaskCompletionSource<string> tcs, double timeoutSeconds = 5.0)
        {
            var completed = await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(timeoutSeconds)));
            Assert.AreSame(tcs.Task, completed, "TCS did not complete in time");
            return await tcs.Task;
        }

        // ── Regression: header-less script keeps the exact legacy gate (INV-005) ────

        [Test]
        public async Task AsyncRunPlaytest_NoHeader_NotPlaying_ReturnsPlayModeError()
        {
            var result = await GetResultAsync("\"script\":\"# empty playtest\"");
            StringAssert.Contains("Not in Play Mode. Use editor(action='play') first.", result);
        }

        // ── New: @needs editmode opts out of the Play-mode gate ─────────────────────

        [Test]
        public async Task AsyncRunPlaytest_EditModeHeader_NotPlaying_DoesNotBlock()
        {
            // 0-step script completes synchronously — never touches Tick()/EditorApplication.update,
            // so this proves the gate alone without depending on B06's Tick() change.
            var result = await GetResultAsync("\"script\":\"# @needs editmode\"");
            StringAssert.DoesNotContain("Not in Play Mode", result);
            StringAssert.Contains("PLAYTEST: 0 steps", result);
        }

        // ── New: @needs editmode + fresh is rejected before Run() ───────────────────

        [Test]
        public async Task AsyncRunPlaytest_EditModeHeader_FreshTrue_ReturnsError()
        {
            var result = await GetResultAsync("\"script\":\"# @needs editmode\",\"fresh\":\"true\"");
            StringAssert.Contains("err:", result);
            StringAssert.Contains("fresh", result);
            StringAssert.Contains("editmode", result);
        }

        // ── B06: Tick() itself must honor requiresPlayMode, not just the entry gate ─

        [Test]
        public async Task Run_EditModeAllowed_LogAndWait_CompletesInEditMode()
        {
            Assert.IsFalse(EditorApplication.isPlaying, "Precondition: test runs in Edit Mode");
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("LOG hello", 5f, tcs, requiresPlayMode: false);
            var result = await AwaitBoundedAsync(tcs);
            StringAssert.DoesNotContain("ABORTED", result);
            StringAssert.Contains("PLAYTEST: 1/1", result);
        }

        [Test]
        public async Task Run_LegacyDefault_EditMode_AbortsImmediately()
        {
            Assert.IsFalse(EditorApplication.isPlaying, "Precondition: test runs in Edit Mode");
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("LOG hello", 5f, tcs); // requiresPlayMode defaults to true (legacy)
            var result = await AwaitBoundedAsync(tcs);
            StringAssert.Contains("[1] ABORTED: Play Mode stopped", result);
        }

        // ── B13: $RUN_ID injected as a parallel-safe VAL ─────────────────────────────
        // LOG's message survives into the report text only when the report takes the
        // detailed (non-compact) branch, i.e. when the run has a failure — hence the
        // trailing ASSERT against a nonexistent object, which fails synchronously.
        private const string RunIdProbeScript = "LOG $RUN_ID\nASSERT /NoSuchObject|C|f == 1";

        private static string ExtractRunIdFromLog(string report)
        {
            var match = Regex.Match(report, @"LOG (\S+)");
            Assert.IsTrue(match.Success, $"Expected a LOG line in report:\n{report}");
            return match.Groups[1].Value;
        }

        [Test]
        public async Task Run_InjectsRunIdVal_UniquePerRun()
        {
            var tcs1 = new TaskCompletionSource<string>();
            PlaytestRunner.Run(RunIdProbeScript, 5f, tcs1, requiresPlayMode: false);
            var result1 = await AwaitBoundedAsync(tcs1);

            var tcs2 = new TaskCompletionSource<string>();
            PlaytestRunner.Run(RunIdProbeScript, 5f, tcs2, requiresPlayMode: false);
            var result2 = await AwaitBoundedAsync(tcs2);

            var id1 = ExtractRunIdFromLog(result1);
            var id2 = ExtractRunIdFromLog(result2);
            Assert.AreNotEqual(id1, id2, $"Expected unique $RUN_ID per run.\nrun1: {result1}\nrun2: {result2}");
        }

        [Test]
        public async Task Run_InjectsRunIdVal_MatchesHexFormat()
        {
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(RunIdProbeScript, 5f, tcs, requiresPlayMode: false);
            var result = await AwaitBoundedAsync(tcs);
            var id = ExtractRunIdFromLog(result);
            StringAssert.IsMatch($"^[0-9a-f]{{{PlaytestRunner.RunIdHexLength}}}$", id);
        }

        [Test]
        public async Task Run_PreallocatedRunId_IsUsedEverywhere()
        {
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(RunIdProbeScript, 5f, tcs, requiresPlayMode: false, runId: "cafebabe");
            var result = await AwaitBoundedAsync(tcs);
            Assert.AreEqual("cafebabe", ExtractRunIdFromLog(result));
        }

        [Test]
        public async Task Parse_UserRunIdDeclaration_IsRejected()
        {
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("VAL $RUN_ID myid\nLOG hi", 5f, tcs, requiresPlayMode: false);
            Assert.IsTrue(tcs.Task.IsCompleted, "Reserved-name rejection must short-circuit before Tick()");
            var result = await tcs.Task;
            StringAssert.Contains("RUN_ID", result);
            StringAssert.Contains("reserved", result);
        }
    }
}
