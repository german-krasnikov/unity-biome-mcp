using NUnit.Framework;

namespace UnityMCP.SourcePatchReadiness.Tests
{
    [TestFixture]
    internal sealed class SourcePatchReloadAckTests
    {
        [TestCase("wedged|epoch=20")]
        [TestCase("blocked|reason=source_patch_disable_not_owned")]
        [TestCase("sync_ack|epoch=99|will_compile=true")]
        [TestCase("requested")]
        public void NonMatchingAck_IsRejectedAfterExactlyOneDispatch(string reply)
        {
            Editor.SyncHelper.Calls = 0;
            Editor.SyncHelper.Reply = reply;
            Assert.Throws<InvalidOperationException>(() => new Editor.SyncHelperReloadPort().RequestReloadVerification(21));
            Assert.That(Editor.SyncHelper.Calls, Is.EqualTo(1));
        }

        [TestCase("true", Editor.ReloadPortOutcome.Accepted)]
        [TestCase("false", Editor.ReloadPortOutcome.AcceptedNoOp)]
        public void MatchingEpochAck_ReturnsExpectedOutcome(string willCompile, Editor.ReloadPortOutcome expected)
        {
            Editor.SyncHelper.Calls = 0;
            Editor.SyncHelper.Reply = "sync_ack|epoch=21|will_compile=" + willCompile;
            var outcome = new Editor.SyncHelperReloadPort().RequestReloadVerification(21);
            Assert.That(outcome, Is.EqualTo(expected));
            Assert.That(Editor.SyncHelper.Calls, Is.EqualTo(1));
        }

        [Test]
        public void RequestReloadVerification_UsesPassedEpoch_NotSelfComputed()
        {
            // Stub SyncHelper.CurrentEpoch below is fixed at 20 (so
            // CurrentEpoch+1 == 21). Passing an expectedEpochAfter that
            // differs from 21, with a reply that matches ONLY the passed
            // value, proves the port uses the caller-supplied epoch — not a
            // self-computed CurrentEpoch+1 (N2a.1 epoch ownership).
            //
            // Negative control: if the port reverted to reading
            // CurrentEpoch+1 internally, it would build the ACK prefix with
            // epoch=21, the reply below (epoch=25) would not match that
            // prefix, RequestReloadVerification would throw instead of
            // returning Accepted, and this test would go RED.
            Editor.SyncHelper.Calls = 0;
            Editor.SyncHelper.Reply = "sync_ack|epoch=25|will_compile=true";
            var outcome = new Editor.SyncHelperReloadPort().RequestReloadVerification(25);
            Assert.That(outcome, Is.EqualTo(Editor.ReloadPortOutcome.Accepted));
            Assert.That(Editor.SyncHelper.Calls, Is.EqualTo(1));
        }
    }
}

// External Unity/host effects only are substituted. The actual production
// SourcePatchReloadPort.cs is compiled into this pure test assembly.
namespace UnityEditor
{
    internal sealed class InitializeOnLoadAttribute : Attribute { }
}
namespace UnityMCP.Editor
{
    internal static class SourcePatchHost
    {
        internal static string ReloadBlockReason(bool explicitOwnedDisable) => null;
    }
    internal static class SyncHelper
    {
        internal static Func<bool, string> ReloadBlockReason;
        internal static int CurrentEpoch => 20;
        internal static int Calls;
        internal static string Reply;
        internal static string TriggerSync(bool resolve, bool explicitOwnedDisable)
        {
            Assert.That(resolve, Is.False);
            Assert.That(explicitOwnedDisable, Is.True);
            Calls++;
            return Reply;
        }
    }
}
