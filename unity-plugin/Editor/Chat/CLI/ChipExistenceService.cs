// Instance-based existence checker for chip paths.
// Resolves async via EditorApplication.update; invalidates on project/hierarchy/scene/asset changes.
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnityMCP.Editor.Chat
{
    internal sealed class ChipExistenceService : IChipExistenceService
    {
        readonly Dictionary<(string kind, string path), bool> _cache = new();
        readonly HashSet<(string kind, string path)> _pending = new();
        readonly Dictionary<(string kind, string path), List<Action<bool>>> _subscribers = new();
        bool _hookInstalled;
        bool _disposed;

        internal event Action<string, string, bool> OnResolved;

        /// <inheritdoc />
        public bool? Exists(string kindKey, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (_cache.TryGetValue((kindKey, path), out var exists)) return exists;
            _pending.Add((kindKey, path));
            EnsureHook();
            return null;
        }

        /// <inheritdoc />
        public IDisposable Observe(string kindKey, string path, Action<bool> onChanged)
        {
            if (onChanged == null) return new SubscriptionToken(this, kindKey, path, null);

            var key = (kindKey, path);
            if (!_subscribers.TryGetValue(key, out var list))
            {
                list = new List<Action<bool>>();
                _subscribers[key] = list;
            }
            list.Add(onChanged);

            // Synchronous callback if already cached.
            if (_cache.TryGetValue(key, out var cached))
            {
                try { onChanged(cached); }
                catch (Exception e) { Debug.LogException(e); }
            }
            else
            {
                _pending.Add(key);
                EnsureHook();
            }

            return new SubscriptionToken(this, kindKey, path, onChanged);
        }

        /// <inheritdoc />
        public void Invalidate(string kindKey, string path)
        {
            _cache.Remove((kindKey, path));
            _pending.Remove((kindKey, path));
        }

        /// <inheritdoc />
        public void Clear()
        {
            _cache.Clear();
            _pending.Clear();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            UninstallHooks();
            Clear();
            ClearEvents();
        }

        internal void ClearEvents()
        {
            OnResolved = null;
            _subscribers.Clear();
        }

#if UNITY_INCLUDE_TESTS
        internal void ForceProcessForTests() => ProcessBatch();
#endif

        // ── lifecycle ─────────────────────────────────────────────────────────

        void EnsureHook()
        {
            if (_hookInstalled || _disposed) return;
            _hookInstalled = true;
            EditorApplication.update += ProcessBatch;
            EditorApplication.projectChanged += OnProjectWindowChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            ChipExistenceAssetPostprocessor.OnAssetsChanged += OnAssetsChanged;
        }

        void UninstallHooks()
        {
            if (!_hookInstalled) return;
            _hookInstalled = false;
            EditorApplication.update -= ProcessBatch;
            EditorApplication.projectChanged -= OnProjectWindowChanged;
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            ChipExistenceAssetPostprocessor.OnAssetsChanged -= OnAssetsChanged;
        }

        void OnProjectWindowChanged() => Clear();
        void OnHierarchyChanged() => Clear();
        void OnSceneOpened(Scene scene, OpenSceneMode mode) => Clear();
        void OnAssetsChanged(string[] imported, string[] deleted, string[] moved) => Clear();

        // ── resolution ────────────────────────────────────────────────────────

        void ProcessBatch()
        {
            if (_pending.Count == 0) return;
            int processed = 0;
            var snapshot = new List<(string, string)>(_pending);
            foreach (var (kind, path) in snapshot)
            {
                if (processed >= 20) break;
                var key = (kind, path);
                if (!_pending.Contains(key)) continue;

                var exists = ResolveExists(kind, path);
                _cache[key] = exists;
                _pending.Remove(key);

                Notify(key.kind, key.path, exists);
                processed++;
            }
        }

        void Notify(string kind, string path, bool exists)
        {
            try { OnResolved?.Invoke(kind, path, exists); }
            catch (Exception e) { Debug.LogException(e); }

            if (_subscribers.TryGetValue((kind, path), out var list))
            {
                foreach (var cb in list)
                {
                    try { cb(exists); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }
        }

        bool ResolveExists(string kindKey, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            try
            {
                if (kindKey == ChipKindKeys.Hierarchy)
                    return SceneObjectFinder.FindGameObject(path) != null;
                if (kindKey == ChipKindKeys.Image)
                    return ResolveImageExists(path);
                // Name-only scene ref (e.g. "MyScene" with no '/' and no ".unity")
                if (kindKey == ChipKindKeys.Scene
                    && !path.Contains("/") && !path.EndsWith(".unity"))
                    return SceneChipProvider.FindScenePathByExactName(path) != null;
                return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null;
            }
            catch
            {
                return false;
            }
        }

        static bool ResolveImageExists(string path)
        {
            if (System.IO.File.Exists(path)) return true;
            return AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path) != null;
        }

        // ── subscription token ────────────────────────────────────────────────

        sealed class SubscriptionToken : IDisposable
        {
            readonly ChipExistenceService _service;
            readonly (string kind, string path) _key;
            readonly Action<bool> _callback;
            bool _disposed;

            internal SubscriptionToken(ChipExistenceService service, string kind, string path,
                Action<bool> callback)
            {
                _service = service;
                _key = (kind, path);
                _callback = callback;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_callback == null) return;
                if (_service._subscribers.TryGetValue(_key, out var list))
                {
                    list.Remove(_callback);
                    if (list.Count == 0)
                        _service._subscribers.Remove(_key);
                }
            }
        }
    }
}
