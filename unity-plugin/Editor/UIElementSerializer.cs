// UIElementSerializer: DFS VisualElement tree → compact text with ~N refids.
// VERefTable lifetime: reset on every Serialize() call and on assembly reload.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor
{
    internal class UIElementSerializer : IDisposable
    {
        private const int MaxElements = 300;

        private readonly Dictionary<int, WeakReference<VisualElement>> _refTable = new();
        private int _counter;
        // _generation is incremented on every reset, making old refs identifiable as stale.
        private int _generation;

        public UIElementSerializer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeReload;
        }

        private void OnBeforeReload() => ResetRefTable();

        public void Dispose()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeReload;
        }

        /// <summary>Clear the ref table and restart the counter. Old ~N refs become stale.</summary>
        internal void ResetRefTable()
        {
            _refTable.Clear();
            _counter = 0;
            _generation++;
        }

        /// <summary>
        /// Serialize a VisualElement tree to compact text. Resets the ref table first,
        /// so every call produces fresh ~N refs starting at ~1.
        /// </summary>
        internal string Serialize(VisualElement root, int depth = 4, string selector = null,
                                  string filter = null, bool includeInternal = false, bool showStyle = false)
        {
            ResetRefTable();

            var target = root;
            if (!string.IsNullOrEmpty(selector))
            {
                target = Dispatch(root, selector);
                if (target == null) return $"err: element not found: {selector}";
            }

            var sb = new StringBuilder();
            int count = 0;
            AppendElement(sb, target, 0, depth, showStyle, includeInternal, filter, ref count);
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// Resolve a ~N ref to its VisualElement.
        /// Returns null when the ref no longer exists in the table (stale or GC'd).
        /// Throws ArgumentException for malformed refs.
        /// </summary>
        internal VisualElement ResolveRef(string refStr)
        {
            if (refStr == null || refStr.Length < 2 || refStr[0] != '~')
                throw new ArgumentException(
                    $"Invalid VE ref '{refStr}'. Expected ~N (e.g. ~1).", nameof(refStr));

            if (!int.TryParse(refStr.Substring(1), out int id))
                throw new ArgumentException(
                    $"Invalid VE ref '{refStr}'. Expected ~N (e.g. ~1).", nameof(refStr));

            return _refTable.TryGetValue(id, out var wr) && wr.TryGetTarget(out var ve) ? ve : null;
        }

        /// <summary>Returns true when s has the form ~N (tilde followed by digits).</summary>
        internal static bool IsVERef(string s) =>
            s != null && s.Length >= 2 && s[0] == '~' &&
            int.TryParse(s.Substring(1), out _);

        // ── Private helpers ─────────────────────────────────────────────────────

        private void AppendElement(StringBuilder sb, VisualElement ve, int depth,
                                   int maxDepth, bool showStyle, bool includeInternal,
                                   string filter, ref int count)
        {
            if (count >= MaxElements)
            {
                sb.AppendLine($"... (limit {MaxElements} reached)");
                return;
            }

            if (!includeInternal && ve.name?.StartsWith("#unity-") == true) return;
            if (!string.IsNullOrEmpty(filter) && !MatchesFilter(ve, filter)) return;

            int id = ++_counter;
            _refTable[id] = new WeakReference<VisualElement>(ve);
            count++;

            sb.AppendLine(new string(' ', depth * 2) + FormatLine(ve, id, showStyle));

            if (depth >= maxDepth)
            {
                int remaining = CountVisibleChildren(ve, includeInternal);
                if (remaining > 0)
                    sb.AppendLine(new string(' ', depth * 2) + $"  ... {remaining} more (depth limit)");
                return;
            }

            foreach (var child in ve.Children())
                AppendElement(sb, child, depth + 1, maxDepth, showStyle, includeInternal, filter, ref count);
        }

        private static int CountVisibleChildren(VisualElement ve, bool includeInternal)
        {
            int n = 0;
            foreach (var child in ve.Children())
                if (includeInternal || child.name?.StartsWith("#unity-") != true)
                    n++;
            return n;
        }

        private static string FormatLine(VisualElement ve, int id, bool showStyle)
        {
            var sb = new StringBuilder();
            sb.Append(string.IsNullOrEmpty(ve.name) ? "_" : ve.name);
            sb.Append($" [{ShortType(ve)}]");

            foreach (var cls in ve.GetClasses())
                sb.Append($" .{cls}");

            // State flags
            if (ve.resolvedStyle.display == DisplayStyle.None) sb.Append(" !hidden");
            if (!ve.enabledSelf) sb.Append(" !disabled");

            // Inline content (text / value)
            var content = GetContent(ve);
            if (content != null) sb.Append($" {content}");

            // Non-default resolved styles (opt-in)
            if (showStyle)
            {
                var style = GetStyle(ve);
                if (!string.IsNullOrEmpty(style)) sb.Append($" [{style}]");
            }

            sb.Append($" ~{id}");
            return sb.ToString();
        }

        private static string GetContent(VisualElement ve)
        {
            if (ve is TextElement te && !string.IsNullOrEmpty(te.text))
                return $"\"{te.text}\"";
            if (ve is Slider sl) return $"val={sl.value:F0}";
            if (ve is Toggle tg) return tg.value ? "val=true" : "val=false";
            return null;
        }

        private static string GetStyle(VisualElement ve)
        {
            var parts = new List<string>();
            var rs = ve.resolvedStyle;
            if (rs.backgroundColor.a > 0f)
                parts.Add($"bg=#{ColorUtility.ToHtmlStringRGBA(rs.backgroundColor)}");
            if (!float.IsNaN(rs.width) && rs.width > 0f)
                parts.Add($"w={rs.width:F0}");
            if (!float.IsNaN(rs.height) && rs.height > 0f)
                parts.Add($"h={rs.height:F0}");
            return string.Join(" ", parts);
        }

        private static string ShortType(VisualElement ve)
        {
            if (ve is TemplateContainer) return "TC";
            var t = ve.GetType();
            if (t == typeof(VisualElement)) return "VE";
            if (t == typeof(TextElement)) return "TE";
            return t.Name;
        }

        private static bool MatchesFilter(VisualElement ve, string filter)
        {
            if (ve.name != null && ve.name.Contains(filter)) return true;
            foreach (var cls in ve.GetClasses())
                if (cls.Contains(filter)) return true;
            return false;
        }

        private static VisualElement Dispatch(VisualElement root, string selector)
        {
            if (selector.StartsWith(".")) return root.Q(className: selector.Substring(1));
            if (selector.StartsWith("#")) return root.Q(selector.Substring(1));
            return root.Q(selector) ?? root.Query().Where(e => e.GetType().Name == selector).First();
        }
    }
}
