// Partial: bullet and ordered list rendering for MarkdownBlockRenderer.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed partial class MarkdownBlockRenderer
    {
        private static VisualElement RenderList(in MdBlock b)
        {
            var list = new VisualElement(); list.AddToClassList("md-list");

            if (b.Lines == null) return list;

            bool ordered = b.Kind == MdBlockKind.OrderedList;
            int  start   = ordered ? b.Level : 1;

            for (int i = 0; i < b.Lines.Count; i++)
            {
                var row = new VisualElement(); row.AddToClassList("md-list-row");
                int depth = (b.Depths != null && i < b.Depths.Count) ? b.Depths[i] : 0;
                row.style.paddingLeft = 12 + depth * 20; // 12=top-level, +20 per depth level

                var marker = new Label(ordered ? $"{start + i}." : "•");
                marker.AddToClassList("md-list-marker");

                var content = MixedParagraphRenderer.InlineElement(b.Lines[i], "md-list-content");

                row.Add(marker);
                row.Add(content);
                list.Add(row);
            }

            return list;
        }
    }
}
