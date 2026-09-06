using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PluginRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private class FakePlugin : IMCPPlugin
        {
            public string Name { get; }
            public string CommandPrefix { get; }
            public int RegisterCommandsCallCount;
            public int OnDomainReloadCallCount;
            public bool OnDomainReloadThrows;
            public bool RegisterCommandsThrows;
            // Task F3 (PR-03): lets a test make RegisterCommands() actually touch
            // CommandRegistry (register a command) before optionally throwing, so
            // rollback behavior can be exercised without a new fake type.
            public Action RegisterCommandsBody;

            public FakePlugin(string name, string prefix = "fake")
            {
                Name = name;
                CommandPrefix = prefix;
            }

            public void RegisterCommands()
            {
                RegisterCommandsCallCount++;
                RegisterCommandsBody?.Invoke();
                if (RegisterCommandsThrows) throw new InvalidOperationException("simulated");
            }

            public void OnDomainReload()
            {
                OnDomainReloadCallCount++;
                if (OnDomainReloadThrows) throw new InvalidOperationException("simulated");
            }

            public IReadOnlyList<string> AdditionalCommands => Array.Empty<string>();
        }

        // Task 3.1 (ROI reliability sprint): a plugin trying to claim core-only trust flags
        // (alwaysAllowed / allowedDuringCompile) via the legacy Register() overload.
        private class PluginRequestingCoreFlags : IMCPPlugin
        {
            public string Name => "CoreFlagPlugin";
            public string CommandPrefix => "plugin";
            public void RegisterCommands() =>
                CommandRegistry.Register("plugin_cmd", _ => "ok",
                    alwaysAllowed: true, allowedDuringCompile: true);
            public void OnDomainReload() { }
            public IReadOnlyList<string> AdditionalCommands => Array.Empty<string>();
        }

        [SetUp]
        public void SetUp()
        {
            PluginRegistry.Clear();
        }

        [Test]
        public void Register_DuplicateName_OnlyOneRegistered()
        {
            var p1 = new FakePlugin("MyPlugin");
            var p2 = new FakePlugin("MyPlugin");

            PluginRegistry.Register(p1);
            PluginRegistry.Register(p2);

            Assert.AreEqual(1, PluginRegistry.GetAll().Count);
        }

        [Test]
        public void Register_ThenRegisterAllPlugins_RegisterCommandsCalledOnce()
        {
            // Register() must NOT call RegisterCommands();
            // only RegisterAllPlugins() should call it — exactly once.
            var plugin = new FakePlugin("TestPlugin");

            PluginRegistry.Register(plugin);
            PluginRegistry.RegisterAllPlugins();

            Assert.AreEqual(1, plugin.RegisterCommandsCallCount,
                "RegisterCommands should be called exactly once (by RegisterAllPlugins only)");
        }

        [Test]
        public void Register_DoesNotCallRegisterCommands()
        {
            var plugin = new FakePlugin("TestPlugin2");

            PluginRegistry.Register(plugin);

            Assert.AreEqual(0, plugin.RegisterCommandsCallCount,
                "Register() must not call RegisterCommands() — that is RegisterAllPlugins() responsibility");
        }

        [Test]
        public void RegisterAllPlugins_MultipleCalls_EachCallRegistersOnce()
        {
            var plugin = new FakePlugin("TestPlugin3");
            PluginRegistry.Register(plugin);

            PluginRegistry.RegisterAllPlugins();
            PluginRegistry.RegisterAllPlugins();

            Assert.AreEqual(2, plugin.RegisterCommandsCallCount,
                "Each RegisterAllPlugins() call invokes RegisterCommands() once per plugin");
        }

        [Test]
        public void OnDomainReload_ExceptionInPlugin_DoesNotPropagate()
        {
            var plugin = new FakePlugin("BadPlugin") { OnDomainReloadThrows = true };
            PluginRegistry.Register(plugin);

            // OnDomainReload swallows the exception but logs it as LogError — expect it
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("BadPlugin.*OnDomainReload failed"));

            Assert.DoesNotThrow(() => PluginRegistry.OnDomainReload(),
                "OnDomainReload must swallow plugin exceptions");
        }

        // ── Task 3.1: CallerIsPlugin gate + failed-plugin tracking ─────────────

        [Test]
        public void RegisterAllPlugins_PluginRequestsAlwaysAllowed_FlagIsDenied()
        {
            CommandRegistry.Clear();
            var plugin = new PluginRequestingCoreFlags();
            PluginRegistry.Register(plugin);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("core-only flags"));
            PluginRegistry.RegisterAllPlugins();

            Assert.IsFalse(CommandRegistry.IsAlwaysAllowed("plugin_cmd"));
            Assert.IsFalse(CommandRegistry.IsAllowedDuringCompile("plugin_cmd"));
        }

        [Test]
        public void RegisterAllPlugins_BuiltInCommands_KeepCoreFlags()
        {
            // Sanity check: the gate must not leak into RegisterAll()'s own trusted
            // registrations. execute_code (CommandRouter.cs) is registered with
            // allowedDuringCompile: true directly by RegisterAll(), entirely outside any
            // RegisterAllPlugins() plugin iteration — must still read true afterward.
            PluginRegistry.RegisterAllPlugins();

            Assert.IsTrue(CommandRegistry.IsAllowedDuringCompile("execute_code"));
        }

        [Test]
        public void RegisterAllPlugins_PluginThrows_RecordedInGetFailedPlugins()
        {
            CommandRegistry.Clear();
            var plugin = new FakePlugin("BadPlugin") { RegisterCommandsThrows = true };
            PluginRegistry.Register(plugin);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("BadPlugin.*RegisterCommands failed"));
            PluginRegistry.RegisterAllPlugins();

            var failed = PluginRegistry.GetFailedPlugins();
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual("BadPlugin", failed[0].Name);
        }

        // ── Task F3 (PR-03): atomic rollback on plugin registration failure ────

        [Test]
        public void RegisterAllPlugins_PluginThrowsAfterRegisteringCommand_RollsBackLeftoverCommand()
        {
            CommandRegistry.Clear();
            var plugin = new FakePlugin("BadPlugin")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("leftover_cmd", _ => "ok"),
                RegisterCommandsThrows = true
            };
            PluginRegistry.Register(plugin);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("BadPlugin.*RegisterCommands failed"));
            PluginRegistry.RegisterAllPlugins();

            Assert.IsFalse(CommandRegistry.IsRegistered("leftover_cmd"),
                "a command registered before the plugin threw must be rolled back");
            Assert.AreEqual(1, PluginRegistry.GetFailedPlugins().Count);
        }

        [Test]
        public void RegisterAllPlugins_FailingPluginDoesNotAffectHostOrSiblingPlugin()
        {
            CommandRegistry.Clear();
            CommandRegistry.Register("host_cmd", _ => "ok");
            var bad = new FakePlugin("BadPlugin")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("leftover_cmd", _ => "ok"),
                RegisterCommandsThrows = true
            };
            var good = new FakePlugin("GoodPlugin")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("good_cmd", _ => "ok")
            };
            PluginRegistry.Register(bad);
            PluginRegistry.Register(good);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("BadPlugin.*RegisterCommands failed"));
            PluginRegistry.RegisterAllPlugins();

            Assert.IsTrue(CommandRegistry.IsRegistered("host_cmd"), "host command must survive a sibling plugin's failure");
            Assert.IsTrue(CommandRegistry.IsRegistered("good_cmd"), "an independent, successful plugin must keep its command");
            Assert.IsFalse(CommandRegistry.IsRegistered("leftover_cmd"));
        }

        [Test]
        public void RegisterAllPlugins_DuplicateCommandFromSecondPlugin_RejectedFirstOwnerSurvives()
        {
            CommandRegistry.Clear();
            var pluginA = new FakePlugin("PluginA")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("shared_cmd", _ => "A")
            };
            var pluginB = new FakePlugin("PluginB")
            {
                // Second registration of the same command name throws via
                // CommandRegistry.AlreadyRegistered (CallerIsPlugin == true) — the plugin
                // never sets RegisterCommandsThrows itself, the duplicate name IS the failure.
                RegisterCommandsBody = () => CommandRegistry.Register("shared_cmd", _ => "B")
            };
            PluginRegistry.Register(pluginA);
            PluginRegistry.Register(pluginB);

            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("PluginB.*RegisterCommands failed"));
            PluginRegistry.RegisterAllPlugins();

            var failed = PluginRegistry.GetFailedPlugins();
            Assert.AreEqual(1, failed.Count);
            Assert.AreEqual("PluginB", failed[0].Name);
            Assert.AreEqual("A", CommandRegistry.Execute("shared_cmd", "{}"),
                "first owner's handler must still be registered after the duplicate is rejected");
        }

        [Test]
        public void RegisterAllPlugins_CalledTwiceAcrossReloadStyleClear_NoExceptionNoDuplication()
        {
            var plugin = new FakePlugin("StablePlugin")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("stable_cmd", _ => "ok")
            };
            PluginRegistry.Register(plugin);

            // Two full passes, each starting from a clean registry — mirrors
            // CommandRouter.RegisterAll() calling CommandRegistry.Clear() before every pass.
            CommandRegistry.Clear();
            PluginRegistry.RegisterAllPlugins();
            CommandRegistry.Clear();
            PluginRegistry.RegisterAllPlugins();

            Assert.IsTrue(CommandRegistry.IsRegistered("stable_cmd"));
            Assert.AreEqual(0, PluginRegistry.GetFailedPlugins().Count,
                "a legitimate re-registration after Clear() must not be recorded as a failure");
        }

        [Test]
        public void IsPluginCommand_MatchesPrefix()
        {
            var plugin = new FakePlugin("PrefixPlugin", "myplugin");
            PluginRegistry.Register(plugin);

            Assert.IsTrue(PluginRegistry.IsPluginCommand("myplugin"));
            Assert.IsTrue(PluginRegistry.IsPluginCommand("myplugin_action"));
            Assert.IsFalse(PluginRegistry.IsPluginCommand("other_command"));
        }

        [TestCase("my")]
        [TestCase("my_")]
        public void IsPluginCommand_NormalizesCanonicalAndLegacyPrefix(string prefix)
        {
            PluginRegistry.Register(new FakePlugin("PrefixPlugin", prefix));

            Assert.IsTrue(PluginRegistry.IsPluginCommand("my"));
            Assert.IsTrue(PluginRegistry.IsPluginCommand("my_count"));
            Assert.IsFalse(PluginRegistry.IsPluginCommand("myth"));
            Assert.IsFalse(PluginRegistry.IsPluginCommand("myth_count"));
        }

        // ── GetCommandsForPlugin ─────────────────────────────────────────────

        [Test]
        public void GetCommandsForPlugin_ReturnsOnlyPluginCommands()
        {
            CommandRegistry.Clear();
            var plugin = new FakePlugin("MyPlugin", "myplugin");
            CommandRegistry.Register("myplugin", _ => "ok");
            CommandRegistry.Register("myplugin_action", _ => "ok");
            CommandRegistry.Register("other", _ => "ok");
            PluginRegistry.Register(plugin);

            var result = PluginRegistry.GetCommandsForPlugin(plugin);

            CollectionAssert.Contains(result, "myplugin");
            CollectionAssert.Contains(result, "myplugin_action");
            CollectionAssert.DoesNotContain(result, "other");
        }

        [Test]
        public void GetCommandsForPlugin_IsolatesPlugins()
        {
            CommandRegistry.Clear();
            var plugin1 = new FakePlugin("Plugin1", "p1");
            var plugin2 = new FakePlugin("Plugin2", "p2");
            CommandRegistry.Register("p1_cmd", _ => "ok");
            CommandRegistry.Register("p2_cmd", _ => "ok");
            PluginRegistry.Register(plugin1);
            PluginRegistry.Register(plugin2);

            var result1 = PluginRegistry.GetCommandsForPlugin(plugin1);
            var result2 = PluginRegistry.GetCommandsForPlugin(plugin2);

            CollectionAssert.Contains(result1, "p1_cmd");
            CollectionAssert.DoesNotContain(result1, "p2_cmd");
            CollectionAssert.Contains(result2, "p2_cmd");
            CollectionAssert.DoesNotContain(result2, "p1_cmd");
        }

        [Test]
        public void GetCommandsForPlugin_LegacyTrailingSeparator_UsesSingleBoundary()
        {
            CommandRegistry.Clear();
            var plugin = new FakePlugin("LegacyPrefix", "my_");
            CommandRegistry.Register("my", _ => "ok");
            CommandRegistry.Register("my_count", _ => "ok");
            CommandRegistry.Register("myth", _ => "ok");
            PluginRegistry.Register(plugin);

            var result = PluginRegistry.GetCommandsForPlugin(plugin);

            CollectionAssert.Contains(result, "my");
            CollectionAssert.Contains(result, "my_count");
            CollectionAssert.DoesNotContain(result, "myth");
        }

        [Test]
        public void GetCommandsForPlugin_IncludesAdditionalCommands()
        {
            CommandRegistry.Clear();
            var plugin = new FakePluginWithExtra("ExtraPlugin", "extra");
            CommandRegistry.Register("extra_base", _ => "ok");
            CommandRegistry.Register("extra_cmd", _ => "ok");
            PluginRegistry.Register(plugin);

            var result = PluginRegistry.GetCommandsForPlugin(plugin);

            CollectionAssert.Contains(result, "extra_cmd");
        }

        [Test]
        public void PreserveStateForTests_RestoresPluginsAndFailureRecordsExactly()
        {
            var original = new FakePlugin("Original") { RegisterCommandsThrows = true };
            PluginRegistry.Register(original);
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("Original.*RegisterCommands failed"));
            PluginRegistry.RegisterAllPlugins();

            using (PluginRegistry.PreserveStateForTests())
            {
                PluginRegistry.Clear();
                PluginRegistry.Register(new FakePlugin("Replacement"));
            }

            Assert.AreEqual(1, PluginRegistry.GetAll().Count);
            Assert.AreSame(original, PluginRegistry.GetAll()[0]);
            Assert.AreEqual(1, PluginRegistry.GetFailedPlugins().Count);
            Assert.AreEqual("Original", PluginRegistry.GetFailedPlugins()[0].Name);
        }

        private class FakePluginWithExtra : IMCPPlugin
        {
            public string Name { get; }
            public string CommandPrefix { get; }
            public FakePluginWithExtra(string name, string prefix) { Name = name; CommandPrefix = prefix; }
            public void RegisterCommands() { }
            public void OnDomainReload() { }
            public IReadOnlyList<string> AdditionalCommands => new[] { "extra_cmd" };
        }
    }
}
