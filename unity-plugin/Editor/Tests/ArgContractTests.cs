// TDD: ArgContract — gate for CommandValidator.Validate on the DIRECT (non-batch) dispatch
// path (F1c). Enabled commit (2/2): the roster now holds the 87 commands whose registration
// delegates straight to ToolMeta.Get(cmd) (see ArgContract.cs's own comment for why the ~50
// hand-written-contract commands, including "batch", are deliberately excluded).
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ArgContractTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Roster hygiene ─────────────────────────────────────────────────

        [Test]
        public void EnabledForTest_HasExactly87Names()
        {
            Assert.AreEqual(87, new System.Collections.Generic.List<string>(ArgContract.EnabledForTest).Count);
        }

        [Test]
        public void Roster_AllNamesAreRegisteredCommands()
        {
            // Catches a typo silently no-op'ing instead of enabling anything.
            foreach (var cmd in ArgContract.EnabledForTest)
                Assert.IsTrue(CommandRegistry.TryGetContract(cmd, out _, out _, out _),
                    $"'{cmd}' is in ArgContract's roster but not a registered command");
        }

        [Test]
        public void Roster_ExcludesBatch_KnownContractDrift()
        {
            // See ArgContract.cs's comment: batch's registered optional list has "timeout", not
            // "timeout_ms" — the key server/src/unity_mcp/tools/batch.py actually sends on
            // effectively every call. Enabling this gate for "batch" today would reject that.
            CollectionAssert.DoesNotContain(
                new System.Collections.Generic.List<string>(ArgContract.EnabledForTest), "batch");
        }

        // ── Gate behavior ──────────────────────────────────────────────────

        [Test]
        public void Check_EnabledCommand_MissingRequiredArg_ReturnsContractError()
        {
            // "create_ui" is ToolMeta-delegated and requires "type".
            var err = ArgContract.Check("create_ui", "{}");
            Assert.IsNotNull(err);
            StringAssert.Contains("!type", err);
        }

        [Test]
        public void Check_EnabledCommand_DelegatesVerbatimToCommandValidator()
        {
            var fromGate = ArgContract.Check("create_ui", "{}");
            var fromValidator = CommandValidator.Validate("create_ui", "{}");
            Assert.AreEqual(fromValidator, fromGate);
        }

        [Test]
        public void Check_NotYetEnabledCommand_ReturnsNull()
        {
            // "batch" requires "commands" — would fail CommandValidator.Validate if the gate
            // were open. Deliberately not enabled (see Roster_ExcludesBatch_KnownContractDrift).
            Assert.IsNull(ArgContract.Check("batch", "{}"));
        }

        [Test]
        public void Check_UnregisteredCommand_ReturnsNull()
        {
            // Never enabled (it isn't a real command) — the gate must not surface
            // "Unknown command" here; that stays CommandRegistry.Execute's job.
            Assert.IsNull(ArgContract.Check("totally_unknown_cmd_xyz", "{}"));
        }

        // ── Router wiring: sync Process ───────────────────────────────────

        [Test]
        public void Process_EnabledCommand_MissingRequiredArg_FailsBeforeHandlerRuns()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;

            // get_components_list requires "id" — omit it. If the handler ran anyway it would
            // return its own (different) error instead of the contract error asserted below.
            var json = "{\"id\":\"ac-sync1\",\"cmd\":\"get_components_list\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            StringAssert.Contains("!id", result);
        }

        [Test]
        public void Process_EnabledCommand_ValidArgs_StillSucceeds()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;

            // get_console has no required args — "{}" must still pass through to the real handler.
            var json = "{\"id\":\"ac-sync2\",\"cmd\":\"get_console\",\"args\":{}}";
            var result = CommandRouter.Process(json);
            Assert.IsTrue(result.Contains("\"ok\":true"), result);
        }

        // ── Router wiring: ProcessAsync async-handler branch ──────────────

        [Test]
        public void ProcessAsync_EnabledCommand_MissingRequiredArg_FailsBeforeHandlerRuns()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;

            // "package" (RegisterAsync, ToolMeta-delegated) requires "action" — omit it.
            var json = "{\"id\":\"ac-async1\",\"cmd\":\"package\",\"args\":{}}";
            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(json, tcs);
            var result = tcs.Task.Result;
            Assert.IsTrue(result.Contains("\"ok\":false"), result);
            StringAssert.Contains("!action", result);
        }
    }
}
