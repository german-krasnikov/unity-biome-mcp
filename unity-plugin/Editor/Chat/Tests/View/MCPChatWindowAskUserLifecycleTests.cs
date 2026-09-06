// TDD (PR-05 05.2): closing the Chat window while it owns a pending ask_user request must
// cancel that exact request immediately. Before this fix, OnDisable unsubscribed from
// CommandRouter.OnAskUser but left the PendingAskRegistry entry dangling until the next
// domain reload (Finding 2 — symmetric gap to the fail-fast fix in CommandRouterAskUserTests).
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MCPChatWindowAskUserLifecycleTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const BindingFlags InstancePrivate =
            BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly MethodInfo CreateGuiMethod =
            typeof(MCPChatWindow).GetMethod("CreateGUI", InstancePrivate);
        private static readonly MethodInfo OnDisableMethod =
            typeof(MCPChatWindow).GetMethod("OnDisable", InstancePrivate);

        [SetUp]
        public void Setup() => PendingAskRegistry.CancelAll();

        [Test]
        public void OnDisable_WithPendingAsk_CancelsOwnedPendingRequest()
        {
            var previousFactory = MCPChatWindow.BackendFactoryForTest;
            MCPChatWindow.BackendFactoryForTest = _ => new FakeBackend();
            MCPChatWindow window;
            try
            {
                // OnEnable runs synchronously here (Unity ScriptableObject.CreateInstance
                // contract) and subscribes CommandRouter.OnAskUser += OnMcpAskUser.
                window = CreateOwnedEditorWindow<MCPChatWindow>();
                // OnMcpAskUser touches _scroll unconditionally — needs a built GUI.
                CreateGuiMethod.Invoke(window, null);
            }
            finally
            {
                MCPChatWindow.BackendFactoryForTest = previousFactory;
            }

            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync(
                "{\"id\":\"ask-lifecycle-probe\",\"cmd\":\"ask_user\",\"args\":{}}", tcs);
            Assert.AreEqual(1, PendingAskRegistry.PendingCountForTests,
                "the ask must be registered in PendingAskRegistry while the window is open");

            OnDisableMethod.Invoke(window, null);

            Assert.AreEqual(0, PendingAskRegistry.PendingCountForTests,
                "closing the window must cancel its own owned pending asks, not leave them dangling");
        }

        private sealed class FakeBackend : IChatBackend
        {
            public bool IsRunning => false;
            public string SessionId => null;
            public void Start() { }
            public void Stop() { }
            public void SendTurn(string _) { }
            public void SendControlResponse(string _) { }
            public void DrainEvents(List<ChatEvent> _, List<ToolCallRecord> __ = null) { }
        }
    }
}
