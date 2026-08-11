// Renders an Image block: loads PNG/JPG from disk into a Texture2D with proper lifecycle.
// T-7c-A Item 2: static _cache avoids re-loading the same file on every re-render.
// Cache is cleared by ClearCache() (TearDown) or domain reload.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityMCP.Editor.Chat
{
    public sealed class ImageBlockRenderer : IChatBlockRenderer
    {
        // Path → Texture2D. Static: survives multiple ImageBlockRenderer instances.
        // Cleared on domain reload (static initializer re-runs). ClearCache() for tests.
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        internal static void ClearCache() => _cache.Clear();

        private const float MaxWidth = 360f;

        public bool CanRender(in MdBlock block) => block.Kind == MdBlockKind.Image;

        public VisualElement Render(in MdBlock block)
        {
            var src = block.Src ?? "";
            var alt = block.Alt ?? "";

            try
            {
                var path = ResolvePath(src);
                if (!IsImageFile(path) || !File.Exists(path))
                    return AltLabel(alt);

                return BuildImageElement(path, alt);
            }
            catch (Exception)
            {
                return AltLabel(alt);
            }
        }

        private static VisualElement BuildImageElement(string path, string alt)
        {
            // Cache by path; re-load if Unity destroyed the cached texture (domain reload, etc.)
            if (!_cache.TryGetValue(path, out var tex) || tex == null)
            {
                var bytes = File.ReadAllBytes(path);
                tex = new Texture2D(2, 2);
                tex.LoadImage(bytes);
                _cache[path] = tex;
            }

            float w = Mathf.Min(MaxWidth, tex.width);
            float h = tex.width > 0 ? w * tex.height / tex.width : w;

            var img = new Image { image = tex, scaleMode = ScaleMode.ScaleToFit };
            img.AddToClassList("md-image");
            img.style.width  = w;
            img.style.height = h;

            // MANDATORY: destroy texture when the element leaves the panel.
            img.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                if (tex != null) UnityEngine.Object.DestroyImmediate(tex);
            });

            img.RegisterCallback<ClickEvent>(_ => ImageViewerWindow.Show(path));

            var container = new VisualElement();
            container.AddToClassList("md-image-container");
            container.Add(img);

            if (!string.IsNullOrEmpty(alt))
            {
                var caption = ChatLabel.Selectable(alt);
                caption.AddToClassList("md-image-alt");
                container.Add(caption);
            }

            return container;
        }

        internal static VisualElement AltLabel(string alt)
        {
            var lbl = new Label(string.IsNullOrEmpty(alt) ? "[image]" : alt);
            lbl.AddToClassList("md-image-alt");
            return lbl;
        }

        internal static bool IsImageFile(string path)
        {
            if (path == null) return false;
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tiff" or ".tif";
        }

        /// <summary>Returns an absolute path. Supports absolute or project-relative paths.</summary>
        internal static string ResolvePath(string src)
        {
            if (Path.IsPathRooted(src)) return src;
            return Path.Combine(Directory.GetCurrentDirectory(), src);
        }
    }
}
