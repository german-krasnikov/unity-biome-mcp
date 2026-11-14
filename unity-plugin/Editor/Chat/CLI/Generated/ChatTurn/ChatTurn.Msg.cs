// Generated from dsl/chat/chatturn.feature
// source-sha256=9996348d029ec77d8d29e79946e2a454180230ead038ce29eda06229b1eee1f7
// Edit the source, then run biome generate.

using System;

namespace UnityMCP.Editor.Chat.Turn
{
    /// <summary>A message is the View's intent: <see cref="ChatTurnIntent"/>.</summary>
    /// <summary>Effects are data: Update only describes them, the host runs them.</summary>
    public enum ChatTurnEffectKind { LockReload, UndoStart, UnlockReload, CancelAsks, UndoClose, SyncAssets, RefreshResolver, NoteProcessExited, NoteTimedOut, FinalizeAssistant, ErrorChip, AfterAnswer, RestoreButton, OfferApprove, RestartBackend, ResetTurn }

    public readonly struct ChatTurnEffect
    {
        public readonly ChatTurnEffectKind Kind;
        public ChatTurnEffect(ChatTurnEffectKind kind) { Kind = kind; }
        public static ChatTurnEffect LockReload() => new ChatTurnEffect(ChatTurnEffectKind.LockReload);
        public static ChatTurnEffect UndoStart() => new ChatTurnEffect(ChatTurnEffectKind.UndoStart);
        public static ChatTurnEffect UnlockReload() => new ChatTurnEffect(ChatTurnEffectKind.UnlockReload);
        public static ChatTurnEffect CancelAsks() => new ChatTurnEffect(ChatTurnEffectKind.CancelAsks);
        public static ChatTurnEffect UndoClose() => new ChatTurnEffect(ChatTurnEffectKind.UndoClose);
        public static ChatTurnEffect SyncAssets() => new ChatTurnEffect(ChatTurnEffectKind.SyncAssets);
        public static ChatTurnEffect RefreshResolver() => new ChatTurnEffect(ChatTurnEffectKind.RefreshResolver);
        public static ChatTurnEffect NoteProcessExited() => new ChatTurnEffect(ChatTurnEffectKind.NoteProcessExited);
        public static ChatTurnEffect NoteTimedOut() => new ChatTurnEffect(ChatTurnEffectKind.NoteTimedOut);
        public static ChatTurnEffect FinalizeAssistant() => new ChatTurnEffect(ChatTurnEffectKind.FinalizeAssistant);
        public static ChatTurnEffect ErrorChip() => new ChatTurnEffect(ChatTurnEffectKind.ErrorChip);
        public static ChatTurnEffect AfterAnswer() => new ChatTurnEffect(ChatTurnEffectKind.AfterAnswer);
        public static ChatTurnEffect RestoreButton() => new ChatTurnEffect(ChatTurnEffectKind.RestoreButton);
        public static ChatTurnEffect OfferApprove() => new ChatTurnEffect(ChatTurnEffectKind.OfferApprove);
        public static ChatTurnEffect RestartBackend() => new ChatTurnEffect(ChatTurnEffectKind.RestartBackend);
        public static ChatTurnEffect ResetTurn() => new ChatTurnEffect(ChatTurnEffectKind.ResetTurn);
    }
}
