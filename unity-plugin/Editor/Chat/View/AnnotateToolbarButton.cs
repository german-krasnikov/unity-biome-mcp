using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class AnnotateToolbarButton : IToolbarButtonProvider
    {
        public string Key         => "annotate";
        public int    Order       => 11; // right after Snap (10)
        public string ButtonLabel => "Annotate";
        public string Tooltip     => "Take screenshot and open annotation editor";
        public bool   MenuOnly    => true;

        static AnnotateToolbarButton()
            => ToolbarButtonRegistry.Register(new AnnotateToolbarButton());

        public void OnClick(EditorWindow window)
        {
            var path = ScreenshotService.Capture();
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning($"{BiomeLabel.Tag} Screenshot capture failed for annotation");
                return;
            }
            AnnotationEditorWindow.Open(path);
        }
    }
}
