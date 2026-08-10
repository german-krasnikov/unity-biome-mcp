// Partial MCPChatWindow — @mention autocomplete (Phase 5).
// Mirrors SlashPopup pattern: same keyboard intercept (_inputArea TrickleDown),
// same blur-dismiss pattern (150ms delay), text-change detection via ChangeEvent.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public partial class MCPChatWindow
    {
        private MentionPopup   _mentionPopup;
        private MentionCoordinator _mentionCoordinator;
        private MentionHistory     _mentionHistory;
        private AssetMentionIndex  _assetMentionIndex;
        private int            _mentionAtIndex;
        private int            _mentionQueryLen;
        private IVisualElementScheduledItem _mentionDebounce;

        private void SetupMention()
        {
            var sceneIndex  = new SceneMentionIndex();
            _assetMentionIndex = new AssetMentionIndex();
            var recentSrc   = new RecentMentionSource();
            _mentionCoordinator = new MentionCoordinator(recentSrc, sceneIndex, _assetMentionIndex);

            _mentionHistory = new MentionHistory();
            _mentionCoordinator.History = _mentionHistory;

            _mentionPopup = new MentionPopup(_inputArea, candidate =>
            {
                _mentionHistory.RecordCommit(candidate.Chip.Path);
                _chipField.ReplaceMentionRangeWithChip(_mentionAtIndex, _mentionQueryLen, candidate.Chip);
            });

            // Text change: same event as SlashPopup but for '@' tokens.
            // cursorIndex is stale (returns 0) during ChangeEvent in UIToolkit;
            // fall back to val.Length (end of text) when typing appends characters.
            _input.RegisterCallback<ChangeEvent<string>>(ev =>
            {
                var val = ev.newValue ?? "";
                int cursor = _input.cursorIndex;
                if (cursor <= 0 && val.Length > 0) cursor = val.Length;
                OnMentionInputChanged(val, cursor);
            });

            // Keyboard intercept on _inputArea at TrickleDown — same as Slash.
            _inputArea.RegisterCallback<KeyDownEvent>(OnMentionKeyDown, TrickleDown.TrickleDown);

            // Blur dismiss with same 150ms delay as slash (lets click events fire first).
            _input.RegisterCallback<BlurEvent>(_ =>
                _inputArea.schedule.Execute(_mentionPopup.OnBlur).StartingIn(150));
        }

        private void OnMentionKeyDown(KeyDownEvent ev)
        {
            if (!_mentionPopup.IsVisible) return;

            switch (ev.keyCode)
            {
                case KeyCode.DownArrow:
                    _mentionPopup.MoveDown();
                    ev.StopPropagation();
                    ev.StopImmediatePropagation();
                    break;
                case KeyCode.UpArrow:
                    _mentionPopup.MoveUp();
                    ev.StopPropagation();
                    ev.StopImmediatePropagation();
                    break;
                case KeyCode.Escape:
                    _mentionPopup.Dismiss();
                    ev.StopPropagation();
                    ev.StopImmediatePropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                case KeyCode.Tab:
                    var selected = _mentionPopup.ApplySelected();
                    if (selected.HasValue)
                    {
                        var text   = _input.value ?? "";
                        int cursor = _input.cursorIndex;
                        var chips  = _chipField?.Model?.PositionedChips;
                        if (MentionTokenParser.TryExtract(text, cursor, chips,
                            out int freshAt, out string freshQuery))
                        {
                            _chipField.ReplaceMentionRangeWithChip(freshAt, freshQuery.Length, selected.Value.Chip);
                        }
                    }
                    ev.StopPropagation();
                    ev.StopImmediatePropagation();
                    break;
            }
        }

        private void OnMentionInputChanged(string text, int cursorPos)
        {
            // Don't compete with slash popup.
            if (_slashPopup != null && _slashPopup.IsVisible) { _mentionPopup.Dismiss(); return; }

            var chips = _chipField?.Model?.PositionedChips;

            if (!MentionTokenParser.TryExtract(text, cursorPos, chips, out int atIndex, out string query))
            {
                _mentionPopup.Dismiss();
                return;
            }

            _mentionAtIndex  = atIndex;
            _mentionQueryLen = query.Length;

            if (query.Length < 2) { _mentionPopup.Dismiss(); return; }

            // Debounce: cancel pending search, schedule new one at 100ms.
            _mentionDebounce?.Pause();
            _mentionDebounce = _inputArea.schedule.Execute(() =>
            {
                var cfg = BackendConfigStore.Load().Mention;
                var results = new List<MentionCandidate>();
                _mentionCoordinator.Search(query, cfg.MaxPopupRows, results, cfg.SortOrder);
                if (results.Count > 0)
                    _mentionPopup.Show(results, cfg.MaxPopupRows);
                else
                    _mentionPopup.Dismiss();
            }).StartingIn(100);
        }
    }
}
