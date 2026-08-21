using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class WatchRegistry
    {
        private static readonly Dictionary<string, WatchEntry> _watches = new();
        private static readonly Queue<string> _log = new();
        private static int _idCounter;

        private const int MaxWatches = 20;
        private const int MaxLogEntries = 100;
        private const string SessionKey = "unity_mcp_watches_v1";

        public static IReadOnlyDictionary<string, WatchEntry> All => _watches;

        // Returns generated ID or null if at capacity.
        public static string Add(string path, string component, string field,
            string condition = "", string action = "log", float intervalMs = 500f)
        {
            if (_watches.Count >= MaxWatches) return null;
            _idCounter++;
            var id = "w" + _idCounter;
            _watches[id] = new WatchEntry
            {
                Id = id, Path = path, Component = component, Field = field,
                Condition = condition ?? "", Action = action ?? "log", IntervalMs = intervalMs
            };
            return id;
        }

        public static bool Remove(string id)
        {
            return _watches.Remove(id);
        }

        public static void Clear()
        {
            _watches.Clear();
            _log.Clear();
        }

#if UNITY_INCLUDE_TESTS
        /// <summary>Test seam: wipes in-memory state, leaves SessionState intact.
        /// Mirrors the effect of a domain reload on static fields.</summary>
        internal static void SimulateDomainReloadForTest()
        {
            _watches.Clear();
            _log.Clear();
            _idCounter = 0;
        }
#endif

        public static string[] DrainLog()
        {
            var result = _log.ToArray();
            _log.Clear();
            return result;
        }

        internal static void AddLogEntry(string entry)
        {
            if (_log.Count >= MaxLogEntries) _log.Dequeue();
            _log.Enqueue(entry);
        }

        public static void Save()
        {
            var wrapper = new EntryList { entries = _watches.Values.ToArray() };
            SessionState.SetString(SessionKey, JsonUtility.ToJson(wrapper));
        }

        public static void Load()
        {
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json)) return;
            var wrapper = JsonUtility.FromJson<EntryList>(json);
            if (wrapper?.entries == null) return;
            _watches.Clear();
            int maxId = 0;
            foreach (var e in wrapper.entries)
            {
                if (e?.Id == null) continue;
                _watches[e.Id] = e;
                // Restore counter so next Add doesn't collide
                if (e.Id.StartsWith("w") && int.TryParse(e.Id.Substring(1), out int n) && n > maxId)
                    maxId = n;
            }
            if (maxId > _idCounter) _idCounter = maxId;
        }

        [Serializable]
        private class EntryList { public WatchEntry[] entries; }
    }
}
