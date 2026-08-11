// Partial MCPChatWindow — send path: OnSend, DispatchTurn.
// F13: AppendChipContext removed — ChipTextInterleaver handles chip serialization.
// Text is clean by construction (InlineChipField — no FFFC/NBSP stripping needed).
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private void OnSend()
        {
            if (!_activity.CanSend) return;
            _autoFix.Disarm();

            var store    = BackendConfigStore.Load();
            var rawText  = (_chipField?.Text ?? _input?.value ?? "").Trim();
            var msg      = ChipTextInterleaver.BuildFromRaw(rawText, _chipField?.Model?.PositionedChips);
            var llmText  = ChipTextInterleaver.ToLlmPayload(msg, store.Chips);
            if (string.IsNullOrEmpty(llmText)) return;

            var debugCtx = BuildDebugContext();
            if (!string.IsNullOrEmpty(debugCtx))
                llmText = debugCtx + "\n" + llmText;

            var images = CollectImageChipBytes(msg.Chips);
            var turnJson = images.Count > 0
                ? UserTurnBuilder.Build(llmText, images)
                : UserTurnBuilder.Build(llmText);
            DispatchTurn(turnJson, msg, llmText, FirstImageChipPath(msg.Chips));
        }

        // Collect bytes from image chips (KindKey == "image") preserving order.
        private static List<byte[]> CollectImageChipBytes(IReadOnlyList<ChipData> chips)
        {
            var result = new List<byte[]>();
            if (chips == null) return result;
            foreach (var chip in chips)
            {
                if (chip.KindKey != ChipKindKeys.Image) continue;
                if (string.IsNullOrEmpty(chip.Path) || !File.Exists(chip.Path)) continue;
                try { result.Add(File.ReadAllBytes(chip.Path)); }
                catch { /* skip unreadable */ }
            }
            return result;
        }

        private static string FirstImageChipPath(IReadOnlyList<ChipData> chips)
        {
            if (chips == null) return null;
            foreach (var chip in chips)
                if (chip.KindKey == ChipKindKeys.Image && !string.IsNullOrEmpty(chip.Path) && File.Exists(chip.Path))
                    return chip.Path;
            return null;
        }

        // Shared send sequence.
        private void DispatchTurn(string turnJson, UserMessage displayMsg,
            string llmPayload, string screenshotPath = null)
        {
            _pendingAgentName = AgentMissDetector.ExtractAgentName(llmPayload);
            ReloadGuard.OnTurnStarted();
            var displayText = ChipTextInterleaver.ToDisplayText(displayMsg);
            _undoTracker.OnTurnStart(displayText.Length > 40
                ? displayText.Substring(0, 40)
                : displayText);
            _sentTextCache.Set(displayText);
            _sentLlmCache.Set(llmPayload); // task#10: persist the EXACT full-path bytes sent this turn
            _resolver?.Refresh(); // ensure objects created since last turn are visible to BareNameNormalizer
            _transcript.SetLastTurnChips(displayMsg.Chips);
            _transcript.AppendUserBubble(displayMsg, llmPayload, screenshotPath);
            _backend.SendTurn(turnJson);
            _lastEventTime = EditorApplication.timeSinceStartup; // watchdog reset
            if (_chipField != null) { _chipField.ClearChips(); _chipField.Text = ""; }
            else if (_input != null) { _input.value = ""; _input.cursorIndex = _input.selectIndex = 0; }
            _heightCalc.Reset();
            ResetInputAreaHeight();
            if (_activity.Send()) OnActivityChanged();
        }

        // Overload accepting plain string — used by InjectCompileErrors and ApproveAndExecute.
        // For these callers the sent text equals displayText (no chip context block).
        private void DispatchTurn(string turnJson, string displayText,
            System.Collections.Generic.IReadOnlyList<ChipData> chipSnapshot = null,
            string screenshotPath = null)
        {
            var positioned = new System.Collections.Generic.List<PositionedChip>();
            if (chipSnapshot != null)
                foreach (var c in chipSnapshot)
                    positioned.Add(new PositionedChip(c, 0));
            var msg = ChipTextInterleaver.Build(displayText ?? "", positioned);
            DispatchTurn(turnJson, msg, llmPayload: displayText, screenshotPath: screenshotPath);
        }
    }
}
