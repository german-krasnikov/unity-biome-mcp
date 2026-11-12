using System.Globalization;

namespace UnityMCP.Editor
{
    // Mechanics of a generated wire record (Generated/WireRecords.g.cs): how a value of each field type is
    // written on the wire. No fact about any record — those are in dsl/protocol.biome (SV1, Q8).
    internal static class Wire
    {
        internal static string w_int(int? v) => v.Value.ToString(CultureInfo.InvariantCulture);
        internal static string w_float1(double? v) => v.Value.ToString("F1", CultureInfo.InvariantCulture);
        internal static string w_bool(bool? v) => v.Value ? "true" : "false";
        internal static string w_word(string v) => v;
        internal static string w_text(string v) => v;
    }
}
