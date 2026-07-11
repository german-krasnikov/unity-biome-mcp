// ChatWindowWindowLifecycleTests — 25 lifecycle tests (tests 51-75).
// Each test manages its own window. No shared SetUp/TearDown.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowWindowLifecycleTests
    {
        static MCPChatWindow Open() => EditorWindow.GetWindow<MCPChatWindow>(false, "LC", false);
        static Button AskBtnOf(MCPChatWindow w) => w.rootVisualElement.Q<Button>(null, "mode-toggle-btn");
        static Button AgentBtnOf(MCPChatWindow w) => w.rootVisualElement.Q<Button>(null, "mode-toggle-btn--last");
        static FieldInfo F(string n) => typeof(MCPChatWindow).GetField(n, BindingFlags.NonPublic | BindingFlags.Instance);

        void W(Action<MCPChatWindow> t) { var w = Open(); try { t(w); } finally { w.Close(); } }

        private sealed class FakeBackend : IChatBackend
        {
            public bool IsRunning { get; private set; }
            public string SessionId => null;
            public void Start()  { IsRunning = true; }
            public void Stop()   { IsRunning = false; }
            public void SendTurn(string _) { }
            public void SendControlResponse(string _) { }
            public void DrainEvents(List<ChatEvent> _, List<ToolCallRecord> __ = null) { }
        }

        [Test] public void Open_DoesNotThrow()
            => Assert.DoesNotThrow(() => { var w = Open(); w.Close(); });

        [Test] public void Open_CreateGUI_BuildsRootElement()
            => W(w => Assert.IsNotNull(w.rootVisualElement));

        [Test] public void Open_Close_NoException()
            => Assert.DoesNotThrow(() => Open().Close());

        [Test] public void DoubleClose_IsNoOp()
        { var w = Open(); w.Close(); Assert.DoesNotThrow(() => w.Close()); }

        [Test] public void Open_AgentMode_DefaultFalse()
            => W(w => Assert.IsFalse((bool)F("_agentMode").GetValue(w)));

        [Test] public void Open_Activity_DefaultIdle()
            => W(w => Assert.AreEqual(ActivityPhase.Idle, ((ChatActivityState)F("_activity").GetValue(w)).Phase));

        [Test] public void Open_InputTokens_DefaultZero()
            => W(w => Assert.AreEqual(0, (int)F("_inputTokens").GetValue(w)));

        [Test] public void Open_OutputTokens_DefaultZero()
            => W(w => Assert.AreEqual(0, (int)F("_outputTokens").GetValue(w)));

        [Test] public void Open_TurnEditedCode_DefaultFalse()
            => W(w => Assert.IsFalse(w._turnEditedCode));

        [Test] public void Open_TurnHasToolCalls_DefaultFalse()
            => W(w => Assert.IsFalse(w._turnHasToolCalls));

        [Test] public void Open_LastToolName_DefaultNull()
            => W(w => Assert.IsNull(w._lastToolName));

        [Test] public void Open_ResumeRetryCount_DefaultZero()
            => W(w => Assert.AreEqual(0, w._resumeRetryCount));

        [Test] public void Open_AutoFix_NotNull()
            => W(w => Assert.IsNotNull(w._autoFix));

        [Test] public void Close_Backend_GetsStopped()
        {
            var w  = Open();
            var fb = new FakeBackend();
            F("_backend").SetValue(w, fb); fb.Start();
            w.Close();
            Assert.IsFalse(fb.IsRunning);
        }

        [Test] public void Close_NullBackend_NoException()
        { var w = Open(); F("_backend").SetValue(w, null); Assert.DoesNotThrow(() => w.Close()); }

        [Test] public void Open_ScrollView_ExistsBeforeSend()
            => W(w => Assert.IsNotNull(w.rootVisualElement.Q<ScrollView>(null,"chat-scroll")));

        [Test] public void Open_InputField_NotReadOnly()
            => W(w => Assert.IsFalse(w.rootVisualElement.Q<TextField>()?.isReadOnly ?? true));

        [Test] public void Open_SendButton_EnabledSelf()
            => W(w => Assert.IsTrue(w.rootVisualElement.Q<Button>(null,"chat-btn--send")?.enabledSelf ?? false));

        [Test] public void Open_StopButton_EnabledSelf()
            => W(w => Assert.IsTrue(w.rootVisualElement.Q<Button>(null,"chat-btn--stop")?.enabledSelf ?? false));

        [Test] public void Open_AskButton_EnabledSelf()
            => W(w => Assert.IsTrue(AskBtnOf(w)?.enabledSelf ?? false));

        [Test] public void Open_AgentButton_EnabledSelf()
            => W(w => Assert.IsTrue(AgentBtnOf(w)?.enabledSelf ?? false));

        [Test] public void Open_AgentDropdown_EnabledSelf()
            => W(w => Assert.IsTrue(w.rootVisualElement.Q<DropdownField>(null,"agent-selector")?.enabledSelf ?? false));

        [Test] public void ShowWindow_SetsMinSize()
        {
            MCPChatWindow.ShowWindow();
            var w = EditorWindow.GetWindow<MCPChatWindow>();
            try { Assert.GreaterOrEqual(w.minSize.x, 300f); } finally { w.Close(); }
        }
    }
}
