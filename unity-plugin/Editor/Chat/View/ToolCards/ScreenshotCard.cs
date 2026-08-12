// T2.1: IToolCardRenderer for screenshot + screenshot_baseline.
// Extracts the image path from ResultText and builds a thumbnail using
// ImageBlockRenderer.BuildImageElement (shared cache, click-to-open wired).
// Ownership: ImageBlockRenderer._cache owns all Texture2D instances — ScreenshotCard never
// creates or destroys textures directly. Fallback to AltLabel when file is missing or path
// is unresolvable.
//
// T2.5: Extends ToolCardBase. The base owns the idempotency guard and marker-last rule.
// TryBuildContent lets IOExceptions propagate — base catches them and leaves the card
// un-marked so the next OnUpdate can retry (TOCTOU scenario).
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal sealed class ScreenshotCard : ToolCardBase
    {
        private const float MaxThumbnailHeight = 160f;

        static ScreenshotCard()
        {
            var inst = new ScreenshotCard();
            ToolCardRendererRegistry.Register("screenshot",          inst);
            ToolCardRendererRegistry.Register("screenshot_baseline", inst);
        }

        internal ScreenshotCard() : base("screenshot-card-rendered") { }

        protected override bool TryBuildContent(VisualElement chip, ToolCallRecord rec)
        {
            if (rec.ArgsJson == null || !rec.HasResult) return false; // not ready

            var path = ScreenshotResultParser.ExtractPath(rec.ResultText);
            if (path == null || !File.Exists(path))
            {
                chip.Add(ImageBlockRenderer.AltLabel(path ?? rec.ResultText));
                return true;
            }

            // May throw IOException / UnauthorizedAccessException (TOCTOU).
            // Base catches it, marker not set → retry on next frame.
            var el = ImageBlockRenderer.BuildImageElement(path, "screenshot");
            el.style.maxHeight = MaxThumbnailHeight;
            chip.Add(el);
            return true;
        }
    }
}
