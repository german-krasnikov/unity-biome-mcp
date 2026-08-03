// Extension → IAssetViewer registry. Dispatches asset chip navigation to the right viewer.
// [InitializeOnLoad] registers built-in viewers and preview builders.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace UnityMCP.Editor.Chat
{
    [InitializeOnLoad]
    internal static class AssetViewerFactory
    {
        private static readonly Dictionary<string, IAssetViewer> _registry =
            new Dictionary<string, IAssetViewer>(StringComparer.OrdinalIgnoreCase);

        static AssetViewerFactory()
        {
            RegisterBuiltIns();
            AssetChipProviderBase.ViewerLauncher  = TryShow;
            ImageChipProvider.ImageFallbackViewer = path => ImageViewerWindow.Show(path);
        }

        private static void RegisterBuiltIns()
        {
            // ── IAssetViewer window dispatch ────────────────────────────────────
            var model = new ModelViewerWindow.ViewerAdapter();
            Register(".fbx",   model);
            Register(".obj",   model);
            Register(".blend", model);
            Register(".dae",   model);

            var sprite = new SpriteViewerWindow.ViewerAdapter();
            Register(".png",  sprite);
            Register(".jpg",  sprite);
            Register(".jpeg", sprite);
            Register(".tga",  sprite);
            Register(".exr",  sprite);
            Register(".bmp",  sprite);
            Register(".gif",  sprite);

            var audio = new AudioViewerWindow.ViewerAdapter();
            Register(".wav",  audio);
            Register(".mp3",  audio);
            Register(".ogg",  audio);
            Register(".aiff", audio);

            Register(".prefab", new PrefabViewerWindow.ViewerAdapter());

            // ── Inline preview builders (Dev 2 registry) ────────────────────────
            PreviewBuilderRegistry.Register(new ImagePreviewBuilder(),      priority: 100);
            PreviewBuilderRegistry.Register(new AudioPreviewBuilder(),      priority: 100);
            PreviewBuilderRegistry.Register(new HierarchyPreviewBuilder(),  priority: 100);
            PreviewBuilderRegistry.Register(new ModelPreviewBuilder(),      priority: 100);
            PreviewBuilderRegistry.Register(new PrefabPreviewBuilder(),     priority: 100);
            PreviewBuilderRegistry.Register(new AssetPreviewBuilder(),      priority: int.MaxValue);
        }

        internal static void Register(string ext, IAssetViewer viewer)
        {
            if (string.IsNullOrEmpty(ext)) return;
            _registry[ext.ToLowerInvariant()] = viewer;
        }

        internal static void Unregister(string ext)
        {
            if (!string.IsNullOrEmpty(ext))
                _registry.Remove(ext.ToLowerInvariant());
        }

        internal static IAssetViewer ForPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var ext = Path.GetExtension(assetPath);
            if (string.IsNullOrEmpty(ext)) return null;
            _registry.TryGetValue(ext.ToLowerInvariant(), out var viewer);
            return viewer;
        }

        internal static bool TryShow(string assetPath)
        {
            var viewer = ForPath(assetPath);
            if (viewer == null) return false;
            viewer.Show(assetPath);
            return true;
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Preserves the exact viewer dispatch table and navigation delegates.
        /// The original viewer instances are restored; built-ins are not reconstructed.
        /// </summary>
        internal static IDisposable PreserveStateForTests()
        {
            var registry = new List<KeyValuePair<string, IAssetViewer>>(_registry);
            return new TestIsolationScope(
                registry,
                AssetChipProviderBase.ViewerLauncher,
                ImageChipProvider.ImageFallbackViewer);
        }

        private sealed class TestIsolationScope : IDisposable
        {
            private List<KeyValuePair<string, IAssetViewer>> _registrySnapshot;
            private Func<string, bool> _viewerLauncher;
            private Action<string> _imageFallbackViewer;

            internal TestIsolationScope(
                List<KeyValuePair<string, IAssetViewer>> registrySnapshot,
                Func<string, bool> viewerLauncher,
                Action<string> imageFallbackViewer)
            {
                _registrySnapshot = registrySnapshot;
                _viewerLauncher = viewerLauncher;
                _imageFallbackViewer = imageFallbackViewer;
            }

            public void Dispose()
            {
                if (_registrySnapshot == null) return;

                _registry.Clear();
                foreach (var pair in _registrySnapshot)
                    _registry.Add(pair.Key, pair.Value);
                AssetChipProviderBase.ViewerLauncher = _viewerLauncher;
                ImageChipProvider.ImageFallbackViewer = _imageFallbackViewer;

                _registrySnapshot = null;
                _viewerLauncher = null;
                _imageFallbackViewer = null;
            }
        }
#endif

        // Test seam: clear all registrations.
        internal static void Reset()
        {
            _registry.Clear();
            AssetChipProviderBase.ViewerLauncher  = null;
            ImageChipProvider.ImageFallbackViewer = null;
            PreviewBuilderRegistry.Reset();
        }

        // Test seam: re-run built-in registration on a clean registry.
        internal static void ReRegisterBuiltIns()
        {
            _registry.Clear();
            PreviewBuilderRegistry.Reset();
            RegisterBuiltIns();
        }
    }
}
