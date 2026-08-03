using System;
using System.Collections.Concurrent;

namespace UnityMCP.Editor
{
    // Marshals actions queued from ThreadPool (post-ConfigureAwait(false) continuations
    // in StartAsync / ClientConnectionHandler) back onto the Unity main thread. Drained
    // once per EditorApplication.update tick. Extracted from MCPServer (Phase 2, M1).
    internal static class MainThreadDispatcher
    {
        private static readonly ConcurrentQueue<Action> _queue = new ConcurrentQueue<Action>();

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
            while (queue.TryDequeue(out var action))
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
