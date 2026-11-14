// Task 1.3: ReloadLifecycleCoordinator owns the single beforeAssemblyReload/
// afterAssemblyReload subscription pair for connection-layer sinks. Tested
// against fake IReloadLifecycleSink instances only — never against real
// MCPServer/SyncHelper statics (those get structural source-text guards
// instead, since invoking them for real would tear down the live listener).
// Every test below MUST call IsolateForTests() first: by the time this
// fixture runs, the real MCPServer/SyncHelper sinks are already registered,
// and SimulateBeforeReloadForTests()/SimulateAfterReloadForTests() dispatch
// to every registered sink — not isolating would also run the real
// production reload handlers for real (see ReloadLifecycleCoordinator.
// IsolateForTests doc comment).
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ReloadLifecycleCoordinatorTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private sealed class FakeSink : IReloadLifecycleSink
        {
            private readonly string _name;
            private readonly List<string> _order;
            public int Priority { get; }
            public bool ThrowOnBeforeReload;

            public FakeSink(string name, int priority, List<string> order)
            {
                _name = name;
                Priority = priority;
                _order = order;
            }

            public void OnBeforeReload()
            {
                if (ThrowOnBeforeReload) throw new InvalidOperationException("simulated");
                _order.Add(_name);
            }

            public void OnAfterReload() => _order.Add(_name);
        }

        [Test]
        public void Register_RunsSinksInPriorityOrder()
        {
            var order = new List<string>();
            var high = new FakeSink("high", priority: 10, order);
            var low = new FakeSink("low", priority: 0, order);

            using (ReloadLifecycleCoordinator.IsolateForTests())
            {
                ReloadLifecycleCoordinator.Register(high);
                ReloadLifecycleCoordinator.Register(low);
                ReloadLifecycleCoordinator.SimulateBeforeReloadForTests();

                CollectionAssert.AreEqual(new[] { "low", "high" }, order,
                    "Lower Priority must run before higher Priority");
            }
        }

        [Test]
        public void Register_SameInstanceTwice_IsIdempotent()
        {
            var order = new List<string>();
            var sink = new FakeSink("sink", 0, order);

            using (ReloadLifecycleCoordinator.IsolateForTests())
            {
                ReloadLifecycleCoordinator.Register(sink);
                ReloadLifecycleCoordinator.Register(sink);
                ReloadLifecycleCoordinator.SimulateBeforeReloadForTests();

                Assert.AreEqual(1, order.Count,
                    "Registering the same sink instance twice must not duplicate dispatch");
            }
        }

        [Test]
        public void RunBeforeReload_ThrowingSink_DoesNotBlockRemainingSinks()
        {
            var order = new List<string>();
            var throwing = new FakeSink("throwing", 0, order) { ThrowOnBeforeReload = true };
            var trailing = new FakeSink("trailing", 10, order);

            using (ReloadLifecycleCoordinator.IsolateForTests())
            {
                ReloadLifecycleCoordinator.Register(throwing);
                ReloadLifecycleCoordinator.Register(trailing);

                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("FakeSink.OnBeforeReload failed"));
                Assert.DoesNotThrow(
                    () => ReloadLifecycleCoordinator.SimulateBeforeReloadForTests(),
                    "A throwing sink must not propagate out of the coordinator");

                CollectionAssert.AreEqual(new[] { "trailing" }, order,
                    "A throwing sink must not block a later sink's dispatch");
            }
        }

        [Test]
        public void IsolateForTests_RestoresSinksAfterDispose()
        {
            var baseline = new List<IReloadLifecycleSink>(ReloadLifecycleCoordinator.SinksForTest);
            var order = new List<string>();
            var temp = new FakeSink("temp", 0, order);

            using (ReloadLifecycleCoordinator.IsolateForTests())
            {
                CollectionAssert.IsEmpty(ReloadLifecycleCoordinator.SinksForTest,
                    "IsolateForTests must clear real sinks for the scope's duration");
                ReloadLifecycleCoordinator.Register(temp);
                CollectionAssert.AreEqual(new IReloadLifecycleSink[] { temp },
                    ReloadLifecycleCoordinator.SinksForTest);
            }

            CollectionAssert.AreEqual(baseline, ReloadLifecycleCoordinator.SinksForTest,
                "Disposing the isolation scope must restore the exact pre-scope sink list");
        }
    }
}
