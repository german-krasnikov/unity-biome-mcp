using System;
using System.Globalization;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Process-local Unity object identity. Persist and transmit <see cref="WireValue"/>
    /// as an unsigned decimal string so negative instance IDs round-trip without ambiguity.
    /// </summary>
    internal readonly struct TransientObjectId : IEquatable<TransientObjectId>
    {
        internal static readonly TransientObjectId None = default;

        internal ulong RawValue { get; }
        internal bool IsNone => RawValue == 0;
        internal string WireValue => RawValue.ToString(CultureInfo.InvariantCulture);

        private TransientObjectId(ulong rawValue)
        {
            RawValue = rawValue;
        }

        internal static TransientObjectId FromObject(Object value)
        {
            if (value == null) return None;
            return new TransientObjectId(ObjectIdCompat.GetRawId(value));
        }

        internal static string GetWireValue(Object value) => FromObject(value).WireValue;

        // CONVENTION (DRY Group A): component references use the component's own instanceID.
        internal static string GetComponentWireValue(Component comp) => FromObject(comp).WireValue;

        internal static bool TryParse(string value, out TransientObjectId objectId)
        {
            objectId = None;
            if (string.IsNullOrWhiteSpace(value)) return false;

            var token = value.Trim();

            // New: $HEX format — strip $ and parse as hex (uppercase digits 0-9A-F)
            if (token[0] == '$')
            {
                var hex = token.Substring(1);
                if (ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hexRaw))
                {
                    objectId = new TransientObjectId(hexRaw);
                    return true;
                }
                return false; // $ prefix but not valid hex (e.g. $g = RefManager slot)
            }

            if (token[0] == '#') token = token.Substring(1);

            if (ulong.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out var raw))
            {
                objectId = new TransientObjectId(raw);
                return true;
            }

            // Legacy wire references used signed 32-bit instance IDs. Sign extension
            // reconstructs the raw value used by the compatibility representation.
            if (int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacy))
            {
                objectId = new TransientObjectId(unchecked((ulong)(long)legacy));
                return true;
            }

            return false;
        }

        internal Object Resolve()
        {
            if (IsNone) return null;
            return ObjectIdCompat.ResolveObject(RawValue);
        }

        internal static Object Resolve(string value)
            => TryParse(value, out var objectId) ? objectId.Resolve() : null;

        internal static bool HasSerializedReference(SerializedProperty property)
        {
            return ObjectIdCompat.HasSerializedReference(property);
        }

        public bool Equals(TransientObjectId other) => RawValue == other.RawValue;
        public override bool Equals(object obj) => obj is TransientObjectId other && Equals(other);
        public override int GetHashCode() => RawValue.GetHashCode();
        public override string ToString() => WireValue;
        public static bool operator ==(TransientObjectId left, TransientObjectId right) => left.Equals(right);
        public static bool operator !=(TransientObjectId left, TransientObjectId right) => !left.Equals(right);
    }
}
