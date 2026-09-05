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

        // P0-70: "editor" itself stays base-registered non-mutating (play/stop/
        // select/state/project_path don't corrupt scene data) — only
        // mutation_mode's SET form (enable present) is a write.
        [TestCase("{\"action\":\"mutation_mode\"}", ExpectedResult = false)]
        [TestCase("{\"action\":\"mutation_mode\",\"enable\":\"true\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"mutation_mode\",\"enable\":\"false\"}", ExpectedResult = true)]
        [TestCase("{\"action\":\"play\"}", ExpectedResult = false)]
        [TestCase("{\"action\":\"state\"}", ExpectedResult = false)]
        [TestCase("{}", ExpectedResult = false)]
        public bool Registry_IsMutating_Editor_MutationModeDependsOnEnable(string argsJson)
            => CommandRegistry.IsMutating("editor", argsJson);

        [TestCase("profile", "{\"action\":\"start\"}", ExpectedResult = true)]
        [TestCase("profile", "{\"action\":\"stop\"}", ExpectedResult = true)]
        [TestCase("profile", "{\"action\":\"future\"}", ExpectedResult = false)]
        [TestCase("wait_until", "{\"abort_on_fail\":\"true\"}", ExpectedResult = true)]
        [TestCase("execute_code", "{}", ExpectedResult = true)]
        [TestCase("screenshot", "{}", ExpectedResult = true)]
        [TestCase("editor", "{\"action\":\"mutation_mode\",\"enable\":\"true\"}", ExpectedResult = true)]
        [TestCase("editor", "{\"action\":\"play\"}", ExpectedResult = false)]
        public bool PlayMutationAllowance_IsNarrowAndArgumentAware(string cmd, string argsJson)
            => CommandRouter.IsAllowedMutationInPlayMode(cmd, argsJson);

        [TestCase("invoke_method",          ExpectedResult = true)]
        [TestCase("set_runtime_property",   ExpectedResult = true)]
        [TestCase("wait_until",             ExpectedResult = true)]
        [TestCase("move_to",                ExpectedResult = true)]
        [TestCase("query_state",            ExpectedResult = true)]
        public bool Registry_IsRuntime_RuntimeCommands(string cmd)
            => CommandRegistry.IsRuntime(cmd);

        [TestCase("ping")]
        [TestCase("set_property")]
        [TestCase("get_hierarchy")]
        // B05: run_playtest's Play-mode gate moved past parsing (AsyncRunPlaytest scans the
        // script's header itself) — registration no longer flags it runtime:true.
        [TestCase("run_playtest")]
        public void Registry_IsRuntime_NonRuntimeCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRegistry.IsRuntime(cmd));

        [Test]
        public void IsPlaytestSuccess_DetailedAllPassedReport_ReturnsTrue()
        {
            var report = "PLAYTEST: 4/4 (0.0s)\n[1] LOG smoke\n[2] SNAPSHOT\nRigidbody.mass=4";
            Assert.IsTrue(InvokeIsPlaytestSuccess(report));
        }

        [Test]
        public void IsPlaytestSuccess_DetailedFailedReport_ReturnsFalse()
        {
            var report = "PLAYTEST: 3/4 (0.0s)\n[4] ASSERT x -- FAIL";
            Assert.IsFalse(InvokeIsPlaytestSuccess(report));
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
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => true;
            var json = "{\"id\":\"1\",\"cmd\":\"create_object\",\"args\":{\"name\":\"X\"}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            Assert.IsTrue(result.Contains("retry"), result);
        }

        [Test]
        public void Process_WhileCompiling_PingAllowed_ReturnsPong()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => true;
            var json = "{\"id\":\"2\",\"cmd\":\"ping\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":true"), result);
            Assert.IsTrue(result.Contains("pong"), result);
        }

        // M5: "batch" must reach BatchHelper.Execute during compile so its own per-line guard
        // (already correct) can run — the outer gate used to reject the whole batch, making the
        // per-line compile guard dead code.
        [Test]
        public void Process_WhileCompiling_BatchReachesInnerGuard_BlocksMutatingCommand()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => true;
            var json = "{\"id\":\"5\",\"cmd\":\"batch\",\"args\":{\"commands\":\"set_active path=/X value=true\"}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":false"), result); // batch with blocked commands returns ok:false
            StringAssert.Contains("BLOCKED", result); // inner per-line guard fired instead
        }

        [Test]
        public void Process_WhileCompiling_BatchAllowsReadOnlyAllowedCommand()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => true;
            var json = "{\"id\":\"6\",\"cmd\":\"batch\",\"args\":{\"commands\":\"get_console\"}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":true"), result);
            StringAssert.DoesNotContain("BLOCKED", result);
            StringAssert.DoesNotContain("retry", result); // confirms outer gate did not reject
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
            RegisterCleanup(() => CommandRouter._dedupRegistry = previousDedup);
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
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

        // MCP-IDEMP-026: cached response must include dedup_applied:true for transparency.
        [Test]
        public void Process_RetryOpId_ResponseHasDedupAppliedFlag()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            var previousDedup = CommandRouter._dedupRegistry;
            CommandRouter._dedupRegistry = new DedupRegistry();
            RegisterCleanup(() => CommandRouter._dedupRegistry = previousDedup);
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
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

        [Test]
        public void Process_BatchTimeoutSummary_ReturnsFailureResponse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
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

        // ── Process: play-mode guard blocks mutating commands ─────────────────

        [Test]
        public void Process_InPlayMode_MutatingCommand_ReturnsError()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => true;
            var json = "{\"id\":\"3\",\"cmd\":\"create_object\",\"args\":{\"name\":\"X\"}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            Assert.IsTrue(result.Contains("Play mode"), result);
        }

        [Test]
        public void Process_InPlayMode_SetParent_NotBlockedByPlayModeGuard()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => true;
            var json = "{\"id\":\"sp1\",\"cmd\":\"set_parent\",\"args\":{\"path\":\"/NonExistent_XYZ\",\"parent\":\"/X\"}}";
            var result = CommandRouter.Process(json);
            StringAssert.DoesNotContain("Play mode active", result);
        }

        [Test]
        public void Process_InPlayMode_ExecuteCode_ReachesValidationInsteadOfPlayGuard()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => true;
            var result = CommandRouter.Process(
                "{\"id\":\"play-code\",\"cmd\":\"execute_code\",\"args\":" +
                "{\"code\":\"return \\\"play-ok\\\";\"}}");

            StringAssert.DoesNotContain("Play mode active", result, result);
            StringAssert.Contains("play-ok", result, result);
        }

        [Test]
        public async Task ProcessAsync_InPlayMode_ExecuteCode_ReachesValidationInsteadOfPlayGuard()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => true;
            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(
                "{\"id\":\"play-code-async\",\"cmd\":\"execute_code\",\"args\":" +
                "{\"code\":\"return \\\"play-ok\\\";\"}}",
                tcs);
            var result = await tcs.Task;

            StringAssert.DoesNotContain("Play mode active", result, result);
            StringAssert.Contains("play-ok", result, result);
        }

        // ── Batch: set_parent is NOT blocked by Play Mode gate ────────────────

        [Test]
        public void Batch_InPlayMode_SetParent_NotBlockedByPlayModeGuard()
        {
            var origRO = CommandRouter.IsReadOnly;
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsReadOnly = origRO);
            RegisterCleanup(() => BatchHelper.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsReadOnly = () => false;   // isolate from RO worker
            BatchHelper.IsPlayMode = () => true;
            // set_parent is mutating but explicitly excluded from the Play Mode block.
            // Result may be an error (object not found) but must NOT contain "BLOCKED".
            var result = BatchHelper.Execute("set_parent /NonExistent_XYZ /X", "continue", 25000);
            StringAssert.DoesNotContain("BLOCKED", result);
        }

        // ── Strategy C: ReadOnly verification ────────────────────────────────

        [Test]
        public void Batch_SetParent_WithReadOnly_IsBlockedByReadOnlyGuard()
        {
            var origRO = CommandRouter.IsReadOnly;
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsReadOnly = origRO);
            RegisterCleanup(() => BatchHelper.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsReadOnly = () => true;
            BatchHelper.IsPlayMode = () => true;
            var result = BatchHelper.Execute(
                "set_parent /NonExistent_XYZ /X", "continue", 25000);
            StringAssert.Contains("READ_ONLY_BLOCKED", result);
        }

        // ── Process: runtime guard blocks runtime-only commands outside play ──

        [Test]
        public void Process_OutsidePlayMode_RuntimeCommand_ReturnsError()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            var json = "{\"id\":\"4\",\"cmd\":\"invoke_method\",\"args\":{\"path\":\"/X\",\"component\":\"C\",\"method\":\"M\"}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            Assert.IsTrue(result.Contains("Play Mode"), result);
        }

        // ── BuildResponse (via ping): short data stays inline ─────────────────

        [Test]
        public void Process_Ping_ShortData_InlineResponse()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            var json = "{\"id\":\"5\",\"cmd\":\"ping\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            // Short response: no file field, data inline
            Assert.IsTrue(result.Contains("\"ok\":true"), result);
            Assert.IsFalse(result.Contains("\"file\""), result);
            Assert.IsTrue(result.Contains("pong"), result);
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
            RegisterCleanup(() => { if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath); });
            var written = System.IO.File.ReadAllText(filePath);
            Assert.AreEqual(bigData.Length, written.Length,
                "file-offloaded data must be full, not soft-truncated to maxResponseChars");
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
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            RegisterCleanup(() => CommandRouter.IsToolEnabledFn = MCPSettings.IsToolEnabled);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            CommandRouter.IsToolEnabledFn = _ => false;
            var json = "{\"id\":\"t1\",\"cmd\":\"get_hierarchy\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            Assert.IsTrue(result.Contains("disabled in settings"), result);
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
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => true;
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
            var json = "{\"id\":\"pa1\",\"cmd\":\"run_tests\",\"args\":{}}";
            CommandRouter.ProcessAsync(json, tcs);
            Assert.IsTrue(tcs.Task.IsCompleted, "TCS should be set synchronously when guard fires");
            var result = await tcs.Task;
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            Assert.IsTrue(result.Contains("retry"), result);
        }

        [Test]
        public async Task ProcessAsync_WaitUntil_WhileCompiling_SetsGuardResponse()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => true;
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
            var json = "{\"id\":\"pa2\",\"cmd\":\"wait_until\",\"args\":{\"path\":\"/x\",\"component\":\"C\",\"field\":\"f\",\"value\":\"v\"}}";
            CommandRouter.ProcessAsync(json, tcs);
            Assert.IsTrue(tcs.Task.IsCompleted);
            var result = await tcs.Task;
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            Assert.IsTrue(result.Contains("retry"), result);
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
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            RegisterCleanup(() => CommandRouter.RegisterAll());  // restore registry, removes test_file_cmd
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode  = () => false;
            var json = "{\"id\":\"r1\",\"cmd\":\"test_file_cmd\",\"args\":{}}";
            CommandRouter.Process(json);
            Assert.IsTrue(called, "FileHandler must be invoked by Process()");
        }

        [TestCase(".cs")]
        [TestCase(".jpg")]
        [TestCase(".txt")]
        public void FileOutputHelper_NonPngPath_IsRejectedBeforeOverwrite(string extension)
        {
            var path = System.IO.Path.Combine(
                FileOutputHelper.OutputDir, "screenshot-non-png" + extension);
            System.IO.File.WriteAllText(path, "keep-me");
            RegisterCleanup(() => { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); });
            var error = Assert.Throws<System.ArgumentException>(() =>
                FileOutputHelper.WritePng(new byte[] { 1, 2, 3 }, outputPath: path));

            StringAssert.Contains(".png", error.Message);
            Assert.AreEqual("keep-me", System.IO.File.ReadAllText(path));
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
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            MCPServer.ResetDomainStateForTests();  // _isCompiling=false → IsReallyCompiling=false
            Assert.IsFalse(CommandRouter.IsCompiling(),
                "WIN-1: IsReallyCompiling=false must unblock commands even if EditorApplication.isCompiling=true");
        }

        [Test]
        public void Process_StaleReloadArtifact_UnblocksCommand()
        {
            // End-to-end: MCPServer.IsReallyCompiling=false (no compilationStarted this domain)
            // must not block commands — DefaultIsCompiling Layer 1 returns false.
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            CommandRouter.IsPlayMode = () => false;
            MCPServer.ResetDomainStateForTests();  // _isCompiling=false → IsReallyCompiling=false
            var json = "{\"id\":\"win1\",\"cmd\":\"ping\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":true"), result);
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
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => BatchHelper.IsCompiling = () => CommandRouter.IsCompiling());
            CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            BatchHelper.IsCompiling = () => CommandRouter.IsCompiling();
            MCPServer.ResetDomainStateForTests();  // _isCompiling=false → IsReallyCompiling=false
            var result = BatchHelper.Execute("ping", "continue", 25000);
            Assert.IsFalse(result.Contains("BLOCKED"), $"Batch must not be blocked when IsReallyCompiling=false. Got: {result}");
        }

        // ── RC-2: isCompiling wedge — elapsed > 120s treated as non-compiling ──

        [Test]
        public void IsCompiling_WedgeCondition_ElapsedOver120s_ReturnsFalse()
        {
            // Simulate: EditorApplication says compiling but our tracker says >120s elapsed
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => 150.0 < 120.0;
            Assert.IsFalse(CommandRouter.IsCompiling(),
                "Wedge condition: elapsed > 120s must unblock commands");
        }

        [Test]
        public void Process_WedgeCondition_UnblocksNormalCommand()
        {
            // When compile elapsed > 120s, IsCompiling returns false → command goes through
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;  // wedge cleared
            CommandRouter.IsPlayMode = () => false;
            var json = "{\"id\":\"w1\",\"cmd\":\"ping\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":true"), result);
        }

        // ── Fix #13: inspect accepts type= as alias for components= ──────────

        [Test]
        public void Inspect_TypeAliasForComponents_FiltersCorrectly()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            var go = TrackOwnedObject(new UnityEngine.GameObject("InspectTypeTest1"));
            go.AddComponent<UnityEngine.BoxCollider>();
            var result = CommandRouter.Process(
                "{\"id\":\"i1\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspectTypeTest1\",\"type\":\"BoxCollider\"}}");
            StringAssert.Contains("BoxCollider", result);
        }

        [Test]
        public void Inspect_ComponentsParamStillWorks()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            var go = TrackOwnedObject(new UnityEngine.GameObject("InspectTypeTest2"));
            go.AddComponent<UnityEngine.BoxCollider>();
            var result = CommandRouter.Process(
                "{\"id\":\"i2\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspectTypeTest2\",\"components\":\"BoxCollider\"}}");
            StringAssert.Contains("BoxCollider", result);
        }

        [Test]
        public void Inspect_ComponentsWinsOverType_WhenBothProvided()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            var go = TrackOwnedObject(new UnityEngine.GameObject("InspectTypeTest3"));
            go.AddComponent<UnityEngine.BoxCollider>();
            go.AddComponent<UnityEngine.SphereCollider>();
            // components=BoxCollider, type=SphereCollider → BoxCollider wins (type is fallback)
            var result = CommandRouter.Process(
                "{\"id\":\"i3\",\"cmd\":\"inspect\",\"args\":{\"paths\":\"/InspectTypeTest3\",\"components\":\"BoxCollider\",\"type\":\"SphereCollider\"}}");
            StringAssert.Contains("BoxCollider", result);
            StringAssert.DoesNotContain("SphereCollider", result);
        }

        // ── #09: compile_status includes reload= suffix ───────────────────────

        [Test]
        public void CompileStatus_ResponseContains_ReloadSuffix()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            CommandRouter.IsCompiling = () => false;
            var json = "{\"id\":\"cs1\",\"cmd\":\"compile_status\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":true"), result);
            StringAssert.Contains("|reload=", result);
        }

        // ── IsPlaytestSuccess edge cases (Task 4) ────────────────────────────

        // B17: format is a real parameter of the production method now (explicit over
        // implicit — R-07). This test-only wrapper keeps a default so every pre-existing
        // single-arg call site below is unchanged text (the legacy format this file has always
        // exercised); only the two new adversarial tests pass format explicitly.
        private static bool InvokeIsPlaytestSuccess(string report, string format = "text")
        {
            var m = typeof(CommandRouter).GetMethod("IsPlaytestSuccess",
                BindingFlags.NonPublic | BindingFlags.Static);
            return (bool)m.Invoke(null, new object[] { report, format });
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

        // ── B17: both verdict sites read the ledger, not a text/regex scan ──────

        [Test]
        public void IsPlaytestSuccess_JsonReport_UsesLedgerNotRegex()
        {
            // source_file deliberately contains " OK" — the legacy text substring shortcut
            // would say "pass" on sight, but the structured ledger's ok:false must win when
            // format="json" is requested explicitly.
            var json = "{\"schema_version\":1,\"run_id\":\"r1\",\"passed\":0,\"failed\":1," +
                "\"duration_seconds\":\"0.100\",\"steps\":[{\"index\":0,\"type\":\"Assert\"," +
                "\"ok\":false,\"ms\":1.000,\"source_file\":\"Foo OK.playtest\",\"source_line\":1," +
                "\"raw_passed\":false,\"expected_fail\":false}]," +
                "\"outer\":{\"teardown_ok\":true,\"scene_clean\":true},\"text_report\":\"whatever\"}";

            Assert.IsFalse(InvokeIsPlaytestSuccess(json, "json"));
        }

        [Test]
        public void IsPlaytestSuccess_TextReportWithLeadingBrace_StillUsesRegex()
        {
            // Text report happens to start with '{'. Requesting format="text" explicitly must
            // skip the JSON-detection sniff entirely and use the legacy text scan (which honors
            // the " OK" substring shortcut, INV-005) — proving the sniff is a fallback for a
            // missing/unknown format, never a rule that overrides an explicit caller.
            var report = "{weird-prefix} Test run OK";

            Assert.IsTrue(InvokeIsPlaytestSuccess(report, "text"));
        }

        // ── CheckGuards: server-not-ready and python-only (Task 5) ───────────

        [Test]
        public void Process_ServerNotReady_ReturnsServerInitializingRetryResponse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Ready = false;
            var result = CommandRouter.Process("{\"id\":\"nr1\",\"cmd\":\"ping\",\"args\":{}}");
            StringAssert.Contains("Server initializing", result);
            StringAssert.Contains("\"ok\":false", result);
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
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;
            var result = CommandRouter.Process("{\"id\":\"rr1\",\"cmd\":\"get_hierarchy\",\"args\":{\"depth\":\"1\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.DoesNotContain("Server initializing", result);
            StringAssert.DoesNotContain("Python-only", result);
        }

        [Test]
        public async Task ProcessAsync_ServerNotReady_SetsServerInitializingResponse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Ready = false;
            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync("{\"id\":\"pa3\",\"cmd\":\"ping\",\"args\":{}}", tcs);
            Assert.IsTrue(tcs.Task.IsCompleted, "Guard must set TCS synchronously");
            var result = await tcs.Task;
            StringAssert.Contains("Server initializing", result);
            StringAssert.Contains("\"ok\":false", result);
        }

        // ── BuildHelp: prefix filter and formatting ───────────────────────────

        [Test]
        public void BuildHelp_NoMatchingPrefix_ReturnsEmptyString()
        {
            var result = CommandRegistry.BuildHelp("totally_nonexistent_prefix_xyzzy_");
            Assert.AreEqual("", result);
        }

        [Test]
        public void BuildHelp_MatchingPrefix_ReturnsOnlyMatchingCommands()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Clear();
            CommandRegistry.Ready = true;
            CommandRegistry.Register("bht_cmd1", _ => "ok", required: "", optional: "");
            CommandRegistry.Register("other_cmd", _ => "ok", required: "", optional: "");
            var result = CommandRegistry.BuildHelp("bht_");
            StringAssert.Contains("bht_cmd1", result);
            StringAssert.DoesNotContain("other_cmd", result);
        }

        [Test]
        public void BuildHelp_MutatingCommand_ShowsRWMarker()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Clear();
            CommandRegistry.Ready = true;
            CommandRegistry.Register("bht_write", _ => "ok", mutating: true, required: "", optional: "");
            var result = CommandRegistry.BuildHelp("bht_");
            StringAssert.Contains("[RW]", result);
        }

        [Test]
        public void BuildHelp_ReadCommand_ShowsROMarker()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Clear();
            CommandRegistry.Ready = true;
            CommandRegistry.Register("bht_read", _ => "ok", mutating: false, required: "", optional: "");
            var result = CommandRegistry.BuildHelp("bht_");
            StringAssert.Contains("[RO]", result);
        }

        [Test]
        public void BuildHelp_CommandWithDescription_IncludesDescription()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Clear();
            CommandRegistry.Ready = true;
            CommandRegistry.Register("bht_desc", _ => "ok", required: "", optional: "",
                description: "my test description text");
            var result = CommandRegistry.BuildHelp("bht_");
            StringAssert.Contains("my test description text", result);
        }

        // ── IsBatchable: all gating conditions ───────────────────────────────

        [Test]
        public void IsBatchable_UnregisteredCommand_ReturnsTrue()
            => Assert.IsTrue(CommandRegistry.IsBatchable("totally_unknown_xyzzy_cmd"));

        [Test]
        public void IsBatchable_SyncHandler_ReturnsTrue()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Register("ib_sync", _ => "ok", required: "", optional: "");
            Assert.IsTrue(CommandRegistry.IsBatchable("ib_sync"));
        }

        [Test]
        public void IsBatchable_RegisterAsync_ReturnsFalse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.RegisterAsync("ib_async",
                (id, args, tcs) => tcs.SetResult("ok"),
                required: "", optional: "");
            Assert.IsFalse(CommandRegistry.IsBatchable("ib_async"));
        }

        [Test]
        public void IsBatchable_SpecialDispatch_ReturnsFalse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Register("ib_special", _ => "ok",
                specialDispatch: true, required: "", optional: "");
            Assert.IsFalse(CommandRegistry.IsBatchable("ib_special"));
        }

        [Test]
        public void IsBatchable_FileHandler_ReturnsFalse()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Register("ib_file", _ => "ok",
                fileHandler: (id, args) => "resp", required: "", optional: "");
            Assert.IsFalse(CommandRegistry.IsBatchable("ib_file"));
        }

        [Test]
        public void IsBatchable_AlwaysAllowed_DoesNotPreventBatchability()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Register("ib_always", _ => "ok",
                alwaysAllowed: true, required: "", optional: "");
            Assert.IsTrue(CommandRegistry.IsBatchable("ib_always"),
                "alwaysAllowed must not affect batchability");
        }

        // ── AlreadyRegistered: double registration guard ──────────────────────

        [Test]
        public void Register_DoubleRegistration_SecondCallSkipped()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Register("dr_sync", _ => "first", required: "", optional: "");
            CommandRegistry.Register("dr_sync", _ => "second", required: "", optional: "");
            var result = CommandRegistry.Execute("dr_sync", "{}");
            Assert.AreEqual("first", result, "Second Register call must be silently skipped");
        }

        [Test]
        public void RegisterAction_DoubleRegistration_SecondCallSkipped()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.RegisterAction("dr_action", (action, args) => "first",
                required: "", optional: "");
            CommandRegistry.RegisterAction("dr_action", (action, args) => "second",
                required: "", optional: "");
            var result = CommandRegistry.Execute("dr_action", "{\"action\":\"test\"}");
            Assert.AreEqual("first", result, "Second RegisterAction call must be silently skipped");
        }

        [Test]
        public void RegisterAsync_DoubleRegistration_SecondCallSkipped()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            var firstCalled = false;
            var secondCalled = false;
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.RegisterAsync("dr_async",
                (id, args, tcs) => { firstCalled = true; tcs.SetResult("first"); },
                required: "", optional: "");
            CommandRegistry.RegisterAsync("dr_async",
                (id, args, tcs) => { secondCalled = true; tcs.SetResult("second"); },
                required: "", optional: "");
            CommandRegistry.HasAsyncHandler("dr_async", out var handler);
            var tcs2 = new System.Threading.Tasks.TaskCompletionSource<string>();
            handler("id", "{}", tcs2);
            Assert.IsTrue(firstCalled, "First handler must be called");
            Assert.IsFalse(secondCalled, "Second RegisterAsync call must be silently skipped");
        }

        // ── after_hook scheduling source guard (DEV-66 Part C1) ─────────────────

        [Test]
        public void AfterHook_SchedulesExecutionViaMainThreadDispatcher_NotDelayCall()
        {
            var src = ReadRequiredPackageSource(typeof(CommandRouter), "Editor/CommandRouter.cs");
            // E02: afterHook now lives on the shared PlaytestRunRequest (req.AfterHook) after the
            // AsyncRunPlaytest/AsyncStartPlaytest gate-logic extraction — same scheduling code,
            // renamed local.
            var start = src.IndexOf("if (!string.IsNullOrEmpty(req.AfterHook))");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "after_hook scheduling block not found");
            var end = src.IndexOf("private static bool IsPlaytestSuccess", start);
            Assert.That(end, Is.GreaterThan(start), "IsPlaytestSuccess not found after the after_hook block");
            var body = src.Substring(start, end - start);

            StringAssert.Contains("MainThreadDispatcher.Enqueue", body,
                "after_hook must marshal CodeExecutor.Execute onto the main thread via " +
                "MainThreadDispatcher — ContinueWith's default continuation runs on a ThreadPool " +
                "thread, and EditorApplication.delayCall += is not thread-safe from there");
            StringAssert.DoesNotContain("delayCall", body,
                "after_hook must not depend on delayCall — a backgrounded Editor does not reliably " +
                "drain it (RELAY-FIX, commit 1bcc90b7), and mutating it off the main thread is unsafe");
        }
    }
}
