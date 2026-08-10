using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>Single source of truth for the "Biome" display string and log tag.</summary>
    public static class BiomeLabel
    {
        const string PrefKey = "MCPPlugin_UseEmojiLabel";

        /// <summary>Fires on main thread when UseEmoji is toggled.</summary>
        public static event System.Action Changed;

        public static bool UseEmoji
        {
            get => EditorPrefs.GetBool(PrefKey, true);
            set
            {
                if (EditorPrefs.GetBool(PrefKey, true) == value) return;
                EditorPrefs.SetBool(PrefKey, value);
                Changed?.Invoke();
            }
        }

        /// <summary>Log tag prefix — e.g. "🧬" or "Biome".</summary>
        public static string Tag => UseEmoji ? "🧬" : "Biome";

        /// <summary>Short display name for window titles, pill, and UI labels.</summary>
        public static string DisplayName => UseEmoji ? "🧬" : "Biome";
    }
}
