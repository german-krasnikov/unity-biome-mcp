// TDD: ArgContract — gate for CommandValidator.Validate on the DIRECT (non-batch) dispatch
// path (F1c). Scaffold commit (1/2): the roster starts empty, so Check() is a no-op for every
// command until a later commit enables it. These tests prove the gate+wiring mechanism itself,
// independent of which commands are (or aren't yet) enabled — they mutate the live roster
// in-place (HashSet returned by EnabledForTest) and restore it in a finally block.
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ArgContractTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Gate closed by default ────────────────────────────────────────────

        [Test]
        public void EnabledForTest_IsEmptyByDefault()
        {
            CollectionAssert.IsEmpty(ArgContract.EnabledForTest);
        }

        [Test]
        public void Check_RegisteredCommandNotEnabled_ReturnsNull()
        {
            // "create_ui" requires "type" — missing here. Would fail CommandValidator.Validate
            // if the gate were open. Not yet enabled, so the gate must stay closed.
            Assert.IsNull(ArgContract.Check("create_ui", "{}"));
        }

        [Test]
        public void Check_UnregisteredCommand_ReturnsNull()
        {
            // Never enabled (it isn't a real command) — the gate must not surface
            // "Unknown command" here; that stays CommandRegistry.Execute's job.
            Assert.IsNull(ArgContract.Check("totally_unknown_cmd_xyz", "{}"));
        }

        // ── Gate open → delegates verbatim ────────────────────────────────────

        [Test]
        public void Check_CommandEnabled_DelegatesToCommandValidator()
        {
            var roster = (HashSet<string>)ArgContract.EnabledForTest;
            roster.Add("create_ui");
            try
            {
                var fromGate = ArgContract.Check("create_ui", "{}");
                var fromValidator = CommandValidator.Validate("create_ui", "{}");
                Assert.IsNotNull(fromGate);
                Assert.AreEqual(fromValidator, fromGate);
            }
            finally
            {
                roster.Remove("create_ui");
            }
        }

        // ── Router wiring: sync Process ───────────────────────────────────────

        [Test]
        public void Process_EnabledCommand_MissingRequiredArg_FailsBeforeHandlerRuns()
        {
            RegisterCleanup(() => CommandRouter.IsCompiling = CommandRouter.DefaultIsCompiling);
            RegisterCleanup(() => CommandRouter.IsPlayMode = () => UnityEditor.EditorApplication.isPlaying);
            CommandRouter.IsCompiling = () => false;
            CommandRouter.IsPlayMode = () => false;

            var roster = (HashSet<string>)ArgContract.EnabledForTest;
            roster.Add("get_component");
            try
            {
                // get_component requires "path" — omit it. If the handler ran anyway it would
                // return its own (different) error text instead of the contract error below.
                var json = "{\"id\":\"ac-sync1\",\"cmd\":\"get_component\",\"args\":{}}";
                var result = CommandRouter.Process(json);
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                StringAssert.Contains("!path", result);
            }
            finally
            {
                roster.Remove("get_component");
            }
        }

        // ── Router wiring: ProcessAsync async-handler branch ──────────────────

        [Test]
        public void ProcessAsync_EnabledCommand_MissingRequiredArg_FailsBeforeHandlerRuns()
        {
            var roster = (HashSet<string>)ArgContract.EnabledForTest;
            roster.Add("ask_user");
            try
            {
                // ask_user requires "questions" — omit it. Its real handler (no listener
                // subscribed in this test) would instead produce the distinct
                // "no interaction provider registered" message — proving it never runs.
                var json = "{\"id\":\"ac-async1\",\"cmd\":\"ask_user\",\"args\":{}}";
                var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();
                CommandRouter.ProcessAsync(json, tcs);
                var result = tcs.Task.Result;
                Assert.IsTrue(result.Contains("\"ok\":false"), result);
                StringAssert.Contains("!questions", result);
                StringAssert.DoesNotContain("no interaction provider registered", result);
            }
            finally
            {
                roster.Remove("ask_user");
            }
        }
    }
}
