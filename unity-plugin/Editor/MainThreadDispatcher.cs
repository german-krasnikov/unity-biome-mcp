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

        internal static void Enqueue(Action action) => _queue.Enqueue(action);

        internal static void Drain()
        {
            if (MCPServer._shuttingDown) return;
            while (_queue.TryDequeue(out var action))
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
        internal static void Clear()
        {
            while (_queue.TryDequeue(out _)) { }
        }
    }
}
