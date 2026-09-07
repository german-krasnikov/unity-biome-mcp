// TDD: CommandRegistry guard-flag tests (DRY audit issues-23-29 Cat.1).
// IsAlwaysAllowed/IsAllowedDuringCompile used to be two hardcoded OR-chains in CommandRouter,
// independent of RegisterAll() — a rename could silently desync the guard. Now both flags
// live on CommandRegistry.Entry, set at the registration call site.
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.SourcePatch;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRegistryGuardFlagsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string FakeCmd = "test_guard_flag_fake_cmd";

        [SetUp]
        public void SetUp()
        {
            // V6 (RegistryReadiness_IndependentOfReloadVerdict) touches SourcePatchHost's
            // static state; every other test in this file leaves it alone, so resetting
            // is a no-op for them but a required isolation boundary for V6.
            RegisterCleanup(SourcePatchHost.ResetForTests);
            SourcePatchHost.ResetForTests();
        }

        [TearDown]
        public void TearDown()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();  // restore built-in commands
        }

        [Test]
        public void Register_AlwaysAllowedFlag_RoundTripsThroughRegistry()
        {
            CommandRegistry.Register(FakeCmd, _ => "ok", alwaysAllowed: true, required: "", optional: "");
            Assert.IsTrue(CommandRegistry.IsAlwaysAllowed(FakeCmd));
            Assert.IsFalse(CommandRegistry.IsAllowedDuringCompile(FakeCmd));
        }

        [Test]
        public void Register_AllowedDuringCompileFlag_RoundTripsThroughRegistry()
        {
            CommandRegistry.Register(FakeCmd, _ => "ok", allowedDuringCompile: true, required: "", optional: "");
            Assert.IsFalse(CommandRegistry.IsAlwaysAllowed(FakeCmd));
            Assert.IsTrue(CommandRegistry.IsAllowedDuringCompile(FakeCmd));
        }

        [Test]
        public void Register_NoFlags_DefaultsToNotAllowed()
        {
            CommandRegistry.Register(FakeCmd, _ => "ok", required: "", optional: "");
            Assert.IsFalse(CommandRegistry.IsAlwaysAllowed(FakeCmd));
            Assert.IsFalse(CommandRegistry.IsAllowedDuringCompile(FakeCmd));
        }

        [Test]
        public void IsAlwaysAllowed_UnregisteredCommand_ReturnsFalse()
            => Assert.IsFalse(CommandRegistry.IsAlwaysAllowed("totally_unknown_cmd_xyz"));

        [Test]
        public void IsAllowedDuringCompile_UnregisteredCommand_ReturnsFalse()
            => Assert.IsFalse(CommandRegistry.IsAllowedDuringCompile("totally_unknown_cmd_xyz"));

        // Regression guard: every name previously hardcoded in CommandRouter.IsAlwaysAllowed's
        // OR-chain must still resolve true post-migration to registry flags.
        private static readonly string[] ExpectedAlwaysAllowed =
        {
            "ping", "get_enabled_tools", "get_disabled_tools", "set_tool_catalog", "diagnose", "ask_user",
            "cancel_test_run",
        };

        [Test]
        public void IsAlwaysAllowed_AllPreviouslyHardcodedNames_StillTrue()
        {
            var failures = new List<string>();
            foreach (var cmd in ExpectedAlwaysAllowed)
                if (!CommandRegistry.IsAlwaysAllowed(cmd)) failures.Add(cmd);
            Assert.IsEmpty(failures, "Regression: dropped alwaysAllowed flag for: " + string.Join(", ", failures));
        }

        // Regression guard: every name previously hardcoded in CommandRouter.IsAllowedDuringCompile's
        // OR-chain must still resolve true post-migration to registry flags.
        private static readonly string[] ExpectedAllowedDuringCompile =
        {
            "ping", "get_console", "clear_console", "screenshot", "get_enabled_tools", "compile_status",
            "get_disabled_tools", "set_tool_catalog", "sync_status", "get_compile_errors", "diagnose",
            "force_refresh", "get_test_results", "get_test_count", "get_test_run", "list_test_runs",
            "resolve_test_request", "cancel_test_run", "execute_code", "ask_user", "compile_preflight",
        };

        [Test]
        public void IsAllowedDuringCompile_AllPreviouslyHardcodedNames_StillTrue()
        {
            var failures = new List<string>();
            foreach (var cmd in ExpectedAllowedDuringCompile)
                if (!CommandRegistry.IsAllowedDuringCompile(cmd)) failures.Add(cmd);
            Assert.IsEmpty(failures, "Regression: dropped allowedDuringCompile flag for: " + string.Join(", ", failures));
        }

        // V6 (N2b.7, Plans/N2-reload-sourcepatch-contract.md): CommandRegistry.Ready
        // (registration readiness) and SourcePatchHost.CurrentState (the Reload
        // verdict) are two independent axes. Neither guard reads the other's state —
        // proven through the real dispatch path (CommandRouter.Process -> CheckGuards),
        // not by re-implementing the guard order in the test.
        [Test]
        public void RegistryReadiness_IndependentOfReloadVerdict()
        {
            // (a) registry NOT ready + Reload verified (Off): the registry gate
            // refuses first, and the Reload verdict is untouched by that refusal.
            // Command choice mirrors RegistrationGateTests.Process_WhenNotReady_ReturnsRetry2000.
            SourcePatchHost.CurrentState = SourcePatchState.Off;
            CommandRegistry.Clear(); // Ready = false
            var notReadyResult = CommandRouter.Process("{\"id\":\"t1\",\"cmd\":\"get_hierarchy\",\"args\":{}}");
            StringAssert.Contains("\"retry\":2000", notReadyResult);
            StringAssert.Contains("initializing", notReadyResult);
            Assert.AreEqual(SourcePatchState.Off, SourcePatchHost.CurrentState,
                "the registry-not-ready guard must not observe or mutate the independently-tracked Reload verdict");

            // (b) registry ready + Reload unverified (Disabling): a distinct
            // outcome — dispatch proceeds past the registry gate regardless of
            // where the Reload verdict currently sits. Command choice mirrors
            // RegistrationGateTests.Process_WhenReady_DoesNotReturnInitializingError
            // (scene-independent, so this assertion isolates the registry gate only).
            CommandRegistry.InitDefaults(); // Ready = true
            SourcePatchHost.CurrentState = SourcePatchState.Disabling;
            var readyResult = CommandRouter.Process("{\"id\":\"t1\",\"cmd\":\"get_disabled_tools\",\"args\":{}}");
            StringAssert.DoesNotContain("initializing", readyResult,
                "registry-ready dispatch must not be blocked by an unrelated, unverified Reload state");
            Assert.AreEqual(SourcePatchState.Disabling, SourcePatchHost.CurrentState,
                "dispatching past the registry gate must not itself resolve the Reload verdict");
        }
    }
}
