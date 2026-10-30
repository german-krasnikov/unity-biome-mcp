using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityMCP.Editor.Chat.Turn.Examples;

namespace UnityMCP.Editor.Chat.Turn.Tests
{
    // The reducer is covered by the generated ChatTurn.Examples.cs; this is the hand-written door around it. The fake ports are the generated ones.
    [TestFixture]
    public class ChatTurnDriverTests
    {
        private static readonly Action<Exception> Ignore = _ => { };

        [Test]
        public void The_view_follows_the_phase_and_the_asks()
        {
            var d = new ChatTurnDriver(new ChatTurnFakePorts(), null, Ignore);
            Assert.That(d.CanSend, Is.True);
            d.Dispatch(ChatTurnIntent.Send());
            d.Dispatch(ChatTurnIntent.AskRaised());
            Assert.That(d.Phase, Is.EqualTo(ChatTurnPhase.Sending));
            Assert.That(d.CanSend, Is.False);
            Assert.That(d.Asking, Is.True);
            d.Dispatch(ChatTurnIntent.Cancel());
            Assert.That(d.CanSend, Is.True);
            Assert.That(d.Asking, Is.False);
        }

        [Test]
        public void A_dispatch_made_while_an_exit_runs_waits_for_it_to_finish()
        {
            var ports = new ChatTurnFakePorts();
            ChatTurnDriver d = null;
            var nest = false;
            d = new ChatTurnDriver(ports, vm => { if (nest && vm.Phase == ChatTurnPhase.Idle) { nest = false; d.Dispatch(ChatTurnIntent.Send()); } }, Ignore);
            d.Dispatch(ChatTurnIntent.Send());
            nest = true;
            d.Dispatch(ChatTurnIntent.Cancel());   // the redraw of the exit asks for a new turn
            Assert.That(ports.Calls, Is.EqualTo(new[] { "lockReload", "undoStart", "unlockReload", "cancelAsks", "undoClose", "finalizeAssistant", "restartBackend", "resetTurn", "lockReload", "undoStart" }));
            Assert.That(d.Phase, Is.EqualTo(ChatTurnPhase.Sending));
        }

        [Test]
        public void The_redraw_sees_every_change_and_nothing_else()
        {
            var seen = new List<ChatTurnPhase>();
            var d = new ChatTurnDriver(new ChatTurnFakePorts(), vm => seen.Add(vm.Phase), Ignore);
            d.Dispatch(ChatTurnIntent.Send());
            d.Dispatch(ChatTurnIntent.Send());
            d.Dispatch(ChatTurnIntent.TurnDone());
            Assert.That(seen, Is.EqualTo(new[] { ChatTurnPhase.Idle, ChatTurnPhase.Sending, ChatTurnPhase.Idle }));
        }

        [Test]
        public void After_dispose_an_intent_is_inert()
        {
            var ports = new ChatTurnFakePorts();
            var d = new ChatTurnDriver(ports, null, Ignore);
            d.Dispatch(ChatTurnIntent.Send());
            d.Dispose();
            d.Dispatch(ChatTurnIntent.TurnDone());   // hangs when the queue is drained inside `Intent?.Invoke(...)`
            Assert.That(ports.Calls, Is.EqualTo(new[] { "lockReload", "undoStart" }));
        }

        [Test]
        public void A_failing_redraw_is_reported_and_does_not_skip_the_exit()
        {
            var ports = new ChatTurnFakePorts();
            var errors = new List<Exception>();
            var armed = false;
            var d = new ChatTurnDriver(ports, vm => { if (armed && vm.Phase == ChatTurnPhase.Idle) throw new InvalidOperationException("boom"); }, errors.Add);
            d.Dispatch(ChatTurnIntent.Send());
            armed = true;
            d.Dispatch(ChatTurnIntent.TurnDone());
            Assert.That(errors, Has.Count.EqualTo(1));
            Assert.That(ports.Calls[ports.Calls.Count - 1], Is.EqualTo("resetTurn"));   // the effects ran to the end
        }

        [Test]
        public void A_throwing_handler_leaves_the_driver_usable_and_drops_what_waited_behind_it()
        {
            var ports = new ChatTurnFakePorts();
            var d = new ChatTurnDriver(ports, null, Ignore);
            Action<ChatTurnIntent> boom = _ => { d.Dispatch(ChatTurnIntent.TurnDone()); throw new InvalidOperationException("boom"); };
            d.Intent += boom;
            Assert.That(() => d.Dispatch(ChatTurnIntent.Send()), Throws.TypeOf<InvalidOperationException>());
            d.Intent -= boom;
            d.Dispatch(ChatTurnIntent.AskRaised());   // the door works again ...
            Assert.That(d.Asking, Is.True);
            Assert.That(ports.Calls, Is.EqualTo(new[] { "lockReload", "undoStart" }));   // ... and the TurnDone that waited behind the failure never ran
        }

        [Test]
        public void A_missing_error_sink_is_refused()
            => Assert.That(() => new ChatTurnDriver(new ChatTurnFakePorts(), null, null), Throws.TypeOf<ArgumentNullException>());
    }
}
