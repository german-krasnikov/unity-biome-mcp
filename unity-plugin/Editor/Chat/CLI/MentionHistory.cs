// Persists path → last-commit-timestamp for ByRecency sort in @-mention popup.
// File: Library/MCP_MentionHistory.json. Cap: 100 entries. Main-thread only.
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UnityMCP.Editor.Chat
{
    internal sealed class MentionHistory
    {
        private const int MaxEntries = 100;

        private static string DefaultPath =>
            Path.Combine(Application.dataPath, "..", "Library", "MCP_MentionHistory.json");

        private readonly string _path;
        private readonly Func<long> _clock;
        private Dictionary<string, long> _entries; // null = not yet loaded (lazy)

        internal MentionHistory(string path = null, Func<long> clock = null)
        {
            _path  = path ?? DefaultPath;
            _clock = clock ?? (() => DateTime.UtcNow.Ticks);
        }

        /// <summary>Record that the user committed a chip at this path. Saves immediately.</summary>
        internal void RecordCommit(string path)
        {
            EnsureLoaded();
            _entries[path] = _clock();
            Evict();
            Save();
        }

        /// <summary>Returns the last-commit timestamp for path, or 0 if unknown.</summary>
        internal long GetTimestamp(string path)
        {
            EnsureLoaded();
            return _entries.TryGetValue(path, out var ts) ? ts : 0L;
        }

        // ── private ──────────────────────────────────────────────────────────

        private void EnsureLoaded()
        {
            if (_entries != null) return;
            _entries = LoadFromDisk(_path);
        }

        private void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new HistoryData(_entries);
                File.WriteAllText(_path, JsonUtility.ToJson(data, prettyPrint: false));
            }
            catch { /* best-effort */ }
        }

        private void Evict()
        {
            if (_entries.Count <= MaxEntries) return;

            // Remove oldest entries until we reach MaxEntries
            var sorted = new List<KeyValuePair<string, long>>(_entries);
            sorted.Sort((a, b) => a.Value.CompareTo(b.Value)); // ascending by timestamp → oldest first

            int toRemove = _entries.Count - MaxEntries;
            for (int i = 0; i < toRemove; i++)
                _entries.Remove(sorted[i].Key);
        }

        private static Dictionary<string, long> LoadFromDisk(string path)
        {
            if (!File.Exists(path))
                return new Dictionary<string, long>();
            try
            {
                var json = File.ReadAllText(path);
                var data = JsonUtility.FromJson<HistoryData>(json);
                return data?.ToDictionary() ?? new Dictionary<string, long>();
            }
            catch
            {
                return new Dictionary<string, long>();
            }
        }

        // ── serialization wrapper (JsonUtility needs arrays, not Dictionary) ─

        [Serializable]
        private sealed class HistoryData
        {
            public string[] paths      = Array.Empty<string>();
            public long[]   timestamps = Array.Empty<long>();

            public HistoryData() { }

            public HistoryData(Dictionary<string, long> entries)
            {
                paths      = new string[entries.Count];
                timestamps = new long[entries.Count];
                int i = 0;
                foreach (var kv in entries)
                {
                    paths[i]      = kv.Key;
                    timestamps[i] = kv.Value;
                    i++;
                }
            }

            public Dictionary<string, long> ToDictionary()
            {
                var d = new Dictionary<string, long>();
                if (paths == null || timestamps == null) return d;
                int len = Math.Min(paths.Length, timestamps.Length);
                for (int i = 0; i < len; i++)
                    if (!string.IsNullOrEmpty(paths[i]))
                        d[paths[i]] = timestamps[i];
                return d;
            }
        }
    }
}
