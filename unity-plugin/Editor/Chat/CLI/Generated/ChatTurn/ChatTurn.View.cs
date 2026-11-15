// Generated from dsl/chat/chatturn.feature
// source-sha256=9996348d029ec77d8d29e79946e2a454180230ead038ce29eda06229b1eee1f7
// Edit the source, then run biome generate.

using System;

namespace UnityMCP.Editor.Chat.Turn
{
    public enum ChatTurnPhase { Idle, Sending, Receiving }
    public static class ChatTurnPhaseText
    {
        public static string Of(ChatTurnPhase value)
        {
            switch (value)
            {
                case ChatTurnPhase.Idle: return "idle";
                case ChatTurnPhase.Sending: return "sending";
                case ChatTurnPhase.Receiving: return "receiving";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }

    public enum ChatTurnExit { None, Done, Error, Dead, Timeout, Cancel, Abort, Reset, Disable }
    public static class ChatTurnExitText
    {
        public static string Of(ChatTurnExit value)
        {
            switch (value)
            {
                case ChatTurnExit.None: return "none";
                case ChatTurnExit.Done: return "done";
                case ChatTurnExit.Error: return "error";
                case ChatTurnExit.Dead: return "dead";
                case ChatTurnExit.Timeout: return "timeout";
                case ChatTurnExit.Cancel: return "cancel";
                case ChatTurnExit.Abort: return "abort";
                case ChatTurnExit.Reset: return "reset";
                case ChatTurnExit.Disable: return "disable";
                default: throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }

    public readonly struct ChatTurnViewModel
    {
        public readonly ChatTurnPhase Phase;
        public readonly int Asks;
        public ChatTurnViewModel(ChatTurnPhase phase, int asks) { Phase = phase; Asks = asks; }
    }

    public enum ChatTurnIntentType { Send, FirstToken, AskRaised, AskAnswered, TurnDone, Fail, DeadProcess, Timeout, Cancel, Abort, ResetSession, Disable }
    public readonly struct ChatTurnIntent
    {
        public readonly ChatTurnIntentType Type;
        public ChatTurnIntent(ChatTurnIntentType type) { Type = type; }
        public static ChatTurnIntent Send() => new ChatTurnIntent(ChatTurnIntentType.Send);
        public static ChatTurnIntent FirstToken() => new ChatTurnIntent(ChatTurnIntentType.FirstToken);
        public static ChatTurnIntent AskRaised() => new ChatTurnIntent(ChatTurnIntentType.AskRaised);
        public static ChatTurnIntent AskAnswered() => new ChatTurnIntent(ChatTurnIntentType.AskAnswered);
        public static ChatTurnIntent TurnDone() => new ChatTurnIntent(ChatTurnIntentType.TurnDone);
        public static ChatTurnIntent Fail() => new ChatTurnIntent(ChatTurnIntentType.Fail);
        public static ChatTurnIntent DeadProcess() => new ChatTurnIntent(ChatTurnIntentType.DeadProcess);
        public static ChatTurnIntent Timeout() => new ChatTurnIntent(ChatTurnIntentType.Timeout);
        public static ChatTurnIntent Cancel() => new ChatTurnIntent(ChatTurnIntentType.Cancel);
        public static ChatTurnIntent Abort() => new ChatTurnIntent(ChatTurnIntentType.Abort);
        public static ChatTurnIntent ResetSession() => new ChatTurnIntent(ChatTurnIntentType.ResetSession);
        public static ChatTurnIntent Disable() => new ChatTurnIntent(ChatTurnIntentType.Disable);
    }

    /// <summary>Render draws the model; Intent carries raw input. The hand-written View implements this and nothing else.</summary>
    public interface IChatTurnView
    {
        void Render(ChatTurnViewModel viewModel);
        event Action<ChatTurnIntent> Intent;
    }
}
