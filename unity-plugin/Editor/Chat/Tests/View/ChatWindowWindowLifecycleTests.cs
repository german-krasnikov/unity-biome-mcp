// ChatWindowWindowLifecycleTests — 25 lifecycle tests (tests 51-75).
// Each test manages its own window. No shared SetUp/TearDown.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatWindowWindowLifecycleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly MethodInfo CreateGuiMethod = RequiredMethod("CreateGUI");
        private static readonly MethodInfo OnDisableMethod = RequiredMethod("OnDisable");

        static Button AskBtnOf(MCPChatWindow w) => w.rootVisualElement.Q<Button>(null, "mode-toggle-btn");
        static Button AgentBtnOf(MCPChatWindow w) => w.rootVisualElement.Q<Button>(null, "mode-toggle-btn--last");
        static FieldInfo F(string n) => typeof(MCPChatWindow).GetField(n, InstancePrivate);

        private static MethodInfo RequiredMethod(string name) =>
            typeof(MCPChatWindow).GetMethod(name, InstancePrivate) ??
            throw new InvalidOperationException($"MCPChatWindow.{name} was not found.");

        private MCPChatWindow CreateOwnedWindow(bool buildGui = false)
        {
            var previousFactory = MCPChatWindow.BackendFactoryForTest;
            MCPChatWindow.BackendFactoryForTest = _ => new FakeBackend();
            try
            {
                var window = CreateOwnedEditorWindow<MCPChatWindow>();
                if (buildGui) CreateGuiMethod.Invoke(window, null);
                return window;
            }
            finally
            {
                MCPChatWindow.BackendFactoryForTest = previousFactory;
            }
        }

        private void WithOwnedWindow(Action<MCPChatWindow> test, bool buildGui = false)
        {
            var window = CreateOwnedWindow(buildGui);
            test(window);
        }

        private static void Disable(MCPChatWindow window) =>
            OnDisableMethod.Invoke(window, null);

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
            => Assert.DoesNotThrow(() => WithOwnedWindow(_ => { }));

        [Test] public void Open_CreateGUI_BuildsRootElement()
            => WithOwnedWindow(w =>
            {
                Assert.IsNotNull(w.rootVisualElement);
                Assert.IsTrue(w.rootVisualElement.ClassListContains("chat-root"));
            }, buildGui: true);

        [Test] public void Open_Close_NoException()
            => WithOwnedWindow(w => Assert.DoesNotThrow(() => Disable(w)));

        [Test] public void DoubleClose_IsNoOp()
            => WithOwnedWindow(w =>
            {
                Disable(w);
                Assert.DoesNotThrow(() => Disable(w));
            });

        [Test] public void Open_AgentMode_DefaultFalse()
            => WithOwnedWindow(w => Assert.IsFalse((bool)F("_agentMode").GetValue(w)));

        [Test] public void Open_Activity_DefaultIdle()
            => WithOwnedWindow(w => Assert.AreEqual(ActivityPhase.Idle, ((ChatActivityState)F("_activity").GetValue(w)).Phase));

        [Test] public void Open_InputTokens_DefaultZero()
            => WithOwnedWindow(w => Assert.AreEqual(0, (int)F("_inputTokens").GetValue(w)));

        [Test] public void Open_OutputTokens_DefaultZero()
            => WithOwnedWindow(w => Assert.AreEqual(0, (int)F("_outputTokens").GetValue(w)));

        [Test] public void Open_TurnEditedCode_DefaultFalse()
            => WithOwnedWindow(w => Assert.IsFalse(w._turnEditedCode));

        [Test] public void Open_TurnHasToolCalls_DefaultFalse()
            => WithOwnedWindow(w => Assert.IsFalse(w._turnHasToolCalls));

        [Test] public void Open_LastToolName_DefaultNull()
            => WithOwnedWindow(w => Assert.IsNull(w._lastToolName));

        [Test] public void Open_ResumeRetryCount_DefaultZero()
            => WithOwnedWindow(w => Assert.AreEqual(0, w._resumeRetryCount));

        [Test] public void Open_AutoFix_NotNull()
            => WithOwnedWindow(w => Assert.IsNotNull(w._autoFix));

        [Test] public void Close_Backend_GetsStopped()
        {
            WithOwnedWindow(w =>
            {
                var backend = new FakeBackend();
                F("_backend").SetValue(w, backend);
                backend.Start();

                Disable(w);

                Assert.IsFalse(backend.IsRunning);
                Assert.IsNull(F("_backend").GetValue(w));
            });
        }

        [Test] public void Close_NullBackend_NoException()
            => WithOwnedWindow(w =>
            {
                F("_backend").SetValue(w, null);
                Assert.DoesNotThrow(() => Disable(w));
            });

        [Test] public void Open_ScrollView_ExistsBeforeSend()
            => WithOwnedWindow(w => Assert.IsNotNull(
                w.rootVisualElement.Q<ScrollView>(null, "chat-scroll")), buildGui: true);

        [Test] public void Open_InputField_NotReadOnly()
            => WithOwnedWindow(w => Assert.IsFalse(
                w.rootVisualElement.Q<TextField>()?.isReadOnly ?? true), buildGui: true);

        [Test] public void Open_SendButton_EnabledSelf()
            => WithOwnedWindow(w => Assert.IsTrue(
                w.rootVisualElement.Q<Button>(null, "chat-btn--send")?.enabledSelf ?? false),
                buildGui: true);

        [Test] public void Open_StopButton_EnabledSelf()
            => WithOwnedWindow(w => Assert.IsTrue(
                w.rootVisualElement.Q<Button>(null, "chat-btn--stop")?.enabledSelf ?? false),
                buildGui: true);

        [Test] public void Open_AskButton_EnabledSelf()
            => WithOwnedWindow(w => Assert.IsTrue(AskBtnOf(w)?.enabledSelf ?? false),
                buildGui: true);

        [Test] public void Open_AgentButton_EnabledSelf()
            => WithOwnedWindow(w => Assert.IsTrue(AgentBtnOf(w)?.enabledSelf ?? false),
                buildGui: true);

        [Test] public void Open_AgentDropdown_EnabledSelf()
            => WithOwnedWindow(w => Assert.IsTrue(
                w.rootVisualElement.Q<DropdownField>(null, "agent-selector")?.enabledSelf ?? false),
                buildGui: true);

        [Test] public void ShowWindow_SetsMinSize()
        {
            var showWindow = typeof(MCPChatWindow).GetMethod(
                nameof(MCPChatWindow.ShowWindow), BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(showWindow);
            var il = showWindow.GetMethodBody()?.GetILAsByteArray();
            Assert.IsNotNull(il);
            Assert.IsTrue(ContainsFloatConstant(il, 320f),
                "ShowWindow must retain the 320 px minimum width contract.");
            Assert.IsTrue(ContainsFloatConstant(il, 400f),
                "ShowWindow must retain the 400 px minimum height contract.");
            Assert.IsTrue(CallsEditorWindowMinSizeSetter(showWindow, il),
                "ShowWindow must apply its size contract through EditorWindow.minSize.");
        }

        private static bool ContainsFloatConstant(byte[] il, float value)
        {
            var bytes = BitConverter.GetBytes(value);
            for (var i = 0; i <= il.Length - bytes.Length - 1; i++)
            {
                if (il[i] != 0x22) continue; // ldc.r4
                var matches = true;
                for (var j = 0; j < bytes.Length; j++)
                    matches &= il[i + j + 1] == bytes[j];
                if (matches) return true;
            }
            return false;
        }

        private static bool CallsEditorWindowMinSizeSetter(MethodInfo owner, byte[] il)
        {
            for (var i = 0; i <= il.Length - 5; i++)
            {
                if (il[i] != 0x28 && il[i] != 0x6f) continue; // call / callvirt
                try
                {
                    var called = owner.Module.ResolveMethod(BitConverter.ToInt32(il, i + 1))
                        as MethodInfo;
                    if (called?.Name == "set_minSize" &&
                        called.DeclaringType == typeof(EditorWindow))
                        return true;
                }
                catch (ArgumentException)
                {
                    // A call-like byte inside another operand is not an instruction.
                }
            }
            return false;
        }
    }
}
