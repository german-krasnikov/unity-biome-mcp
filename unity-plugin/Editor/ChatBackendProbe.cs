// Owned status probe: reports whether a Chat backend turn is running without a compile-time
// dependency from Core -> Chat. Chat.View wires RunningProvider via [InitializeOnLoad]
// (ChatBackendStatusPlugin) on every domain reload. Absent Chat.View => RunningProvider stays
// null => false, same as before this PR replaced the (permanently broken) reflection lookup.
using System;

namespace UnityMCP.Editor
{
    internal static class ChatBackendProbe
    {
        internal static Func<bool> RunningProvider { get; set; }

        internal static bool IsChatBackendRunning()
        {
            try { return RunningProvider?.Invoke() ?? false; }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[Biome] ChatBackendProbe: {ex.GetType().Name}");
                return false;
            }
        }
    }
}
