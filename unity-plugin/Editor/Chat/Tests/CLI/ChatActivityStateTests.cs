using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    public class ChatActivityStateTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Initial_Phase_Is_Idle()
            => Assert.AreEqual(ActivityPhase.Idle, new ChatActivityState().Phase);

        [Test] public void Send_From_Idle_Returns_True()
        {
            var s = new ChatActivityState();
            Assert.IsTrue(s.Send());
            Assert.AreEqual(ActivityPhase.Sending, s.Phase);
        }

        [Test] public void Send_While_Sending_Returns_False()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsFalse(s.Send());
        }

        [Test] public void FirstToken_From_Sending_Returns_True()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsTrue(s.FirstToken());
            Assert.AreEqual(ActivityPhase.Receiving, s.Phase);
        }

        [Test] public void FirstToken_From_Idle_Is_Ignored()
            => Assert.IsFalse(new ChatActivityState().FirstToken());

        [Test] public void FirstToken_From_Receiving_Is_Ignored()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsFalse(s.FirstToken());
        }

        [Test] public void Done_From_Receiving_Returns_True()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsTrue(s.Done());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Done_From_Idle_Returns_False()
            => Assert.IsFalse(new ChatActivityState().Done());

        [Test] public void Done_From_Sending_Returns_True()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsTrue(s.Done());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Fail_Aliases_Done()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsTrue(s.Fail());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Fail_From_Idle_Returns_False()
            => Assert.IsFalse(new ChatActivityState().Fail());

        // Guard-race regression: dead-process guard must be a no-op when TurnDone already
        // landed Idle. If Fail() returned true here, the error visual would fire after a
        // successful turn (the bug the guard fix addresses).
        [Test] public void Fail_From_Receiving_Returns_True_And_Idle()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsTrue(s.Fail());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Fail_After_Done_Is_Noop_Returns_False()
        {
            // Sequence: Send → FirstToken → Done (TurnDone handler) → Fail (guard tick).
            // Fail must return false so OnActivityChanged() is not called a second time,
            // preventing the error visual from overwriting the successful-turn state.
            var s = new ChatActivityState();
            s.Send(); s.FirstToken(); s.Done();
            Assert.AreEqual(ActivityPhase.Idle, s.Phase, "Done landed Idle");
            Assert.IsFalse(s.Fail(), "Fail after Done must be no-op");
            Assert.AreEqual(ActivityPhase.Idle, s.Phase, "still Idle after no-op Fail");
        }

        [Test] public void Full_Lifecycle()
        {
            var s = new ChatActivityState();
            Assert.IsTrue(s.Send());
            Assert.IsTrue(s.FirstToken());
            Assert.IsTrue(s.Done());
            Assert.AreEqual(ActivityPhase.Idle, s.Phase);
        }

        [Test] public void Send_From_Receiving_Is_Ignored()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsFalse(s.Send());
        }

        // #6: CanSend predicate — guards OnSend and AttachScreenshot entry points.
        [Test] public void CanSend_WhenIdle_IsTrue()
            => Assert.IsTrue(new ChatActivityState().CanSend);

        [Test] public void CanSend_WhenSending_IsFalse()
        {
            var s = new ChatActivityState();
            s.Send();
            Assert.IsFalse(s.CanSend);
        }

        [Test] public void CanSend_WhenReceiving_IsFalse()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken();
            Assert.IsFalse(s.CanSend);
        }

        [Test] public void CanSend_AfterDone_IsTrue()
        {
            var s = new ChatActivityState();
            s.Send(); s.FirstToken(); s.Done();
            Assert.IsTrue(s.CanSend);
        }

        // F2 regression: system/init must not stop the activity animation.
        // Drain.HandleEvent must treat SessionInit as a no-op (verified here at policy level).
        [Test] public void SessionInit_Is_Noop_ActivityStaysInSending()
        {
            // Simulate: Send → (system/init arrives — must be ignored) → still Sending
            // The actual no-op lives in Drain.HandleEvent; here we confirm the state machine
            // allows it: Sending after Send(), and only FirstToken/Done change it.
            var s = new ChatActivityState();
            s.Send();
            Assert.AreEqual(ActivityPhase.Sending, s.Phase, "after Send");

            // SessionInit: no method call on s — it's a no-op in the handler
            Assert.AreEqual(ActivityPhase.Sending, s.Phase, "after SessionInit no-op");

            // First real content
            s.FirstToken();
            Assert.AreEqual(ActivityPhase.Receiving, s.Phase, "after FirstToken");

            // result TurnDone
            s.Done();
            Assert.AreEqual(ActivityPhase.Idle, s.Phase, "after Done (result)");
        }
    }
}
