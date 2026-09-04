// TDD: C02 — PlaytestMcpPolicy post-parse compile-time rejection for MCP DSL steps.
// "Zero handler calls on rejection" is proven via CommandRouter.LastCommandName staying
// unchanged — PlaytestMcpPolicy.Validate never touches CommandRouter.Process*/CommandRegistry
// .Execute, so a real dispatch would be the only thing able to move that field.
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestMcpPolicyTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static PlaytestStep McpStep(string cmd, string args = "{}") =>
            new PlaytestStep { Type = StepType.Mcp, Method = cmd, Args = args };

        [TestCase("execute_code")]
        [TestCase("create_script")]
        [TestCase("sync_unity")]
        [TestCase("await_compile")]
        [TestCase("smart_build")]
        [TestCase("run_tests")]
        [TestCase("run_playtest")]
        [TestCase("package")]
        [TestCase("build")]
        public void Validate_DenylistedCommand_ProducesCompileError(string cmd)
        {
            var before = CommandRouter.LastCommandName;

            var errors = PlaytestMcpPolicy.Validate(
                new List<PlaytestStep> { McpStep(cmd) }, null, isEditModeRun: false);

            Assert.IsNotNull(errors, $"'{cmd}' must produce a compile error");
            Assert.IsTrue(errors.Exists(e => e.Contains(cmd)), $"error should name '{cmd}': {string.Join("; ", errors)}");
            Assert.AreEqual(before, CommandRouter.LastCommandName, "policy validation must never dispatch the denied command");
        }

        [Test]
        public void Validate_EditorPlayRejected_EditorSelectAllowed()
        {
            var before = CommandRouter.LastCommandName;

            var playErrors = PlaytestMcpPolicy.Validate(
                new List<PlaytestStep> { McpStep("editor", "{\"action\":\"play\"}") }, null, isEditModeRun: false);
            Assert.IsNotNull(playErrors, "editor(action=play) must be rejected");
            Assert.IsTrue(playErrors.Exists(e => e.Contains("editor")));

            var selectErrors = PlaytestMcpPolicy.Validate(
                new List<PlaytestStep> { McpStep("editor", "{\"action\":\"select\"}") }, null, isEditModeRun: false);
            Assert.IsNull(selectErrors, "editor(action=select) must be allowed");

            Assert.AreEqual(before, CommandRouter.LastCommandName);
        }

        [Test]
        public void Validate_EditModeRuntimeCommandRejectedBeforeDispatch()
        {
            var before = CommandRouter.LastCommandName;

            // get_frame_stats is registered runtime:true (CommandRouter.Registration.cs) —
            // only usable while Play Mode is active.
            var errors = PlaytestMcpPolicy.Validate(
                new List<PlaytestStep> { McpStep("get_frame_stats") }, null, isEditModeRun: true);

            Assert.IsNotNull(errors, "a runtime-only command must be rejected in an Edit-mode playtest");
            Assert.IsTrue(errors.Exists(e => e.Contains("get_frame_stats")));
            Assert.AreEqual(before, CommandRouter.LastCommandName);

            // The same command is fine when the run targets Play Mode.
            var playModeErrors = PlaytestMcpPolicy.Validate(
                new List<PlaytestStep> { McpStep("get_frame_stats") }, null, isEditModeRun: false);
            Assert.IsNull(playModeErrors);
        }

        [Test]
        public void Validate_RegisteredPluginCommandAllowedWithoutParserChange()
        {
            var snapshot = CommandRegistry.CaptureForTest();
            RegisterCleanup(() => CommandRegistry.RestoreForTest(snapshot));
            CommandRegistry.Register("test_mcp_policy_plugin_cmd", _ => "ok", required: "", optional: "");

            var errors = PlaytestMcpPolicy.Validate(
                new List<PlaytestStep> { McpStep("test_mcp_policy_plugin_cmd") }, null, isEditModeRun: true);

            Assert.IsNull(errors, "a registered command must be accepted without any PlaytestMcpPolicy source change");
        }

        [Test]
        public void Validate_AllowedCommand_NoError()
        {
            var errors = PlaytestMcpPolicy.Validate(
                new List<PlaytestStep> { McpStep("get_hierarchy", "{\"depth\":1}") }, null, isEditModeRun: true);

            Assert.IsNull(errors);
        }

        // ── Wiring proof: PlaytestRunner.Run() itself short-circuits before Tick() ever runs ──

        [Test]
        public async Task Run_ScriptWithDenylistedMcpStep_RejectedBeforeAnySideEffect()
        {
            var before = CommandRouter.LastCommandName;
            var tcs = new TaskCompletionSource<string>();

            PlaytestRunner.Run("MCP execute_code code=1\n", 5f, tcs, requiresPlayMode: false);

            Assert.IsTrue(tcs.Task.IsCompleted, "policy rejection must short-circuit before Tick()/EditorApplication.update");
            var result = await tcs.Task;

            StringAssert.StartsWith("PARSE ERROR", result);
            StringAssert.Contains("execute_code", result);
            Assert.AreEqual(before, CommandRouter.LastCommandName, "the denied command must never reach CommandRouter dispatch");
        }
    }
}
