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
    public class CommandRouterRegistrationTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private static readonly string[] MetaCommands =
        {
            "ping", "get_capabilities", "get_enabled_tools", "get_disabled_tools",
            "set_tool_catalog", "set_client_label", "screenshot", "diagnose", "force_play_stop",
            "get_status",
        };

        private static readonly string[] AsyncCommands =
        {
            "run_tests", "ask_user", "wait_until", "move_to", "test_step", "run_playtest",
            "build", "package", "source_patch_write",
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
        public void RunTests_RegistrationAcceptsSelectionArgs()
        {
            Assert.IsTrue(CommandRegistry.TryGetContract(
                "run_tests", out _, out var optional, out _));
            CollectionAssert.AreEqual(
                new[] { "mode", "filter", "group", "categories", "assemblies", "tests" },
                optional);
        }

        [Test]
        public void GetTestRun_RegistrationAcceptsCompactArg()
        {
            Assert.IsTrue(CommandRegistry.TryGetContract(
                "get_test_run", out var required, out var optional, out _));
            CollectionAssert.AreEqual(new[] { "run_id" }, required);
            CollectionAssert.AreEqual(new[] { "compact" }, optional);
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

        // ── force_play_stop reload-survival source guard (DEV-66 Part B) ────────

        [Test]
        public void ForcePlayStop_DoesNotDependOnDelayCall()
        {
            var src = ReadRequiredPackageSource(typeof(CommandRouter), "Editor/CommandRouter.Registration.cs");
            var start = src.IndexOf("CommandRegistry.Register(\"force_play_stop\"");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "force_play_stop registration not found");
            var end = src.IndexOf("CommandRegistry.Register(\"set_client_label\"", start);
            Assert.That(end, Is.GreaterThan(start), "set_client_label registration not found after force_play_stop");
            var body = src.Substring(start, end - start);

            StringAssert.Contains("EnterPlayModeWithPendingStop", body,
                "force_play_stop's direct branch must go through the shared reload-survival helper " +
                "so a refused entry cannot strand PendingPlayStopKey for an unrelated later Play " +
                "Mode session (C1 r6 #2)");
            StringAssert.Contains("PendingPlayStartKey", body,
                "force_play_stop's compiling branch must persist a SessionState flag that survives " +
                "a domain reload while waiting for compilation to finish");
            StringAssert.DoesNotContain("delayCall", body,
                "force_play_stop must not depend on delayCall — entering Play Mode triggers a domain " +
                "reload that wipes any delayCall/update subscription registered inline here " +
                "(RELAY-FIX, commit 1bcc90b7)");
        }

        // ── force_refresh reload-nudge source guard (DEV-66 Part C2) ────────────

        [Test]
        public void ForceRefresh_SchedulesReloadViaMainThreadDispatcher_NotDelayCall()
        {
            var src = ReadRequiredPackageSource(typeof(CommandRouter), "Editor/CommandRouter.Registration.cs");
            var start = src.IndexOf("CommandRegistry.Register(\"force_refresh\"");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "force_refresh registration not found");
            var end = src.IndexOf("CommandRegistry.Register(\"search_scene\"", start);
            Assert.That(end, Is.GreaterThan(start), "search_scene registration not found after force_refresh");
            var body = src.Substring(start, end - start);

            StringAssert.Contains("MainThreadDispatcher.Enqueue", body,
                "force_refresh's deferred RequestScriptReload must run via MainThreadDispatcher " +
                "(EditorApplication.update-driven) — delayCall does not drain in a backgrounded Editor " +
                "(RELAY-FIX, commit 1bcc90b7)");
            StringAssert.Contains("!EditorApplication.isCompiling", body,
                "the deferred reload must still guard on !isCompiling before requesting a reload");
            StringAssert.DoesNotContain("delayCall", body,
                "force_refresh must not depend on delayCall anywhere");
        }
    }
}
