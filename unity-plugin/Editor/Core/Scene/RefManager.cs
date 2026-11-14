using System.Text;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class RefManager
    {
        private static readonly char[] Base62 =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

        /// <summary>Assign ref to any UnityEngine.Object. Stateless — delegates to Ref.</summary>
        public static string AssignAny(Object obj) => Ref(obj);

        /// <summary>Resolve &amp;ref to any UnityEngine.Object. Stateless — delegates to ResolveRef.</summary>
        public static Object ResolveAny(string r) => ResolveRef(r);

        /// <summary>Assign ref to GO. Backward-compat wrapper over Ref.</summary>
        public static string Assign(GameObject go) => Ref(go);

        /// <summary>Resolve &amp;ref to GO. Backward-compat wrapper over ResolveRef.</summary>
        public static GameObject Resolve(string r) => ResolveRef(r) as GameObject;

        // & prefix only: base62 chars (0-9, a-z, A-Z) — locale-invariant.
        // $ is a hex instance ID prefix (TransientObjectId), never a ref.
        public static bool IsRef(string s)
        {
            if (s == null || s.Length < 2) return false;
            if (s[0] == WirePrefix.Ref)
            {
                for (int i = 1; i < s.Length; i++)
                    if (!IsBase62Char(s[i])) return false;
                return true;
            }
            return false;
        }

        private static bool IsBase62Char(char c)
            => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        /// <summary>No-op: stateless backend has no cache to clear.</summary>
        public static void Invalidate() { }

        /// <summary>No-op: stateless backend has no dict to prune.</summary>
        public static void Prune() { }

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
