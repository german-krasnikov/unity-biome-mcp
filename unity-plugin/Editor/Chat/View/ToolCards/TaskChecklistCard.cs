// T-4.3a: Card that accumulates agent task state across multiple tool calls.
//
// Design: static class — state lives per-turn in static dicts.
// Caller (T-4.3b ChatTranscript) routes TaskCreate / TaskUpdate calls here
// and calls ClearForNextTurn() at FinalizeAssistant().
//
// Idempotency: _rowByToolId guards against double-rendering on the second
// OnTaskCreate call (ArgsComplete → Result two-pass pattern).
//
// TryExtractTaskId strategies (via TodoTaskParser):
//   A: "^Task #(\d+) created successfully:" on resultText
//   B: "^Task (\d+):" on subject — primary for backends that mute tool_result
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    internal static class TaskChecklistCard
    {
        internal const string CardClass = "task-card";

        // Per-turn state — cleared by ClearForNextTurn() at FinalizeAssistant.
        // toolId  → row VisualElement (idempotency + update lookup)
        private static readonly Dictionary<string, VisualElement> _rowByToolId =
            new Dictionary<string, VisualElement>();
        // taskId  → toolId  (so OnTaskUpdate can find the right row)
        private static readonly Dictionary<string, string> _taskIdToToolId =
            new Dictionary<string, string>();

        // ── Public API ────────────────────────────────────────────────────────────

        internal static VisualElement Create()
        {
            var card = new VisualElement();
            card.AddToClassList(CardClass);
            return card;
        }

        internal static void OnTaskCreate(VisualElement card, ToolCallRecord rec)
        {
            if (string.IsNullOrEmpty(rec.ArgsJson)) return;

            var args = TodoTaskParser.Parse("TaskCreate", rec.ArgsJson);
            if (!args.IsValid) return;

            // Idempotency: one row per toolId; second call only updates taskId mapping.
            if (rec.Id != null && !_rowByToolId.TryGetValue(rec.Id, out _))
            {
                var row = BuildRow(args.Subject);
                if (rec.Id != null) _rowByToolId[rec.Id] = row;
                card.Add(row);
            }

            // Both passes: try to register taskId → toolId mapping.
            // Strategy A (resultText) beats B (subject) when result is present.
            RegisterTaskId(rec.Id, rec.ResultText, args.Subject, rec.HasResult);
        }

        internal static void OnTaskUpdate(VisualElement card, ToolCallRecord rec)
        {
            var args = TodoTaskParser.Parse("TaskUpdate", rec.ArgsJson);
            if (!args.IsValid || string.IsNullOrEmpty(args.TaskId)) return;

            if (!_taskIdToToolId.TryGetValue(args.TaskId, out var toolId))
            {
                Debug.LogWarning(
                    $"[TaskChecklistCard] Unknown taskId '{args.TaskId}' — no matching row.");
                return;
            }

            if (!_rowByToolId.TryGetValue(toolId, out var row)) return;
            UpdateRowStatus(row, args.Status);
        }

        internal static void ClearForNextTurn()
        {
            _rowByToolId.Clear();
            _taskIdToToolId.Clear();
        }

        // ── Private helpers ───────────────────────────────────────────────────────

        private static VisualElement BuildRow(string subject)
        {
            var row = new VisualElement();
            row.AddToClassList("task-row");

            var statusLabel = new Label("○");
            statusLabel.AddToClassList("task-status");
            row.Add(statusLabel);

            var textLabel = new Label(subject ?? "");
            textLabel.AddToClassList("task-text");
            row.Add(textLabel);

            return row;
        }

        private static void RegisterTaskId(
            string toolId, string resultText, string subject, bool hasResult)
        {
            var taskId = TodoTaskParser.TryExtractTaskId(resultText, subject);
            if (taskId != null)
            {
                if (toolId != null) _taskIdToToolId[taskId] = toolId;
                return;
            }
            // Log warning only once: when result is final and both strategies failed.
            if (hasResult)
                Debug.LogWarning(
                    "[TaskChecklistCard] Cannot extract taskId from result or subject.");
        }

        private static void UpdateRowStatus(VisualElement row, TaskStatus status)
        {
            var statusLabel = row.Q<Label>(className: "task-status");
            if (statusLabel == null) return;

            switch (status)
            {
                case TaskStatus.InProgress:
                    statusLabel.text = "⬤";
                    break;
                case TaskStatus.Completed:
                    statusLabel.text = "✓";
                    var textLabel = row.Q<Label>(className: "task-text");
                    if (textLabel != null)
                        textLabel.AddToClassList("task-text--completed");
                    break;
            }
        }
    }
}
