// T19: Checkpoint command extension + checkpoint_undo_restore + client_hello projectId.
// EditMode unit tests — no TCP, no async.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CheckpointManagerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            CommandRegistry.Clear();
            CommandRegistry.InitDefaults();
            // Restore injectable after each test
            UndoGroupHelper.RevertToGroupAction = id => UnityEditor.Undo.RevertAllDownToGroup(id);
        }

        [TearDown]
        public void TearDown()
        {
            UndoGroupHelper.RevertToGroupAction = id => UnityEditor.Undo.RevertAllDownToGroup(id);
        }

        // ── checkpoint command ────────────────────────────────────────────────

        [Test]
        public void Checkpoint_ResponseIncludesGroupId()
        {
            var resp = CommandRegistry.Execute("checkpoint", "{\"label\":\"test\"}");
            StringAssert.Contains("group_id=", resp);
        }

        [Test]
        public void Checkpoint_ResponseIncludesDomainStamp()
        {
            var resp = CommandRegistry.Execute("checkpoint", "{\"label\":\"test\"}");
            StringAssert.Contains("domain_stamp=", resp);
        }

        // ── checkpoint_undo_restore ───────────────────────────────────────────

        [Test]
        public void CheckpointUndoRestore_ValidGroupAndStamp_CallsRevertAction()
        {
            int spiedGroupId = -1;
            UndoGroupHelper.RevertToGroupAction = id => { spiedGroupId = id; };

            // Use the actual current domain stamp (must match to pass the guard).
            var stamp = SyncHelper.CurrentDomainStamp;
            var args = $"{{\"group_id\":\"5\",\"domain_stamp\":\"{stamp}\"}}";
            var resp = CommandRegistry.Execute("checkpoint_undo_restore", args);

            Assert.That(resp, Is.EqualTo("ok"));
            Assert.That(spiedGroupId, Is.EqualTo(5));
        }

        [Test]
        public void CheckpointUndoRestore_StaleDomainStamp_ReturnsStaleDomain()
        {
            const string staleStamp = "stale-stamp-that-never-matches-12345";
            var args = $"{{\"group_id\":\"5\",\"domain_stamp\":\"{staleStamp}\"}}";
            var resp = CommandRegistry.Execute("checkpoint_undo_restore", args);
            Assert.That(resp, Is.EqualTo("stale_domain"));
        }

        [Test]
        public void CheckpointUndoRestore_InvalidGroupId_ReturnsError()
        {
            var stamp = SyncHelper.CurrentDomainStamp;
            var args = $"{{\"group_id\":\"-1\",\"domain_stamp\":\"{stamp}\"}}";
            var resp = CommandRegistry.Execute("checkpoint_undo_restore", args);
            Assert.That(resp, Is.EqualTo("err=invalid_group_id"));
        }

        // ── client_hello projectId ─────────────────────────────────────────

        [Test]
        public void ClientHello_ResponseIncludesProjectId()
        {
            // MCPServer._cachedProjectId may be "" in tests (StartAsync not called),
            // but the field must be present in the JSON response.
            var resp = ClientConnectionHandler.BuildClientHelloResponse(
                "msg1", "proto:3|plugin:1.0|stamp:abc", "/proj/path");
            StringAssert.Contains("projectId", resp);
        }
    }
}
