// TDD — MCPChatWindow.Plugins.cs toolbar button injection via ToolbarButtonRegistry.
// Tests that registered buttons appear in the footer bar.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PluginToolbarButtonTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]    public void SetUp()    => ToolbarButtonRegistry.ResetForTests();
        [TearDown] public void TearDown() => ToolbarButtonRegistry.ResetForTests();

        // 1. One registered button → button visible in footer with correct label.
        [Test]
        public void BuildPluginButtons_OneProvider_ButtonAdded()
        {
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("my_btn", "My Button"));

            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var bar = new VisualElement();
                window.BuildPluginButtons(bar);

                var buttons = bar.Query<Button>().ToList();
                Assert.AreEqual(1, buttons.Count, "Expected exactly one plugin button");
                Assert.AreEqual("My Button", buttons[0].text);
                Assert.AreEqual("my_btn tooltip", buttons[0].tooltip);
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 2. Button click → OnClick(window) called exactly once.
        [Test]
        public void BuildPluginButtons_ButtonClick_CallsOnClickOnce()
        {
            var provider = new FakeToolbarButtonProvider("my_btn");
            ToolbarButtonRegistry.Register(provider);

            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var bar = new VisualElement();
                window.BuildPluginButtons(bar);

                var btn = bar.Q<Button>();
                Assert.IsNotNull(btn);

                // Button.clicked is an event — can't invoke directly. Call OnClick explicitly.
                provider.OnClick(window);

                Assert.AreEqual(1, provider.ClickCount, "OnClick must be called exactly once on button click");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 3. Zero registered buttons → bar unchanged (no buttons added).
        [Test]
        public void BuildPluginButtons_ZeroProviders_BarUnchanged()
        {
            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var bar = new VisualElement();
                window.BuildPluginButtons(bar);

                Assert.AreEqual(0, bar.childCount, "No buttons should be added when registry is empty");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 4. Throwing provider → button still added, BuildPluginButtons does not throw.
        [Test]
        public void BuildPluginButtons_ThrowingProvider_ButtonStillAdded()
        {
            ToolbarButtonRegistry.Register(new ThrowingToolbarButtonProvider("bad_btn"));

            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var bar = new VisualElement();
                Assert.DoesNotThrow(() => window.BuildPluginButtons(bar));
                Assert.AreEqual(1, bar.Query<Button>().ToList().Count,
                    "Button must be added even for a throwing provider");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 5. MenuOnly provider → NOT rendered in toolbar bar.
        [Test]
        public void BuildPluginButtons_MenuOnlyProvider_SkippedInBar()
        {
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("snap", menuOnly: true));

            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var bar = new VisualElement();
                window.BuildPluginButtons(bar);
                Assert.AreEqual(0, bar.childCount, "MenuOnly provider must not appear in toolbar bar");
            }
            finally { Object.DestroyImmediate(window); }
        }

        // 6. Mixed providers → only non-MenuOnly ones rendered in toolbar bar.
        [Test]
        public void BuildPluginButtons_MixedProviders_OnlyToolbarOnesAdded()
        {
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("regular_btn", "Regular"));
            ToolbarButtonRegistry.Register(new FakeToolbarButtonProvider("menu_btn", "MenuOnly", menuOnly: true));

            var window = ScriptableObject.CreateInstance<MCPChatWindow>();
            try
            {
                var bar = new VisualElement();
                window.BuildPluginButtons(bar);

                var buttons = bar.Query<Button>().ToList();
                Assert.AreEqual(1, buttons.Count, "Only non-MenuOnly providers must appear in toolbar");
                Assert.AreEqual("Regular", buttons[0].text);
            }
            finally { Object.DestroyImmediate(window); }
        }
    }

    internal sealed class ThrowingToolbarButtonProvider : IToolbarButtonProvider
    {
        public string Key         { get; }
        public int    Order       => 100;
        public string ButtonLabel => "Throw";
        public string Tooltip     => "throws";

        public ThrowingToolbarButtonProvider(string key) => Key = key;

        public void OnClick(UnityEditor.EditorWindow window) =>
            throw new System.InvalidOperationException("provider crash");
    }
}
