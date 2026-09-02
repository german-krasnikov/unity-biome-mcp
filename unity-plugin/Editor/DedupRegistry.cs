// P-322: Operation-ID deduplication registry for mutation retry safety.
// Prevents re-execution of a command whose op_id was already processed.
// Thread-safe for Unity's single-threaded editor main loop.
//
// DEV-64: a domain reload (triggered by the very C# script edit a mutation
// can cause) wipes CommandRouter's plain-static instance. Without help, a
// Python retry (retry_op_id, DEV-59) that lands right after reload finds an
// empty cache and re-executes the mutation. SessionState is unmanaged native
// storage that survives domain reload (cleared only on Editor process
// restart), so entries are mirrored there on every register and restored in
// the constructor.
using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Tracks processed operation IDs so that retried requests (with matching
    /// retry_op_id) are detected and their execution is suppressed.
    /// </summary>
    internal sealed class DedupRegistry
    {
        // Public constants — tests use these directly.
        internal const int Capacity = 512;
        internal const double TtlSeconds = 300.0;
        private const int EvictEveryN = 16;

        // DEV-64: persisted snapshot is capped well below Capacity — this is a
        // reload-survival bridge for in-flight retries, not a full mirror of
        // the in-memory dedup window. Keeps SessionState small.
        internal const string SessionKey = "MCP_DedupRegistry_v1";
        private const int PersistCapacity = 64;

        private readonly Dictionary<string, (long ts, string result)> _store = new Dictionary<string, (long, string)>(Capacity);
        private readonly Queue<string> _persistOrder = new Queue<string>();
        private readonly Func<long> _clock;
        private int _sinceLastEvict;

        internal DedupRegistry(Func<long> clock = null)
        {
            _clock = clock ?? (() => DateTime.UtcNow.Ticks);
            Restore();
        }

        internal int Count => _store.Count;

        internal bool TryRegister(string opId, string result = null)
        {
            if (string.IsNullOrEmpty(opId))
                return true;

            if (_store.TryGetValue(opId, out var entry))
            {
                long ageMs = (_clock() - entry.ts) / TimeSpan.TicksPerMillisecond;
                if (ageMs < TtlSeconds * 1000)
                    return false;
                _store.Remove(opId);
            }

            if (_store.Count >= Capacity)
                EvictOldest();

            _store[opId] = (_clock(), result);
            TrackPersistOrder(opId);
            Persist();

            _sinceLastEvict++;
            if (_sinceLastEvict >= EvictEveryN)
            {
                Evict();
                _sinceLastEvict = 0;
            }

            return true;
        }

        internal string TryGetResult(string opId)
        {
            if (string.IsNullOrEmpty(opId) || !_store.TryGetValue(opId, out var entry))
                return null;
            long ageMs = (_clock() - entry.ts) / TimeSpan.TicksPerMillisecond;
            return ageMs < TtlSeconds * 1000 ? entry.result : null;
        }

        internal void Evict()
        {
            long cutoff = _clock() - (long)(TtlSeconds * TimeSpan.TicksPerSecond);
            var toRemove = new List<string>();
            foreach (var kv in _store)
            {
                if (kv.Value.ts < cutoff)
                    toRemove.Add(kv.Key);
            }
            foreach (var key in toRemove)
                _store.Remove(key);
        }

        private void EvictOldest()
        {
            string oldest = null;
            long minTs = long.MaxValue;
            foreach (var kv in _store)
            {
                if (kv.Value.ts < minTs) { minTs = kv.Value.ts; oldest = kv.Key; }
            }
            if (oldest != null)
                _store.Remove(oldest);
        }

        private void TrackPersistOrder(string opId)
        {
            _persistOrder.Enqueue(opId);
            while (_persistOrder.Count > PersistCapacity)
                _persistOrder.Dequeue();
        }

        // DEV-64: mirror the most-recently-registered entries into SessionState
        // so a fresh instance built after a domain reload can restore them.
        private void Persist()
        {
            var sb = new System.Text.StringBuilder("[");
            int n = 0;
            foreach (var opId in _persistOrder)
            {
                if (!_store.TryGetValue(opId, out var entry)) continue;
                if (n++ > 0) sb.Append(',');
                sb.Append("{\"id\":\"").Append(JsonHelper.EscapeJson(opId))
                  .Append("\",\"ts\":").Append(entry.ts)
                  .Append(",\"result\":")
                  .Append(entry.result == null ? "null" : "\"" + JsonHelper.EscapeJson(entry.result) + "\"")
                  .Append('}');
            }
            sb.Append(']');
            SessionState.SetString(SessionKey, sb.ToString());
        }

        // DEV-64: rebuild from the SessionState snapshot on construction. Entries
        // already past TTL relative to the current clock are skipped — restore
        // must not resurrect stale dedup state forever.
        private void Restore()
        {
            var json = SessionState.GetString(SessionKey, "");
            if (string.IsNullOrEmpty(json) || json == "[]")
                return;

            int pos = 0;
            string obj;
            while ((obj = JsonHelper.ExtractNextArrayObject(json, ref pos)) != null)
            {
                var id = JsonHelper.ExtractString(obj, "id");
                if (string.IsNullOrEmpty(id)) continue;
                if (!long.TryParse(JsonHelper.ExtractString(obj, "ts"), out var ts)) continue;

                long ageMs = (_clock() - ts) / TimeSpan.TicksPerMillisecond;
                if (ageMs >= TtlSeconds * 1000) continue;

                _store[id] = (ts, JsonHelper.ExtractString(obj, "result"));
                _persistOrder.Enqueue(id);
            }
        }
    }
}
