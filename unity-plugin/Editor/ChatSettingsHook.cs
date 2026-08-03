using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Event hook that lets Chat inject a settings section into MCPSettingsUI
    /// WITHOUT reversing the dependency. Core fires it; Chat subscribes.
    /// Zero subscribers = no-op.
    /// </summary>
    public static class ChatSettingsHook
    {
        public static event Action<VisualElement> OnBuildConnection;

        internal static void InvokeConnection(VisualElement root) => OnBuildConnection?.Invoke(root);
        internal static bool HasConnectionSubscribers => OnBuildConnection != null;
        internal static void ResetConnectionEvent() => OnBuildConnection = null;

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Preserve the complete invocation list for a test scope. This keeps
        /// subscribers installed by Chat or third-party packages alive across tests.
        /// </summary>
        internal static IDisposable PreserveConnectionEventForTests()
            => new ConnectionEventScope(OnBuildConnection);

        private sealed class ConnectionEventScope : IDisposable
        {
            private Action<VisualElement> _snapshot;
            private bool _disposed;

            internal ConnectionEventScope(Action<VisualElement> snapshot) => _snapshot = snapshot;

            public void Dispose()
            {
                if (_disposed) return;
                OnBuildConnection = _snapshot;
                _snapshot = null;
                _disposed = true;
            }
        }
#endif

        internal static void InvokeConnectionViaReflection(VisualElement root)
        {
            try
            {
                var t = System.Type.GetType(
                    "UnityMCP.Editor.Chat.ChatSettingsSection, UnityMCP.Editor.Chat.View");
                if (t == null) return;
                var m = t.GetMethod("BuildContent",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(VisualElement) }, null);
                m?.Invoke(null, new object[] { root });
            }
            catch { }
        }

        public static bool IsChatBinaryAvailable()
        {
            try
            {
                var t = System.Type.GetType("UnityMCP.Editor.Chat.ChatBinaryResolver, UnityMCP.Editor.Chat");
                if (t == null) return false;
                var method = t.GetMethod("Resolve",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic,
                    null, new[] { typeof(bool) }, null);
                return method?.Invoke(null, new object[] { false }) as string != null;
            }
            catch { return false; }
        }

    }
}
