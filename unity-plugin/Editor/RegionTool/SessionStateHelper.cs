using System.Collections.Generic;
#if UNITY_INCLUDE_TESTS
using System;
#endif
using UnityEditor;
using UnityEngine;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.RegionTool
{
    /// <summary>
    /// Shadow-persists RegionSnapshot instances to UnityEditor.SessionState so snaps
    /// survive domain reload even when the Library/ JSON write fails silently.
    /// Main-thread only. All SessionState calls are in-process and never throw.
    /// </summary>
    internal static class SessionStateHelper
    {
        const string DefaultKeyPrefix = "MCP_";
        static string _keyPrefix = DefaultKeyPrefix;
        static string IdsKey => _keyPrefix + "SnapIds";
        static string SnapPfx => _keyPrefix + "Snap_";

        internal static void Cache(RegionSnapshot snap)
        {
            SessionState.SetString(SnapPfx + snap.Id, JsonUtility.ToJson(snap));
            var ids = GetIds();
            if (!ids.Contains(snap.Id)) ids.Add(snap.Id);
            while (ids.Count > SceneRegionState.MaxRegions) ids.RemoveAt(0);
            SessionState.SetString(IdsKey, string.Join(",", ids));
        }

        internal static void RecoverInto(Dictionary<string, RegionSnapshot> cache, long cutoff)
        {
            foreach (var id in GetIds())
            {
                if (cache.ContainsKey(id)) continue;
                var json = SessionState.GetString(SnapPfx + id, "");
                if (string.IsNullOrEmpty(json)) continue;
                try
                {
                    var r = JsonUtility.FromJson<RegionSnapshot>(json);
                    if (r?.Id != null && r.CreatedTicks > cutoff)
                        cache[r.Id] = r;
                }
                catch { /* corrupt entry — skip */ }
            }
        }

        internal static void Remove(string id)
        {
            SessionState.EraseString(SnapPfx + id);
            var ids = GetIds();
            ids.Remove(id);
            SessionState.SetString(IdsKey, string.Join(",", ids));
        }

        internal static void ClearAll()
        {
            foreach (var id in GetIds())
                SessionState.EraseString(SnapPfx + id);
            SessionState.EraseString(IdsKey);
        }

#if UNITY_INCLUDE_TESTS
        internal static IDisposable IsolateForTests(string keyPrefix)
        {
            if (string.IsNullOrWhiteSpace(keyPrefix))
                throw new ArgumentException("A SessionState test prefix is required.", nameof(keyPrefix));

            var originalPrefix = _keyPrefix;
            var isolatedState = CaptureForTests(keyPrefix);
            _keyPrefix = keyPrefix;
            ClearAll();
            return new NamespaceScope(originalPrefix, keyPrefix, isolatedState);
        }

        private static StateSnapshot CaptureForTests(string keyPrefix)
        {
            var idsKey = keyPrefix + "SnapIds";
            var snapPrefix = keyPrefix + "Snap_";
            var rawIds = SessionState.GetString(idsKey, null);
            var values = new Dictionary<string, string>();
            if (rawIds != null)
            {
                foreach (var id in ParseIds(rawIds))
                {
                    if (!values.ContainsKey(id))
                        values[id] = SessionState.GetString(snapPrefix + id, null);
                }
            }
            return new StateSnapshot(keyPrefix, rawIds, values);
        }

        private sealed class StateSnapshot
        {
            private readonly string _keyPrefix;
            private readonly string _rawIds;
            private readonly Dictionary<string, string> _values;

            internal StateSnapshot(
                string keyPrefix,
                string rawIds,
                Dictionary<string, string> values)
            {
                _keyPrefix = keyPrefix;
                _rawIds = rawIds;
                _values = values;
            }

            internal void Restore()
            {
                var idsKey = _keyPrefix + "SnapIds";
                var snapPrefix = _keyPrefix + "Snap_";
                var idsToErase = new HashSet<string>(GetIds(_keyPrefix));
                foreach (var id in _values.Keys)
                    idsToErase.Add(id);
                foreach (var id in idsToErase)
                    SessionState.EraseString(snapPrefix + id);

                foreach (var pair in _values)
                {
                    if (pair.Value == null)
                        SessionState.EraseString(snapPrefix + pair.Key);
                    else
                        SessionState.SetString(snapPrefix + pair.Key, pair.Value);
                }

                if (_rawIds == null)
                    SessionState.EraseString(idsKey);
                else
                    SessionState.SetString(idsKey, _rawIds);
            }
        }

        private sealed class NamespaceScope : IDisposable
        {
            private readonly string _originalPrefix;
            private readonly string _isolatedPrefix;
            private StateSnapshot _isolatedState;

            internal NamespaceScope(
                string originalPrefix,
                string isolatedPrefix,
                StateSnapshot isolatedState)
            {
                _originalPrefix = originalPrefix;
                _isolatedPrefix = isolatedPrefix;
                _isolatedState = isolatedState;
            }

            public void Dispose()
            {
                var isolatedState = _isolatedState;
                if (isolatedState == null) return;
                _isolatedState = null;

                try
                {
                    _keyPrefix = _isolatedPrefix;
                    ClearAll();
                    isolatedState.Restore();
                }
                finally
                {
                    _keyPrefix = _originalPrefix;
                }
            }
        }

        private static IEnumerable<string> ParseIds(string rawIds) =>
            string.IsNullOrEmpty(rawIds) ? Array.Empty<string>() : rawIds.Split(',');
#endif

        static List<string> GetIds()
            => GetIds(_keyPrefix);

        static List<string> GetIds(string keyPrefix)
        {
            var s = SessionState.GetString(keyPrefix + "SnapIds", "");
            return string.IsNullOrEmpty(s)
                ? new List<string>()
                : new List<string>(s.Split(','));
        }
    }
}
