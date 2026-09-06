using System.Runtime.CompilerServices;
using UnityEngine;
using UnityMCP.ReadinessQualification;

// Installed only into a uniquely owned qualification folder. The controller
// registers the exact created GameObject and removes only that object afterward.
public sealed class BuildReadinessCanaryProbe : MonoBehaviour
{
    private readonly BuildReadinessCanaryTarget _retained = new BuildReadinessCanaryTarget();
    public int Observed;
    public int RetainedIdentity => RuntimeHelpers.GetHashCode(_retained);
    public void Sample() { Observed = _retained.Read(); }
}
