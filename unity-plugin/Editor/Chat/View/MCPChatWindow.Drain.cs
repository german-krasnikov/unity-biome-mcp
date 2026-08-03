// Drain loop and post-reload resume logic — partial of MCPChatWindow.
// Event/tool-record handlers live in MCPChatWindow.EventHandlers.cs.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private double _lastRefresh;
        // Watchdog: timestamp of the last drained event; checked for 30s inactivity.
        private double _lastEventTime;

        // #1 Save turn state before domain reload so it can be resumed after.
        private void SaveStateBeforeReload()
        {
            // F21: save transcript before UI tree is destroyed
            SessionState.SetString(PrefKeys.ChatTranscript,
                _transcript?.SerializeForReload() ?? "");
            string[] chipPaths, kindKeys;
            int[]    chipOffsets;
            if (_chipField?.Model != null && _chipField.Model.Count > 0)
            {
                var reload  = _chipField.Model.SerializeForReload();
                chipPaths   = reload.Paths;
                kindKeys    = reload.KindKeys;
                chipOffsets = _chipField.Model.GetTextOffsets();
            }
            else
            {
                chipPaths   = System.Array.Empty<string>();
                kindKeys    = System.Array.Empty<string>();
                chipOffsets = System.Array.Empty<int>();
            }

            var isIdle = _activity.Phase == ActivityPhase.Idle;

            // Nothing to save: idle with no chips and no input text.
            var inputText = isIdle
                ? (_chipField?.Text ?? _input?.value ?? "").Trim()
                : _sentTextCache.Get();
            if (isIdle && chipPaths.Length == 0 && string.IsNullOrEmpty(inputText))
                return;

            var state = new PendingTurnState(
                isIdle ? null
                       : (_backend?.SessionId
                          ?? SessionState.GetString(PrefKeys.ChatBackendSessionId, null)),
                inputText,
                chipPaths,
                _agentMode,
                _selectedAgent,
                _activity.Phase.ToString(),
                undoGroupId:      isIdle ? -1 : _undoTracker.InflightGroupId,
                savedAtUtc:       DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                backendKind:      _selectedKind,
                kindKeys:         kindKeys,
                chipTextOffsets:  chipOffsets,
                // task#10: in-flight saves carry the full-path payload to re-send; idle saves don't.
                pendingLlmPayload: isIdle ? "" : _sentLlmCache.Get());
            ReloadGuard.SavePendingState(state);
        }

        // #1 Called by RelaySpawner.OnAfterReloadResume after assembly reload completes.
        private void TryResumePendingTurn()
        {
            // P0-1: consume the restore flag exactly once per call, regardless of which
            // return path we take (D6 gate / stale / null / idle all return before dispatch).
            var transcriptRestored = _transcriptRestored;
            _transcriptRestored = false;

            // D6 gate: if compile is not clean, reschedule via delayCall (bounded 30 retries).
            // Spec: 31st call with IsCompileClean=false → give up (discard pending state).
            if (!SyncHelper.IsCompileClean)
            {
                if (_resumeRetryCount >= MaxResumeRetries)
                {
                    // Give up: discard pending state to avoid zombie resume.
                    ReloadGuard.ClearPendingState();
                    _resumeRetryCount = 0;
                    CancelPendingTurnResume();
                    return;
                }
                if (SchedulePendingTurnResume())
                    _resumeRetryCount++;
                return;
            }
            CancelPendingTurnResume();
            _resumeRetryCount = 0;  // reset on success path

            var pending = ReloadGuard.LoadPendingState();
            if (pending == null) return;
            ReloadGuard.ClearPendingState();

            var p = pending.Value;

            // #26 Staleness guard: discard state saved by a crash/restart older than threshold.
            // SavedAtUtc == 0 means legacy file (no timestamp) — allowed through.
            // Idle saves are exempt: they represent user-composed (not in-flight) state.
            if (PendingTurnState.IsStale(p, DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
                return; // stale crash/restart artifact — file already cleared, silently discard

            _agentMode     = p.AgentMode;
            _selectedAgent = p.AgentName;
            _selectedKind  = p.BackendKind;

            // Restore chips from PendingTurnState (v4/v5 format).
            if (_chipField?.Model != null && p.ChipPaths?.Length > 0)
                _chipField.Model.RestoreFromReload(p.ChipPaths, p.KindKeys, p.ChipTextOffsets);
            _chipField?.RebuildFromModel();

            // Idle save: restore chips + input text only, no turn dispatch.
            if (p.ActivityPhase == "Idle")
            {
                if (!string.IsNullOrEmpty(p.PendingText) && _chipField != null)
                    _chipField.Text = p.PendingText;
                return;
            }

            CreateBackendWithSession(p.SessionId);

            var displayText = p.PendingText;
            // task#10: re-send the full-path payload (paths + [kind:path] block), NOT the short-name
            // display text. Pre-v6 blobs have no payload → fall back to PendingText.
            var resendBody = string.IsNullOrEmpty(p.PendingLlmPayload)
                ? displayText
                : p.PendingLlmPayload;
            if (!string.IsNullOrEmpty(displayText))
            {
                // Build the full text sent to Claude (snapshot + payload), but display only original.
                var snap     = EditorStateSnapshot.Capture();
                var sentText = string.IsNullOrEmpty(snap)
                    ? resendBody
                    : snap + "\n" + resendBody;
                // FIX B: lock reload for the resumed turn, symmetric with DispatchTurn.
                ReloadGuard.OnTurnStarted();
                // #12: close pre-reload undo group BEFORE opening the resumed one;
                // ordering is critical — CloseNamedGroup must precede OnTurnStart.
                if (p.UndoGroupId >= 0)
                    UndoGroupHelper.CloseNamedGroup(p.UndoGroupId);
                // F6: resumed turns also need an undo group so OnTurnEnd/OnTurnFailed
                // can commit them to the restore stack (MAJOR 1 fix).
                _undoTracker.OnTurnStart(displayText.Length > 40
                    ? displayText.Substring(0, 40)
                    : displayText);
                // Cache the FULL sent text (with snapshot) so a re-reload can persist it.
                _sentTextCache.Set(sentText);
                // task#10: keep the llm cache symmetric — a re-reload persists the same full payload.
                _sentLlmCache.Set(sentText);
                // Show only the original user text in the bubble (no state dump).
                var chipList = _chipField?.Model?.Chips is { Count: > 0 } c
                    ? new System.Collections.Generic.List<ChipData>(c) : null;
                _transcript?.SetLastTurnChips(chipList);          // P0-1: ALWAYS — normalization context for resumed response
                if (!transcriptRestored)
                    _transcript?.AppendUserBubble(displayText, chipList, llmPayload: sentText); // P0-1: skip when transcript restore already rendered it; llmPayload=sentText so "Copy as sent to LLM" reveals snapshot
                // (no field reset here — already cleared at entry)
                // task#10 RESOLVED: sentText now carries the full-path payload (paths + [kind:path]
                // block) from PendingLlmPayload, matching the fresh-send payload exactly.
                _backend.SendTurn(UserTurnBuilder.Build(sentText));
                _lastEventTime = EditorApplication.timeSinceStartup; // watchdog reset
                if (_activity.Send()) OnActivityChanged();
            }
        }

        private bool SchedulePendingTurnResume()
        {
            if (_resumeDelayScheduled) return false;
            _resumeDelayScheduled = true;
            EditorApplication.delayCall += ResumePendingTurnAfterDelay;
            return true;
        }

        private void ResumePendingTurnAfterDelay()
        {
            _resumeDelayScheduled = false;
            TryResumePendingTurn();
        }

        private void CancelPendingTurnResume()
        {
            if (!_resumeDelayScheduled) return;
            EditorApplication.delayCall -= ResumePendingTurnAfterDelay;
            _resumeDelayScheduled = false;
        }

        private double InactivityTimeoutSec
        {
            get
            {
                int cfg = BackendConfigStore.Load().InactivityTimeoutSec;
                int floor = _selectedKind == BackendKind.Codex ? 300 : 30;
                return System.Math.Max(floor, cfg);
            }
        }

        // Tier 2 (chat-relay-upm-fix.md): surfaces RelaySpawnState.IsPending — the async spawn
        // path RelayBackend.Start() uses in production so a uvx cold start never freezes this tick.
        private void UpdateRelayStatusLabel()
        {
            if (_relayStatusLabel == null) return;
            if (RelaySpawnState.IsPending)
            {
                _relayStatusLabel.text = "Starting relay (first run ~30s)...";
                _relayStatusLabel.style.color = StyleKeyword.Null;
                _relayStatusLabel.style.display = DisplayStyle.Flex;
            }
            else if (RelaySpawnState.Error != null)
            {
                _relayStatusLabel.text = RelaySpawnState.Error;
                _relayStatusLabel.style.color = new StyleColor(new Color(1f, 0.35f, 0.35f));
                _relayStatusLabel.style.display = DisplayStyle.Flex;
            }
            else
            {
                _relayStatusLabel.style.display = DisplayStyle.None;
            }
        }

        private void DrainAndRender()
        {
            UpdateRelayStatusLabel();
            _evBuf.Clear(); _toolBuf.Clear();
            _backend?.DrainEvents(_evBuf, _toolBuf);

            if (_evBuf.Count == 0 && _toolBuf.Count == 0)
            {
                // Dead-process guard: buffer is empty, so no result line is coming.
                // Only fires if the process truly died (crash/kill) without emitting a result.
                // Phase-first: cheap field read before the nullable backend deref.
                if (_activity.Phase != ActivityPhase.Idle && _backend != null && !_backend.IsRunning)
                {
                    // #2 fix: release the reload lock; without this the lock was held until the 120s watchdog.
                    ReloadGuard.OnTurnFinished();
                    ResetTurnFlags(); // P0-2: dead-process guard must also clear stale flags
                    // F6: dead process — treat as failed turn.
                    _undoTracker.OnTurnFailed();
                    // M2: surface that the process exited unexpectedly.
                    _transcript?.AppendOrExtendAssistant("\n[Process exited]");
                    _transcript?.FinalizeAssistant();
                    if (_activity.Fail()) OnActivityChanged();
                    return;
                }
                // Inactivity watchdog: process alive but no events for too long after turn started.
                // Fires when a backend silently stalls after a tool error (no turn/completed emitted).
                if (_activity.Phase != ActivityPhase.Idle && _backend != null && _backend.IsRunning
                    && _lastEventTime > 0
                    && EditorApplication.timeSinceStartup - _lastEventTime > InactivityTimeoutSec)
                {
                    ReloadGuard.OnTurnFinished();
                    ResetTurnFlags();
                    _undoTracker.OnTurnFailed();
                    var hint = !string.IsNullOrEmpty(_lastToolName) ? $" (last tool: {_lastToolName})" : "";
                    _transcript?.AppendOrExtendAssistant($"\n[Timed out: no response for {(int)InactivityTimeoutSec}s{hint}]");
                    _transcript?.FinalizeAssistant();
                    if (_activity.Fail()) OnActivityChanged();
                }
                return;
            }
            _lastEventTime = EditorApplication.timeSinceStartup;
            // Refresh the ref cache at most ~1/sec while streaming so objects Claude just
            // created become clickable without reopening the window.
            if (EditorApplication.timeSinceStartup - _lastRefresh > 1.0)
            {
                _resolver?.Refresh();
                _lastRefresh = EditorApplication.timeSinceStartup;
            }
            foreach (var ev in _evBuf) HandleEvent(ev);
            foreach (var rec in _toolBuf) HandleToolRecord(rec);
            _transcript.FlushStreaming();
            // _needsRefresh is now debounced to TurnDone (HandleEvent case TurnDone).
            // Do NOT act on it here — mid-stream partial compiles cause phantom CS errors.
            if (EditorPrefs.GetBool(PrefKeys.ChatAutoScroll, true))
                _scroll.scrollOffset = new Vector2(0, float.MaxValue);
        }

        // Called by CompileAutoFix.OnErrorsDetected — fires on Unity main thread (compilationFinished).
        // Only builds the message and dispatches the turn; cap chip is shown at TurnDone.
        private void InjectCompileErrors(string errors)
        {
            if (!_activity.CanSend) return;
            var msg = $"Compile errors after your edit:\n{errors}\nFix them.";
            DispatchTurn(UserTurnBuilder.Build(msg), msg);
        }

        // Tool names that edit source files — used to gate auto-fix arming (CRITICAL #2).
        internal static bool IsCodeEditingTool(ToolCallRecord rec)
        {
            var n = rec.Name;
            if (n == "Edit" || n == "Write" || n == "MultiEdit") return true;
            // MCP mutating tools that write .cs files are identified by a .cs path in args.
            if (rec.ArgsJson != null && rec.ArgsJson.Contains(".cs\"")) return true;
            return false;
        }

    }
}
