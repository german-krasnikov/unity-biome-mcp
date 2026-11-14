// TDD tests for ChatPlugin — the first production IMCPPlugin registrant
// (Plans/task-phase2-chat-plugin.md sub-task 4 / Phase 2 sub-task 2.4).
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatPluginTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // No SetUp touching PluginRegistry here on purpose: this test reads the ambient
        // production state left by ChatPluginBootstrap's [InitializeOnLoad] static ctor.
        // It is only reliable once PluginRegistryTests/PluginRegistryOwnershipTests/
        // PluginSettingsPageTests stop destructively Clear()-ing without restoring (see
        // the fix applied to those 3 files in this same commit).
        [Test]
        public void ProductionBootstrap_RegistersChatPlugin_AmbientStateAfterDomainReload()
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (var p in PluginRegistry.GetAll()) names.Add(p.Name);

            CollectionAssert.Contains(names, "Chat",
                "ChatPluginBootstrap's [InitializeOnLoad] static ctor must have registered ChatPlugin");
        }

        [Test]
        public void Name_IsChat()
        {
            Assert.AreEqual("Chat", new ChatPlugin().Name);
        }

        [Test]
        public void CommandPrefix_IsChat()
        {
            Assert.AreEqual("chat", new ChatPlugin().CommandPrefix);
        }

        [Test]
        public void RegisterAllPlugins_WithRealChatPluginAndFakeSibling_DoesNotThrow_SiblingSurvives()
        {
            RegisterCleanup(PluginRegistry.PreserveStateForTests().Dispose);
            PluginRegistry.Clear();
            CommandRegistry.Clear();

            PluginRegistry.Register(new ChatPlugin());
            PluginRegistry.Register(new FakeSiblingPlugin());

            Assert.DoesNotThrow(() => PluginRegistry.RegisterAllPlugins());

            Assert.IsTrue(CommandRegistry.IsRegistered("fakesibling_cmd"),
                "a real ChatPlugin in the list must not roll back an independent sibling plugin's registration");
            Assert.AreEqual(0, PluginRegistry.GetFailedPlugins().Count);
        }

        [Test]
        public void OnDomainReload_CallsChatPlugin_WithoutThrowing()
        {
            RegisterCleanup(PluginRegistry.PreserveStateForTests().Dispose);
            PluginRegistry.Clear();
            PluginRegistry.Register(new ChatPlugin());

            Assert.DoesNotThrow(() => PluginRegistry.OnDomainReload());
        }

        [Test]
        public void BuildSettingsUI_ReturnsNonNull_MatchesChatConnectionSectionBuildDirectly()
        {
            var viaPlugin = new ChatPlugin().BuildSettingsUI();
            var viaDirect = new VisualElement();
            ChatConnectionSection.Build(viaDirect);

            Assert.IsNotNull(viaPlugin);
            Assert.AreEqual(viaDirect.childCount, viaPlugin.childCount,
                "BuildSettingsUI must produce the same content as calling ChatConnectionSection.Build directly");
        }

        [Test]
        public void HasSettingsUI_IsTrue()
        {
            Assert.IsTrue(new ChatPlugin().HasSettingsUI);
        }

        private sealed class FakeSiblingPlugin : IMCPPlugin
        {
            public string Name => "FakeSibling";
            public string CommandPrefix => "fakesibling";
            public void RegisterCommands() => CommandRegistry.Register("fakesibling_cmd", _ => "ok");
            public void OnDomainReload() { }
        }
    }
}
