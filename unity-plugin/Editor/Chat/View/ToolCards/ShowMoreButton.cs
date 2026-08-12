// T2.5: Shared "▼ N more …" button used by HierarchyCard and BashCard.
// On click: removes itself from container, calls onExpand to append remaining items.
using System;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class ShowMoreButton
    {
        public static void Append(
            VisualElement container,
            string        cssClass,
            string        labelText,
            Action        onExpand)
        {
            var showMore = new Label(labelText);
            showMore.AddToClassList(cssClass);
            showMore.RegisterCallback<ClickEvent>(_ =>
            {
                container.Remove(showMore);
                onExpand();
            });
            container.Add(showMore);
        }
    }
}
