using System.Collections.Generic;

namespace UnityMCP.Editor
{
    /// <summary>LRU store of compiled assembly bytes keyed by label.
    /// Zero Unity/Roslyn dependencies — domain reload wipes all statics automatically.</summary>
    internal static class HeldTypeStore
    {
        private const int MaxHeld = 20;

        private static readonly Dictionary<string, byte[]> _store = new Dictionary<string, byte[]>();
        private static readonly LinkedList<string> _lru = new LinkedList<string>();
        // maps label → its LinkedList node for O(1) Remove
        private static readonly Dictionary<string, LinkedListNode<string>> _nodes =
            new Dictionary<string, LinkedListNode<string>>();

        /// <summary>Add or update bytes for label. Evicts oldest when at capacity.</summary>
        internal static void Register(string label, byte[] bytes)
        {
            if (_nodes.TryGetValue(label, out var node))
            {
                _lru.Remove(node);   // O(1) — we hold the node reference
                _store[label] = bytes;
            }
            else
            {
                if (_store.Count >= MaxHeld)
                {
                    var evict = _lru.First.Value;
                    _lru.RemoveFirst();
                    _store.Remove(evict);
                    _nodes.Remove(evict);
                }
                _store[label] = bytes;
            }
            _nodes[label] = _lru.AddLast(label); // most-recent at tail
        }

        /// <summary>All current entries as label→bytes dictionary (read-only view).</summary>
        internal static IReadOnlyDictionary<string, byte[]> GetAll() => _store;

        internal static void Clear()
        {
            _store.Clear();
            _lru.Clear();
            _nodes.Clear();
        }

        internal static int Count => _store.Count;
    }
}
