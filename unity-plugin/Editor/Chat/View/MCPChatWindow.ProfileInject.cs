// T22: profile-chip insertion, called by ChatHostBridgeImpl.OnAnalyzeInChatRequested.
namespace UnityMCP.Editor.Chat
{
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
