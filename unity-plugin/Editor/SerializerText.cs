using System.Globalization;

namespace UnityMCP.Editor
{
    // Boundary hardening Phase 4.2: ComponentSerializer, AnimationSerializer and
    // AnimatorControllerSerializer each repeat `v.ToString(format, CultureInfo.InvariantCulture)`
    // dozens of times with varying format strings. Pure byte-shuffling (no decision
    // content) -- see Plans/task-p4-serializers-record.md §5.
    internal static class SerializerText
    {
        internal static string FormatFloat(float v, string format) =>
            v.ToString(format, CultureInfo.InvariantCulture);
    }
}
