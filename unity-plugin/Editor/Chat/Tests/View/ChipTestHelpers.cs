// View-layer test helpers extending the CLI ChipTestHelpers (H/S live in CLI assembly).
using System.Collections.Generic;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    internal static class ChipViewTestHelpers
    {
        // Delegates to AddChip — injects @DisplayName into TextField + tracks position.
        internal static void InsertChip(InlineChipField field, ChipData chip)
            => field.AddChip(chip);

        internal static void SetCursor(InlineChipField field, int pos)
        {
            int clamped = System.Math.Min(pos, (field.Text ?? "").Length);
            field.TextField.cursorIndex = clamped;
            field.TextField.selectIndex = clamped;
            // UI Toolkit cursorIndex may report 0 when the field is not attached/focused;
            // InlineChipField.AddChip falls back to _lastCursorPos in that case, so keep it in sync.
            var lastPosField = typeof(InlineChipField).GetField("_lastCursorPos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            lastPosField?.SetValue(field, clamped);
        }

        internal static void Type(InlineChipField field, string text)
        {
            field.Text = (field.Text ?? "") + text;
            SetCursor(field, field.Text.Length);
        }

        // BuildFromRaw strips @mention text from TextField and remaps chip offsets.
        internal static (string turnJson, string rawText) SimulateSend(
            InlineChipField field, ChatTranscript transcript, ChipConfig cfg)
        {
            var rawText    = (field.Text ?? "").Trim();
            var positioned = new List<PositionedChip>(field.Model.PositionedChips);
            var msg        = ChipTextInterleaver.BuildFromRaw(rawText, positioned);
            var llmText    = ChipTextInterleaver.ToLlmPayload(msg, cfg);
            if (string.IsNullOrEmpty(llmText)) return (null, rawText);
            var turnJson = UserTurnBuilder.Build(llmText);
            transcript.SetLastTurnChips(msg.Chips);
            transcript.AppendUserBubble(msg);
            field.ClearChips();
            field.Text = "";
            SetCursor(field, 0);
            return (turnJson, rawText);
        }

        // Variant that also stores the llmPayload in the bubble userData (UserBubbleData).
        internal static (string turnJson, string rawText) SimulateSendWithPayload(
            InlineChipField field, ChatTranscript transcript, ChipConfig cfg)
        {
            var rawText    = (field.Text ?? "").Trim();
            var positioned = new List<PositionedChip>(field.Model.PositionedChips);
            var msg        = ChipTextInterleaver.BuildFromRaw(rawText, positioned);
            var llmText    = ChipTextInterleaver.ToLlmPayload(msg, cfg);
            if (string.IsNullOrEmpty(llmText)) return (null, rawText);
            var turnJson = UserTurnBuilder.Build(llmText);
            transcript.SetLastTurnChips(msg.Chips);
            transcript.AppendUserBubble(msg, llmText);
            field.ClearChips();
            field.Text = "";
            SetCursor(field, 0);
            return (turnJson, rawText);
        }
    }
}
