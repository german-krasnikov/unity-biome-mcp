// Reusable base class for UIToolkit tests that need a real MCPChatWindow.
// Q<>() helpers for all standard UI elements. TearDown closes window + cleans prefs.
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public abstract class RealWindowFixture
    {
        static readonly MethodInfo s_setMode = typeof(MCPChatWindow)
            .GetMethod("SetMode", BindingFlags.NonPublic | BindingFlags.Instance);

        protected void SetMode(bool agent) => s_setMode.Invoke(W, new object[] { agent });
        protected MCPChatWindow W;

        [SetUp]
        public virtual void SetUp()
            => W = EditorWindow.GetWindow<MCPChatWindow>(false, "MCPTest", false);

        [TearDown]
        public virtual void TearDown()
        {
            if (W != null) { W.Close(); W = null; }
            EditorPrefs.DeleteKey("MCPChat.SelectedBackend");
        }

        protected TextField     InputField() => W.rootVisualElement.Q<TextField>();
        protected Button        SendBtn()    => W.rootVisualElement.Q<Button>(null, "chat-btn--send");
        protected Button        StopBtn()    => W.rootVisualElement.Q<Button>(null, "chat-btn--stop");
        protected Button        AskBtn()     => W.rootVisualElement.Q<Button>(null, "mode-toggle-btn");
        protected Button        AgentBtn()   => W.rootVisualElement.Q<Button>(null, "mode-toggle-btn--last");
        protected ScrollView    Scroll()     => W.rootVisualElement.Q<ScrollView>(null, "chat-scroll");
        protected Label         TokenLabel() => W.rootVisualElement.Q<Label>(null, "token-readout");
        protected VisualElement FlowBar()    => W.rootVisualElement.Q(null, "flowbar");
        protected VisualElement FlowFill()   => W.rootVisualElement.Q(null, "flowbar__fill");
        protected VisualElement InputArea()  => W.rootVisualElement.Q(null, "input-area");
        protected DropdownField AgentDrop()  => W.rootVisualElement.Q<DropdownField>(null, "agent-selector");
    }
}
