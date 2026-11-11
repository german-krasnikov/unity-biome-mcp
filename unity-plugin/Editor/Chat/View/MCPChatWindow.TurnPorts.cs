// The 16 effects of the chat-turn FEATURE (step 7.3, dsl/chat/chatturn.feature) — partial of MCPChatWindow.
// Which of them run when a turn ends, in which order and how many times is decided there; this file is only their bodies.
// A body may read the boxes below, which the caller fills BEFORE it dispatches the intent that fires the port.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat.Turn;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow : IChatTurnPorts
    {
        internal ChatTurnDriver _turn;                                        // the one door of the FEATURE: every way out of a turn is a Dispatch (made in OnEnable; internal for the tests)
        private string    _undoLabel;                                          // label of the undo group of the turn being sent
        private ChatEvent _exitEvent;                                          // the TurnDone or Error event that ends the turn (usage, error text)
        private readonly HashSet<string> _turnAskIds = new HashSet<string>();  // MCP asks raised while the turn is open: they die with it

        // The host does not catch: a port that throws is logged here and the other effects of the exit still run.
        private static void Run(Action port)
        {
            try { port(); }
            catch (Exception e) { Debug.LogException(e); }
        }

        void IChatTurnPorts.LockReload()        => Run(ReloadGuard.OnTurnStarted);
        void IChatTurnPorts.UndoStart()         => Run(() => _undoTracker.OnTurnStart(_undoLabel));
        void IChatTurnPorts.UnlockReload()      => Run(ReloadGuard.OnTurnFinished);
        void IChatTurnPorts.CancelAsks()        => Run(CancelTurnAsks);
        void IChatTurnPorts.UndoClose()         => Run(_undoTracker.OnTurnEnd);
        void IChatTurnPorts.SyncAssets()        => Run(SyncAssetsOnce);
        void IChatTurnPorts.RefreshResolver()   => Run(() => { RefreshResolver(); _lastRefresh = EditorApplication.timeSinceStartup; });
        void IChatTurnPorts.NoteProcessExited() => Run(() => _transcript?.AppendOrExtendAssistant("\n[Process exited]"));
        void IChatTurnPorts.NoteTimedOut()      => Run(AppendTimeoutNote);
        void IChatTurnPorts.FinalizeAssistant() => Run(() => _transcript?.FinalizeAssistant());
        void IChatTurnPorts.ErrorChip()         => Run(() => AppendErrorChip(_exitEvent.Text));
        void IChatTurnPorts.AfterAnswer()       => Run(ShowUsageAndAutoFixChips);
        void IChatTurnPorts.RestoreButton()     => Run(() => _transcript?.Append(RestoreButton.Create(_undoTracker)));
        void IChatTurnPorts.OfferApprove()      => Run(OfferApproveButton);
        void IChatTurnPorts.RestartBackend()    => Run(() => { _backend?.Stop(); _backend = null; CreateBackend(); });
        void IChatTurnPorts.ResetTurn()         => Run(ResetTurnFlags);

        private void AppendErrorChip(string text) => _transcript?.AppendToolChip(text ?? "Error", ok: false);

        // PR-05 05.2: the asks this turn raised would otherwise wait their 300 s for an answer that the dead turn cannot use.
        // Asks of other providers, raised before the turn, are not touched; OnDisable still cancels every ask of the window.
        private void CancelTurnAsks()
        {
            foreach (var id in _turnAskIds) { PendingAskRegistry.Cancel(id); _pendingAskRequestIds.Remove(id); }
            _turnAskIds.Clear();
        }

        // Debounced refresh: once at the end of the turn, after the unlock (D2.3) — a mid-stream partial compile gives phantom CS errors.
        private void SyncAssetsOnce()
        {
            if (!_needsRefresh) return;
            _needsRefresh = false;
            SyncHelper.TriggerSync(resolve: false);
        }

        // M1: reads _lastToolName, so the FEATURE resets the per-turn scratch only after this note.
        private void AppendTimeoutNote()
        {
            var hint = !string.IsNullOrEmpty(_lastToolName) ? $" (last tool: {_lastToolName})" : "";
            _transcript?.AppendOrExtendAssistant($"\n[Timed out: no response for {(int)InactivityTimeoutSec}s{hint}]");
        }

        // What the TurnDone handler did after finalizing the answer: usage bar, auto-fix cap or arm, agent-miss chip.
        private void ShowUsageAndAutoFixChips()
        {
            var ev = _exitEvent;
            if (ev.InputTokens < 0)
            {
                // Backend does not report usage — hide the bar rather than showing empty.
                _contextBar?.Update(-1,
                    ModelContextWindows.GetContextWindow(_selectedModel, _selectedKind));
            }
            else if (ev.InputTokens > 0 || ev.OutputTokens > 0)
            {
                // result.usage carries cumulative session totals, not per-turn deltas.
                _inputTokens  = ev.InputTokens;
                _outputTokens = ev.OutputTokens;
                if (_tokenReadout != null)
                    _tokenReadout.text = TokenFormat.FormatReadout(_inputTokens, _outputTokens);
                _contextBar?.Update(_inputTokens,
                    ModelContextWindows.GetContextWindow(_selectedModel, _selectedKind));
            }
            if (CompileErrorCapture.HasErrors())
            {
                // Cap reached: show chip once here, after the 3rd dispatched turn.
                if (_autoFix.RetriesLeft == 0 && !_autoFix.IsArmed)
                    _transcript?.AppendToolChip("Auto-fix capped at 3 attempts.", ok: false);
                // Arm only when this turn edited code (CRITICAL #2 gate).
                else if (_turnEditedCode)
                    _autoFix.Arm();
            }
            // T-6.3: if agent was mentioned but delegation never happened (no Agent/Task
            // tool fired in this turn), warn. Check at TurnDone so the whole turn is visible.
            if (_pendingAgentName != null && !_turnHadDelegation)
                _transcript?.AppendToolChip(
                    $"[Biome] @{_pendingAgentName} was mentioned but no delegation occurred.",
                    ok: false);
        }

        // Ask mode + valid session + tool calls present → inject one-shot approve button.
        private void OfferApproveButton()
        {
            if (_agentMode || string.IsNullOrEmpty(_backend?.SessionId) || !_turnHasToolCalls) return;
            var approveContainer = new VisualElement();
            _transcript?.Append(approveContainer);
            ApproveButtonFactory.MaybeAppend(approveContainer,
                agentMode: false, sessionId: _backend.SessionId,
                onApprove: ApproveAndExecute);
        }
    }
}
