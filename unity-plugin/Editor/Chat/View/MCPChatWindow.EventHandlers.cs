// Event and tool-record handlers — partial of MCPChatWindow.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor;
using UnityMCP.Editor.Chat.Turn;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        // PR-05 05.2 (Finding 2, symmetric gap): requestIds this window itself registered in
        // PendingAskRegistry via OnMcpAskUser. OnDisable cancels exactly these — not the whole
        // registry — so an ask owned by another provider is never touched.
        private readonly HashSet<string> _pendingAskRequestIds = new HashSet<string>();

        private void HandleEvent(ChatEvent ev)
        {
            switch (ev.Kind)
            {
                case ChatEventKind.TextDelta:
                    _turn.Dispatch(ChatTurnIntent.FirstToken());
                    _transcript?.AppendOrExtendAssistant(ev.Text);
                    break;
                case ChatEventKind.TurnDone:
                    _exitEvent = ev;   // the afterAnswer port reads its usage
                    _turn.Dispatch(ChatTurnIntent.TurnDone());
                    break;
                case ChatEventKind.SessionInit:
                    break; // non-terminal: session established, keep animation running
                case ChatEventKind.Error:
                    _exitEvent = ev;   // the errorChip port reads its text
                    if (_turn.Active) _turn.Dispatch(ChatTurnIntent.Fail());
                    else { _transcript?.FinalizeAssistant(); AppendErrorChip(ev.Text); }   // no turn is open: the error ends nothing, and is still shown as before (T1.2)
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
                    _turn.Dispatch(ChatTurnIntent.AskRaised());
                    var askCard = new AskUserCard(ev.RequestId, ev.RawJson,
                        responseJson => {
                            _turn.Dispatch(ChatTurnIntent.AskAnswered());
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
                case ChatEventKind.PlanUpdate:
                    _transcript?.Append(new PlanStepCard(ev.Text, ev.ArgsJson ?? ""));
                    if (_scroll != null) _scroll.scrollOffset = new UnityEngine.Vector2(0, float.MaxValue);
                    break;
                case ChatEventKind.FileChange:
                    _transcript?.AppendToolChip($"Changed: {ev.Text}", ok: true);
                    break;
                case ChatEventKind.CapabilitiesChanged:
                    _capabilities = ev.State;
                    break;
            }
        }

        private void OnMcpAskUser(string requestId, string rawQuestionsJson)
        {
            _pendingAskRequestIds.Add(requestId);
            if (_turn.Active) _turnAskIds.Add(requestId);   // raised by the turn: the cancelAsks port cancels it when the turn ends
            _turn.Dispatch(ChatTurnIntent.AskRaised());
            var rawJson = "{\"questions\":" + rawQuestionsJson + "}";
            var card = new AskUserCard(requestId, rawJson,
                responseJson => {
                    _pendingAskRequestIds.Remove(requestId);
                    if (_turnAskIds.Remove(requestId)) _turn.Dispatch(ChatTurnIntent.AskAnswered());   // only an ask that was counted is taken off the count
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
                _turn.Dispatch(ChatTurnIntent.FirstToken());
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
