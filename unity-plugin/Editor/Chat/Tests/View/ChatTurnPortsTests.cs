// The 16 ports of the chat-turn FEATURE (step 7.3): the body of each effect, alone, on a real window with a fake backend.
// When a port runs, in which order and how often is dsl/chat/chatturn.examples (dotnet test Tests~/ChatTurn); it is not tested again here.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat.Turn;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ChatTurnPortsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const BindingFlags InstancePrivate = BindingFlags.NonPublic | BindingFlags.Instance;
        private MCPChatWindow _window;
        private FakeBackend _backend;

        private IChatTurnPorts Ports => _window;

        [SetUp]
        public void SetUp()
        {
            PendingAskRegistry.CancelAll();
            _backend = new FakeBackend();
            WithBackendFactory(_ => _backend, () => _window = CreateOwnedEditorWindow<MCPChatWindow>());
        }

        [TearDown]
        public void TearDown() => PendingAskRegistry.CancelAll();

        private static void WithBackendFactory(Func<string, IChatBackend> factory, Action body)
        {
            var previous = MCPChatWindow.BackendFactoryForTest;
            MCPChatWindow.BackendFactoryForTest = factory;
            try { body(); }
            finally { MCPChatWindow.BackendFactoryForTest = previous; }
        }

        private T Field<T>(string name) => (T)typeof(MCPChatWindow).GetField(name, InstancePrivate).GetValue(_window);
        private void SetField(string name, object value) => typeof(MCPChatWindow).GetField(name, InstancePrivate).SetValue(_window, value);

        private ChatTranscript UseTranscript()
        {
            _window._transcript = new ChatTranscript(new VisualElement(), ChatBlockRendererFactory.CreateDefault(null, null));
            return _window._transcript;
        }

        private static List<TranscriptEntry> Entries(ChatTranscript transcript) => TranscriptSerializer.Deserialize(transcript.SerializeForReload());

        [Test]
        public void LockReload_takes_the_guard_and_UnlockReload_gives_it_back()
        {
            Assert.That(ReloadGuard.IsLocked, Is.False, "precondition");
            Ports.LockReload();
            Assert.That(ReloadGuard.IsLocked, Is.True);
            Ports.UnlockReload();
            Assert.That(ReloadGuard.IsLocked, Is.False);
        }

        [Test]
        public void UndoStart_opens_the_group_of_the_label_and_UndoClose_commits_it()
        {
            var tracker = Field<TurnUndoTracker>("_undoTracker");
            SetField("_undoLabel", "move the camera");
            Ports.UndoStart();
            Assert.That(tracker.InflightGroupId, Is.GreaterThanOrEqualTo(0));
            Ports.UndoClose();
            Assert.That(tracker.InflightGroupId, Is.EqualTo(-1));
            Assert.That(tracker.TurnCount, Is.EqualTo(1));
        }

        [Test]
        public void CancelAsks_cancels_the_asks_of_the_turn_and_leaves_the_others()
        {
            PendingAskRegistry.Register("turn-ask");
            PendingAskRegistry.Register("other-ask");
            var turn = PendingAskRegistry.GetTcs("turn-ask");
            var other = PendingAskRegistry.GetTcs("other-ask");
            Field<HashSet<string>>("_turnAskIds").Add("turn-ask");
            Field<HashSet<string>>("_pendingAskRequestIds").Add("turn-ask");

            Ports.CancelAsks();

            Assert.That(turn.Task.IsCanceled, Is.True);
            Assert.That(other.Task.IsCompleted, Is.False, "an ask that the turn did not raise is not its to cancel");
            Assert.That(PendingAskRegistry.PendingCountForTests, Is.EqualTo(1));
            Assert.That(Field<HashSet<string>>("_turnAskIds"), Is.Empty);
            Assert.That(Field<HashSet<string>>("_pendingAskRequestIds"), Is.Empty);
        }

        [Test]
        public void NoteTimedOut_names_the_last_tool_of_the_turn()
        {
            var transcript = UseTranscript();
            _window._lastToolName = "Edit";
            Ports.NoteTimedOut();
            Ports.FinalizeAssistant();
            var entries = Entries(transcript);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].Text, Does.Contain("[Timed out: no response for").And.Contain("(last tool: Edit)]"));
        }

        [Test]
        public void ErrorChip_shows_the_text_of_the_event_that_ended_the_turn_or_a_default()
        {
            var transcript = UseTranscript();
            SetField("_exitEvent", ChatEvent.Error("connection lost"));
            Ports.ErrorChip();
            SetField("_exitEvent", default(ChatEvent));
            Ports.ErrorChip();
            var entries = Entries(transcript);
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(entries[0].Text, Is.EqualTo("connection lost"));
            Assert.That(entries[1].Text, Is.EqualTo("Error"));
        }

        [Test]
        public void ResetTurn_clears_every_per_turn_scratch_field()
        {
            _window._turnEditedCode = _window._turnHasToolCalls = _window._needsRefresh = _window._turnHadDelegation = true;
            _window._lastToolName = "Edit";
            _window._pendingAgentName = "reviewer";
            Ports.ResetTurn();
            Assert.That(_window._turnEditedCode || _window._turnHasToolCalls || _window._needsRefresh || _window._turnHadDelegation, Is.False);
            Assert.That(_window._lastToolName, Is.Null);
            Assert.That(_window._pendingAgentName, Is.Null);
        }

        [Test]
        public void RestartBackend_stops_the_old_backend_and_takes_a_new_one()
        {
            var fresh = new FakeBackend();
            WithBackendFactory(_ => fresh, () => Ports.RestartBackend());
            Assert.That(_backend.Stopped, Is.True);
            Assert.That(Field<IChatBackend>("_backend"), Is.SameAs(fresh));
        }

        [Test]
        public void A_port_that_throws_is_logged_and_does_not_escape()
        {
            LogAssert.Expect(LogType.Exception, new Regex("backend factory down"));
            WithBackendFactory(_ => throw new InvalidOperationException("backend factory down"),
                () => Assert.DoesNotThrow(() => Ports.RestartBackend()));
        }

        private sealed class FakeBackend : IChatBackend
        {
            public bool Stopped { get; private set; }
            public bool IsRunning => !Stopped;
            public string SessionId => null;
            public void Start() { }
            public void Stop() { Stopped = true; }
            public void SendTurn(string _) { }
            public void SendControlResponse(string _) { }
            public void DrainEvents(List<ChatEvent> _, List<ToolCallRecord> __ = null) { }
        }
    }
}
