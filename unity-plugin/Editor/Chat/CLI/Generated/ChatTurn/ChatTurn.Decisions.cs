// Generated from dsl/chat/chatturn.feature
// source-sha256=9996348d029ec77d8d29e79946e2a454180230ead038ce29eda06229b1eee1f7
// Edit the source, then run biome generate.

using System;

namespace UnityMCP.Editor.Chat.Turn
{
    public sealed class ChatTurnLogicDecisions : IChatTurnDecisions
    {
        public bool CanSend(ChatTurnCanSendInput input) => UnityMCP.Editor.Chat.Turn.Policy.CanSend(new UnityMCP.Editor.Chat.Turn.Policy.CanSendInput(ChatTurnPhase: ChatTurnPhaseText.Of(input.Phase)));
        public bool Active(ChatTurnActiveInput input) => UnityMCP.Editor.Chat.Turn.Policy.Active(new UnityMCP.Editor.Chat.Turn.Policy.ActiveInput(ChatTurnPhase: ChatTurnPhaseText.Of(input.Phase)));
    }
}
