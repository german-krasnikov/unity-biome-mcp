// TDD (TICK-DISPATCHER Part A): MainThreadDispatcher now owns its own idempotent
// EditorApplication.update subscription ([InitializeOnLoad]) instead of MCPServer
// subscribing/unsubscribing Drain on its lifecycle. Drain also switched from an
// unbounded while(TryDequeue) loop to a snapshot-count pass, so an action enqueued
// from inside a running action lands on the NEXT Drain, never the current one — and
// gained a reentrancy guard for any future second entry point (e.g. a
// SynchronizationContext pump).
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class MainThreadDispatcherTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Drain_ActionEnqueuedFromInsideDrain_RunsOnNextDrainNotThisOne()
        {
            var queue = new ConcurrentQueue<System.Action>();
            bool innerRan = false;
            MainThreadDispatcher.Enqueue(queue, () =>
            {
                MainThreadDispatcher.Enqueue(queue, () => innerRan = true);
            });

            MainThreadDispatcher.Drain(queue);
            Assert.IsFalse(innerRan,
                "an action enqueued from inside a running action must not run in the same Drain pass");

            MainThreadDispatcher.Drain(queue);
            Assert.IsTrue(innerRan,
                "the re-entrantly enqueued action must run on the next Drain call");
        }

        [Test]
        public void Drain_ExceptionInOneAction_DoesNotSuppressNextQueuedAction()
        {
            var queue = new ConcurrentQueue<System.Action>();
            bool secondRan = false;
            MainThreadDispatcher.Enqueue(queue, () => throw new System.InvalidOperationException("boom"));
            MainThreadDispatcher.Enqueue(queue, () => secondRan = true);

            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException"));
            MainThreadDispatcher.Drain(queue);

            Assert.IsTrue(secondRan,
                "a throwing action must not suppress the next queued action in the same Drain pass");
        }

        [Test]
        public void Drain_ReentrantCallFromAction_IsNoOp()
        {
            // Both a no-op reentrant call and a naive unguarded recursive drain leave
            // siblingRan == true by the time everything settles — the outer loop picks
            // up whatever the reentrant call didn't. The only way to tell them apart is
            // to check whether the sibling had *already* run by the moment the reentrant
            // call itself returns, still inside the first action's stack frame.
            var queue = new ConcurrentQueue<System.Action>();
            bool siblingRan = false;
            bool siblingRanImmediatelyAfterReentrantCall = true; // starts wrong on purpose
            MainThreadDispatcher.Enqueue(queue, () =>
            {
                MainThreadDispatcher.Drain(queue); // reentrant — must be a no-op
                siblingRanImmediatelyAfterReentrantCall = siblingRan;
            });
            MainThreadDispatcher.Enqueue(queue, () => siblingRan = true);

            MainThreadDispatcher.Drain(queue);

            Assert.IsFalse(siblingRanImmediatelyAfterReentrantCall,
                "a reentrant Drain call must be a no-op — it must not process the sibling action " +
                "queued alongside the reentrant caller");
            Assert.IsTrue(siblingRan,
                "the outer Drain call must still process the sibling action once the no-op " +
                "reentrant call returns");
        }

        [Test]
        public async Task Enqueue_FromThreadPoolThread_RunsOnDrainingThread()
        {
            var queue = new ConcurrentQueue<System.Action>();
            int? actionThreadId = null;

            await Task.Run(() => MainThreadDispatcher.Enqueue(queue, () =>
            {
                actionThreadId = Thread.CurrentThread.ManagedThreadId;
            }));

            MainThreadDispatcher.Drain(queue);

            Assert.AreEqual(Thread.CurrentThread.ManagedThreadId, actionThreadId,
                "an action enqueued from a ThreadPool thread must run on whichever thread calls Drain");
        }

        [Test]
        public void MainThreadDispatcher_IsHookedToEditorUpdate_IndependentlyOfMcpServer()
        {
            var hooked = (EditorApplication.update?.GetInvocationList() ?? System.Array.Empty<System.Delegate>())
                .Any(d => d.Target == null
                    && d.Method.DeclaringType == typeof(MainThreadDispatcher)
                    && d.Method.Name == "Drain"
                    && d.Method.GetParameters().Length == 0);

            Assert.IsTrue(hooked,
                "MainThreadDispatcher must subscribe its own Drain to EditorApplication.update via its " +
                "own [InitializeOnLoad] static constructor, independently of MCPServer's lifecycle");
        }
    }
}
