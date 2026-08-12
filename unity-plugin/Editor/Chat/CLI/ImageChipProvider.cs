// Chip kind for external image files dropped from Finder or pasted from clipboard.
// Priority 50 — beats all asset providers (they need non-null obj).
// CanHandle: obj must be null AND path must have an image extension.
// FormatPayload returns "" — images are sent as binary image_url blocks, not text refs.
using System;
using System.IO;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ImageChipProvider : IChipKindProvider
    {
        private static readonly string[] _exts = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tiff", ".tif" };

        // Seam: View assembly wires this to ImageViewerWindow.Show at [InitializeOnLoad].
        internal static Action<string> ImageFallbackViewer;

        public string Key        => ChipKindKeys.Image;
        public int    Priority   => 50;
        public string IconName   => "d_Texture Icon";
        public string HexColor   => "#f472b6";
        public string DefaultDepth => "path";
        public string[] BarePathExtensions => _exts;

        public bool CanHandle(UnityEngine.Object obj, string assetPath)
        {
            if (obj != null || string.IsNullOrEmpty(assetPath)) return false;
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            foreach (var e in _exts) if (ext == e) return true;
            return false;
        }

        public ChipData Create(UnityEngine.Object obj, string assetPath)
            => new ChipData(Key, assetPath, Path.GetFileName(assetPath), 0);

        // Images go as binary image_url blocks — no text bracket needed.
        public string FormatPayload(ChipData chip, ChipPayloadContext ctx) => "";

        public void Navigate(string reference)
        {
            if (AssetChipProviderBase.ViewerLauncher?.Invoke(reference) == true) return;
            ImageFallbackViewer?.Invoke(reference);
        }

        public void Ping(string reference)
        {
            // Images have no project asset to ping; reuse the viewer fallback.
            Navigate(reference);
        }

        public void AppendContextMenuItems(DropdownMenu menu, string reference)
        {
            menu.AppendAction("Ping in Project", _ => Ping(reference));
            menu.AppendAction("Open in Viewer",  _ => Navigate(reference));
        }
    }
}
