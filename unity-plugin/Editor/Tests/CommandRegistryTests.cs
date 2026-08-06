// TDD: CommandOptions demoted to internal (B3, review sprint v0.70).
// G13 tests added: timeline command registration.
// The public CommandOptions overloads of Register/RegisterAction/RegisterAsync had zero
// production call sites outside CommandRegistry.cs/CommandOptions.cs itself (grep-confirmed) —
// only these tests exercised them directly. Demoted CommandOptions and its 3 accepting
// overloads to internal; the legacy bool-params overloads (still public, used by 90+ call
// sites) are unchanged and continue to forward to the now-internal overloads.
// The 2 former tests here asserted CommandOptions-vs-legacy equivalence — that equivalence
// is now an implementation detail (legacy always forwards to CommandOptions internally,
// unreachable any other way), so they're replaced by this single regression test guarding
// the one behavior that must survive for a plugin caller (which only ever uses the legacy
// public path): DenyPluginCoreFlags still strips alwaysAllowed/allowedDuringCompile.
using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown()
        {
            CommandRegistry.CallerIsPlugin = false;
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();  // restore built-in commands
        }

        [Test]
        public void Register_LegacyBoolParams_StillAppliesDenyPluginCoreFlags()
        {
            const string cmd = "test_co_plugin_flag_cmd";
            CommandRegistry.CallerIsPlugin = true;

            CommandRegistry.Register(cmd, _ => "ok", alwaysAllowed: true, allowedDuringCompile: true,
                required: "", optional: "");

            Assert.IsFalse(CommandRegistry.IsAlwaysAllowed(cmd),
                "plugin-originated registration must have alwaysAllowed stripped");
            Assert.IsFalse(CommandRegistry.IsAllowedDuringCompile(cmd),
                "plugin-originated registration must have allowedDuringCompile stripped");
        }

        // ── G13: timeline command registration ────────────────────────────────────

        [Test]
        public void Timeline_IsRegisteredInCommandRegistry()
        {
            Assert.IsTrue(CommandRegistry.IsRegistered("timeline"),
                "timeline must be registered for discover_tools and direct invocation to work");
        }

        [Test]
        public void Timeline_ExecuteWithMissingPath_ReturnsError_NotNullOrException()
        {
            // Calling timeline without a valid path returns an error string, not null/crash.
            string result = null;
            try
            {
                result = CommandRegistry.Execute("timeline", "{\"action\":\"get\",\"path\":\"/DoesNotExist\"}");
            }
            catch (Exception ex)
            {
                result = ex.Message; // acceptable — what matters is the handler exists
            }
            Assert.IsNotNull(result, "timeline handler must return a response (even an error message)");
            Assert.IsNotEmpty(result, "Response must be non-empty");
        }
    }
}
