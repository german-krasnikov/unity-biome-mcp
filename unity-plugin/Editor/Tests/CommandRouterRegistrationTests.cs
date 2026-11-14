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
        // A5: "ping", "get_status", "get_capabilities" and "get_enabled_tools" moved to
        // ToolBindings.g.cs (HANDLER DSL item) — RegisterMetaCommands() alone no longer
        // registers them, only CommandRouter.RegisterAll() does (via ToolBindings.RegisterAll()).
        private static readonly string[] MetaCommands =
        {
            "get_disabled_tools",
            "set_tool_catalog", "set_client_label", "screenshot", "force_play_stop",
        };

        // A5: these three have a HANDLER line in dsl/tools.biome as a TOOL block — ToolBindings.RegisterAll()
        // generates their Register* calls; RegisterAsyncCommands() alone registers none of them.
        private static readonly string[] GeneratedAsyncCommands =
        {
            "run_tests", "ask_user", "run_playtest",
        };

        // P0-50/E02/E03: these three are declared INTERNAL (not TOOL) in dsl/tools.biome — the
        // generator does not emit a Register* call for an INTERNAL block, so RegisterAsyncCommands()
        // still hand-registers them (see CommandRouter.Registration.cs).
        private static readonly string[] HandRegisteredAsyncCommands =
        {
            "start_playtest", "get_playtest_run", "source_patch_write",
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
            CollectionAssert.AreEquivalent(HandRegisteredAsyncCommands, CommandRegistry.GetAllCommands());
            foreach (var cmd in HandRegisteredAsyncCommands)
                Assert.IsTrue(CommandRegistry.HasAsyncHandler(cmd, out _), $"{cmd} should be async-registered");

            CommandRegistry.Clear();
            ToolBindings.RegisterAll();
            foreach (var cmd in GeneratedAsyncCommands)
                Assert.IsTrue(CommandRegistry.HasAsyncHandler(cmd, out _), $"{cmd} should be async-registered");
        }

        // B05: the Play-mode gate moved past parsing — registration must no longer flag
        // run_playtest runtime:true, or CheckGuards blocks Edit-mode scripts before their
        // header (`# @needs editmode`) is ever read.
        [Test]
        public void RegisterAsyncCommands_RunPlaytest_NotFlaggedRuntime()
        {
            CommandRegistry.Clear();
            ToolBindings.RegisterAll();
            Assert.IsFalse(CommandRegistry.IsRuntime("run_playtest"),
                "run_playtest's Play-mode gate moved into AsyncRunPlaytest's header check (B05) — " +
                "registration must not pre-block Edit-mode scripts before the header is read");
        }

        [Test]
        public void RunTests_RegistrationAcceptsSelectionArgs()
        {
            Assert.IsTrue(CommandRegistry.TryGetContract(
                "run_tests", out _, out var optional, out _));
            // A5: optional order now comes from ToolBindings.g.cs (HANDLER DSL item), which
            // buckets DEFAULT None ARGs before DEFAULT_TEXT ones — an incidental codegen
            // ordering, not a functional contract (JSON args are key-based). Only the set
            // of accepted selection args is load-bearing here.
            CollectionAssert.AreEquivalent(
                new[] { "request_id", "mode", "filter", "group", "categories", "assemblies", "tests" },
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
            // A5 prep: the inline force_play_stop lambda was extracted to the named
            // ExecForcePlayStop handler further down in this file — the registration call
            // itself is now just `CommandRegistry.Register("force_play_stop", ExecForcePlayStop, ...)`,
            // so the logic this guard checks must be found in the handler body instead.
            var src = ReadRequiredPackageSource(typeof(CommandRouter), "Editor/CommandRouter.Registration.cs");
            var start = src.IndexOf("private static string ExecForcePlayStop(string args)");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "ExecForcePlayStop handler not found");
            var end = src.IndexOf("private static string ExecSetClientLabel(string args)", start);
            Assert.That(end, Is.GreaterThan(start), "ExecSetClientLabel handler not found after ExecForcePlayStop");
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
            // A5 prep: the inline force_refresh lambda was extracted to the named
            // ExecForceRefresh handler further down in this file — the registration call
            // itself is now just `CommandRegistry.Register("force_refresh", ExecForceRefresh, ...)`,
            // so the logic this guard checks must be found in the handler body instead.
            // ExecSearchScene is the next handler immediately after ExecForceRefresh in source.
            var src = ReadRequiredPackageSource(typeof(CommandRouter), "Editor/CommandRouter.Registration.cs");
            var start = src.IndexOf("private static string ExecForceRefresh(string args)");
            Assert.That(start, Is.GreaterThanOrEqualTo(0), "ExecForceRefresh handler not found");
            var end = src.IndexOf("internal static string ExecSearchScene(string args)", start);
            Assert.That(end, Is.GreaterThan(start), "ExecSearchScene handler not found after ExecForceRefresh");
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
