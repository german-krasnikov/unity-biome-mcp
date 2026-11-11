// Partial MCPChatWindow — public entry point for injecting pre-built prompts (F1 Error Resolver).
namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        /// <summary>
        /// Inject a pre-built prompt as a new chat turn.
        /// Used by ErrorResolverButton to dispatch grouped error fix requests.
        /// Guard: silently no-ops if CanSend is false (busy / no session).
        /// </summary>
        public void InjectMessage(string prompt)
        {
            if (!_turn.CanSend) return;
            DispatchTurn(UserTurnBuilder.Build(prompt), prompt);
        }
    }
}
