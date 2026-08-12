// Event and tool-record handlers — partial of MCPChatWindow.
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private bool _askPending;

        private void HandleEvent(ChatEvent ev)
        {
            switch (ev.Kind)
            {
                case ChatEventKind.TextDelta:
                    if (_activity.FirstToken()) OnActivityChanged();
                    _transcript?.AppendOrExtendAssistant(ev.Text);
                    break;
                case ChatEventKind.TurnDone:
                    ReloadGuard.OnTurnFinished(); // #1 unlock after turn complete
                    _askPending = false;
                    if (_activity.Done()) OnActivityChanged();
                    // Debounced refresh: fire once at TurnDone (not mid-stream per tool result).
                    // D2.3: single Refresh after unlock avoids phantom CS errors from partial edits.
                    if (_needsRefresh)
                    {
                        _needsRefresh = false;
                        SyncHelper.TriggerSync(resolve: false);
                    }
                    // F20: refresh resolver before freeze so objects created during this turn
                    // are visible to BareNameNormalizer's scene-wide pass (closes cache-staleness gap).
                    _resolver?.Refresh();
                    _lastRefresh = EditorApplication.timeSinceStartup;
                    _transcript?.FinalizeAssistant();
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
                    var hasErrors = CompileErrorCapture.HasErrors();
                    if (hasErrors)
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
                    var hadToolCalls = _turnHasToolCalls; // P0-2: capture before reset
                    ResetTurnFlags(); // P0-2: DRY reset (was 3 inline assignments)
                    // F6: close the undo group and append a Restore button.
                    _undoTracker.OnTurnEnd();
                    _transcript?.Append(RestoreButton.Create(_undoTracker));
                    // Ask mode + valid session + tool calls present → inject one-shot approve button.
                    if (!_agentMode && !string.IsNullOrEmpty(_backend?.SessionId) && hadToolCalls)
                    {
                        var approveContainer = new VisualElement();
                        _transcript?.Append(approveContainer);
                        ApproveButtonFactory.MaybeAppend(approveContainer,
                            agentMode: false, sessionId: _backend.SessionId,
                            onApprove: ApproveAndExecute);
                    }
                    break;
                case ChatEventKind.SessionInit:
                    break; // non-terminal: session established, keep animation running
                case ChatEventKind.Error:
                    ReloadGuard.OnTurnFinished(); // #1 unlock even on error
                    _askPending = false;
                    if (_activity.Fail()) OnActivityChanged();
                    ApplyErrorTurn(_transcript, ev.Text); // T1.2: extracted for testability
                    ResetTurnFlags(); // P0-2: DRY reset (was 3 inline assignments)
                    // F6: partial mutations still restorable on error.
                    _undoTracker.OnTurnFailed();
                    _transcript?.Append(RestoreButton.Create(_undoTracker));
                    break;
                case ChatEventKind.PermissionPrompt:
                    if (_agentMode || _sessionAllowlist.IsAutoApproved(ev.Text))
                    {
                        _backend?.SendControlResponse(ControlResponseBuilder.Allow(ev.RequestId));
                        _transcript?.AppendToolChip($"Auto-approved: {ev.Text}", ok: true);
                    }
                    else
                    {
                        var risk = RiskClassifier.Classify(ev.Text);
                        var permCard = new ToolApprovalCard(
                            ev.RequestId, ev.Text, ev.ToolInput, risk,
                            decision =>
                            {
                                if (decision == ApprovalDecision.Deny)
                                {
                                    _backend?.SendControlResponse(
                                        ControlResponseBuilder.Deny(ev.RequestId));
                                }
                                else
                                {
                                    if (decision == ApprovalDecision.AllowSession)
                                        _sessionAllowlist.AddSession(ev.Text);
                                    else if (decision == ApprovalDecision.AlwaysAllow)
                                        _sessionAllowlist.AddAlways(ev.Text);
                                    _backend?.SendControlResponse(
                                        ControlResponseBuilder.Allow(ev.RequestId));
                                }
                            });
                        _transcript?.Append(permCard);
                        _scroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue);
                    }
                    break;
                case ChatEventKind.AskUser:
                    _askPending = true;
                    OnActivityChanged();
                    var askCard = new AskUserCard(ev.RequestId, ev.RawJson,
                        responseJson => {
                            _askPending = false;
                            OnActivityChanged();
                            _backend?.SendControlResponse(responseJson);
                        });
                    _transcript?.Append(askCard);
                    _scroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue);
                    break;
                case ChatEventKind.ToolProgress:
                case ChatEventKind.RateLimit:
                case ChatEventKind.SessionState:
                case ChatEventKind.Heartbeat:
                    break;
                case ChatEventKind.Thinking:
                    if (EditorPrefs.GetBool(PrefKeys.ShowThinkingBlocks, true))
                        _transcript?.AppendThinkingBlock(ev.Text);
                    break;
            }
        }

        /// <summary>Error-event transcript operations extracted so EditMode tests
        /// can call the production method directly without CreateGUI context.
        /// Red B: removing FinalizeAssistant here makes the test fail (stale _taskCard).</summary>
        internal static void ApplyErrorTurn(ChatTranscript transcript, string errorText)
        {
            transcript?.FinalizeAssistant(); // T1.2: clear _taskCard before error chip
            transcript?.AppendToolChip(errorText ?? "Error", ok: false);
        }

        private void OnMcpAskUser(string requestId, string rawQuestionsJson)
        {
            _askPending = true;
            OnActivityChanged();
            var rawJson = "{\"questions\":" + rawQuestionsJson + "}";
            var card = new AskUserCard(requestId, rawJson,
                responseJson => {
                    _askPending = false;
                    OnActivityChanged();
                    PendingAskRegistry.Complete(requestId, responseJson);
                },
                directAnswer: true);
            _transcript?.Append(card);
            _scroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue);
        }

        private void HandleToolRecord(ToolCallRecord rec)
        {
            if (rec.ArgsJson == null && !rec.HasResult)
            {
                // null ArgsJson = chip-creation record (ToolStart moment)
                if (rec.Name == "Agent" || rec.Name == "Task")
                    _turnHadDelegation = true; // T-6.3: delegation detected — suppress miss warning
                if (_activity.FirstToken()) OnActivityChanged();
                _transcript?.AppendToolChip(rec.Name, ok: true, toolId: rec.Id);
                _turnHasToolCalls = true;
                _lastToolName = rec.Name; // M1: track last tool for timeout hint
            }
            else
            {
                // ArgsJson non-null + no result = args-complete record: ping + check provenance.
                // HasResult = result record: detail update only (no double-ping).
                if (rec.ArgsJson != null && !rec.HasResult)
                {
                    ToolPing.TryPing(rec);
                    if (IsCodeEditingTool(rec))
                        _turnEditedCode = true;
                }
                _transcript?.UpdateToolDetail(rec.Id, rec);
                if (rec.HasResult && IsCodeEditingTool(rec))
                    _needsRefresh = true;
            }
        }
    }
}
