using System.Runtime.CompilerServices;
using UnityEngine;
using UnityMCP.Worker.SourcePatchHarness;

// P1-20 CI qualification harness holder — retained Unity Editor object,
// NEVER mutated. Promoted byte-for-byte from the local P0-80 evidence
// generator (harness_files.py:holder_cs, proven across two local final-SHA
// product cycles). "Same retained Unity Editor object" = this holder's
// GetInstanceID() (a real UnityEngine.Object, stable across a no-reload ON
// window while the scene stays open) plus ImplHash
// (RuntimeHelpers.GetHashCode of the POCO instance). ImplHash is stable only
// within a no-reload window, not across an actual Domain Reload: `_impl` is
// a plain, non-serialized field, so Unity's ordinary MonoBehaviour
// reconstruction after a reload re-runs the field initializer like any other
// non-serialized instance field — expected, not itself evidence of anything
// broken.
public class SourcePatchHarnessHolder : MonoBehaviour
{
    private FastReloadTarget _impl = new FastReloadTarget();
    public int ComputeViaImpl() => _impl.Compute();
    public int ImplHash => RuntimeHelpers.GetHashCode(_impl);
}
