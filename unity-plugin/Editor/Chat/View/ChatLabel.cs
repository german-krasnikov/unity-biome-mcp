// Helper for creating selectable (copyable) Labels. UIToolkit Labels are NOT
// selectable by default; enabling selection lets users copy chat text (Cmd/Ctrl+C).
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class ChatLabel
    {
        /// <summary>A Label whose text the user can select and copy.</summary>
        internal static Label Selectable(string text, bool richText = false)
        {
            var l = new Label(text);
            l.enableRichText         = richText;
            l.selection.isSelectable = true;
            l.AddToClassList("chat-text");
            return l;
        }
    }
}
