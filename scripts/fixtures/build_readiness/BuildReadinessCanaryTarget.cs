using System.Runtime.CompilerServices;

namespace UnityMCP.ReadinessQualification
{
    // Only Read's body changes. The FSR target is a POCO, not a MonoBehaviour.
    public sealed class BuildReadinessCanaryTarget
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public int Read() { return 101; }
    }
}
