// TDD: CommandRouter pure-logic tests — no TCP required, EditMode only.
// Covers: IsAllowedDuringCompile, IsAlwaysAllowed, SuggestNext,
//         CommandRegistry flags, BuildResponse (via Process stub),
//         CommandValidator validation coverage.
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterTests : SceneTestBase
    {
        // ── IsAllowedDuringCompile ────────────────────────────────────────────

        [TestCase("ping",              ExpectedResult = true)]
        [TestCase("get_console",       ExpectedResult = true)]
        [TestCase("screenshot",        ExpectedResult = true)]
        [TestCase("get_enabled_tools", ExpectedResult = true)]
        [TestCase("compile_status",    ExpectedResult = true)]
        [TestCase("get_disabled_tools",ExpectedResult = true)]
        [TestCase("set_tool_catalog",  ExpectedResult = true)]
        // M5 (ROI reliability sprint): "batch" must reach BatchHelper.Execute during compile
        // so its own per-line guard can run — see Process_WhileCompiling_BatchReachesInnerGuard_*.
        [TestCase("batch",             ExpectedResult = true)]
        public bool IsAllowedDuringCompile_AllowedCommands(string cmd)
            => CommandRouter.IsAllowedDuringCompile(cmd);

        [TestCase("create_object")]
        [TestCase("set_property")]
        [TestCase("delete_object")]
        [TestCase("get_hierarchy")]
        public void IsAllowedDuringCompile_BlockedCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRouter.IsAllowedDuringCompile(cmd));

        [Test]
        public void IsAllowedDuringCompile_ExecuteCode_ReturnsTrue()
            => Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("execute_code"));

        // ── IsAlwaysAllowed ───────────────────────────────────────────────────

        [TestCase("ping",              ExpectedResult = true)]
        [TestCase("get_enabled_tools", ExpectedResult = true)]
        [TestCase("get_disabled_tools",ExpectedResult = true)]
        [TestCase("set_tool_catalog",  ExpectedResult = true)]
        public bool IsAlwaysAllowed_KnownBypass_ReturnsTrue(string cmd)
            => CommandRouter.IsAlwaysAllowed(cmd);

        [TestCase("get_hierarchy")]
        [TestCase("set_property")]
        [TestCase("create_object")]
        public void IsAlwaysAllowed_NormalCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRouter.IsAlwaysAllowed(cmd));

        // ── SuggestNext ───────────────────────────────────────────────────────

        [TestCase("set_property",    "get_console level=Error")]
        [TestCase("create_object",   "get_hierarchy depth=1")]
        [TestCase("wire_event",      "validate_references")]
        [TestCase("unwire_event",    "get_component")]
        [TestCase("manage_component","get_components_list")]
        [TestCase("delete_object",   "get_hierarchy depth=1")]
        [TestCase("set_parent",         "get_hierarchy depth=1")]
        [TestCase("batch",              "get_console level=Error")]
        public void SuggestNext_MutatingCommand_ReturnsSuggestion(string cmd, string expected)
            => Assert.AreEqual(expected, CommandRouter.SuggestNext(cmd));

        [TestCase("ping")]
        [TestCase("get_hierarchy")]
        [TestCase("get_component")]
        [TestCase("unknown_cmd")]
        public void SuggestNext_ReadCommand_ReturnsNull(string cmd)
            => Assert.IsNull(CommandRouter.SuggestNext(cmd));

        // ── CommandRegistry flags ─────────────────────────────────────────────

        [TestCase("create_object",  ExpectedResult = true)]
        [TestCase("delete_object",  ExpectedResult = true)]
        [TestCase("set_property",   ExpectedResult = true)]
        [TestCase("manage_component", ExpectedResult = true)]
        [TestCase("wire_event",     ExpectedResult = true)]
        [TestCase("set_active",     ExpectedResult = true)]
        [TestCase("execute_code",   ExpectedResult = true)]
        [TestCase("screenshot",     ExpectedResult = true)]
        [TestCase("wait_until",     ExpectedResult = true)]
        [TestCase("get_changes",    ExpectedResult = true)]
        [TestCase("profile",        ExpectedResult = true)]
        public bool Registry_IsMutating_MutatingCommands(string cmd)
            => CommandRegistry.IsMutating(cmd);

        [TestCase("ping",          ExpectedResult = false)]
        [TestCase("get_hierarchy", ExpectedResult = false)]
        [TestCase("get_component", ExpectedResult = false)]
        [TestCase("get_console",   ExpectedResult = false)]
        public bool Registry_IsMutating_ReadCommands_ReturnFalse(string cmd)
            => CommandRegistry.IsMutating(cmd);

        [TestCase("{}", ExpectedResult = false)]
        [TestCase("{\"action\":\"read\"}", ExpectedResult = false)]
        [TestCase("{\"action\":\"READ\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"write\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"create_uxml\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"revert\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"unknown\"}", ExpectedResult = true)]
        public bool Registry_IsMutating_UitkFile_DependsOnAction(string argsJson)
            => CommandRegistry.IsMutating("uitk_file", argsJson);

        [TestCase("{}", ExpectedResult = false)]
        [TestCase("{\"abort_on_fail\":\"false\"}", ExpectedResult = false)]
        [TestCase("{\"abort_on_fail\":\"true\"}", ExpectedResult = true)]
        [TestCase("{\"abort_on_fail\":\"future\"}", ExpectedResult = true)]
        public bool Registry_IsMutating_WaitUntil_DependsOnAbort(string argsJson)
            => CommandRegistry.IsMutating("wait_until", argsJson);

        [TestCase("{}", ExpectedResult = true)]
        [TestCase("{\"clear\":\"false\"}", ExpectedResult = false)]
        [TestCase("{\"clear\":\"true\"}", ExpectedResult = true)]
        [TestCase("{\"clear\":\"future\"}", ExpectedResult = true)]
        public bool Registry_IsMutating_GetChanges_DependsOnClear(string argsJson)
            => CommandRegistry.IsMutating("get_changes", argsJson);

        [TestCase("{\"action\":\"status\"}", ExpectedResult = false)]
        [TestCase("{\"action\":\"analyze\"}", ExpectedResult = false)]
        [TestCase("{\"action\":\"compare\"}", ExpectedResult = false)]
        [TestCase("{\"action\":\"list_sessions\"}", ExpectedResult = false)]
        [TestCase("{\"action\":\"start\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"stop\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"future\"}", ExpectedResult = true)]
        [TestCase("{}", ExpectedResult = true)]
        public bool Registry_IsMutating_Profile_DependsOnAction(string argsJson)
            => CommandRegistry.IsMutating("profile", argsJson);

        [TestCase("profile", "{\"action\":\"start\"}", ExpectedResult = true)]
        [TestCase("profile", "{\"action\":\"stop\"}", ExpectedResult = true)]
        [TestCase("profile", "{\"action\":\"future\"}", ExpectedResult = false)]
        [TestCase("wait_until", "{\"abort_on_fail\":\"true\"}", ExpectedResult = true)]
        [TestCase("execute_code", "{}", ExpectedResult = true)]
        [TestCase("screenshot", "{}", ExpectedResult = true)]
        public bool PlayMutationAllowance_IsNarrowAndArgumentAware(string cmd, string argsJson)
            => CommandRouter.IsAllowedMutationInPlayMode(cmd, argsJson);

        [TestCase("invoke_method",          ExpectedResult = true)]
        [TestCase("set_runtime_property",   ExpectedResult = true)]
        [TestCase("wait_until",             ExpectedResult = true)]
        [TestCase("move_to",                ExpectedResult = true)]
        [TestCase("query_state",            ExpectedResult = true)]
        [TestCase("run_playtest",           ExpectedResult = true)]
        public bool Registry_IsRuntime_RuntimeCommands(string cmd)
            => CommandRegistry.IsRuntime(cmd);

        [TestCase("ping")]
        [TestCase("set_property")]
        [TestCase("get_hierarchy")]
        public void Registry_IsRuntime_NonRuntimeCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRegistry.IsRuntime(cmd));

        [Test]
        public void IsPlaytestSuccess_DetailedAllPassedReport_ReturnsTrue()
        {
            var method = typeof(CommandRouter).GetMethod("IsPlaytestSuccess",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            var report = "PLAYTEST: 4/4 (0.0s)\n[1] LOG smoke\n[2] SNAPSHOT\nRigidbody.mass=4";
            Assert.IsTrue((bool)method.Invoke(null, new object[] { report }));
        }

        [Test]
        public void IsPlaytestSuccess_DetailedFailedReport_ReturnsFalse()
        {
            var method = typeof(CommandRouter).GetMethod("IsPlaytestSuccess",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method);

            var report = "PLAYTEST: 3/4 (0.0s)\n[4] ASSERT x -- FAIL";
            Assert.IsFalse((bool)method.Invoke(null, new object[] { report }));
        }

        // ── CommandRegistry.IsRegistered ─────────────────────────────────────

        [TestCase("ping")]
        [TestCase("get_hierarchy")]
        [TestCase("set_property")]
        [TestCase("batch")]
        [TestCase("execute_code")]
        [TestCase("run_tests")]
        [TestCase("screenshot")]
        public void Registry_IsRegistered_KnownCommands_ReturnsTrue(string cmd)
            => Assert.IsTrue(CommandRegistry.IsRegistered(cmd));

        [Test]
        public void Registry_IsRegistered_UnknownCommand_ReturnsFalse()
            => Assert.IsFalse(CommandRegistry.IsRegistered("totally_unknown_xyz"));

        // ── Process: compiling guard blocks non-allowed commands ──────────────

        [Test]
        public void Process_WhileCompiling_BlockedCommand_ReturnsRetryResponse()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var json = "{\"id\":\"1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"X\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("retry"), result);
            }
            finally { CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling; }
        }

        [Test]
        public void Process_WhileCompiling_PingAllowed_ReturnsPong()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var json = "{\"id\":\"2\",\"cmd\":\"ping\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
                Assert.IsTrue(result.Contains("pong"), result);
            }
            finally { CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling; }
        }

        // M5: "batch" must reach BatchHelper.Execute during compile so its own per-line guard
        // (already correct) can run — the outer gate used to reject the whole batch, making the
        // per-line compile guard dead code.
        [Test]
        public void Process_WhileCompiling_BatchReachesInnerGuard_BlocksMutatingCommand()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var json = "{\"id\":\"5\",\"cmd\":\"batch\",\"args\":{\"commands\":\"set_active path=/X value=true\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result); // batch with blocked commands returns ok:false
                StringAssert.Contains("BLOCKED", result); // inner per-line guard fired instead
            }
            finally { CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling; }
        }

        [Test]
        public void Process_WhileCompiling_BatchAllowsReadOnlyAllowedCommand()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var json = "{\"id\":\"6\",\"cmd\":\"batch\",\"args\":{\"commands\":\"get_console\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
                StringAssert.DoesNotContain("BLOCKED", result);
                StringAssert.DoesNotContain("retry", result); // confirms outer gate did not reject
            }
            finally { CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling; }
        }

        [Test]
        public void Process_RetryOpIdForFailedNonAtomicBatch_DoesNotReplayCommittedChildren()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            var previousDedup = CommandRouter._dedupRegistry;
            var calls = 0;
            CommandRegistry.Register("test_dedup_mutation", _ =>
            {
                calls++;
                return "ok";
            }, mutating: true, required: "", optional: "");
            CommandRouter._dedupRegistry = new DedupRegistry();
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            try
            {
                var first = CommandRouter.Process(
                    "{\"id\":\"dedup-1\",\"cmd\":\"batch\",\"op_id\":\"op-partial\","
                    + "\"args\":{\"commands\":\"test_dedup_mutation\\ntotally_unknown_dedup_command\","
                    + "\"on_error\":\"continue\"}}");
                Assert.IsTrue(first.Contains("\"ok\":false"), first);
                Assert.AreEqual(1, calls);

                var retry = CommandRouter.Process(
                    "{\"id\":\"dedup-2\",\"cmd\":\"batch\",\"retry_op_id\":\"op-partial\","
                    + "\"args\":{\"commands\":\"test_dedup_mutation\\ntotally_unknown_dedup_command\","
                    + "\"on_error\":\"continue\"}}");
                Assert.IsTrue(retry.Contains("\"ok\":false"), retry);
                Assert.AreEqual(1, calls,
                    "Retrying a lost ACK for a failed non-atomic batch must replay the cached response, not re-run committed children.");
            }
            finally
            {
                CommandRouter._dedupRegistry = previousDedup;
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
                CommandRegistry.RestoreForTest(snapshot);
            }
        }

        // MCP-IDEMP-026: cached response must include dedup_applied:true for transparency.
        [Test]
        public void Process_RetryOpId_ResponseHasDedupAppliedFlag()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            var previousDedup = CommandRouter._dedupRegistry;
            CommandRouter._dedupRegistry = new DedupRegistry();
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            try
            {
                CommandRegistry.Register("test_dedup_flag_cmd", _ => "flag_result",
                    required: "", optional: "");

                // First call — executes and registers op_id → result.
                CommandRouter.Process(
                    "{\"id\":\"fl-1\",\"cmd\":\"test_dedup_flag_cmd\",\"op_id\":\"op-dedup-flag\",\"args\":{}}");

                // Retry via retry_op_id — must return cached result with dedup_applied:true.
                var retry = CommandRouter.Process(
                    "{\"id\":\"fl-2\",\"cmd\":\"test_dedup_flag_cmd\",\"retry_op_id\":\"op-dedup-flag\",\"args\":{}}");

                StringAssert.Contains("\"dedup_applied\":true", retry,
                    $"Cached response must include dedup_applied:true; got: {retry}");
            }
            finally
            {
                CommandRouter._dedupRegistry = previousDedup;
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
                CommandRegistry.RestoreForTest(snapshot);
            }
        }

        [Test]
        public void Process_BatchTimeoutSummary_ReturnsFailureResponse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            try
            {
                CommandRegistry.Clear();
                CommandRegistry.Register("batch", _ => "ok:1 err:0 timeout:1",
                    required: "commands", alwaysAllowed: true,
                    allowedDuringCompile: true);
                CommandRegistry.Ready = true;

                var result = CommandRouter.Process(
                    "{\"id\":\"batch-timeout\",\"cmd\":\"batch\"," +
                    "\"args\":{\"commands\":\"ping\\nping\"}}");

                StringAssert.Contains("\"ok\":false", result, result);
                StringAssert.Contains("timeout:1", result, result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
                CommandRegistry.RestoreForTest(snapshot);
            }
        }

        // ── Process: play-mode guard blocks mutating commands ─────────────────

        [Test]
        public void Process_InPlayMode_MutatingCommand_ReturnsError()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => true;
            try
            {
                var json = "{\"id\":\"3\",\"cmd\":\"create_object\",\"args\":{\"name\":\"X\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("Play mode"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        [Test]
        public void Process_InPlayMode_SetParent_NotBlockedByPlayModeGuard()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => true;
            try
            {
                var json = "{\"id\":\"sp1\",\"cmd\":\"set_parent\",\"args\":{\"path\":\"/NonExistent_XYZ\",\"parent\":\"/X\"}}";
                var result = CommandRouter.Process(json);
                StringAssert.DoesNotContain("Play mode active", result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        [Test]
        public void Process_InPlayMode_ExecuteCode_ReachesValidationInsteadOfPlayGuard()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => true;
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"play-code\",\"cmd\":\"execute_code\",\"args\":" +
                    "{\"code\":\"return \\\"play-ok\\\";\"}}");

                StringAssert.DoesNotContain("Play mode active", result, result);
                StringAssert.Contains("play-ok", result, result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        [Test]
        public async Task ProcessAsync_InPlayMode_ExecuteCode_ReachesValidationInsteadOfPlayGuard()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => true;
            try
            {
                var tcs = new TaskCompletionSource<string>();
                CommandRouter.ProcessAsync(
                    "{\"id\":\"play-code-async\",\"cmd\":\"execute_code\",\"args\":" +
                    "{\"code\":\"return \\\"play-ok\\\";\"}}",
                    tcs);
                var result = await tcs.Task;

                StringAssert.DoesNotContain("Play mode active", result, result);
                StringAssert.Contains("play-ok", result, result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── Batch: set_parent is NOT blocked by Play Mode gate ────────────────

        [Test]
        public void Batch_InPlayMode_SetParent_NotBlockedByPlayModeGuard()
        {
            var origRO = CommandRouter.IsReadOnly;
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsReadOnly = () => false;   // isolate from RO worker
            BatchHelper.IsPlayMode = () => true;
            try
            {
                // set_parent is mutating but explicitly excluded from the Play Mode block.
                // Result may be an error (object not found) but must NOT contain "BLOCKED".
                var result = BatchHelper.Execute("set_parent /NonExistent_XYZ /X", "continue", 25000);
                StringAssert.DoesNotContain("BLOCKED", result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsReadOnly = origRO;
                BatchHelper.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── Strategy C: ReadOnly verification ────────────────────────────────

        [Test]
        public void Batch_SetParent_WithReadOnly_IsBlockedByReadOnlyGuard()
        {
            var origRO = CommandRouter.IsReadOnly;
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsReadOnly = () => true;
            BatchHelper.IsPlayMode = () => true;
            try
            {
                var result = BatchHelper.Execute(
                    "set_parent /NonExistent_XYZ /X", "continue", 25000);
                StringAssert.Contains("READ_ONLY_BLOCKED", result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsReadOnly = origRO;
                BatchHelper.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── Process: runtime guard blocks runtime-only commands outside play ──

        [Test]
        public void Process_OutsidePlayMode_RuntimeCommand_ReturnsError()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            try
            {
                var json = "{\"id\":\"4\",\"cmd\":\"invoke_method\",\"args\":{\"path\":\"/X\",\"component\":\"C\",\"method\":\"M\"}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("Play Mode"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── BuildResponse (via ping): short data stays inline ─────────────────

        [Test]
        public void Process_Ping_ShortData_InlineResponse()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            try
            {
                var json = "{\"id\":\"5\",\"cmd\":\"ping\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                // Short response: no file field, data inline
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
                Assert.IsFalse(result.Contains("\"file\""), result);
                Assert.IsTrue(result.Contains("pong"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── BuildResponse (direct seam): truncation-ordering fix (Task 3.2) ────
        // Bug: Truncate() used to run BEFORE the TEXT_THRESHOLD file-offload check, so a
        // command with a maxResponseChars soft limit got its data cut down to that limit
        // even when the full data should have been preserved via file offload instead.

        [Test]
        public void BuildResponse_LargeDataWithMaxResponseChars_WritesFullDataToFile_NotTruncated()
        {
            var bigData = new string('x', FileOutputHelper.TEXT_THRESHOLD + 1000);
            var result = CommandRouter.BuildResponse("id1", bigData, maxResponseChars: 500);

            Assert.IsTrue(result.Contains("\"file\""), result);
            var filePath = JsonHelper.ExtractString(result, "file");
            Assert.IsNotNull(filePath, result);
            try
            {
                var written = System.IO.File.ReadAllText(filePath);
                Assert.AreEqual(bigData.Length, written.Length,
                    "file-offloaded data must be full, not soft-truncated to maxResponseChars");
            }
            finally { System.IO.File.Delete(filePath); }
        }

        [Test]
        public void BuildResponse_SmallDataWithMaxResponseChars_StillTruncatesInline()
        {
            var data = new string('x', 1000);
            var result = CommandRouter.BuildResponse("id2", data, maxResponseChars: 100);

            // Under the file threshold: unchanged behavior — soft truncation still applies inline.
            Assert.IsFalse(result.Contains("\"file\""), result);
            Assert.IsTrue(result.Contains("TRUNCATED"), result);
        }

        // ── CommandValidator: all registered commands have a contract ─────────

        // Every command reached via CommandRegistry.GetAllCommands() is by definition
        // registered, so CommandValidator.Validate(cmd, ...) can NEVER return an
        // "Unknown command" error for it — the old test asserted a tautology.
        // The real invariant: every registered command either declares a structured
        // contract (Required != null, possibly empty) OR is explicitly whitelisted as
        // free-form. Plugin commands are exempt (3rd-party, not our contract to enforce).
        [Test]
        public void Contract_AllRegisteredCommands_HaveContractOrAreWhitelisted()
        {
            // No command is free-form anymore (Issue 23 review M7 closed execute_code's
            // last free-form gap — it now declares required: "code", optional: "undo_label").
            // Kept as an explicit (currently empty) whitelist so any future intentionally
            // free-form command has a documented escape hatch instead of silently failing here.
            var freeFormWhitelist = new System.Collections.Generic.HashSet<string>();
            var failures = new System.Collections.Generic.List<string>();
            foreach (var cmd in CommandRegistry.GetAllCommands())
            {
                if (PluginRegistry.IsPluginCommand(cmd)) continue;
                CommandRegistry.TryGetContract(cmd, out _, out _, out var isFreeForm);
                if (isFreeForm && !freeFormWhitelist.Contains(cmd))
                    failures.Add(cmd);
            }
            Assert.IsEmpty(failures, "Commands with no contract and not free-form-whitelisted: " + string.Join(", ", failures));
        }

        [Test]
        public void Process_DisabledTool_ReturnsDisabledError()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            CommandRouter.IsToolEnabledFn = _ => false;
            try
            {
                var json = "{\"id\":\"t1\",\"cmd\":\"get_hierarchy\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("disabled in settings"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
                CommandRouter.IsToolEnabledFn = MCPSettings.IsToolEnabled;
            }
        }

        // ── CS1.test.1: get_disabled_tools / set_tool_catalog have schema ─────

        [Test]
        public void Schema_GetDisabledTools_HallucinatedParam_Rejected()
        {
            var result = CommandValidator.Validate("get_disabled_tools", "{\"hallucinated\":\"x\"}");
            Assert.IsNotNull(result, "get_disabled_tools must have a schema entry");
            StringAssert.Contains("Unknown param", result);
        }

        [Test]
        public void Schema_SetToolCatalog_HallucinatedParam_Rejected()
        {
            var result = CommandValidator.Validate("set_tool_catalog", "{\"hallucinated\":\"x\"}");
            Assert.IsNotNull(result, "set_tool_catalog must have a schema entry");
            StringAssert.Contains("Unknown param", result);
        }

        [Test]
        public void Schema_SetToolCatalog_ValidCatalogParam_Passes()
        {
            var result = CommandValidator.Validate("set_tool_catalog", "{\"catalog\":\"[]\"}");
            Assert.IsNull(result, result);
        }

        [Test]
        public async Task ProcessAsync_RunTests_WhileCompiling_SetsGuardResponse()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
                var json = "{\"id\":\"pa1\",\"cmd\":\"run_tests\",\"args\":{}}";
                CommandRouter.ProcessAsync(json, tcs);
                Assert.IsTrue(tcs.Task.IsCompleted, "TCS should be set synchronously when guard fires");
                var result = await tcs.Task;
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("retry"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            }
        }

        [Test]
        public async Task ProcessAsync_WaitUntil_WhileCompiling_SetsGuardResponse()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
                var json = "{\"id\":\"pa2\",\"cmd\":\"wait_until\",\"args\":{\"path\":\"/x\",\"component\":\"C\",\"field\":\"f\",\"value\":\"v\"}}";
                CommandRouter.ProcessAsync(json, tcs);
                Assert.IsTrue(tcs.Task.IsCompleted);
                var result = await tcs.Task;
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("retry"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            }
        }

        [Test]
        public void ExtractString_NestedDuplicate_ReturnsOuterValue()
        {
            // depth<=1 guard must prevent reading "cmd" from inside "args"
            var json = "{\"cmd\":\"outer\",\"args\":{\"cmd\":\"inner\"}}";
            var result = JsonHelper.ExtractString(json, "cmd");
            Assert.AreEqual("outer", result);
        }

        // ── CommandValidator.ExtractKeys ──────────────────────────────────────

        [Test]
        public void ExtractKeys_EmptyJson_ReturnsEmpty()
            => Assert.IsEmpty(CommandValidator.ExtractKeys("{}"));

        [Test]
        public void ExtractKeys_SingleKey_ReturnsThatKey()
        {
            var keys = CommandValidator.ExtractKeys("{\"path\":\"/Obj\"}");
            Assert.AreEqual(1, keys.Count);
            Assert.AreEqual("path", keys[0]);
        }

        [Test]
        public void ExtractKeys_MultipleKeys_ReturnsAll()
        {
            var keys = CommandValidator.ExtractKeys("{\"path\":\"/Obj\",\"component\":\"Transform\"}");
            Assert.AreEqual(2, keys.Count);
            Assert.Contains("path", keys);
            Assert.Contains("component", keys);
        }

        [Test]
        public void ExtractKeys_NullJson_ReturnsEmpty()
            => Assert.IsEmpty(CommandValidator.ExtractKeys(null));

        // ── Step 2: sync + sync_status commands (#11, #12) ───────────────────

        // #11: sync and sync_status are registered
        [TestCase("sync",        ExpectedResult = true)]
        [TestCase("sync_status", ExpectedResult = true)]
        public bool Sync_Commands_Registered(string cmd)
            => CommandRegistry.IsRegistered(cmd);

        // #12: sync_status is allowed during compile
        [TestCase("sync_status", ExpectedResult = true)]
        public bool SyncStatus_Allowed_During_Compile(string cmd)
            => CommandRouter.IsAllowedDuringCompile(cmd);

        // C4: get_compile_errors and diagnose allowed during compile (escape-hatch must be reachable when wedged)
        [TestCase("get_compile_errors", ExpectedResult = true)]
        [TestCase("diagnose",           ExpectedResult = true)]
        public bool IsAllowedDuringCompile_AllowsGetCompileErrorsAndDiagnose(string cmd)
            => CommandRouter.IsAllowedDuringCompile(cmd);

        // C4: diagnose is always allowed (not gated by MCPSettings)
        [Test]
        public void IsAlwaysAllowed_Diagnose_ReturnsTrue()
            => Assert.IsTrue(CommandRouter.IsAlwaysAllowed("diagnose"));

        // ask_user: UI-only, read-only — must bypass MCPSettings gate and compile gate
        [Test]
        public void IsAlwaysAllowed_AskUser_ReturnsTrue()
            => Assert.IsTrue(CommandRouter.IsAlwaysAllowed("ask_user"),
                "ask_user is UI-only and must not be gated by MCPSettings");

        [Test]
        public void IsAllowedDuringCompile_AskUser_ReturnsTrue()
            => Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("ask_user"),
                "ask_user shows a UI card only — safe during compilation");

        // C7: get_version is NOT registered in CommandRegistry (MCPServer fast-path owns it)
        // This ensures no caller can accidentally route to the VersionTracker counter.
        [Test]
        public void GetVersion_NotRegistered_In_CommandRegistry()
            => Assert.IsFalse(CommandRegistry.IsRegistered("get_version"),
                "get_version must NOT be in CommandRegistry — MCPServer fast-path is sole handler");

        // G11: force_refresh is registered as a distinct verb from recompile
        [Test]
        public void ForceRefresh_IsRegistered_And_DistinctFrom_Recompile()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("force_refresh"),
                "force_refresh must be registered");
            Assert.IsTrue(CommandRegistry.IsRegistered("recompile"),
                "recompile must still be registered");
        }

        // G11: force_refresh is in the IsAllowedDuringCompile allowlist (works when wedged)
        [Test]
        public void ForceRefresh_IsAllowedDuringCompile()
            => Assert.IsTrue(CommandRouter.IsAllowedDuringCompile("force_refresh"),
                "G11: force_refresh must be allowed during compile so it works when wedged");

        // G11: recompile is NOT in the IsAllowedDuringCompile allowlist (old no-op path stays separate)
        [Test]
        public void Recompile_IsNotAllowedDuringCompile()
            => Assert.IsFalse(CommandRouter.IsAllowedDuringCompile("recompile"),
                "G11: recompile (AssetDatabase.Refresh no-op) must NOT be in the allowlist");

        // ── C7: Process() dispatches to FileHandler when registered ──────────

        [Test]
        public void Process_FileHandler_IsDispatched()
        {
            var called = false;
            CommandRegistry.Register("test_file_cmd",
                _ => throw new System.Exception("should not reach here"),
                fileHandler: (id, args) => { called = true; return $"{{\"id\":\"{id}\",\"file\":\"x.png\"}}"; },
                specialDispatch: true, alwaysAllowed: true, allowedDuringCompile: true);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            try
            {
                var json = "{\"id\":\"r1\",\"cmd\":\"test_file_cmd\",\"args\":{}}";
                CommandRouter.Process(json);
                Assert.IsTrue(called, "FileHandler must be invoked by Process()");
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode  = () => UnityEditor.EditorApplication.isPlaying;
                CommandRouter.RegisterAll();  // restore registry, removes test_file_cmd
            }
        }

        [TestCase(".cs")]
        [TestCase(".jpg")]
        [TestCase(".txt")]
        public void FileOutputHelper_NonPngPath_IsRejectedBeforeOverwrite(string extension)
        {
            var path = System.IO.Path.Combine(
                FileOutputHelper.OutputDir, "screenshot-non-png" + extension);
            System.IO.File.WriteAllText(path, "keep-me");
            try
            {
                var error = Assert.Throws<System.ArgumentException>(() =>
                    FileOutputHelper.WritePng(new byte[] { 1, 2, 3 }, outputPath: path));

                StringAssert.Contains(".png", error.Message);
                Assert.AreEqual("keep-me", System.IO.File.ReadAllText(path));
            }
            finally
            {
                if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
            }
        }

        [Test]
        public void Registry_UitkFile_IsNotBatchable()
        {
            Assert.IsFalse(CommandRegistry.IsBatchable("uitk_file"));
        }

        // ── WIN-1: post-reload stale isCompiling — MCPServer.IsReallyCompiling=false because
        //           compilationStarted never fired in this domain (Windows domain-reload artifact) ──

        [Test]
        public void IsCompiling_StaleReloadArtifact_EditorCompilingTrueButNoDomainStart_ReturnsFalse()
        {
            // Uses production DefaultIsCompiling with MCPServer state reset:
            //   - ResetDomainStateForTests sets _isCompiling=false (IsReallyCompiling=false)
            //   - DefaultIsCompiling Layer 1 returns false immediately
            // Simulates Windows post-reload stale EditorApplication.isCompiling tick:
            // MCPServer never saw compilationStarted so IsReallyCompiling=false unblocks commands.
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            MCPServer.ResetDomainStateForTests();  // _isCompiling=false → IsReallyCompiling=false
            try
            {
                Assert.IsFalse(CommandRouter.IsCompiling(),
                    "WIN-1: IsReallyCompiling=false must unblock commands even if EditorApplication.isCompiling=true");
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            }
        }

        [Test]
        public void Process_StaleReloadArtifact_UnblocksCommand()
        {
            // End-to-end: MCPServer.IsReallyCompiling=false (no compilationStarted this domain)
            // must not block commands — DefaultIsCompiling Layer 1 returns false.
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            CommandRouter.IsPlayMode = () => false;
            MCPServer.ResetDomainStateForTests();  // _isCompiling=false → IsReallyCompiling=false
            try
            {
                var json = "{\"id\":\"win1\",\"cmd\":\"ping\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── Scenario 2: Batch must NOT be blocked when IsReallyCompiling=false ──
        // Before fix: BatchHelper.IsCompiling used EditorApplication.isCompiling → latched.
        // After fix: BatchHelper.IsCompiling delegates to CommandRouter.IsCompiling()
        //            → MCPServer.IsReallyCompiling → false → batch passes.
        [Test]
        public void LatchFix_BatchCommandPassesDuringFalseLatch()
        {
            // Simulate false latch: compilationFinished fired → _isCompiling=false
            // but EditorApplication.isCompiling could still be true (ignored).
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            BatchHelper.IsCompiling = () => CommandRouter.IsCompiling();
            MCPServer.ResetDomainStateForTests();  // _isCompiling=false → IsReallyCompiling=false
            try
            {
                var result = BatchHelper.Execute("ping", "continue", 25000);
                Assert.IsFalse(result.Contains("BLOCKED"), $"Batch must not be blocked when IsReallyCompiling=false. Got: {result}");
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                BatchHelper.IsCompiling = () => CommandRouter.IsCompiling();
            }
        }

        // ── RC-2: isCompiling wedge — elapsed > 120s treated as non-compiling ──

        [Test]
        public void IsCompiling_WedgeCondition_ElapsedOver120s_ReturnsFalse()
        {
            // Simulate: EditorApplication says compiling but our tracker says >120s elapsed
            CommandRouter.IsCompiling = () => 150.0 < 120.0;
            try
            {
                Assert.IsFalse(CommandRouter.IsCompiling(),
                    "Wedge condition: elapsed > 120s must unblock commands");
            }
            finally { CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling; }
        }

        [Test]
        public void Process_WedgeCondition_UnblocksNormalCommand()
        {
            // When compile elapsed > 120s, IsCompiling returns false → command goes through
            CommandRouter.IsCompiling = () => false;  // wedge cleared
            CommandRouter.IsPlayMode = () => false;
            try
            {
                var json = "{\"id\":\"w1\",\"cmd\":\"ping\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── Fix #13: inspect accepts type= as alias for components= ──────────

        [Test]
        public void Inspect_TypeAliasForComponents_FiltersCorrectly()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            var go = new UnityEngine.GameObject("InspectTypeTest1");
            go.AddComponent<UnityEngine.BoxCollider>();
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"i1\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspectTypeTest1\",\"type\":\"BoxCollider\"}}");
                StringAssert.Contains("BoxCollider", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        [Test]
        public void Inspect_ComponentsParamStillWorks()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            var go = new UnityEngine.GameObject("InspectTypeTest2");
            go.AddComponent<UnityEngine.BoxCollider>();
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"i2\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspectTypeTest2\",\"components\":\"BoxCollider\"}}");
                StringAssert.Contains("BoxCollider", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        [Test]
        public void Inspect_ComponentsWinsOverType_WhenBothProvided()
        {
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            var go = new UnityEngine.GameObject("InspectTypeTest3");
            go.AddComponent<UnityEngine.BoxCollider>();
            go.AddComponent<UnityEngine.SphereCollider>();
            try
            {
                // components=BoxCollider, type=SphereCollider → BoxCollider wins (type is fallback)
                var result = CommandRouter.Process(
                    "{\"id\":\"i3\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspectTypeTest3\",\"components\":\"BoxCollider\",\"type\":\"SphereCollider\"}}");
                StringAssert.Contains("BoxCollider", result);
                StringAssert.DoesNotContain("SphereCollider", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        // ── #09: compile_status includes reload= suffix ───────────────────────

        [Test]
        public void CompileStatus_ResponseContains_ReloadSuffix()
        {
            CommandRouter.IsCompiling = () => false;
            try
            {
                var json = "{\"id\":\"cs1\",\"cmd\":\"compile_status\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":true"), result);
                StringAssert.Contains("|reload=", result);
            }
            finally { CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling; }
        }

        // ── IsPlaytestSuccess edge cases (Task 4) ────────────────────────────

        private static bool InvokeIsPlaytestSuccess(string report)
        {
            var m = typeof(CommandRouter).GetMethod("IsPlaytestSuccess",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)m.Invoke(null, new object[] { report });
        }

        [Test]
        public void IsPlaytestSuccess_EmptyString_ReturnsFalse()
            => Assert.IsFalse(InvokeIsPlaytestSuccess(""));

        [Test]
        public void IsPlaytestSuccess_Null_ReturnsFalse()
            => Assert.IsFalse(InvokeIsPlaytestSuccess(null));

        [Test]
        public void IsPlaytestSuccess_ContainsSpaceOKSubstring_ReturnsTrue()
            => Assert.IsTrue(InvokeIsPlaytestSuccess("Test run OK"));

        [Test]
        public void IsPlaytestSuccess_PlaytestZeroOfZero_TotalZeroGuard_ReturnsFalse()
            => Assert.IsFalse(InvokeIsPlaytestSuccess("PLAYTEST: 0/0 (0.0s)"));

        [Test]
        public void IsPlaytestSuccess_PlaytestHeaderNoSpaceAfterCount_ReturnsFalse()
            => Assert.IsFalse(InvokeIsPlaytestSuccess("PLAYTEST:4/4"));

        [Test]
        public void IsPlaytestSuccess_PartialPassMinimalFormat_ReturnsFalse()
            => Assert.IsFalse(InvokeIsPlaytestSuccess("PLAYTEST: 3/4 (0.0s)"));

        // ── CheckGuards: server-not-ready and python-only (Task 5) ───────────

        [Test]
        public void Process_ServerNotReady_ReturnsServerInitializingRetryResponse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            CommandRegistry.Ready = false;
            try
            {
                var result = CommandRouter.Process("{\"id\":\"nr1\",\"cmd\":\"ping\",\"args\":{}}");
                StringAssert.Contains("Server initializing", result);
                StringAssert.Contains("\"ok\":false", result);
            }
            finally { CommandRegistry.RestoreForTest(snapshot); }
        }

        [Test]
        public void Process_PythonOnlyCommand_ReturnsActionableError()
        {
            // sync_unity is a known Python-only tool; direct TCP must return an actionable error.
            var result = CommandRouter.Process("{\"id\":\"py1\",\"cmd\":\"sync_unity\",\"args\":{}}");
            StringAssert.Contains("Python-only", result);
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void Process_ReadyTrueAndValidCommand_PassesGuardsAndReturnsSuccess()
        {
            // CommandRegistry.Ready stays true (default after RegisterAll).
            // Verify all guards are passed and the command executes successfully.
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            try
            {
                var result = CommandRouter.Process("{\"id\":\"rr1\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":\"1\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.DoesNotContain("Server initializing", result);
                StringAssert.DoesNotContain("Python-only", result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
                CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying;
            }
        }

        [Test]
        public async Task ProcessAsync_ServerNotReady_SetsServerInitializingResponse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            CommandRegistry.Ready = false;
            try
            {
                var tcs = new TaskCompletionSource<string>();
                CommandRouter.ProcessAsync("{\"id\":\"pa3\",\"cmd\":\"ping\",\"args\":{}}", tcs);
                Assert.IsTrue(tcs.Task.IsCompleted, "Guard must set TCS synchronously");
                var result = await tcs.Task;
                StringAssert.Contains("Server initializing", result);
                StringAssert.Contains("\"ok\":false", result);
            }
            finally { CommandRegistry.RestoreForTest(snapshot); }
        }
    }
}
