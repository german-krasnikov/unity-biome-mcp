using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Accessible category disclosure with a tri-state master toggle.
    /// Avoids querying or modifying Foldout's internal visual tree.
    /// </summary>
    internal sealed class BiomeToggleGroup
    {
        private readonly string _category;
        private readonly string[] _items;
        private readonly Func<string, bool> _getValue;
        private readonly Action<string, bool> _setValue;
        private readonly Action<bool> _setAll;
        private readonly Action _onChanged;
        private readonly Button _disclosure;
        private readonly Toggle _master;
        private readonly VisualElement _content;
        private readonly List<(string name, VisualElement row, Toggle toggle)> _rows;
        private bool _expanded;
        private bool _filtering;

        internal VisualElement Element { get; }

        internal BiomeToggleGroup(
            string category,
            string[] items,
            Func<string, bool> getValue,
            Action<string, bool> setValue,
            Action<bool> setAll,
            bool readOnly = false,
            Action onChanged = null)
        {
            _category = category;
            _items = items;
            _getValue = getValue;
            _setValue = setValue;
            _setAll = setAll;
            _onChanged = onChanged;
            _rows = new List<(string, VisualElement, Toggle)>(items.Length);

            Element = new VisualElement();
            Element.AddToClassList("category-foldout");

            var header = new VisualElement();
            header.AddToClassList("category-header");

            _disclosure = new Button(ToggleExpanded);
            _disclosure.AddToClassList("category-disclosure");
            header.Add(_disclosure);

            _master = new Toggle { label = string.Empty };
            _master.AddToClassList("master-toggle");
            _master.tooltip = $"Enable or disable all tools in {_category}";
            _master.SetEnabled(!readOnly);
            _master.RegisterValueChangedCallback(OnMasterChanged);
            header.Add(_master);

            _content = new VisualElement();
            _content.AddToClassList("category-content");
            _content.style.display = DisplayStyle.None;

            foreach (string item in items)
            {
                var row = new VisualElement();
                row.AddToClassList("tool-row");

                var toggle = new Toggle(item);
                toggle.AddToClassList("tool-toggle");
                toggle.SetEnabled(!readOnly);
                string captured = item;
                toggle.RegisterValueChangedCallback(evt =>
                {
                    _setValue(captured, evt.newValue);
                    RefreshHeader();
                    _onChanged?.Invoke();
                });

                row.Add(toggle);
                _content.Add(row);
                _rows.Add((item, row, toggle));
            }

            Element.Add(header);
            Element.Add(_content);
            Refresh();
        }

        internal void Refresh()
        {
            foreach (var row in _rows)
                row.toggle.SetValueWithoutNotify(_getValue(row.name));
            RefreshHeader();
        }

        internal void Filter(string query)
        {
            _filtering = !string.IsNullOrWhiteSpace(query);
            bool anyVisible = false;
            foreach (var row in _rows)
            {
                bool visible = !_filtering
                    || row.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                row.row.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                anyVisible |= visible;
            }

            Element.style.display = anyVisible ? DisplayStyle.Flex : DisplayStyle.None;
            _content.style.display = _filtering || _expanded
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshHeader();
        }

        private void ToggleExpanded()
        {
            _expanded = !_expanded;
            _content.style.display = _filtering || _expanded
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshHeader();
        }

        private void OnMasterChanged(ChangeEvent<bool> evt)
        {
            _setAll(evt.newValue);
            foreach (var row in _rows)
                row.toggle.SetValueWithoutNotify(evt.newValue);
            RefreshHeader();
            _onChanged?.Invoke();
        }

        private void RefreshHeader()
        {
            int enabled = _rows.Count(row => _getValue(row.name));
            _disclosure.text = $"{(_expanded || _filtering ? "▼" : "▶")}  {_category}  ({enabled}/{_items.Length})";
            _disclosure.tooltip = $"{_category}: {enabled} of {_items.Length} enabled";

            _master.RemoveFromClassList("toggle-mixed");
            if (enabled == _items.Length)
                _master.SetValueWithoutNotify(true);
            else if (enabled == 0)
                _master.SetValueWithoutNotify(false);
            else
            {
                _master.SetValueWithoutNotify(false);
                _master.AddToClassList("toggle-mixed");
            }
        }
    }
}
