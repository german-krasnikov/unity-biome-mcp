using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// N1a T7/T8: Entry.Owner is registration data (not a prefix guess), and
    /// PluginRegistry.Register() diagnoses conflicting module IDs explicitly.
    /// Split out of PluginRegistryTests.cs to keep that file at its existing size.
    /// </summary>
    [TestFixture]
    public class PluginRegistryOwnershipTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private class FakePlugin : IMCPPlugin
        {
            public string Name { get; }
            public string CommandPrefix { get; }
            public bool RegisterCommandsThrows;
            public Action RegisterCommandsBody;

            public FakePlugin(string name, string prefix = "fake")
            {
                Name = name;
                CommandPrefix = prefix;
            }

            public void RegisterCommands()
            {
                RegisterCommandsBody?.Invoke();
                if (RegisterCommandsThrows) throw new InvalidOperationException("simulated");
            }

            public void OnDomainReload() { }

            public IReadOnlyList<string> AdditionalCommands => Array.Empty<string>();
        }

        [SetUp]
        public void SetUp()
        {
            PluginRegistry.Clear();
            CommandRegistry.Clear();
        }

        // ── T7: Owner field ──────────────────────────────────────────────────

        [Test]
        public void RegisterAllPlugins_OwnerFieldSetFromCallerPluginName()
        {
            CommandRegistry.Register("host_probe_cmd", _ => "ok");
            var plugin = new FakePlugin("OwnerTest")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("owner_test_cmd", _ => "ok")
            };
            PluginRegistry.Register(plugin);

            PluginRegistry.RegisterAllPlugins();

            Assert.AreEqual("OwnerTest", CommandRegistry.GetOwner("owner_test_cmd"),
                "a plugin-registered command's Owner must be the registering plugin's Name");
            Assert.IsNull(CommandRegistry.GetOwner("host_probe_cmd"),
                "a host-registered command must have a null Owner");
        }

        [Test]
        public void RegisterAllPlugins_ThrowingPlugin_CallerPluginNameClearedAfterReturn()
        {
            var plugin = new FakePlugin("ThrowingOwner") { RegisterCommandsThrows = true };
            PluginRegistry.Register(plugin);

            LogAssert.Expect(LogType.Error,
                new Regex("ThrowingOwner.*RegisterCommands failed"));
            PluginRegistry.RegisterAllPlugins();

            Assert.IsNull(CommandRegistry.CallerPluginName,
                "the scoped owner must be cleared via finally even when the plugin throws");
        }

        // ── Checklist (4): failed plugin never owns a sibling's commands ──────

        [Test]
        public void FailedPlugin_DoesNotOwnSiblingCommands()
        {
            var pluginA = new FakePlugin("PluginA")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("a_cmd", _ => "A")
            };
            var pluginB = new FakePlugin("PluginB")
            {
                RegisterCommandsBody = () => CommandRegistry.Register("b_leftover_cmd", _ => "B"),
                RegisterCommandsThrows = true
            };
            PluginRegistry.Register(pluginA);
            PluginRegistry.Register(pluginB);

            LogAssert.Expect(LogType.Error, new Regex("PluginB.*RegisterCommands failed"));
            PluginRegistry.RegisterAllPlugins();

            Assert.IsFalse(CommandRegistry.IsRegistered("b_leftover_cmd"),
                "the failed plugin's own command must be rolled back");
            Assert.IsTrue(CommandRegistry.IsRegistered("a_cmd"));
            Assert.AreEqual("PluginA", CommandRegistry.GetOwner("a_cmd"));
            Assert.AreNotEqual("PluginB", CommandRegistry.GetOwner("a_cmd"),
                "a failed sibling must never appear as the owner of another plugin's command");
            Assert.IsNull(CommandRegistry.GetOwner("b_leftover_cmd"),
                "a rolled-back command carries no ownership record at all");
        }

        // ── T8: module ID conflict diagnosis ───────────────────────────────────

        [Test]
        public void Register_SameInstanceTwice_IsIdempotent()
        {
            var plugin = new FakePlugin("SameInstance");

            LogAssert.Expect(LogType.Log, new Regex("Plugin registered: SameInstance"));
            PluginRegistry.Register(plugin);
            PluginRegistry.Register(plugin);

            Assert.AreEqual(1, PluginRegistry.GetAll().Count);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Register_DifferentInstanceSameId_Refused()
        {
            var first = new FakePlugin("Same");
            var second = new FakePlugin("Same");

            LogAssert.Expect(LogType.Log, new Regex("Plugin registered: Same"));
            PluginRegistry.Register(first);
            LogAssert.Expect(LogType.Error, new Regex("conflict"));
            PluginRegistry.Register(second);

            Assert.AreEqual(1, PluginRegistry.GetAll().Count);
            Assert.AreSame(first, PluginRegistry.GetAll()[0],
                "the first-registered instance must be retained; the conflicting instance is refused");
        }
    }
}
