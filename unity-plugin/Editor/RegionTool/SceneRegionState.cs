using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// In-memory + file-persisted registry of RegionSnapshot instances.
    /// Thread-safety: main thread only (Editor API).
    /// Persistence: Library/MCP_Regions.json (gitignored, survives domain reload + restart).
    /// </summary>
    [InitializeOnLoad]
    internal static class SceneRegionState
    {
        // ── Test seams ───────────────────────────────────────────────────────
        internal static string PersistPath = DefaultPath();
        internal static int    MaxRegions  = 20;

        static readonly Dictionary<string, RegionSnapshot> _cache = new();

        static SceneRegionState()
        {
            Load();
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        // ── Version tracking (staleness) ─────────────────────────────────────
        static int _globalVersion;

        static void OnHierarchyChanged() => _globalVersion++;

        internal static int CurrentVersion => _globalVersion;

        // ── CRUD ─────────────────────────────────────────────────────────────

        /// <summary>Add or replace. Returns the ID used.</summary>
        internal static string SetRegion(RegionSnapshot snap)
        {
            if (string.IsNullOrEmpty(snap.Id))
                throw new System.ArgumentException("snap.Id must be set by caller before SetRegion.");
            snap.SnapshotVersion = _globalVersion;
            _cache[snap.Id] = snap;
            if (_cache.Count > MaxRegions) Evict();
            Save();
            SessionStateHelper.Cache(snap);
            return snap.Id;
        }

        internal static RegionSnapshot GetById(string id)
        {
            if (id == null) return null;
            _cache.TryGetValue(id, out var snap);
            return snap;
        }

        internal static bool Remove(string id)
        {
            var removed = _cache.Remove(id);
            if (removed) { Save(); SessionStateHelper.Remove(id); }
            return removed;
        }

        internal static IReadOnlyCollection<RegionSnapshot> All => _cache.Values;

        internal static bool IsStale(string id)
        {
            var snap = GetById(id);
            return snap != null && snap.SnapshotVersion != _globalVersion;
        }

        // ── Navigation (used by RegionChipProvider) ───────────────────────────

        internal static void FrameRegion(string id)
        {
            var snap = GetById(id);
            if (snap == null) { Debug.LogWarning($"{BiomeLabel.Tag} Region not found: " + id); return; }
            var sv = SceneView.lastActiveSceneView;
            if (sv == null) return;
            var center = new Vector3(snap.CenterX, 0f, snap.CenterZ);
            var size   = Mathf.Max(snap.MaxX - snap.MinX, snap.MaxZ - snap.MinZ, 1f);
            sv.Frame(new Bounds(center, Vector3.one * size * 1.5f), instant: false);
        }

        internal static void HighlightRegion(string id) => FrameRegion(id);

        // ── Persistence ───────────────────────────────────────────────────────

        internal static void Save()
        {
            var list  = new List<RegionSnapshot>(_cache.Values);
            var store = new RegionStore { Regions = list.ToArray() };
            try
            {
                var dir = Path.GetDirectoryName(PersistPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(PersistPath, JsonUtility.ToJson(store, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{BiomeLabel.Tag} RegionStore save failed: " + e.Message);
            }
        }

        internal static void Load()
        {
            _cache.Clear();
            long cutoff = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 86400; // 24h
            if (File.Exists(PersistPath))
            {
                try
                {
                    var store = JsonUtility.FromJson<RegionStore>(File.ReadAllText(PersistPath));
                    if (store?.Regions != null)
                        foreach (var r in store.Regions)
                            if (r?.Id != null && r.CreatedTicks > cutoff)
                                _cache[r.Id] = r;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"{BiomeLabel.Tag} RegionStore load failed: " + e.Message);
                }
            }
            SessionStateHelper.RecoverInto(_cache, cutoff);
        }

        internal static void Clear()
        {
            _cache.Clear();
            SessionStateHelper.ClearAll();
            try { File.Delete(PersistPath); } catch { /* ignore */ }
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>
        /// Starts an isolated region-state transaction for a fixture. The scope
        /// preserves cache identity, version, path, limit, persisted bytes and the
        /// SessionState shadow, then restores them exactly on disposal.
        /// </summary>
        internal static IDisposable IsolateForTests(string persistPath, int maxRegions)
        {
            if (string.IsNullOrWhiteSpace(persistPath))
                throw new ArgumentException("A test persistence path is required.", nameof(persistPath));
            if (maxRegions <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxRegions));

            var isolatedPath = Path.GetFullPath(persistPath);
            if (string.Equals(
                    Path.GetFullPath(PersistPath),
                    isolatedPath,
                    StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    "The isolated path must differ from the production persistence path.",
                    nameof(persistPath));

            var cacheSnapshot = new Dictionary<string, RegionSnapshot>(_cache);
            var persistedFile = CaptureFile(PersistPath);
            var isolatedFile = CaptureFile(isolatedPath);
            var sessionStateScope = SessionStateHelper.IsolateForTests(
                "MCP_Test_Region_" + Guid.NewGuid().ToString("N") + "_");
            var snapshot = new TestStateScope(
                PersistPath,
                MaxRegions,
                _globalVersion,
                cacheSnapshot,
                sessionStateScope,
                persistedFile,
                isolatedPath,
                isolatedFile);

            try
            {
                PersistPath = isolatedPath;
                MaxRegions = maxRegions;
                _cache.Clear();
                File.Delete(isolatedPath);
                return snapshot;
            }
            catch (Exception setupError)
            {
                try
                {
                    snapshot.Dispose();
                }
                catch (Exception restoreError)
                {
                    throw new AggregateException(
                        "Region test isolation setup and rollback both failed.",
                        setupError, restoreError);
                }
                throw;
            }
        }

        /// <summary>Simulate domain reload for tests: clears cache and reloads from file + SessionState.</summary>
        internal static void SimulateDomainReload() { _cache.Clear(); Load(); }

        private static FileSnapshot CaptureFile(string path)
        {
            if (!File.Exists(path)) return new FileSnapshot(false, null);
            return new FileSnapshot(true, File.ReadAllBytes(path));
        }

        private static void RestoreFile(string path, FileSnapshot snapshot)
        {
            if (!snapshot.Existed)
            {
                File.Delete(path);
                return;
            }

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, snapshot.Bytes);
        }

        private readonly struct FileSnapshot
        {
            internal readonly bool Existed;
            internal readonly byte[] Bytes;

            internal FileSnapshot(bool existed, byte[] bytes)
            {
                Existed = existed;
                Bytes = bytes;
            }
        }

        private sealed class TestStateScope : IDisposable
        {
            private readonly string _persistPath;
            private readonly int _maxRegions;
            private readonly int _globalVersion;
            private readonly Dictionary<string, RegionSnapshot> _cacheSnapshot;
            private readonly IDisposable _sessionStateScope;
            private readonly FileSnapshot _persistedFile;
            private readonly string _isolatedPath;
            private readonly FileSnapshot _isolatedFile;
            private bool _disposed;

            internal TestStateScope(
                string persistPath,
                int maxRegions,
                int globalVersion,
                Dictionary<string, RegionSnapshot> cache,
                IDisposable sessionStateScope,
                FileSnapshot persistedFile,
                string isolatedPath,
                FileSnapshot isolatedFile)
            {
                _persistPath = persistPath;
                _maxRegions = maxRegions;
                _globalVersion = globalVersion;
                _cacheSnapshot = cache;
                _sessionStateScope = sessionStateScope;
                _persistedFile = persistedFile;
                _isolatedPath = isolatedPath;
                _isolatedFile = isolatedFile;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                var errors = new List<Exception>();

                TryRestore(() => SceneRegionState._cache.Clear(), errors);
                TryRestore(() => RestoreFile(_isolatedPath, _isolatedFile), errors);

                PersistPath = _persistPath;
                MaxRegions = _maxRegions;
                TryRestore(() =>
                {
                    SceneRegionState._cache.Clear();
                    foreach (var pair in _cacheSnapshot)
                        SceneRegionState._cache.Add(pair.Key, pair.Value);
                }, errors);
                SceneRegionState._globalVersion = _globalVersion;
                TryRestore(_sessionStateScope.Dispose, errors);

                if (!string.Equals(_persistPath, _isolatedPath, StringComparison.OrdinalIgnoreCase))
                    TryRestore(() => RestoreFile(_persistPath, _persistedFile), errors);

                if (errors.Count > 0)
                    throw new AggregateException("Region test isolation rollback failed.", errors);
            }

            private static void TryRestore(Action restore, ICollection<Exception> errors)
            {
                try { restore(); }
                catch (Exception error) { errors.Add(error); }
            }
        }
#endif

        // ── Helpers ───────────────────────────────────────────────────────────

        static string DefaultPath() =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MCP_Regions.json"));

        static void Evict()
        {
            RegionSnapshot oldest = null;
            foreach (var r in _cache.Values)
                if (oldest == null || r.CreatedTicks < oldest.CreatedTicks)
                    oldest = r;
            if (oldest != null) _cache.Remove(oldest.Id);
        }

        [Serializable]
        private sealed class RegionStore
        {
            public RegionSnapshot[] Regions;
        }
    }
}
