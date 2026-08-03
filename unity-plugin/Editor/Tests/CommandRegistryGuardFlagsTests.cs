// TDD: CommandRegistry guard-flag tests (DRY audit issues-23-29 Cat.1).
// IsAlwaysAllowed/IsAllowedDuringCompile used to be two hardcoded OR-chains in CommandRouter,
// independent of RegisterAll() — a rename could silently desync the guard. Now both flags
// live on CommandRegistry.Entry, set at the registration call site.
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRegistryGuardFlagsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string FakeCmd = "test_guard_flag_fake_cmd";

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
    }
}
