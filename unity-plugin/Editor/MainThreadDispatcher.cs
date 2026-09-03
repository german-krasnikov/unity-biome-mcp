using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEditor;

namespace UnityMCP.Editor
{
    // Marshals actions queued from ThreadPool (post-ConfigureAwait(false) continuations
    // in StartAsync / ClientConnectionHandler) back onto the Unity main thread. Drained
    // once per EditorApplication.update tick. Extracted from MCPServer (Phase 2, M1).
    //
    // Contract: Enqueue from any thread; the action runs on the next Editor update tick
    // on the main thread. Enqueue from inside a running action lands on the tick after
    // that one — Drain snapshots the queue count before it starts dequeuing, so it never
    // drains its own re-entrant additions in the same pass. A future second entry point
    // into Drain (e.g. a SynchronizationContext pump) needs a reentrancy guard — one
    // already exists below.
    [InitializeOnLoad]
    internal static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();
        private static bool _tickHooked;
        private static int _draining;

        static MainThreadDispatcher()
        {
            if (_tickHooked) return;
            _tickHooked = true;
            EditorApplication.update += Drain;
        }

        internal static void Enqueue(Action action) => Enqueue(_queue, action);

        internal static void Enqueue(ConcurrentQueue<Action> queue, Action action)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            if (action == null) throw new ArgumentNullException(nameof(action));
            queue.Enqueue(action);
        }

        internal static void Drain()
        {
            Drain(_queue, MCPServer._shuttingDown);
        }

        internal static void Drain(ConcurrentQueue<Action> queue, bool shuttingDown = false)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            if (shuttingDown) return;
            if (Interlocked.CompareExchange(ref _draining, 1, 0) != 0) return;
            try
            {
                int n = queue.Count;
                for (int i = 0; i < n && queue.TryDequeue(out var action); i++)
                {
                    try
                    {
                        action();
                    }
                    catch (Exception e)
                    {
                        UnityEngine.Debug.LogException(e);
                    }
                }
            }
            finally
            {
                Interlocked.Exchange(ref _draining, 0);
            }
        }

        // Discards pending actions without running them — used on teardown so
        // stale closures don't fire after the domain/connection is gone.
        internal static void Clear() => Clear(_queue);

        internal static void Clear(ConcurrentQueue<Action> queue)
        {
            if (queue == null) throw new ArgumentNullException(nameof(queue));
            while (queue.TryDequeue(out _)) { }
        }
    }
}
