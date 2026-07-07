// Instance-based asset preview service.
// Single EditorApplication.update daemon; max 5 concurrent; dedupes in-flight requests;
// cancellation-safe; invalidates on import / project-window changes.
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class AssetPreviewService : IAssetPreviewService, IDisposable
    {
        const int MaxConcurrent = 5;
        const int MaxAttempts   = 15;

        readonly Dictionary<string, Texture2D> _cache = new();
        readonly Queue<Request> _queue = new();
        readonly Dictionary<string, Active> _activeByPath = new();
        readonly List<Active> _active = new();

        bool _hooked;
        bool _disposed;

        // Instance test seams — avoid cross-test static mutation.
        internal Func<string, UnityEngine.Object> AssetLoader;
        internal Func<UnityEngine.Object, Texture2D> PreviewExtractor;
        internal static bool AutoHookEditorUpdate = true;

#if UNITY_INCLUDE_TESTS
        internal int ActiveCountForTests => _active.Count;
        internal int QueueCountForTests  => _queue.Count;
        internal void ProcessForTests()  => OnUpdate();
        internal int CacheCountForTests  => _cache.Count;
#endif

        public void RequestPreview(string assetPath, Action<Texture2D> onDone, CancellationToken ct = default)
        {
            if (_disposed || string.IsNullOrEmpty(assetPath) || onDone == null) return;
            if (ct.IsCancellationRequested) return;

            if (_cache.TryGetValue(assetPath, out var cached))
            {
                InvokeSafely(onDone, cached, ct);
                return;
            }

            if (_activeByPath.TryGetValue(assetPath, out var active))
            {
                active.Callbacks.Add(new Callback(onDone, ct));
                EnsureHook();
                return;
            }

            _queue.Enqueue(new Request(assetPath, onDone, ct));
            EnsureHook();
        }

        public void Invalidate(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return;
            _cache.Remove(assetPath);
        }

        public void Clear()
        {
            _cache.Clear();
            _queue.Clear();
            _activeByPath.Clear();
            _active.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_hooked)
            {
                EditorApplication.update -= OnUpdate;
                AssetPreviewAssetPostprocessor.OnAssetsChanged -= OnAssetsChanged;
                EditorApplication.projectChanged -= OnProjectWindowChanged;
                _hooked = false;
            }
            Clear();
        }

        // ── lifecycle hooks ─────────────────────────────────────────────────────

        void EnsureHook()
        {
            if (_hooked) return;
            _hooked = true;
            if (AutoHookEditorUpdate)
                EditorApplication.update += OnUpdate;
            AssetPreviewAssetPostprocessor.OnAssetsChanged += OnAssetsChanged;
            EditorApplication.projectChanged += OnProjectWindowChanged;
        }

        void OnProjectWindowChanged() => Clear();

        void OnAssetsChanged(string[] imported, string[] deleted, string[] moved)
        {
            if (deleted.Length > 0 || moved.Length > 0)
            {
                Clear();
                return;
            }
            foreach (var path in imported)
                Invalidate(path);
        }

        // ── daemon ──────────────────────────────────────────────────────────────

        void OnUpdate()
        {
            if (_disposed) return;

            while (_active.Count < MaxConcurrent && _queue.Count > 0)
            {
                var req = _queue.Dequeue();
                if (req.Ct.IsCancellationRequested) continue;

                if (_cache.TryGetValue(req.Path, out var hit))
                {
                    InvokeSafely(req.OnDone, hit, req.Ct);
                    continue;
                }

                if (_activeByPath.TryGetValue(req.Path, out var existing))
                {
                    existing.Callbacks.Add(new Callback(req.OnDone, req.Ct));
                    continue;
                }

                var asset = (AssetLoader ?? AssetDatabase.LoadAssetAtPath<UnityEngine.Object>)(req.Path);
                if (asset == null)
                {
                    InvokeSafely(req.OnDone, null, req.Ct);
                    continue;
                }

                var preview = (PreviewExtractor ?? AssetPreview.GetAssetPreview)(asset);
                if (preview != null)
                {
                    _cache[req.Path] = preview;
                    InvokeSafely(req.OnDone, preview, req.Ct);
                    continue;
                }

                var active = new Active(req.Path, asset, new List<Callback> { new Callback(req.OnDone, req.Ct) });
                _activeByPath[req.Path] = active;
                _active.Add(active);
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var a = _active[i];
                a.Callbacks.RemoveAll(c => c.Ct.IsCancellationRequested);
                if (a.Callbacks.Count == 0)
                {
                    _active.RemoveAt(i);
                    _activeByPath.Remove(a.Path);
                    continue;
                }

                var tex = (PreviewExtractor ?? AssetPreview.GetAssetPreview)(a.Asset);
                if (tex != null)
                {
                    Complete(a, tex);
                    _active.RemoveAt(i);
                    continue;
                }

                if (++a.Attempts >= MaxAttempts)
                {
                    Complete(a, null);
                    _active.RemoveAt(i);
                }
            }
        }

        void Complete(Active active, Texture2D texture)
        {
            _activeByPath.Remove(active.Path);
            if (texture != null)
                _cache[active.Path] = texture;

            foreach (var cb in active.Callbacks)
                InvokeSafely(cb.OnDone, texture, cb.Ct);
        }

        static void InvokeSafely(Action<Texture2D> callback, Texture2D texture, CancellationToken ct)
        {
            if (ct.IsCancellationRequested) return;
            try { callback(texture); }
            catch (Exception e) { Debug.LogException(e); }
        }

        // ── data ────────────────────────────────────────────────────────────────

        readonly struct Request
        {
            public readonly string Path;
            public readonly Action<Texture2D> OnDone;
            public readonly CancellationToken Ct;

            public Request(string path, Action<Texture2D> onDone, CancellationToken ct)
            {
                Path   = path;
                OnDone = onDone;
                Ct     = ct;
            }
        }

        readonly struct Callback
        {
            public readonly Action<Texture2D> OnDone;
            public readonly CancellationToken Ct;

            public Callback(Action<Texture2D> onDone, CancellationToken ct)
            {
                OnDone = onDone;
                Ct     = ct;
            }
        }

        sealed class Active
        {
            public readonly string Path;
            public readonly UnityEngine.Object Asset;
            public readonly List<Callback> Callbacks;
            public int Attempts;

            public Active(string path, UnityEngine.Object asset, List<Callback> callbacks)
            {
                Path      = path;
                Asset     = asset;
                Callbacks = callbacks;
            }
        }

        // ── nested postprocessor ─────────────────────────────────────────────────
        sealed class AssetPreviewAssetPostprocessor : AssetPostprocessor
        {
            internal static event Action<string[], string[], string[]> OnAssetsChanged;

            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
                string[] movedAssets, string[] movedFromAssetPaths)
            {
                try { OnAssetsChanged?.Invoke(importedAssets, deletedAssets, movedAssets); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
