// TDD: CommandOptions struct (M6, ROI reliability sprint).
// Register/RegisterAction/RegisterAsync now have a CommandOptions overload alongside the
// legacy bool-params overload (which forwards to it). Verifies both paths produce an
// identical registered Entry — the migration must be behavior-preserving.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRegistryTests
    {
        private const string LegacyCmd = "test_co_legacy_cmd";
        private const string OptionsCmd = "test_co_options_cmd";
        private const string LegacyActionCmd = "test_co_legacy_action_cmd";
        private const string OptionsActionCmd = "test_co_options_action_cmd";

        [TearDown]
        public void TearDown()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();  // restore built-in commands
        }

        [Test]
        public void Register_ViaCommandOptions_MatchesLegacyOverload_Behavior()
        {
            CommandRegistry.Register(LegacyCmd, _ => "ok", mutating: true, alwaysAllowed: true,
                allowedDuringCompile: true, required: "a", optional: "b", maxResponseChars: 42);
            CommandRegistry.Register(OptionsCmd, _ => "ok", new CommandOptions
            {
                Mutating = true,
                AlwaysAllowed = true,
                AllowedDuringCompile = true,
                Required = "a",
                Optional = "b",
                MaxResponseChars = 42
            });

            Assert.IsTrue(CommandRegistry.IsMutating(LegacyCmd) == CommandRegistry.IsMutating(OptionsCmd));
            Assert.IsTrue(CommandRegistry.IsAlwaysAllowed(LegacyCmd) == CommandRegistry.IsAlwaysAllowed(OptionsCmd));
            Assert.IsTrue(CommandRegistry.IsAllowedDuringCompile(LegacyCmd) == CommandRegistry.IsAllowedDuringCompile(OptionsCmd));
            Assert.AreEqual(CommandRegistry.GetMaxResponseChars(LegacyCmd), CommandRegistry.GetMaxResponseChars(OptionsCmd));

            CommandRegistry.TryGetContract(LegacyCmd, out var legacyReq, out var legacyOpt, out var legacyFreeForm);
            CommandRegistry.TryGetContract(OptionsCmd, out var optReq, out var optOpt, out var optFreeForm);
            CollectionAssert.AreEqual(legacyReq, optReq);
            CollectionAssert.AreEqual(legacyOpt, optOpt);
            Assert.AreEqual(legacyFreeForm, optFreeForm);
        }

        [Test]
        public void RegisterAction_ViaCommandOptions_PrependsActionToRequired()
        {
            CommandRegistry.RegisterAction(LegacyActionCmd, (action, args) => "ok", mutating: true, required: "x");
            CommandRegistry.RegisterAction(OptionsActionCmd, (action, args) => "ok", new CommandOptions
            {
                Mutating = true,
                Required = "x"
            });

            CommandRegistry.TryGetContract(LegacyActionCmd, out var legacyReq, out _, out _);
            CommandRegistry.TryGetContract(OptionsActionCmd, out var optReq, out _, out _);
            CollectionAssert.AreEqual(legacyReq, optReq);
            CollectionAssert.Contains(optReq, "action");
            CollectionAssert.Contains(optReq, "x");
        }
    }
}
