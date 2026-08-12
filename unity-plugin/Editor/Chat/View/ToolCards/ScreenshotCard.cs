// T2.1: IToolCardRenderer for screenshot + screenshot_baseline.
// Extracts the image path from ResultText and builds a thumbnail using
// ImageBlockRenderer.BuildImageElement (shared cache, click-to-open wired).
// Ownership: ImageBlockRenderer._cache owns all Texture2D instances — ScreenshotCard never
// creates or destroys textures directly. Fallback to AltLabel when file is missing or path
// is unresolvable.
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class ScreenshotCard : IToolCardRenderer
    {
        private const float MaxThumbnailHeight = 160f;
        private const string RenderedClass     = "screenshot-card-rendered";

        static ScreenshotCard()
        {
            var inst = new ScreenshotCard();
            ToolCardRendererRegistry.Register("screenshot",          inst);
            ToolCardRendererRegistry.Register("screenshot_baseline", inst);
        }

        public void OnStart(VisualElement chip, ToolCallRecord rec) { }

        public void OnUpdate(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ArgsJson == null) return;                          // chip-creation call
            if (!rec.HasResult) return;                                // result not arrived yet
            if (chip.ClassListContains(RenderedClass)) return;         // idempotency

            chip.AddToClassList(RenderedClass);

            var path = ScreenshotResultParser.ExtractPath(rec.ResultText);
            if (path == null || !File.Exists(path))
            {
                chip.Add(ImageBlockRenderer.AltLabel(path ?? rec.ResultText));
                return;
            }

            var el = ImageBlockRenderer.BuildImageElement(path, "screenshot");
            el.style.maxHeight = MaxThumbnailHeight;
            chip.Add(el);
        }
    }
}
