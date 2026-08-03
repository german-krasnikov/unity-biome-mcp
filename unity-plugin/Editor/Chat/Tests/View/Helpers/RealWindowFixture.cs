// Reusable base class for UI Toolkit tests that need an initialized MCPChatWindow.
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public abstract class RealWindowFixture : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        static readonly MethodInfo s_setMode = typeof(MCPChatWindow)
            .GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly MethodInfo s_createGui = typeof(MCPChatWindow)
            .GetMethod("CreateGUI", BindingFlags.NonPublic | BindingFlags.Instance);

        protected void SetMode(bool agent) => s_setMode.Invoke(W, new object[] { agent });
        protected MCPChatWindow W;

        [SetUp]
        public void CreateRealWindow()
        {
            DeleteEditorPrefString("MCPChat.SelectedBackend");
            foreach (var backend in new[] { "Claude", "Codex", "Antigravity" })
            {
                ProtectEditorPrefString("MCPChat.SelectedModel." + backend);
                ProtectEditorPrefString("MCPChat.SelectedModel." + backend + ".custom");
            }
            W = CreateInitializedWindow("MCPTest");
        }

        protected MCPChatWindow CreateInitializedWindow(string title)
        {
            var window = CreateOwnedEditorWindow<MCPChatWindow>();
            Assert.That(window, Is.Not.Null, "Unity did not create MCPChatWindow.");
            window.titleContent = new GUIContent(title);

            var root = window.rootVisualElement;
            Assert.That(root, Is.Not.Null, "MCPChatWindow has no root visual element.");
            Assert.That(s_createGui, Is.Not.Null,
                "MCPChatWindow.CreateGUI was not found.");
            s_createGui.Invoke(window, null);

            Assert.That(root.ClassListContains("chat-root"), Is.True,
                "MCPChatWindow.CreateGUI did not initialize the UI tree.");
            return window;
        }

        protected TextField InputField() => RequireElement(
            W.rootVisualElement.Q<TextField>(), "chat input field");
        protected Button SendBtn() => RequireElement(
            W.rootVisualElement.Q<Button>(null, "chat-btn--send"), "send button");
        protected Button StopBtn() => RequireElement(
            W.rootVisualElement.Q<Button>(null, "chat-btn--stop"), "stop button");
        protected Button AskBtn() => RequireElement(
            W.rootVisualElement.Q<Button>(null, "mode-toggle-btn"), "ask button");
        protected Button AgentBtn() => RequireElement(
            W.rootVisualElement.Q<Button>(null, "mode-toggle-btn--last"), "agent button");
        protected ScrollView Scroll() => RequireElement(
            W.rootVisualElement.Q<ScrollView>(null, "chat-scroll"), "chat scroll view");
        protected Label TokenLabel() => RequireElement(
            W.rootVisualElement.Q<Label>(null, "token-readout"), "token readout");
        protected VisualElement FlowBar() => RequireElement(
            W.rootVisualElement.Q(null, "flowbar"), "flow bar");
        protected VisualElement FlowFill() => RequireElement(
            W.rootVisualElement.Q(null, "flowbar__fill"), "flow bar fill");
        protected VisualElement InputArea() => RequireElement(
            W.rootVisualElement.Q(null, "input-area"), "input area");
        protected DropdownField AgentDrop() => RequireElement(
            W.rootVisualElement.Q<DropdownField>(null, "agent-selector"), "agent selector");

        private static T RequireElement<T>(T element, string description) where T : class
        {
            Assert.That(element, Is.Not.Null,
                $"MCPChatWindow.CreateGUI did not create the required {description}.");
            return element;
        }
    }
}
