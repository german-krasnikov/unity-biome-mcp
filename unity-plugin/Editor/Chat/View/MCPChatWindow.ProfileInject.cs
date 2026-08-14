// T22: Cross-assembly bridge subscriber — receives session ID from PerfWindow
// and inserts a profile chip into the open (or newly opened) chat window.
using UnityEditor;
using UnityMCP.Editor.Profiling;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class ProfileInjectConnector
    {
        static ProfileInjectConnector()
            => ProfileContextBridge.AnalyzeInChatRequested += OnRequest;

        private static void OnRequest(string sessionId)
        {
            var win = EditorWindow.GetWindow<MCPChatWindow>($"{BiomeLabel.DisplayName} Chat");
            win.Focus();
            win.InsertProfileChip(sessionId);
        }
    }

    public partial class MCPChatWindow
    {
        internal void InsertProfileChip(string sessionId)
        {
            var chip = new ChipData(ChipKindKeys.Profile, sessionId,
                $"Profile: {sessionId}", "");
            _chipField?.AddChip(chip);
            UpdateAutoHeight();
        }
    }
}
