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
            Assert.Throws<InvalidOperationException>(() => new Editor.SyncHelperReloadPort().RequestReloadVerification());
            Assert.That(Editor.SyncHelper.Calls, Is.EqualTo(1));
        }

        [TestCase("true")]
        [TestCase("false")]
        public void MatchingEpochAck_IsAcceptedWithoutClaimingReloadFinished(string willCompile)
        {
            Editor.SyncHelper.Calls = 0;
            Editor.SyncHelper.Reply = "sync_ack|epoch=21|will_compile=" + willCompile;
            new Editor.SyncHelperReloadPort().RequestReloadVerification();
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
