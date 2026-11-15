// Generated from dsl/chat/chatturn.feature
// source-sha256=9996348d029ec77d8d29e79946e2a454180230ead038ce29eda06229b1eee1f7
// Edit the source, then run biome generate.

using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityMCP.Editor.Chat.Turn;

namespace UnityMCP.Editor.Chat.Turn.Examples
{
    public sealed class ChatTurnFakeView : IChatTurnView
    {
        public readonly List<ChatTurnViewModel> Renders = new List<ChatTurnViewModel>();
        public Action<ChatTurnViewModel> OnRender;
        private readonly List<Action<ChatTurnIntent>> _recorded = new List<Action<ChatTurnIntent>>();
        private readonly List<Action<ChatTurnIntent>> _active = new List<Action<ChatTurnIntent>>();

        public int Active => _active.Count;
        public ChatTurnViewModel Last => Renders[Renders.Count - 1];
        public event Action<ChatTurnIntent> Intent
        {
            add { _recorded.Add(value); _active.Add(value); }
            remove { _active.Remove(value); }
        }

        public void Render(ChatTurnViewModel viewModel) { OnRender?.Invoke(viewModel); Renders.Add(viewModel); }
        public void Send(ChatTurnIntent intent) { foreach (var h in _active.ToArray()) h(intent); }
        /// <summary>Hostile late delivery: through a handler recorded earlier, even after it was unsubscribed.</summary>
        public void SendStale(ChatTurnIntent intent) { foreach (var h in _recorded.ToArray()) h(intent); }
    }

    public sealed class ChatTurnFakePorts : IChatTurnPorts
    {
        public readonly List<string> Calls = new List<string>();
        public void LockReload() => Calls.Add("lockReload");
        public void UndoStart() => Calls.Add("undoStart");
        public void UnlockReload() => Calls.Add("unlockReload");
        public void CancelAsks() => Calls.Add("cancelAsks");
        public void UndoClose() => Calls.Add("undoClose");
        public void SyncAssets() => Calls.Add("syncAssets");
        public void RefreshResolver() => Calls.Add("refreshResolver");
        public void NoteProcessExited() => Calls.Add("noteProcessExited");
        public void NoteTimedOut() => Calls.Add("noteTimedOut");
        public void FinalizeAssistant() => Calls.Add("finalizeAssistant");
        public void ErrorChip() => Calls.Add("errorChip");
        public void AfterAnswer() => Calls.Add("afterAnswer");
        public void RestoreButton() => Calls.Add("restoreButton");
        public void OfferApprove() => Calls.Add("offerApprove");
        public void RestartBackend() => Calls.Add("restartBackend");
        public void ResetTurn() => Calls.Add("resetTurn");
    }

    [TestFixture]
    public sealed class ChatTurnExamplesTests
    {
        [Test]
        public void mounts_idle()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[0]));
            Assert.That(view.Renders.Count, Is.EqualTo(1));
        }

        [Test]
        public void send_takes_the_lock_and_the_undo_group_once()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.Send());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Sending));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart" }));
        }

        [Test]
        public void done_leaves_in_order()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.FirstToken());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.TurnDone());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "syncAssets", "refreshResolver", "finalizeAssistant", "afterAnswer", "restoreButton", "offerApprove", "resetTurn" }));
        }

        [Test]
        public void error_finalizes_before_the_error_chip()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.Fail());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "finalizeAssistant", "errorChip", "restoreButton", "resetTurn" }));
        }

        [Test]
        public void dead_process_notes_before_finalize()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.DeadProcess());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "noteProcessExited", "finalizeAssistant", "resetTurn" }));
        }

        [Test]
        public void timeout_notes_before_the_scratch_is_reset()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.Timeout());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "noteTimedOut", "finalizeAssistant", "resetTurn" }));
        }

        [Test]
        public void cancel_leaves_and_restarts_the_backend()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.Cancel());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "finalizeAssistant", "restartBackend", "resetTurn" }));
        }

        [Test]
        public void abort_leaves_the_backend_to_the_caller()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.Abort());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "finalizeAssistant", "resetTurn" }));
        }

        [Test]
        public void reset_mid_turn_closes_what_it_then_forgets()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.ResetSession());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "finalizeAssistant", "resetTurn" }));
        }

        [Test]
        public void disable_mid_turn_releases_everything()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.Disable());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "finalizeAssistant", "resetTurn" }));
        }

        [Test]
        public void every_intent_at_idle_is_inert()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.FirstToken());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.AskAnswered());
            view.Send(ChatTurnIntent.TurnDone());
            view.Send(ChatTurnIntent.Fail());
            view.Send(ChatTurnIntent.DeadProcess());
            view.Send(ChatTurnIntent.Timeout());
            view.Send(ChatTurnIntent.Cancel());
            view.Send(ChatTurnIntent.Abort());
            view.Send(ChatTurnIntent.ResetSession());
            view.Send(ChatTurnIntent.Disable());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
            Assert.That(ports.Calls, Is.EqualTo(new string[0]));
            Assert.That(view.Renders.Count, Is.EqualTo(1));
        }

        [Test]
        public void a_second_exit_is_inert()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.Cancel());
            view.Send(ChatTurnIntent.Cancel());
            view.Send(ChatTurnIntent.TurnDone());
            view.Send(ChatTurnIntent.Fail());
            view.Send(ChatTurnIntent.Timeout());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "finalizeAssistant", "restartBackend", "resetTurn" }));
        }

        [Test]
        public void a_stale_token_after_an_exit_is_inert()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.Cancel());
            view.Send(ChatTurnIntent.FirstToken());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
        }

        [Test]
        public void a_stale_answer_after_an_exit_is_inert()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.Cancel());
            view.Send(ChatTurnIntent.AskAnswered());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Idle));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
        }

        [Test]
        public void overlapping_asks_keep_the_turn_asking()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.AskAnswered());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Sending));
            Assert.That(view.Last.Asks, Is.EqualTo(1));
        }

        [Test]
        public void the_next_turn_starts_clean_after_an_exit_with_an_ask()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.Cancel());
            view.Send(ChatTurnIntent.Send());
            Assert.That(view.Last.Phase, Is.EqualTo(ChatTurnPhase.Sending));
            Assert.That(view.Last.Asks, Is.EqualTo(0));
        }

        [Test]
        public void the_next_turn_leaves_in_order_again()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.TurnDone());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.TurnDone());
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "syncAssets", "refreshResolver", "finalizeAssistant", "afterAnswer", "restoreButton", "offerApprove", "resetTurn", "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "syncAssets", "refreshResolver", "finalizeAssistant", "afterAnswer", "restoreButton", "offerApprove", "resetTurn" }));
        }

        [Test]
        public void the_window_redraws_only_when_phase_or_asks_change()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            view.Send(ChatTurnIntent.FirstToken());
            view.Send(ChatTurnIntent.AskRaised());
            view.Send(ChatTurnIntent.AskAnswered());
            view.Send(ChatTurnIntent.TurnDone());
            Assert.That(view.Renders.Count, Is.EqualTo(6));
        }

        [Test]
        public void intents_after_dispose_are_inert()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            view.Send(ChatTurnIntent.Send());
            handle.Dispose();
            view.SendStale(ChatTurnIntent.TurnDone());
            Assert.That(view.Active, Is.EqualTo(0));
            Assert.That(view.Renders.Count, Is.EqualTo(2));
            Assert.That(ports.Calls, Is.EqualTo(new string[] { "lockReload", "undoStart" }));
        }

        [Test]
        public void Mount_RollsBackWhenTheFirstRenderThrows()
        {
            var view = new ChatTurnFakeView { OnRender = _ => throw new InvalidOperationException("render failed") };
            // try/catch, not Assert.Throws: the delegate type differs between Unity's NUnit 3.5 and NUnit 4.
            var threw = false;
            try { ChatTurnFeature.Mount(view, new ChatTurnFakePorts(), new ChatTurnLogicDecisions()); }
            catch (InvalidOperationException) { threw = true; }
            Assert.That(threw, Is.True, "the failed first render must propagate");
            Assert.That(view.Active, Is.EqualTo(0));
        }

        [Test]
        public void Dispose_IsIdempotent_AndStaleHandlersOfEveryActionAreInert()
        {
            var view = new ChatTurnFakeView();
            var ports = new ChatTurnFakePorts();
            var handle = ChatTurnFeature.Mount(view, ports, new ChatTurnLogicDecisions());
            handle.Dispose();
            handle.Dispose();
            view.SendStale(ChatTurnIntent.Send());
            view.SendStale(ChatTurnIntent.FirstToken());
            view.SendStale(ChatTurnIntent.AskRaised());
            view.SendStale(ChatTurnIntent.AskAnswered());
            view.SendStale(ChatTurnIntent.TurnDone());
            view.SendStale(ChatTurnIntent.Fail());
            view.SendStale(ChatTurnIntent.DeadProcess());
            view.SendStale(ChatTurnIntent.Timeout());
            view.SendStale(ChatTurnIntent.Cancel());
            view.SendStale(ChatTurnIntent.Abort());
            view.SendStale(ChatTurnIntent.ResetSession());
            view.SendStale(ChatTurnIntent.Disable());
            Assert.That(new object[] { view.Active, view.Renders.Count, ports.Calls.Count }, Is.EqualTo(new object[] { 0, 1, 0 }));
        }
    }
}
