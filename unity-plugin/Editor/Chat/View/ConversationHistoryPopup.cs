// T23: Popup listing past Biome conversations for browse/resume.
using System;
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat.CLI;

namespace UnityMCP.Editor.Chat
{
    internal static class ConversationHistoryPopup
    {
        internal static void Show(Action<BiomeConversationMeta> onSelected)
        {
            var metas = BiomeConversationStore.Scan();
            var menu  = new GenericMenu();
            if (metas.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No history found"));
            }
            else
            {
                foreach (var m in metas)
                {
                    var captured = m;
                    menu.AddItem(
                        new GUIContent($"{m.Title}  ({m.Date})"),
                        false,
                        () => onSelected(captured));
                }
            }
            menu.ShowAsContext();
        }
    }
}
