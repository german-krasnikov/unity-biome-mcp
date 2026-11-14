using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal static class PlaytestPositionResolver
    {
        // Testability seam — null in production, injected in unit tests
        internal static Func<string, GameObject> _findOverride;

        /// <summary>Convert raw position expression to Vector3. Throws ArgumentException on failure.</summary>
        internal static Vector3 Resolve(string raw)
        {
            if (string.IsNullOrEmpty(raw))
                throw new ArgumentException("Position expression is null/empty");

            // Literal: starts with digit or minus
            if (char.IsDigit(raw[0]) || raw[0] == '-')
            {
                var f = ValueParser.ParseFloats(raw, 3);
                return new Vector3(f[0], f[1], f[2]);
            }

            // Object ref: @path.position [+ (dx,dy,dz)]
            if (raw[0] == '@')
                return ResolveObjectRef(raw.Substring(1));

            throw new ArgumentException($"Invalid position expression: '{raw}'");
        }

        static Vector3 ResolveObjectRef(string expr)
        {
            const string suffix = ".position";
            int dotIdx = expr.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
            if (dotIdx < 0)
                throw new ArgumentException($"@-expression missing '.position': '@{expr}'");

            var path = expr.Substring(0, dotIdx).Trim();
            var rest = expr.Substring(dotIdx + suffix.Length).Trim(); // "" or "+ (dx,dy,dz)"

            var go = _findOverride != null
                ? _findOverride(path)
                : ComponentSerializer.FindObject(path);
            if (go == null)
                throw new ArgumentException($"Position ref '@{path}.position': object not found in scene");

            var pos = go.transform.position;
            if (string.IsNullOrEmpty(rest)) return pos;

            // Parse offset: + (dx,dy,dz) or - (dx,dy,dz)
            var sign = 1f;
            if (rest[0] == '+') { sign = 1f; rest = rest.Substring(1).Trim(); }
            else if (rest[0] == '-') { sign = -1f; rest = rest.Substring(1).Trim(); }
            rest = rest.Trim('(', ')');
            var o = ValueParser.ParseFloats(rest, 3);
            return pos + sign * new Vector3(o[0], o[1], o[2]);
        }
    }
}
