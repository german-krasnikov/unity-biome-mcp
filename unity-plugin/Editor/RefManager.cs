using System.Collections.Generic;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class RefManager
    {
        private static Dictionary<string, GameObject> _refToObj = new Dictionary<string, GameObject>();
        private static Dictionary<GameObject, string> _objectToRef = new Dictionary<GameObject, string>();
        private static int _counter = 0;

        /// <summary>Assign ref to GO. Returns existing ref if already mapped.</summary>
        public static string Assign(GameObject go)
        {
            if (_objectToRef.TryGetValue(go, out var existing)) return existing;
            var r = GenerateRef(_counter++);
            // Ring-wrap: evict old GO's _idToRef entry before overwriting the slot
            if (_refToObj.TryGetValue(r, out var old) && old != null)
                _objectToRef.Remove(old);
            _refToObj[r] = go;
            _objectToRef[go] = r;
            return r;
        }

        /// <summary>Resolve $ref to GO. Returns null if stale.</summary>
        public static GameObject Resolve(string r)
        {
            if (!_refToObj.TryGetValue(r, out var go) || go == null)
            {
                _refToObj.Remove(r);
                return null;
            }
            return go;
        }

        public static bool IsRef(string s) => s != null && s.StartsWith("$") && s.Length >= 2 && s.Length <= 3;

        public static void Invalidate()
        {
            _refToObj.Clear();
            _objectToRef.Clear();
            _counter = 0;
        }

        public static void Prune()
        {
            var stale = new List<string>();
            foreach (var kv in _refToObj)
                if (kv.Value == null) stale.Add(kv.Key);
            foreach (var r in stale)
            {
                // Rebuild reverse lookup from remaining live entries.
                _refToObj.Remove(r);
            }
            _objectToRef.Clear();
            foreach (var kv in _refToObj)
                if (kv.Value != null) _objectToRef[kv.Value] = kv.Key;
        }

        /// <summary>$a-$z (n=0..25), $aa-$zz (n=26..701), wraps at 702</summary>
        internal static string GenerateRef(int n)
        {
            n = n % 702; // 26 + 26*26 = 702 total slots
            if (n < 26) return "$" + (char)('a' + n);
            var i = n - 26;
            return "$" + (char)('a' + i / 26) + (char)('a' + i % 26);
        }
    }
}
