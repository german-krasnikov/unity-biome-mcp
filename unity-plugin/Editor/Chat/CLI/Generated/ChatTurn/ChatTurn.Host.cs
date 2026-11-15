// Generated from dsl/chat/chatturn.feature
// source-sha256=9996348d029ec77d8d29e79946e2a454180230ead038ce29eda06229b1eee1f7
// Edit the source, then run biome generate.

using System;

namespace UnityMCP.Editor.Chat.Turn
{
    public interface IChatTurnPorts
    {
        void LockReload();
        void UndoStart();
        void UnlockReload();
        void CancelAsks();
        void UndoClose();
        void SyncAssets();
        void RefreshResolver();
        void NoteProcessExited();
        void NoteTimedOut();
        void FinalizeAssistant();
        void ErrorChip();
        void AfterAnswer();
        void RestoreButton();
        void OfferApprove();
        void RestartBackend();
        void ResetTurn();
    }

    /// <summary>Owns the current state and the subscription; runs the effects Update returns, in order, after rendering.</summary>
    public sealed class ChatTurnHost : IDisposable
    {
        private ChatTurnState _state = ChatTurnInitial.Value;
        private readonly IChatTurnView _view;
        private readonly IChatTurnPorts _ports;
        private readonly IChatTurnDecisions _decisions;
        private bool _disposed;

        public ChatTurnHost(IChatTurnView view, IChatTurnPorts ports, IChatTurnDecisions decisions)
        {
            _view = view;
            _ports = ports;
            _decisions = decisions;
            try
            {
                _view.Intent += OnIntent;
                Render();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _view.Intent -= OnIntent;
        }

        private void Render() => _view.Render(ChatTurnUpdate.View(_state, _decisions));

        private void OnIntent(ChatTurnIntent msg)
        {
            if (_disposed) return;
            var result = ChatTurnUpdate.Apply(_state, msg, _decisions);
            var changed = !result.State.Equals(_state);
            _state = result.State;
            if (changed) Render();
            foreach (var e in result.Effects) Run(e);
        }

        private void Run(ChatTurnEffect e)
        {
            if (_disposed) return;
            switch (e.Kind)
            {
                    case ChatTurnEffectKind.LockReload: _ports.LockReload(); break;
                    case ChatTurnEffectKind.UndoStart: _ports.UndoStart(); break;
                    case ChatTurnEffectKind.UnlockReload: _ports.UnlockReload(); break;
                    case ChatTurnEffectKind.CancelAsks: _ports.CancelAsks(); break;
                    case ChatTurnEffectKind.UndoClose: _ports.UndoClose(); break;
                    case ChatTurnEffectKind.SyncAssets: _ports.SyncAssets(); break;
                    case ChatTurnEffectKind.RefreshResolver: _ports.RefreshResolver(); break;
                    case ChatTurnEffectKind.NoteProcessExited: _ports.NoteProcessExited(); break;
                    case ChatTurnEffectKind.NoteTimedOut: _ports.NoteTimedOut(); break;
                    case ChatTurnEffectKind.FinalizeAssistant: _ports.FinalizeAssistant(); break;
                    case ChatTurnEffectKind.ErrorChip: _ports.ErrorChip(); break;
                    case ChatTurnEffectKind.AfterAnswer: _ports.AfterAnswer(); break;
                    case ChatTurnEffectKind.RestoreButton: _ports.RestoreButton(); break;
                    case ChatTurnEffectKind.OfferApprove: _ports.OfferApprove(); break;
                    case ChatTurnEffectKind.RestartBackend: _ports.RestartBackend(); break;
                    case ChatTurnEffectKind.ResetTurn: _ports.ResetTurn(); break;
            }
        }
    }

    public static class ChatTurnFeature
    {
        public static IDisposable Mount(IChatTurnView view, IChatTurnPorts ports, IChatTurnDecisions decisions) => new ChatTurnHost(view, ports, decisions);
    }
}
