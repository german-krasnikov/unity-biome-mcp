using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class RefManager
    {
        private static Dictionary<string, GameObject> _refToObj = new Dictionary<string, GameObject>();
        private static Dictionary<GameObject, string> _objectToRef = new Dictionary<GameObject, string>();
        private static int _counter = 0;

        private static readonly char[] Base62 =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        /// <summary>Assign ref to GO. Returns existing ref if already mapped.</summary>
        public static string Assign(GameObject go)
        {
            if (_objectToRef.TryGetValue(go, out var existing)) return existing;
            var r = GenerateRef(_counter++);
            // Evict old GO's reverse-lookup entry before overwriting the slot.
            if (_refToObj.TryGetValue(r, out var old) && old != null)
                _objectToRef.Remove(old);
            _refToObj[r] = go;
            _objectToRef[go] = r;
            return r;
        }

        /// <summary>Resolve &amp;ref to GO. Returns null if stale.</summary>
        public static GameObject Resolve(string r)
        {
            if (!_refToObj.TryGetValue(r, out var go) || go == null)
            {
                _refToObj.Remove(r);
                return null;
            }
            return go;
        }

        // & prefix: alphanumeric (base62 chars — letters and digits).
        // $ prefix: digits only — backward compat for one version; $abc is an alias, not a ref.
        public static bool IsRef(string s)
        {
            if (s == null || s.Length < 2) return false;
            if (s[0] == WirePrefix.Ref)
            {
                for (int i = 1; i < s.Length; i++)
                    if (!char.IsLetterOrDigit(s[i])) return false;
                return true;
            }
            if (s[0] == WirePrefix.Alias) // $ — backward compat, digits only
            {
                for (int i = 1; i < s.Length; i++)
                    if (!char.IsDigit(s[i])) return false;
                return true;
            }
            return false;
        }

        public static void Invalidate()
        {
            _refToObj.Clear();
            _objectToRef.Clear();
            // Compact references can outlive this cache in rendered chat results. Keep
            // the counter monotonic so an invalidated reference cannot alias a newly
            // assigned object later in the same Editor process.
        }

        public static void Prune()
        {
            var stale = new List<string>();
            foreach (var kv in _refToObj)
                if (kv.Value == null) stale.Add(kv.Key);
            foreach (var r in stale)
                _refToObj.Remove(r);
            _objectToRef.Clear();
            foreach (var kv in _refToObj)
                if (kv.Value != null) _objectToRef[kv.Value] = kv.Key;
        }

        /// <summary>
        /// Encodes n+1 in base62 (counter grows freely, no wrap-around).
        /// n=0 → "&amp;1", n=9 → "&amp;a", n=60 → "&amp;Z", n=61 → "&amp;10".
        /// </summary>
        internal static string GenerateRef(int n)
        {
            int val = n + 1;
            var sb = new StringBuilder(4);
            do {
                sb.Insert(0, Base62[val % 62]);
                val /= 62;
            } while (val > 0);
            return WirePrefix.Ref + sb.ToString();
        }
    }
}
