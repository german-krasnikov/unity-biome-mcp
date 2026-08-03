// Stress tests for reload stability: ComputeStamp multi-assembly (SD-1).
// EditMode only. Every test < 15 lines.
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    // ── Group 1: ComputeStamp multi-assembly (SD-1) ───────────────────────────

    [TestFixture]
    public class ComputeStampStressTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // T-A: stamp always contains ';' (SD-1: each MVID appended with ';')
        [Test]
        public void ComputeStamp_AlwaysContainsSemicolon()
        {
            var stamp = SyncHelper.ComputeStamp();
            StringAssert.Contains(";", stamp,
                "SD-1: each MVID is appended with ';'; stamp must always contain it");
        }

        // T-B: two consecutive calls return identical strings (no side effects)
        [Test]
        public void ComputeStamp_IsDeterministic()
        {
            var s1 = SyncHelper.ComputeStamp();
            var s2 = SyncHelper.ComputeStamp();
            Assert.AreEqual(s1, s2, "ComputeStamp must be deterministic within same domain");
        }
    }
}
