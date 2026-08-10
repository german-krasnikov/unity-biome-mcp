// UIToolkit popup for @mention autocomplete suggestions.
// Mirrors SlashPopup pattern. focusable=false on all rows to prevent focus theft.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class MentionPopup
    {
        private readonly VisualElement             _anchor;
        private readonly System.Action<MentionCandidate> _onCommit;
        private VisualElement          _root;
        private ScrollView             _scroll;
        private List<MentionCandidate> _candidates = new List<MentionCandidate>();
        private int                    _selectedIndex;

        internal bool IsVisible    => _root != null && _root.parent != null;
        internal int  SelectedIndex => _selectedIndex;

        internal MentionPopup(VisualElement anchor, System.Action<MentionCandidate> onCommit)
        {
            _anchor   = anchor;
            _onCommit = onCommit;
        }

        internal void Show(List<MentionCandidate> candidates, int maxRows = 8)
        {
            Dismiss();
            if (candidates == null || candidates.Count == 0) return;

            _candidates.Clear();
            int cap = System.Math.Min(candidates.Count, maxRows);
            for (int i = 0; i < cap; i++)
                _candidates.Add(candidates[i]);

            _root = new VisualElement { focusable = false };
            _root.AddToClassList("mention-popup");

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            for (int i = 0; i < _candidates.Count; i++)
            {
                var idx = i;
                var c   = _candidates[i];

                var row = new VisualElement { focusable = false };
                row.AddToClassList("mention-row");
                if (i == 0) row.AddToClassList("mention-row--selected");

                var icon = new Image { focusable = false };
                icon.AddToClassList("mention-icon");
                var iconContent = EditorGUIUtility.IconContent(c.IconName);
                if (iconContent != null) icon.image = iconContent.image;

                var label = new Label(c.Chip.DisplayName) { focusable = false };
                label.AddToClassList("mention-label");

                var detail = new Label(c.Chip.Path) { focusable = false };
                detail.AddToClassList("mention-detail");

                row.Add(icon);
                row.Add(label);
                row.Add(detail);

                row.RegisterCallback<MouseEnterEvent>(_ => { _selectedIndex = idx; Highlight(); });
                row.RegisterCallback<ClickEvent>(_ => Commit(idx));

                // Right-click context menu — ClickEvent fires on button==0 only; no conflict.
                var capturedIdx = idx;
                var capturedC   = c;
                row.AddManipulator(new ContextualMenuManipulator(evt =>
                {
                    evt.menu.AppendAction("Add to Context",
                        _ => Commit(capturedIdx));

                    evt.menu.AppendAction("Copy Reference",
                        _ => MentionRowActions.CopyRef(capturedC));

                    evt.menu.AppendAction("Ping in Hierarchy",
                        _ => MentionRowActions.PingInHierarchy(capturedC),
                        _ => MentionRowActions.IsHierarchyChip(capturedC)
                            ? DropdownMenuAction.Status.Normal
                            : DropdownMenuAction.Status.Disabled);

                    evt.menu.AppendAction("Ping in Project",
                        _ => MentionRowActions.PingInProject(capturedC),
                        _ => !MentionRowActions.IsHierarchyChip(capturedC)
                            ? DropdownMenuAction.Status.Normal
                            : DropdownMenuAction.Status.Disabled);
                }));

                _scroll.Add(row);
            }

            _root.Add(_scroll);
            _anchor.Add(_root);
            _selectedIndex = 0;
        }

        internal MentionCandidate? ApplySelected()
        {
            if (_candidates.Count == 0) return null;
            var result = _candidates[_selectedIndex];
            Dismiss();
            return result;
        }

        internal void Dismiss()
        {
            _root?.RemoveFromHierarchy();
            _root   = null;
            _scroll = null;
            _candidates.Clear();
        }

        internal void OnBlur() => Dismiss();

        // Test seam: exercises the Commit(idx) path (normally triggered by mouse click).
        internal void CommitSelected() => Commit(_selectedIndex);

        internal void MoveDown()
        {
            if (_candidates.Count == 0) return;
            _selectedIndex = (_selectedIndex + 1) % _candidates.Count;
            Highlight();
            ScrollToSelected();
        }

        internal void MoveUp()
        {
            if (_candidates.Count == 0) return;
            _selectedIndex = (_selectedIndex - 1 + _candidates.Count) % _candidates.Count;
            Highlight();
            ScrollToSelected();
        }

        private void Highlight()
        {
            if (_scroll == null) return;
            for (int i = 0; i < _scroll.childCount; i++)
                _scroll[i].EnableInClassList("mention-row--selected", i == _selectedIndex);
        }

        private void ScrollToSelected()
        {
            if (_scroll?.panel == null || _selectedIndex < 0 || _selectedIndex >= _scroll.childCount) return;
            _scroll.ScrollTo(_scroll[_selectedIndex]);
        }

        private void Commit(int idx)
        {
            if (idx < 0 || idx >= _candidates.Count) return;
            var candidate = _candidates[idx];
            Dismiss();
            _onCommit?.Invoke(candidate);
        }
    }
}
