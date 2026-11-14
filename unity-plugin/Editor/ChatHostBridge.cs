#if UNITY_INCLUDE_TESTS
using System;
#endif

namespace UnityMCP.Editor
{
    /// <summary>
    /// Registration holder for the single <see cref="IChatHostBridge"/> instance.
    /// Null until Chat.View's ChatHostBridgeImpl registers via [InitializeOnLoad].
    /// Every call site uses the null-conditional operator — no NullChatHostBridge
    /// object needed.
    /// </summary>
    internal static class ChatHostBridge
    {
        internal static IChatHostBridge Current { get; set; }

#if UNITY_INCLUDE_TESTS
        /// <summary>Preserve the current registration for a test scope.</summary>
        internal static IDisposable PreserveForTests() => new CurrentScope(Current);

        private sealed class CurrentScope : IDisposable
        {
            private IChatHostBridge _snapshot;
            private bool _disposed;

            internal CurrentScope(IChatHostBridge snapshot) => _snapshot = snapshot;

            public void Dispose()
            {
                if (_disposed) return;
                Current = _snapshot;
                _snapshot = null;
                _disposed = true;
            }
        }
#endif
    }
}
