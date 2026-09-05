using System;
using System.Globalization;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Engine-free numeric parsing for Core (no UnityEngine/UnityEditor dependency).
    /// Lifted from ValueParser.ParseFloats, which now delegates here — this is the
    /// version PlaytestParser.SetPosition calls directly so a future Core-hosted
    /// parser (D06+) never reaches back into the Editor assembly for it.
    /// </summary>
    public static class NumericParsing
    {
        public static float[] ParseFloats(string value, int expected)
        {
            var parts = value.Trim('(', ')').Split(',');
            if (parts.Length != expected)
                throw new ArgumentException($"Expected {expected} components but got {parts.Length}: {value}");
            var result = new float[expected];
            for (int i = 0; i < expected; i++)
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out result[i]))
                    throw new ArgumentException($"Invalid float at index {i}: {value}");
            return result;
        }
    }
}
