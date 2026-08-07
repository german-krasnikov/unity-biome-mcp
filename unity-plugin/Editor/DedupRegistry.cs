// P-322: Operation-ID deduplication registry for mutation retry safety.
// Prevents re-execution of a command whose op_id was already processed.
// Thread-safe for Unity's single-threaded editor main loop.
using System;
using System.Collections.Generic;

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

        private readonly Dictionary<string, (long ts, string result)> _store = new Dictionary<string, (long, string)>(Capacity);
        private readonly Func<long> _clock;
        private int _sinceLastEvict;

        internal DedupRegistry(Func<long> clock = null)
        {
            _clock = clock ?? (() => DateTime.UtcNow.Ticks);
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
    }
}
