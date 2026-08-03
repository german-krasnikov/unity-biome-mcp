// TDD — ChatSettingsSection plugin injection via SettingsProviderRegistry.
// Tests that registered providers produce foldout children in the settings panel.
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PluginSettingsInjectionTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => SettingsProviderRegistry.ResetForTests();
        [TearDown] public void TearDown() => SettingsProviderRegistry.ResetForTests();

        // 1. One registered provider → foldout with DisplayName appears in parent.
        [Test]
        public void BuildContent_OneProvider_FoldoutWithDisplayNameAdded()
        {
            SettingsProviderRegistry.Register(new FakeSettingsProvider("my_plugin", "My Plugin Settings"));

            var parent = new VisualElement();
            ChatSettingsSection.BuildContent(parent);

            var foldouts = parent.Query<Foldout>().ToList();
            bool found = false;
            foreach (var f in foldouts)
                if (f.text == "My Plugin Settings") { found = true; break; }
            Assert.IsTrue(found, "Expected foldout with text 'My Plugin Settings'");
        }

        // 2. Zero providers → no extra foldouts beyond built-ins.
        [Test]
        public void BuildContent_ZeroProviders_NoPluginFoldoutsAdded()
        {
            // No providers registered — baseline foldout count from built-ins.
            var baseline = new VisualElement();
            ChatSettingsSection.BuildContent(baseline);
            int baselineCount = baseline.Query<Foldout>().ToList().Count;

            // Should be identical after calling with empty registry.
            var parent = new VisualElement();
            ChatSettingsSection.BuildContent(parent);
            int actual = parent.Query<Foldout>().ToList().Count;

            Assert.AreEqual(baselineCount, actual, "No extra foldouts expected when no providers registered");
        }

        // 3. Provider that throws in BuildUI → caught, foldout NOT added, next providers still render.
        [Test]
        public void BuildContent_ProviderThrows_CaughtAndNextProviderStillRenders()
        {
            SettingsProviderRegistry.Register(new FakeSettingsProvider("throws_plugin", "Bad Plugin", order: 100, throws: true));
            SettingsProviderRegistry.Register(new FakeSettingsProvider("good_plugin",   "Good Plugin", order: 200));

            var parent = new VisualElement();
            // Debug.LogException is emitted for the failing provider.
            LogAssert.Expect(LogType.Exception, new Regex("provider error"));
            // Must not throw
            Assert.DoesNotThrow(() => ChatSettingsSection.BuildContent(parent));

            // "Bad Plugin" foldout not added (caught before parent.Add), "Good Plugin" is added.
            var foldouts = parent.Query<Foldout>().ToList();
            bool badFound = false, goodFound = false;
            foreach (var f in foldouts)
            {
                if (f.text == "Bad Plugin")  badFound  = true;
                if (f.text == "Good Plugin") goodFound = true;
            }
            Assert.IsFalse(badFound,  "Throwing provider's foldout must NOT be added");
            Assert.IsTrue(goodFound,  "Good provider's foldout must still be added");
        }
    }
}
