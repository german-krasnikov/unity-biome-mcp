using System;
using System.Globalization;

namespace UnityMCP.Editor
{
    /// <summary>
    /// Engine-free 3-component float value, replacing UnityEngine.Vector3 for
    /// PlaytestStep.Position (D04) so Core (noEngineReferences) never touches
    /// UnityEngine. Conversions to/from Vector3 live at the Editor-side consumers.
    /// Bare data type — no equality members (KISS, v1 doesn't require them).
    /// </summary>
    [Serializable]
    public struct Float3
    {
        public float x;
        public float y;
        public float z;

        public Float3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public override string ToString() =>
            $"{x.ToString(CultureInfo.InvariantCulture)},{y.ToString(CultureInfo.InvariantCulture)},{z.ToString(CultureInfo.InvariantCulture)}";
    }
}
