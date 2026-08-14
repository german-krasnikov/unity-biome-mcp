// T23: MCPChatWindow partial — browse and restore conversation history.
using UnityMCP.Editor.Chat.CLI;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        // Called from OnSessionMenu.
        private void OnBrowseHistory()
        {
            ConversationHistoryPopup.Show(OnHistoryEntrySelected);
        }

        private void OnHistoryEntrySelected(BiomeConversationMeta meta)
        {
            var lines   = BiomeConversationStore.LoadEventLines(meta.Id);
            var entries = AgentEventReader.ReadEntries(lines);
            var serial  = TranscriptSerializer.Serialize(entries);

            bool canResume = !string.IsNullOrEmpty(meta.SessionId)
                          && meta.BackendKind == _selectedKind.ToString();

            if (canResume) ResetSession(meta.SessionId);
            else           NewSession();

            _transcript.RestoreFromReload(serial);
        }
    }
}
