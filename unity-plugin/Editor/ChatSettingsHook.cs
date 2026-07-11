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
