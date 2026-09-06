// TDD (PR-05 05.2): ask_user must fail fast when no interaction provider (Chat window) is
// subscribed to CommandRouter.OnAskUser, instead of creating a 300s-timeout pending entry
// that can never be answered (Finding 2). Zero `using UnityMCP.Editor.Chat;` by construction —
// this exercises Core-only command dispatch, not Chat internals.
using System.Threading.Tasks;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class CommandRouterAskUserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void Setup()
        {
            // EditMode tests never have an MCPChatWindow open, so OnAskUser has zero
            // subscribers here by construction (same ambient guarantee ChatBackendProbeTests
            // relies on) — the event exposes no reset seam outside its declaring class, nor
            // does this test need one.
            PendingAskRegistry.CancelAll();
        }

        [Test]
        public void AsyncAskUser_NoSubscriber_ReturnsImmediateError()
        {
            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync("{\"id\":\"au1\",\"cmd\":\"ask_user\",\"args\":{}}", tcs);

            Assert.IsTrue(tcs.Task.IsCompleted,
                "no subscriber must resolve synchronously — never wait on PendingAskRegistry");
            StringAssert.Contains("ask_user unavailable", tcs.Task.Result);
        }

        [Test]
        public void AsyncAskUser_NoSubscriber_LeavesNoPendingRegistryEntry()
        {
            var tcs = new TaskCompletionSource<string>();
            CommandRouter.ProcessAsync("{\"id\":\"au2\",\"cmd\":\"ask_user\",\"args\":{}}", tcs);

            Assert.AreEqual(0, PendingAskRegistry.PendingCountForTests,
                "a request nobody can ever answer must not create a dangling 300s-timeout entry");
        }
    }
}
