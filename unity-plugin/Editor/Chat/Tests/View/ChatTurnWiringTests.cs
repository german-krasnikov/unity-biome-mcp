// The wiring of MCPChatWindow around the chat-turn FEATURE (step 7.3): what the window does with an event or a call when no turn is open,
// and that a Stop releases what the turn raised. The exits themselves are dsl/chat/chatturn.examples; the bodies of the ports are ChatTurnPortsTests.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.UIElements;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat.Turn;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatTurnWiringTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const BindingFlags InstancePrivate = BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly MethodInfo HandleEventMethod = typeof(MCPChatWindow).GetMethod("HandleEvent", InstancePrivate);
        private static readonly MethodInfo CreateGuiMethod = typeof(MCPChatWindow).GetMethod("CreateGUI", InstancePrivate);
        private static readonly MethodInfo DispatchTurnMethod = typeof(MCPChatWindow).GetMethod("DispatchTurn", InstancePrivate, null,
            new[] { typeof(string), typeof(string), typeof(IReadOnlyList<ChipData>), typeof(string) }, null);

        [SetUp]
        public void SetUp() => PendingAskRegistry.CancelAll();

        [TearDown]
        public void TearDown() => PendingAskRegistry.CancelAll();

        private MCPChatWindow OpenWindow(FakeBackend backend, bool buildGui = false)
        {
            var window = WithBackendFactory(_ => backend, () =>
            {
                var created = CreateOwnedEditorWindow<MCPChatWindow>();
                if (buildGui) CreateGuiMethod.Invoke(created, null);
                return created;
            });
            return window;
        }

        private static T WithBackendFactory<T>(Func<string, IChatBackend> factory, Func<T> body)
        {
            var previous = MCPChatWindow.BackendFactoryForTest;
            MCPChatWindow.BackendFactoryForTest = factory;
            try { return body(); }
            finally { MCPChatWindow.BackendFactoryForTest = previous; }
        }

        private static void Fire(MCPChatWindow window, ChatEvent ev) => HandleEventMethod.Invoke(window, new object[] { ev });

        private static VisualElement UseTranscript(MCPChatWindow window)
        {
            var container = new VisualElement();
            window._transcript = new ChatTranscript(container, ChatBlockRendererFactory.CreateDefault(null, null));
            return container;
        }

        [Test]
        public void A_TurnDone_with_no_turn_open_does_nothing_and_with_one_open_ends_it()
        {
            var window = OpenWindow(new FakeBackend());
            var container = UseTranscript(window);

            Fire(window, ChatEvent.TurnDone("s1", 0f, 500, 200));
            Assert.That(container.childCount, Is.EqualTo(0), "a late TurnDone adds no second tail (no Restore button)");

            window._turn.Dispatch(ChatTurnIntent.Send());
            Fire(window, ChatEvent.TurnDone("s1", 0f, 500, 200));
            Assert.That(window._turn.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(container.childCount, Is.GreaterThan(0), "the TurnDone of an open turn runs its tail");
        }

        [Test]
        public void An_Error_with_no_turn_open_is_still_shown()
        {
            var window = OpenWindow(new FakeBackend());
            var container = UseTranscript(window);

            Fire(window, ChatEvent.Error("relay lost"));

            Assert.That(window._turn.Phase, Is.EqualTo(ChatTurnPhase.Idle), "it ends nothing");
            Assert.That(container.Query<Label>().ToList().Exists(label => label.text.Contains("relay lost")), Is.True);
        }

        [Test]
        public void A_send_over_an_open_turn_sends_nothing_and_takes_the_lock_once()
        {
            var backend = new FakeBackend();
            var window = OpenWindow(backend);
            window._turn.Dispatch(ChatTurnIntent.Send());
            Assert.That(ReloadGuard.IsLocked, Is.True, "precondition: the open turn holds the lock");

            DispatchTurnMethod.Invoke(window, new object[] { "{}", "a second message", null, null });

            Assert.That(backend.Sent, Is.Empty);
            window._turn.Dispatch(ChatTurnIntent.Abort());   // one exit gives the lock back only if it was taken once
            Assert.That(ReloadGuard.IsLocked, Is.False);
        }

        [Test]
        public void Stop_releases_the_ask_that_the_turn_raised()
        {
            var window = OpenWindow(new FakeBackend(), buildGui: true);
            window._turn.Dispatch(ChatTurnIntent.Send());

            CommandRouter.ProcessAsync("{\"id\":\"wiring-ask\",\"cmd\":\"ask_user\",\"args\":{}}", new TaskCompletionSource<string>());
            Assert.That(PendingAskRegistry.PendingCountForTests, Is.EqualTo(1), "precondition: the ask waits");
            Assert.That(window._turn.Asking, Is.True);

            WithBackendFactory(_ => new FakeBackend(), () => { window.CancelTurn(); return 0; });

            Assert.That(PendingAskRegistry.PendingCountForTests, Is.EqualTo(0), "the caller of ask_user is released, not left to its 300 s");
            Assert.That(window._turn.Asking, Is.False);
        }

        private sealed class FakeBackend : IChatBackend
        {
            public readonly List<string> Sent = new List<string>();
            public bool IsRunning { get; private set; } = true;
            public string SessionId => null;
            public void Start() { }
            public void Stop() { IsRunning = false; }
            public void SendTurn(string turnJson) { Sent.Add(turnJson); }
            public void SendControlResponse(string _) { }
            public void DrainEvents(List<ChatEvent> _, List<ToolCallRecord> __ = null) { }
        }
    }
}
