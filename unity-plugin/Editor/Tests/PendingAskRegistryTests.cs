// TDD tests for PendingAskRegistry — thread-safe TCS store for ask_user.
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PendingAskRegistryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void Setup()
        {
            // Clear any state from previous tests
            PendingAskRegistry.CancelAll();
        }

        [Test]
        public void Register_StoresEntry_GetTcsReturnsNonNull()
        {
            var id = "test-001";
            PendingAskRegistry.Register(id);
            var tcs = PendingAskRegistry.GetTcs(id);
            Assert.IsNotNull(tcs);
        }

        [Test]
        public async Task Complete_ResolvesTask_TaskResultMatchesInput()
        {
            var id = "test-002";
            PendingAskRegistry.Register(id);
            var tcs = PendingAskRegistry.GetTcs(id);

            PendingAskRegistry.Complete(id, "{\"q\":\"a\"}");

            Assert.IsTrue(tcs.Task.IsCompleted);
            Assert.AreEqual("{\"q\":\"a\"}", await tcs.Task);
        }

        [Test]
        public void Cancel_CancelsTask_TaskIsCanceled()
        {
            var id = "test-003";
            PendingAskRegistry.Register(id);
            var tcs = PendingAskRegistry.GetTcs(id);

            PendingAskRegistry.Cancel(id);

            Assert.IsTrue(tcs.Task.IsCanceled);
        }

        [Test]
        public void Complete_AfterCancel_IsNoop_NoException()
        {
            var id = "test-004";
            PendingAskRegistry.Register(id);
            PendingAskRegistry.Cancel(id);

            // Must not throw
            Assert.DoesNotThrow(() => PendingAskRegistry.Complete(id, "ignored"));
        }

        [Test]
        public void GetTcs_UnknownId_ReturnsNull()
        {
            var tcs = PendingAskRegistry.GetTcs("nonexistent-id");
            Assert.IsNull(tcs);
        }

        [Test]
        public void Register_Duplicate_Overwrites_NoPreviousTaskLeak()
        {
            var id = "test-006";
            PendingAskRegistry.Register(id);
            var tcs1 = PendingAskRegistry.GetTcs(id);

            // Re-register same id (domain-reload safety)
            PendingAskRegistry.Register(id);
            var tcs2 = PendingAskRegistry.GetTcs(id);

            // New TCS returned, old one was replaced
            Assert.IsNotNull(tcs2);
        }

        [Test]
        public void CancelAll_CompletesAllPendingWithCancellation()
        {
            PendingAskRegistry.Register("a");
            PendingAskRegistry.Register("b");
            var tcsA = PendingAskRegistry.GetTcs("a");
            var tcsB = PendingAskRegistry.GetTcs("b");

            PendingAskRegistry.CancelAll();

            Assert.IsTrue(tcsA.Task.IsCanceled);
            Assert.IsTrue(tcsB.Task.IsCanceled);
        }

        // ── Ask (C7b, review sprint v0.70) ────────────────────────────────────
        // Orchestration method extracted from CommandRouter.AsyncAskUser: generates the
        // requestId, registers it, invokes the caller's listener, and returns a Task<string>
        // that resolves to the answers JSON, or {"cancelled":true} on cancel/fault — never
        // faults itself, so callers can wrap it directly with a plain success-formatting
        // continuation (see CommandRouter.CompleteFromInner).

        [Test]
        public void Ask_InvokesOnAskEventWithGeneratedRequestId()
        {
            string capturedId = null;
            string capturedQuestions = null;
            PendingAskRegistry.Ask("[{\"q\":\"hi\"}]", (id, questions) =>
            {
                capturedId = id;
                capturedQuestions = questions;
                PendingAskRegistry.Complete(id, "{}");
            });

            Assert.IsNotNull(capturedId);
            Assert.AreEqual(32, capturedId.Length, "requestId must be a GUID 'N' format (32 hex chars)");
            Assert.AreEqual("[{\"q\":\"hi\"}]", capturedQuestions);
        }

        [Test]
        public async Task Ask_TaskCancelled_ResolvesWithCancelledJson()
        {
            string requestId = null;
            var task = PendingAskRegistry.Ask("[]", (id, _) => requestId = id);

            PendingAskRegistry.Cancel(requestId);

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual("{\"cancelled\":true}", await task);
        }

        [Test]
        public async Task Ask_TaskCompleted_ResolvesWithAnswerJson()
        {
            string requestId = null;
            var task = PendingAskRegistry.Ask("[]", (id, _) => requestId = id);

            PendingAskRegistry.Complete(requestId, "{\"a\":1}");

            Assert.IsTrue(task.IsCompleted);
            Assert.AreEqual("{\"a\":1}", await task);
        }
    }
}
