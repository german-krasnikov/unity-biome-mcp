// TDD: CommandRouter pure-logic tests — no TCP required, EditMode only.
// Covers: IsAllowedDuringCompile, IsAlwaysAllowed, SuggestNext,
//         CommandRegistry flags, BuildResponse (via Process stub),
//         CommandValidator validation coverage.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterTests
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
        [TestCase("set_parent",      "get_hierarchy depth=1")]
        [TestCase("batch",           "get_console level=Error")]
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
        public bool Registry_IsMutating_MutatingCommands(string cmd)
            => CommandRegistry.IsMutating(cmd);

        [TestCase("ping",          ExpectedResult = false)]
        [TestCase("get_hierarchy", ExpectedResult = false)]
        [TestCase("get_component", ExpectedResult = false)]
        [TestCase("get_console",   ExpectedResult = false)]
        [TestCase("execute_code",  ExpectedResult = false)]
        public bool Registry_IsMutating_ReadCommands_ReturnFalse(string cmd)
            => CommandRegistry.IsMutating(cmd);

        [TestCase("invoke_method",        ExpectedResult = true)]
        [TestCase("set_runtime_property", ExpectedResult = true)]
        [TestCase("wait_until",           ExpectedResult = true)]
        [TestCase("move_to",              ExpectedResult = true)]
        [TestCase("query_state",          ExpectedResult = true)]
        [TestCase("run_playtest",         ExpectedResult = true)]
        public bool Registry_IsRuntime_RuntimeCommands(string cmd)
            => CommandRegistry.IsRuntime(cmd);

        [TestCase("ping")]
        [TestCase("set_property")]
        [TestCase("get_hierarchy")]
        public void Registry_IsRuntime_NonRuntimeCommands_ReturnFalse(string cmd)
            => Assert.IsFalse(CommandRegistry.IsRuntime(cmd));

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
                Assert.IsTrue(result.Contains("\"ok\":true"), result); // outer gate did not reject the batch itself
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
        public void ProcessAsync_RunTests_WhileCompiling_SetsGuardResponse()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
                var json = "{\"id\":\"pa1\",\"cmd\":\"run_tests\",\"args\":{}}";
                CommandRouter.ProcessAsync(json, tcs);
                Assert.IsTrue(tcs.Task.IsCompleted, "TCS should be set synchronously when guard fires");
                var result = tcs.Task.Result;
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                Assert.IsTrue(result.Contains("retry"), result);
            }
            finally
            {
                CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling;
            }
        }

        [Test]
        public void ProcessAsync_WaitUntil_WhileCompiling_SetsGuardResponse()
        {
            CommandRouter.IsCompiling = () => true;
            try
            {
                var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
                var json = "{\"id\":\"pa2\",\"cmd\":\"wait_until\",\"args\":{\"path\":\"/x\",\"component\":\"C\",\"field\":\"f\",\"value\":\"v\"}}";
                CommandRouter.ProcessAsync(json, tcs);
                Assert.IsTrue(tcs.Task.IsCompleted);
                var result = tcs.Task.Result;
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
            CommandRouter.IsCompiling = () =>
            {
                if (!true) return false;  // would be EditorApplication.isCompiling = true
                return 150.0 < 120.0;    // elapsed=150s → not a real compile → false
            };
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
    }
}
