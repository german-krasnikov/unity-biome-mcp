// T-5.2: collapsed Foldout displaying extended thinking / reasoning text.
// Ephemeral — never added to _entries, not reloaded after domain reload.
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal static class ThinkingBlock
    {
        internal static Foldout Build(string text)
        {
            var foldout = new Foldout { value = false, text = "Reasoning…" };
            foldout.AddToClassList("thinking-block");
            foldout.Add(ChatLabel.Selectable(text));
            return foldout;
        }
    }
}
