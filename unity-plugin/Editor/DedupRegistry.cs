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

        private readonly Dictionary<string, long> _store = new Dictionary<string, long>(Capacity);
        private readonly Func<long> _clock;
        private int _sinceLastEvict;

        internal DedupRegistry(Func<long> clock = null)
        {
            _clock = clock ?? (() => DateTime.UtcNow.Ticks);
        }

        internal int Count => _store.Count;

        /// <summary>
        /// Attempts to register op_id as "executed".
        /// Returns true (first time) or false (duplicate within TTL).
        /// </summary>
        internal bool TryRegister(string opId)
        {
            if (string.IsNullOrEmpty(opId))
                return true;

            if (_store.TryGetValue(opId, out var ts))
            {
                // Already registered — check if still within TTL.
                long ageMs = (_clock() - ts) / TimeSpan.TicksPerMillisecond;
                if (ageMs < TtlSeconds * 1000)
                    return false;  // duplicate; suppress re-execution
                // Expired — remove and re-register below.
                _store.Remove(opId);
            }

            // Evict stale entries before adding to stay within capacity.
            if (_store.Count >= Capacity)
                EvictOldest();

            _store[opId] = _clock();

            _sinceLastEvict++;
            if (_sinceLastEvict >= EvictEveryN)
            {
                Evict();
                _sinceLastEvict = 0;
            }

            return true;
        }

        /// <summary>Removes all entries older than TTL.</summary>
        internal void Evict()
        {
            long cutoff = _clock() - (long)(TtlSeconds * TimeSpan.TicksPerSecond);
            var toRemove = new List<string>();
            foreach (var kv in _store)
            {
                if (kv.Value < cutoff)
                    toRemove.Add(kv.Key);
            }
            foreach (var key in toRemove)
                _store.Remove(key);
        }

        /// <summary>Evicts the oldest entry when at capacity.</summary>
        private void EvictOldest()
        {
            string oldest = null;
            long minTs = long.MaxValue;
            foreach (var kv in _store)
            {
                if (kv.Value < minTs) { minTs = kv.Value; oldest = kv.Key; }
            }
            if (oldest != null)
                _store.Remove(oldest);
        }
    }
}
