// Generated from dsl/chat/chatturn.feature
// source-sha256=9996348d029ec77d8d29e79946e2a454180230ead038ce29eda06229b1eee1f7
// Edit the source, then run biome generate.

using System;

namespace UnityMCP.Editor.Chat.Turn
{
    public readonly struct ChatTurnCanSendInput
    {
        public readonly ChatTurnPhase Phase;
        public ChatTurnCanSendInput(ChatTurnPhase phase) { Phase = phase; }
    }

    public readonly struct ChatTurnActiveInput
    {
        public readonly ChatTurnPhase Phase;
        public ChatTurnActiveInput(ChatTurnPhase phase) { Phase = phase; }
    }

    public interface IChatTurnDecisions
    {
        bool CanSend(ChatTurnCanSendInput input);
        bool Active(ChatTurnActiveInput input);
    }

    public static class ChatTurnDecisionInputs
    {
        public static ChatTurnCanSendInput CanSend(ChatTurnState s) => new ChatTurnCanSendInput(s.Phase);
        public static ChatTurnActiveInput Active(ChatTurnState s) => new ChatTurnActiveInput(s.Phase);
    }

    public readonly struct ChatTurnUpdateResult
    {
        public readonly ChatTurnState State;
        public readonly ChatTurnEffect[] Effects;
        public ChatTurnUpdateResult(ChatTurnState state, ChatTurnEffect[] effects) { State = state; Effects = effects; }
    }

    public static class ChatTurnUpdate
    {
        public static ChatTurnViewModel View(ChatTurnState s, IChatTurnDecisions d) => new ChatTurnViewModel(s.Phase, s.Asks);

        /// <summary>Reactions fire on the edge old != v -> new == v, after the action's own effects.</summary>
        private static ChatTurnUpdateResult Step(ChatTurnState prev, ChatTurnState next, params ChatTurnEffect[] effects)
        {
            var all = new System.Collections.Generic.List<ChatTurnEffect>(effects);
            if (prev.Phase != ChatTurnPhase.Idle && next.Phase == ChatTurnPhase.Idle) all.Add(ChatTurnEffect.UnlockReload());
            if (prev.Phase != ChatTurnPhase.Idle && next.Phase == ChatTurnPhase.Idle) all.Add(ChatTurnEffect.CancelAsks());
            if (prev.Phase != ChatTurnPhase.Idle && next.Phase == ChatTurnPhase.Idle) all.Add(ChatTurnEffect.UndoClose());
            if (prev.Exit != ChatTurnExit.Done && next.Exit == ChatTurnExit.Done) all.Add(ChatTurnEffect.SyncAssets());
            if (prev.Exit != ChatTurnExit.Done && next.Exit == ChatTurnExit.Done) all.Add(ChatTurnEffect.RefreshResolver());
            if (prev.Exit != ChatTurnExit.Dead && next.Exit == ChatTurnExit.Dead) all.Add(ChatTurnEffect.NoteProcessExited());
            if (prev.Exit != ChatTurnExit.Timeout && next.Exit == ChatTurnExit.Timeout) all.Add(ChatTurnEffect.NoteTimedOut());
            if (prev.Phase != ChatTurnPhase.Idle && next.Phase == ChatTurnPhase.Idle) all.Add(ChatTurnEffect.FinalizeAssistant());
            if (prev.Exit != ChatTurnExit.Error && next.Exit == ChatTurnExit.Error) all.Add(ChatTurnEffect.ErrorChip());
            if (prev.Exit != ChatTurnExit.Done && next.Exit == ChatTurnExit.Done) all.Add(ChatTurnEffect.AfterAnswer());
            if (prev.Exit != ChatTurnExit.Done && next.Exit == ChatTurnExit.Done) all.Add(ChatTurnEffect.RestoreButton());
            if (prev.Exit != ChatTurnExit.Error && next.Exit == ChatTurnExit.Error) all.Add(ChatTurnEffect.RestoreButton());
            if (prev.Exit != ChatTurnExit.Done && next.Exit == ChatTurnExit.Done) all.Add(ChatTurnEffect.OfferApprove());
            if (prev.Exit != ChatTurnExit.Cancel && next.Exit == ChatTurnExit.Cancel) all.Add(ChatTurnEffect.RestartBackend());
            if (prev.Phase != ChatTurnPhase.Idle && next.Phase == ChatTurnPhase.Idle) all.Add(ChatTurnEffect.ResetTurn());
            return new ChatTurnUpdateResult(next, all.ToArray());
        }

        public static ChatTurnUpdateResult Apply(ChatTurnState s, ChatTurnIntent msg, IChatTurnDecisions d)
        {
            switch (msg.Type)
            {
                case ChatTurnIntentType.Send:
                {
                    if (!d.CanSend(ChatTurnDecisionInputs.CanSend(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Sending, ChatTurnExit.None, s.Asks);
                    return Step(s, next, ChatTurnEffect.LockReload(), ChatTurnEffect.UndoStart());
                }
                case ChatTurnIntentType.FirstToken:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Receiving, s.Exit, s.Asks);
                    return Step(s, next);
                }
                case ChatTurnIntentType.AskRaised:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(s.Phase, s.Exit, Math.Min(8, Math.Max(0, s.Asks + 1)));
                    return Step(s, next);
                }
                case ChatTurnIntentType.AskAnswered:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(s.Phase, s.Exit, Math.Min(8, Math.Max(0, s.Asks - 1)));
                    return Step(s, next);
                }
                case ChatTurnIntentType.TurnDone:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Done, 0);
                    return Step(s, next);
                }
                case ChatTurnIntentType.Fail:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Error, 0);
                    return Step(s, next);
                }
                case ChatTurnIntentType.DeadProcess:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Dead, 0);
                    return Step(s, next);
                }
                case ChatTurnIntentType.Timeout:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Timeout, 0);
                    return Step(s, next);
                }
                case ChatTurnIntentType.Cancel:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Cancel, 0);
                    return Step(s, next);
                }
                case ChatTurnIntentType.Abort:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Abort, 0);
                    return Step(s, next);
                }
                case ChatTurnIntentType.ResetSession:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Reset, 0);
                    return Step(s, next);
                }
                case ChatTurnIntentType.Disable:
                {
                    if (!d.Active(ChatTurnDecisionInputs.Active(s))) return new ChatTurnUpdateResult(s, new ChatTurnEffect[0]);
                    var next = new ChatTurnState(ChatTurnPhase.Idle, ChatTurnExit.Disable, 0);
                    return Step(s, next);
                }
                default: throw new ArgumentOutOfRangeException(nameof(msg));
            }
        }
    }
}
