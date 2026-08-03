using System;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Tests
{
    /// <summary>
    /// TDD: Plugins settings page — conditional display based on plugin UI availability.
    /// </summary>
    [TestFixture]
    public class PluginSettingsPageTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp() => PluginRegistry.Clear();

        [TearDown]
        public void TearDown() => PluginRegistry.Clear();

        [Test]
        public void NoPlugins_PageShowsEmptyMessage()
        {
            var page = SettingsPageFactory.BuildPluginsPage(() => { });
            var labels = page.Query<Label>().ToList();
            Assert.IsTrue(labels.Exists(l => l.text.Contains("No plugins")),
                "Empty registry must show 'No plugins' message");
        }

        [Test]
        public void PluginWithNoUI_NotShownOnPage()
        {
            PluginRegistry.Register(new FakePlugin("NoUI", null));
            var page = SettingsPageFactory.BuildPluginsPage(() => { });
            var cards = page.Query<VisualElement>(className: "hub-card").ToList();
            Assert.AreEqual(0, cards.Count, "Plugin returning null BuildSettingsUI must not produce a card");
        }

        [Test]
        public void PluginWithUI_CardAppearsOnPage()
        {
            PluginRegistry.Register(new FakePlugin("MyPlugin", new Label("settings")));
            var page = SettingsPageFactory.BuildPluginsPage(() => { });
            var cards = page.Query<VisualElement>(className: "hub-card").ToList();
            Assert.AreEqual(1, cards.Count, "Plugin with BuildSettingsUI must produce exactly one card");
        }

        [Test]
        public void HubUI_PluginsCard_HiddenWhenNoPluginHasUI()
        {
            PluginRegistry.Register(new FakePlugin("NoUI", null));
            bool anyWithUI = false;
            foreach (var p in PluginRegistry.All)
                anyWithUI |= p.HasSettingsUI;
            Assert.IsFalse(anyWithUI, "No plugin with UI registered — hub must not show Plugins card");
        }
    }

    /// <summary>Test double: IMCPPlugin with configurable BuildSettingsUI return value.</summary>
    internal class FakePlugin : IMCPPlugin
    {
        readonly VisualElement _ui;

        public FakePlugin(string name, VisualElement ui)
        {
            Name  = name;
            _ui   = ui;
        }

        public string Name           { get; }
        public string CommandPrefix  => "";
        public bool HasSettingsUI    => _ui != null;
        public void RegisterCommands() { }
        public void OnDomainReload()   { }
        public VisualElement BuildSettingsUI() => _ui;
    }
}
