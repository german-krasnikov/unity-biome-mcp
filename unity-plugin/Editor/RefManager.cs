using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class RefManager
    {
        private static Dictionary<string, Object> _refToObj = new Dictionary<string, Object>();
        private static Dictionary<Object, string> _objectToRef = new Dictionary<Object, string>();
        private static int _counter = 0;

        private static readonly char[] Base62 =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        /// <summary>Assign ref to any UnityEngine.Object. Returns existing ref if already mapped.</summary>
        public static string AssignAny(Object obj)
        {
            if (_objectToRef.TryGetValue(obj, out var existing)) return existing;
            var r = GenerateRef(_counter++);
            // Evict old object's reverse-lookup entry before overwriting the slot.
            if (_refToObj.TryGetValue(r, out var old) && old != null)
                _objectToRef.Remove(old);
            _refToObj[r] = obj;
            _objectToRef[obj] = r;
            return r;
        }

        /// <summary>Resolve &amp;ref to any UnityEngine.Object. Returns null if stale.</summary>
        public static Object ResolveAny(string r)
        {
            if (!_refToObj.TryGetValue(r, out var obj) || obj == null)
            {
                _refToObj.Remove(r);
                return null;
            }
            return obj;
        }

        /// <summary>Assign ref to GO. Returns existing ref if already mapped.</summary>
        public static string Assign(GameObject go) => AssignAny(go);

        /// <summary>Resolve &amp;ref to GO. Returns null if stale or not a GameObject.</summary>
        public static GameObject Resolve(string r) => ResolveAny(r) as GameObject;

        // & prefix only: alphanumeric (base62 chars — letters and digits).
        // $ is a hex instance ID prefix (TransientObjectId), never a ref.
        public static bool IsRef(string s)
        {
            if (s == null || s.Length < 2) return false;
            if (s[0] == WirePrefix.Ref)
            {
                for (int i = 1; i < s.Length; i++)
                    if (!char.IsLetterOrDigit(s[i])) return false;
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

        /// <summary>Pure base62 encode — no offset, no prefix.</summary>
        internal static string Base62Encode(ulong value)
        {
            if (value == 0) return "0";
            var sb = new StringBuilder(11);
            while (value > 0)
            {
                sb.Insert(0, Base62[(int)(value % 62)]);
                value /= 62;
            }
            return sb.ToString();
        }

        /// <summary>Decode base62 string. Returns false on empty input or invalid chars.</summary>
        internal static bool TryBase62Decode(string encoded, out ulong result)
        {
            result = 0;
            if (string.IsNullOrEmpty(encoded)) return false;
            foreach (var c in encoded)
            {
                int digit;
                if (c >= '0' && c <= '9') digit = c - '0';
                else if (c >= 'a' && c <= 'z') digit = c - 'a' + 10;
                else if (c >= 'A' && c <= 'Z') digit = c - 'A' + 36;
                else return false;
                result = result * 62 + (ulong)digit;
            }
            return true;
        }

        /// <summary>Deterministic stateless ref: same object → same string, always.</summary>
        public static string Ref(Object obj)
        {
            if (obj == null) return null;
            return WirePrefix.Ref + Base62Encode(ObjectIdCompat.GetRawId(obj));
        }

        /// <summary>Resolve &amp;base62 ref back to Object. O(1) via Unity internals.</summary>
        public static Object ResolveRef(string r)
        {
            if (string.IsNullOrEmpty(r) || r.Length < 2 || r[0] != WirePrefix.Ref) return null;
            if (!TryBase62Decode(r.Substring(1), out var rawId)) return null;
            return ObjectIdCompat.ResolveObject(rawId);
        }
    }
}
