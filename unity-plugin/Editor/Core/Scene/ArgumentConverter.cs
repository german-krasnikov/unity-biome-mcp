using System;
using UnityEngine;

namespace UnityMCP.Editor
{
    internal interface IArgumentConverter
    {
        bool CanConvert(Type targetType, string value);
        object Convert(string value, Type targetType);
    }

    // Handles "hash:XXXXXXXX" prefix notation for Hash128 parameters.
    internal sealed class Hash128Converter : IArgumentConverter
    {
        public bool CanConvert(Type targetType, string value) => targetType == typeof(Hash128);

        public object Convert(string value, Type targetType)
        {
            var hex = value.StartsWith("hash:", StringComparison.OrdinalIgnoreCase)
                ? value.Substring(5)
                : value;
            // Hash128.Parse requires a 32-character hex string; pad if shorter
            return Hash128.Parse(hex.PadLeft(32, '0'));
        }
    }

    // Converts layer-name strings or raw bitmask ints to LayerMask.
    internal sealed class LayerMaskConverter : IArgumentConverter
    {
        public bool CanConvert(Type targetType, string value) => targetType == typeof(LayerMask);

        public object Convert(string value, Type targetType)
        {
            if (int.TryParse(value, out int rawMask))
                return (LayerMask)rawMask;
            int bitmask = LayerMask.GetMask(value);
            if (bitmask == 0 && LayerMask.NameToLayer(value) < 0)
                throw new ArgumentException($"Layer '{value}' not found");
            return (LayerMask)bitmask;
        }
    }
}
