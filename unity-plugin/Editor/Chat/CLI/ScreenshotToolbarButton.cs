// Toolbar button that captures the current Unity view and inserts it as an image chip.
// Wired into MCPChatWindow via OnScreenshotCaptured seam (set in OnEnable/OnDisable).
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class ScreenshotToolbarButton : IToolbarButtonProvider
    {
        public string Key         => "screenshot";
        public int    Order       => 10;
        public string ButtonLabel => "Snap";
        public string Tooltip     => "Capture screenshot and add to message";
        public bool   MenuOnly    => true;

        /// <summary>Set by MCPChatWindow.OnEnable to insert the stored path as a chip.</summary>
        internal static System.Action<string> OnScreenshotCaptured;

        static ScreenshotToolbarButton()
            => ToolbarButtonRegistry.Register(new ScreenshotToolbarButton());

        public void OnClick(EditorWindow window)
        {
            var path = ScreenshotService.Capture();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Screenshot capture failed");
                return;
            }
            OnScreenshotCaptured?.Invoke(path);
        }
    }
}
