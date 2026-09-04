// TDD: B05 — the Play-mode gate for run_playtest moved past parsing. Registration no longer
// flags run_playtest runtime:true (CommandRouterRegistrationTests carries that assertion);
// AsyncRunPlaytest now scans the script's `# @needs editmode` header itself and decides.
// Dispatched end-to-end through CommandRouter.ProcessAsync (same pattern as PlaytestPathTests.cs)
// since AsyncRunPlaytest is a private handler reachable only through the command dispatch path.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

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

        // ── B16: format=json threading + receipt persistence ────────────────────────

        private static string ReceiptFullPath(string runId) => Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", PlaytestReceiptStore.ReceiptPath(runId)));

        [Test]
        public async Task Run_FormatText_Default_Unchanged_AndPersistsCanonicalReceipt()
        {
            const string runId = "b16text01";
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("LOG hello", 5f, tcs, requiresPlayMode: false, runId: runId);
            var result = await AwaitBoundedAsync(tcs);

            StringAssert.Contains("PLAYTEST: 1/1", result);
            StringAssert.DoesNotContain("{", result);

            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });
            Assert.IsTrue(File.Exists(receiptPath), $"Expected canonical receipt at {receiptPath}");
            var json = File.ReadAllText(receiptPath, System.Text.Encoding.UTF8);
            StringAssert.Contains($"\"run_id\":\"{runId}\"", json);
            StringAssert.Contains($"\"text_report\":\"{result}\"", json);
        }

        [Test]
        public async Task Run_FormatJson_EmitsOuterAndSteps()
        {
            const string runId = "b16json01";
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(RunIdProbeScript, 5f, tcs, requiresPlayMode: false, runId: runId, format: "json");
            var result = await AwaitBoundedAsync(tcs);

            StringAssert.StartsWith("{", result);
            StringAssert.Contains("\"teardown_ok\":", result);
            StringAssert.Contains("\"scene_clean\":", result);
            StringAssert.Contains("\"steps\":[{", result);
            // RunIdProbeScript = "LOG $RUN_ID\nASSERT ...==1" — one passing step, one failing step.
            StringAssert.Contains("\"type\":\"Log\",\"ok\":true,", result);
            StringAssert.Contains("\"raw_passed\":true,\"expected_fail\":false", result);
            StringAssert.Contains("\"ok\":false,", result);
            StringAssert.Contains("\"raw_passed\":false,\"expected_fail\":false", result);

            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });
            Assert.IsTrue(File.Exists(receiptPath), $"Expected canonical receipt at {receiptPath}");
        }

        // ── C03: MCP DSL steps execute through the real CommandRouter.ProcessAsync path ────

        [Test]
        public async Task Run_McpStep_ReadCommand_CapturesDataAndAssertConsumesIt()
        {
            TrackOwnedObject(new GameObject("McpFixtureProbe"));
            const string runId = "c03mcp01";

            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(
                "MCP get_hierarchy depth=2 INTO $tree\nASSERT $tree contains Fixture\n",
                5f, tcs, requiresPlayMode: false, runId: runId, format: "json");
            var result = await AwaitBoundedAsync(tcs);

            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });

            // get_hierarchy returns Biome's own formatted text block, not JSON — "contains" is
            // a plain substring match, so no JSON parsing of $tree is involved anywhere here.
            StringAssert.Contains("\"type\":\"Mcp\",\"ok\":true,", result);
            StringAssert.Contains("\"type\":\"Assert\",\"ok\":true,", result);
            StringAssert.Contains("\"teardown_ok\":true", result);
        }

        [Test]
        public void Run_McpStep_ObjectData_CapturesCompactJson()
        {
            var step = new PlaytestStep { Type = StepType.Mcp, Method = "test_cmd", ResultVar = "obj" };
            var results = new List<string>();
            int passed = 0, failed = 0;
            var varRegistry = new PlaytestVarRegistry();
            const string json = "{\"a\":1,\"b\":[2,3]}";
            var response = "{\"id\":\"t\",\"ok\":true,\"data\":\"" + JsonHelper.EscapeJson(json) + "\"}";

            PlaytestRunner.ApplyMcpResult(step, 0, response, results, varRegistry, ref passed, ref failed);

            Assert.AreEqual(1, passed);
            Assert.AreEqual(0, failed);
            Assert.IsTrue(varRegistry.TryGetCaptured("$obj", out var captured));
            Assert.AreEqual(json, captured, "the captured compact JSON must survive byte-for-byte");
        }

        [Test]
        public void Run_McpStep_ReadCommand_WithoutInto_ReportsDataInStepMessage()
        {
            var step = new PlaytestStep { Type = StepType.Mcp, Method = "get_hierarchy", ResultVar = null };
            var results = new List<string>();
            int passed = 0, failed = 0;
            var response = "{\"id\":\"t\",\"ok\":true,\"data\":\"hierarchy_text_blob\"}";

            PlaytestRunner.ApplyMcpResult(step, 0, response, results, null, ref passed, ref failed);

            Assert.AreEqual(1, passed);
            Assert.AreEqual(0, failed);
            Assert.AreEqual(1, results.Count);
            StringAssert.Contains("hierarchy_text_blob", results[0]);
        }

        [Test]
        public async Task Run_McpStep_MutatingCommandEditMode_ActuallyMutatesScene()
        {
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(
                "MCP create_object name=McpMutationTarget primitive=Cube\n",
                5f, tcs, requiresPlayMode: false);
            var result = await AwaitBoundedAsync(tcs);

            StringAssert.Contains("PLAYTEST: 1/1", result);
            var go = GameObject.Find("McpMutationTarget");
            Assert.IsNotNull(go, "the MCP create_object step must have actually mutated the scene");
            TrackOwnedObject(go);
        }

        [Test]
        public async Task Run_McpStep_UnknownCommand_ReportsFailNotCrash()
        {
            // CommandRegistry.Execute throws for an unregistered command; CommandRouter.Process's
            // outer catch logs that as an Error (not classified VALIDATION) before returning a
            // clean ok:false envelope. Expected and asserted on, per this codebase's convention
            // (CommandRouterReadOnlyTests.cs) — this is the graceful-failure path, not a crash.
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("Command failed: STATE: Command not registered: totally_unknown_command_xyz"));

            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("MCP totally_unknown_command_xyz\n", 5f, tcs, requiresPlayMode: false);
            var result = await AwaitBoundedAsync(tcs);

            StringAssert.DoesNotContain("PARSE ERROR", result); // C02 does not compile-block unknown commands
            // The step itself reports a graceful FAIL (ApplyMcpResult); the pre-existing
            // CheckStepConsoleErrors mechanism separately also counts the Debug.LogError
            // CommandRouter.Process logs for the same failure — hence 0/2, not 0/1. Neither
            // path throws, and the run reaches a normal terminal PLAYTEST report.
            StringAssert.Contains(
                "[1] MCP totally_unknown_command_xyz — FAIL: STATE: Command not registered: totally_unknown_command_xyz",
                result);
            StringAssert.Contains("PLAYTEST: 0/2", result);
        }

        // ── C04: guard interactions + runtime denylist defense-in-depth ────────────

        [Test]
        public async Task Run_McpStep_MutatingCommandInPlayMode_BlockedByExistingGuard()
        {
            // Proves CommandRouter's existing Play-mode guard already covers MCP DSL
            // steps end-to-end through ProcessAsync — zero new production code needed.
            var savedIsPlayMode = CommandRouter.IsPlayMode;
            CommandRouter.IsPlayMode = () => true;
            try
            {
                var tcs = new TaskCompletionSource<string>();
                PlaytestRunner.Run(
                    "MCP set_property path=/NoSuchObject component=Transform prop=x value=1\n",
                    5f, tcs, requiresPlayMode: false);
                var result = await AwaitBoundedAsync(tcs);

                StringAssert.Contains(
                    "[1] MCP set_property — FAIL: Play mode active — changes will be lost. Stop play mode first.",
                    result);
                StringAssert.Contains("PLAYTEST: 0/1", result);
            }
            finally
            {
                CommandRouter.IsPlayMode = savedIsPlayMode;
            }
        }

        [Test]
        public void Run_McpStep_DenylistedCommandAtRuntime_NeverReached()
        {
            // Belt and suspenders: construct the Mcp step directly, bypassing the parser
            // (and therefore Validate()'s compile-time C02 policy gate entirely), and prove
            // ExecuteStep's own runtime re-check still refuses it before ever building an
            // envelope or touching CommandRouter.
            // sync_unity: hard-denylisted (C02) AND a Python-only tool (CommandRouter's own
            // guard) — even if this test's production check were mistakenly disabled,
            // dispatch could not trigger a real Editor refresh/reload as a side effect.
            var step = new PlaytestStep { Type = StepType.Mcp, Method = "sync_unity", Args = "{}" };
            var results = new List<string>();
            var phase = PlaytestRunner.Phase.Ready;
            float phaseStart = 0f;
            int passed = 0, failed = 0;
            var state = new PlaytestState();

            PlaytestRunner.ExecuteStep(step, null, results, ref phase, ref phaseStart, ref passed, ref failed, 0, state);

            Assert.AreEqual(0, passed);
            Assert.AreEqual(1, failed);
            Assert.AreEqual(PlaytestRunner.Phase.Done, phase);
            StringAssert.Contains("denied", results[0]);
        }
    }
}
