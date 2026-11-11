// Generated from dsl/chat/chatturn.feature
// source-sha256=9996348d029ec77d8d29e79946e2a454180230ead038ce29eda06229b1eee1f7
// Edit the source, then run biome generate.

using System;

namespace UnityMCP.Editor.Chat.Turn
{
    public readonly struct ChatTurnState: IEquatable<ChatTurnState>
    {
        public readonly ChatTurnPhase Phase;
        public readonly ChatTurnExit Exit;
        public readonly int Asks;
        public ChatTurnState(ChatTurnPhase phase, ChatTurnExit exit, int asks) { Phase = phase; Exit = exit; Asks = asks; }
        public bool Equals(ChatTurnState other) => Phase == other.Phase && Exit == other.Exit && Asks == other.Asks;
    }

    public static class ChatTurnInitial
    {
        public static readonly ChatTurnState Value = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.None, 0);
    }
}
