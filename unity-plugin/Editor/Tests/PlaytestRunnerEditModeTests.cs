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
using UnityMCP.Playtest.Core;

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

        // ── Blocker 1: Tick()'s outer catch must route through FinishRun() ──────────
        // A step-handler exception mid-Tick (PollExtrema's readFn is not wrapped in a
        // try/catch, unlike every per-phase branch) used to hit a 5th termination path
        // that skipped FinishRun() entirely: no receipt, no sentinel deletion, no
        // PlaytestRunState.Finish. CAPTURE_MIN registers a tracker whose query is
        // re-evaluated by PollExtrema on every tick regardless of phase; destroying its
        // target object before the next tick makes ReadValue throw ArgumentException
        // ("Object not found") uncaught, exactly like the review's MissingReferenceException
        // scenario (a step handler throwing mid-run after its target is deleted).

        [Test]
        public async Task Run_UnhandledExceptionMidTick_WritesReceiptDeletesSentinelAndReachesTerminalPhase()
        {
            const string runId = "blocker1err";
            var probe = TrackOwnedObject(new GameObject("Blocker1Probe"));

            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(
                "CAPTURE_MIN $probe /Blocker1Probe|Transform|position\nLOG done",
                5f, tcs, requiresPlayMode: false, runId: runId);
            // Destroyed before the tracker is ever polled — the first Tick() call only
            // registers it; the second Tick() call's PollExtrema is where the un-caught
            // "Object not found" exception fires, past every per-phase try/catch.
            UnityEngine.Object.DestroyImmediate(probe);

            var result = await AwaitBoundedAsync(tcs);

            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });
            Assert.IsTrue(File.Exists(receiptPath),
                $"Expected a durable receipt at {receiptPath} even after an unhandled Tick() exception:\n{result}");

            var sentinelPath = Path.GetFullPath(Path.Combine(
                Application.dataPath, "..", PlaytestReceiptStore.SentinelPath(runId)));
            Assert.IsFalse(File.Exists(sentinelPath),
                "Sentinel must be deleted once FinishRun() completes, even on the outer-catch path");

            Assert.AreNotEqual(PlaytestRunState.RunPhase.Running, PlaytestRunState.Current.Phase);
            Assert.AreNotEqual(PlaytestRunState.RunPhase.Idle, PlaytestRunState.Current.Phase);
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

        // ── C20 WAVE C GATE: self-hosting Biome — scalar + composite JSON args reach a
        // registered plugin-style command through the real CommandRouter.ProcessAsync path ──

        [Test]
        public async Task Run_McpStep_PluginCommandReceivesScalarAndCompositeJsonArgs_ThroughRealRouterPath()
        {
            // Simulates a plugin registering its own command outside the parser/runner
            // assemblies — same idiom as PlaytestMcpPolicyTests.Validate_
            // RegisteredPluginCommandAllowedWithoutParserChange (C02) — then proves the
            // DSL's scalar/composite JSON assembly (C01) survives byte-for-byte all the way
            // through PlaytestRunner.Run -> BuildMcpEnvelope -> CommandRouter.ProcessAsync ->
            // CommandRegistry.Execute into the handler's own `args` parameter.
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            string capturedArgs = null;
            CommandRegistry.Register(
                "test_c20_plugin_cmd",
                args => { capturedArgs = args; return "captured"; },
                required: "", optional: "n,flag,tags,obj,label");

            const string runId = "c20gate01";
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run(
                "MCP test_c20_plugin_cmd n=42 flag=true tags=[1,2,\"x\"] obj={\"k\":1} label=hi\n",
                5f, tcs, requiresPlayMode: false, runId: runId, format: "json");
            var result = await AwaitBoundedAsync(tcs);

            var receiptPath = ReceiptFullPath(runId);
            RegisterCleanup(() => { if (File.Exists(receiptPath)) File.Delete(receiptPath); });

            StringAssert.Contains("\"type\":\"Mcp\",\"ok\":true,", result);
            Assert.AreEqual(
                "{\"n\":42,\"flag\":true,\"tags\":[1,2,\"x\"],\"obj\":{\"k\":1},\"label\":\"hi\"}",
                capturedArgs,
                "the plugin handler must receive the exact int/bool/array/object/string JSON " +
                "the DSL line encoded, through the real dispatch path, not a parser-only echo");
        }

        [Test]
        public void CommandRegistry_EnumerateCountAndHardDenylistSubset_AtGateTime()
        {
            // C20 WAVE C GATE: "enumerate the current CommandRegistry count and exact
            // policy-denied subset at implementation time (no approximate hardcoded target)".
            // PlaytestMcpPolicy's own doc comment says the hard denylist is "independent of
            // registration" — this test proves that literally: some denylisted names (Python
            // MCP-tool names) currently have no same-named raw C# CommandRegistry entry at
            // all, so denial for them is pure defense-in-depth, not registry policing.
            var hardDenylist = new[]
            {
                "execute_code", "create_script", "sync_unity", "await_compile",
                "smart_build", "run_tests", "run_playtest", "package", "build",
            };
            foreach (var name in hardDenylist)
                Assert.IsTrue(PlaytestMcpPolicy.IsHardDenied(name),
                    $"'{name}' must be in PlaytestMcpPolicy's hard denylist");

            var registered = new HashSet<string>(CommandRegistry.GetAllCommands());
            var deniedAndRegistered = new List<string>();
            var deniedButUnregistered = new List<string>();
            foreach (var name in hardDenylist)
                (registered.Contains(name) ? deniedAndRegistered : deniedButUnregistered).Add(name);

            TestContext.WriteLine($"CommandRegistry total={registered.Count}");
            TestContext.WriteLine(
                $"hard-denylisted AND registered ({deniedAndRegistered.Count}): {string.Join(",", deniedAndRegistered)}");
            TestContext.WriteLine(
                $"hard-denylisted but not a raw registered command ({deniedButUnregistered.Count}): " +
                string.Join(",", deniedButUnregistered));

            CollectionAssert.AreEquivalent(
                new[] { "execute_code", "run_tests", "run_playtest", "package", "build" },
                deniedAndRegistered);
            CollectionAssert.AreEquivalent(
                new[] { "create_script", "sync_unity", "await_compile", "smart_build" },
                deniedButUnregistered);
        }

        // ── C07: EXPECT_FAIL wired into AdvanceStep, console-error carve-out ────────

        [Test]
        public async Task Run_ExpectFailStep_ConsoleErrorStillFails()
        {
            // The MCP step itself raw-fails (unregistered command — same as
            // Run_McpStep_UnknownCommand_ReportsFailNotCrash) AND CommandRouter logs a real
            // Debug.LogError for that same failure. EXPECT_FAIL must invert the step's own
            // raw fail into a pass, but the separately-counted console error must still stand
            // — the console-error channel is never inverted.
            UnityEngine.TestTools.LogAssert.Expect(
                UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("Command failed: STATE: Command not registered: totally_unknown_command_xyz"));

            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("EXPECT_FAIL\nMCP totally_unknown_command_xyz\n", 5f, tcs, requiresPlayMode: false);
            var result = await AwaitBoundedAsync(tcs);

            // 1 passed (the raw MCP fail inverted by EXPECT_FAIL) + 1 failed (the console
            // error, never inverted) — NOT "0/2" (no inversion) and NOT "1/1" (console
            // channel swallowed by the inversion).
            StringAssert.Contains("PLAYTEST: 1/2", result);
            StringAssert.Contains("CONSOLE_ERR", result);
            StringAssert.DoesNotContain("ABORTED", result);
        }

        [Test]
        public async Task Run_ExpectFailOnPolledStep_Works()
        {
            // WAIT_UNTIL always resolves through Phase.WaitingPoll (never synchronously in
            // Phase.Ready), proving EXPECT_FAIL's before/after baseline survives a step that
            // spans multiple Tick() calls before AdvanceStep is finally reached. The query
            // targets a nonexistent object, so ReadValue throws on every poll tick; after 3
            // consecutive exceptions the poll gives up and raw-fails without waiting out the
            // full timeout.
            var tcs = new TaskCompletionSource<string>();
            PlaytestRunner.Run("EXPECT_FAIL\nWAIT_UNTIL /NoSuchC07PollObject|Foo|bar == 1\n", 5f, tcs, requiresPlayMode: false);
            var result = await AwaitBoundedAsync(tcs);

            StringAssert.Contains("PLAYTEST: 1/1", result);
            StringAssert.DoesNotContain("ABORTED", result);
        }
    }
}
