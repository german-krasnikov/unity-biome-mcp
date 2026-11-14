using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    // Owns the single AssemblyReloadEvents.beforeAssemblyReload/afterAssemblyReload
    // subscription pair for connection-layer sinks (Task 1.3). Dispatches in
    // Priority order and isolates a throwing sink so it cannot block the rest.
    internal static class ReloadLifecycleCoordinator
    {
        private static readonly List<IReloadLifecycleSink> _sinks = new();
        private static bool _subscribed;

        internal static void Register(IReloadLifecycleSink sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            if (!_subscribed)
            {
                AssemblyReloadEvents.beforeAssemblyReload += RunBeforeReload;
                AssemblyReloadEvents.afterAssemblyReload  += RunAfterReload;
                _subscribed = true;
            }
            if (!_sinks.Contains(sink)) _sinks.Add(sink);
        }

        private static IEnumerable<IReloadLifecycleSink> Ordered() =>
            _sinks.OrderBy(s => s.Priority);  // stable sort — ties keep registration order

        private static void RunBeforeReload() => RunEach(s => s.OnBeforeReload(), "OnBeforeReload");
        private static void RunAfterReload()  => RunEach(s => s.OnAfterReload(),  "OnAfterReload");

        private static void RunEach(Action<IReloadLifecycleSink> step, string phase)
        {
            foreach (var sink in Ordered())
            {
                try { step(sink); }
                catch (Exception e)
                {
                    Debug.LogError($"{BiomeLabel.Tag} ReloadLifecycleCoordinator: " +
                        $"{sink.GetType().Name}.{phase} failed: {e.Message}");
                }
            }
        }

#if UNITY_INCLUDE_TESTS
        internal static IReadOnlyList<IReloadLifecycleSink> SinksForTest => _sinks;
        internal static void SimulateBeforeReloadForTests() => RunBeforeReload();
        internal static void SimulateAfterReloadForTests()  => RunAfterReload();

        /// <summary>
        /// Clears the registered-sink list for a test scope and restores the exact
        /// prior list (including the real MCPServer/SyncHelper sinks) on Dispose.
        /// Required, not optional: once production code registers real sinks,
        /// SimulateBeforeReloadForTests()/SimulateAfterReloadForTests() dispatch to
        /// every registered sink — a fake-sink test that does not isolate first
        /// would also invoke the real MCPServer.OnBeforeReload()/SyncHelper.
        /// OnAfterReload() for real, which tears down the live TCP listener
        /// (TeardownCore unsubscribes WatchdogTick too, so nothing self-heals it).
        /// Never touches _subscribed — the real AssemblyReloadEvents subscription
        /// is made at most once per process and must never be torn down or
        /// re-added by tests (same lifetime contract as
        /// MCPServer._lifecycleCallbacksRegistered).
        /// </summary>
        internal static IDisposable IsolateForTests()
        {
            var snapshot = new List<IReloadLifecycleSink>(_sinks);
            _sinks.Clear();
            return new RestoreScope(snapshot);
        }

        private sealed class RestoreScope : IDisposable
        {
            private List<IReloadLifecycleSink> _snapshot;

            internal RestoreScope(List<IReloadLifecycleSink> snapshot) => _snapshot = snapshot;

            public void Dispose()
            {
                if (_snapshot == null) return;
                _sinks.Clear();
                _sinks.AddRange(_snapshot);
                _snapshot = null;
            }
        }
#endif
    }
}
