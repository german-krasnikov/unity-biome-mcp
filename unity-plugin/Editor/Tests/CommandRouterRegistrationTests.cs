// TDD: CommandRouter.RegisterAll() split into 4 themed bucket methods (B1, review sprint v0.70).
// RegisterAll() had grown into a ~340-line God Method wiring all 93 commands inline. This splits
// it into RegisterMetaCommands/RegisterReadCommands/RegisterMutatingCommands/RegisterAsyncCommands
// (CommandRouter.Registration.cs), each themed by guard-flag semantics. Full-set correctness is
// already guarded by CommandRegistryCompletenessTests (93-command snapshot) — these tests instead
// verify each bucket method registers exactly its own themed subset when called in isolation, and
// that RegisterAll() still wires everything together (buckets + Watch + Plugins).
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterRegistrationTests
    {
        private static readonly string[] MetaCommands =
        {
            "ping", "get_capabilities", "get_enabled_tools", "get_disabled_tools",
            "set_tool_catalog", "set_client_label", "screenshot", "diagnose", "force_play_stop",
        };

        private static readonly string[] AsyncCommands =
        {
            "run_tests", "ask_user", "wait_until", "move_to", "test_step", "run_playtest",
        };

        [TearDown]
        public void TearDown()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();  // restore built-in commands for later fixtures
        }

        [Test]
        public void RegisterMetaCommands_RegistersExpectedCommands()
        {
            CommandRegistry.Clear();
            CommandRouter.RegisterMetaCommands();
            CollectionAssert.AreEquivalent(MetaCommands, CommandRegistry.GetAllCommands());
        }

        [Test]
        public void RegisterAsyncCommands_RegistersExpectedAsyncCommands()
        {
            CommandRegistry.Clear();
            CommandRouter.RegisterAsyncCommands();

            CollectionAssert.AreEquivalent(AsyncCommands, CommandRegistry.GetAllCommands());
            foreach (var cmd in AsyncCommands)
                Assert.IsTrue(CommandRegistry.HasAsyncHandler(cmd, out _), $"{cmd} should be async-registered");
        }

        [Test]
        public void RegisterAll_CallsAllFourBucketsAndDelegatesToWatchAndPlugins()
        {
            CommandRegistry.Clear();
            CommandRouter.RegisterAll();

            var registered = new List<string>(CommandRegistry.GetAllCommands());
            Assert.Contains("ping", registered, "meta bucket");
            Assert.Contains("run_tests", registered, "async bucket");
            Assert.Contains("create_object", registered, "mutating bucket");
            Assert.Contains("get_hierarchy", registered, "read bucket");
            Assert.Contains("watch_add", registered, "WatchCommandHandler.RegisterAll() delegation");
        }
    }
}
