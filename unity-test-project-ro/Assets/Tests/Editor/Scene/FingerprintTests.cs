using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Scene
{
    public class FingerprintTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ---- Fingerprint Tests ----

        [Test]
        public void Fingerprint_EmptyScene_ReturnsFpPrefix()
        {
            var result = FingerprintHelper.Fingerprint(null, 3);
            Assert.That(result, Does.StartWith("fp:"));
            Assert.That(result.Length, Is.EqualTo(11)); // "fp:" + 8 hex chars
        }

        [Test]
        public void Fingerprint_SameState_SameHash()
        {
            var go = new GameObject("FP_Test");
            try
            {
                var h1 = FingerprintHelper.Fingerprint("FP_Test", 3);
                var h2 = FingerprintHelper.Fingerprint("FP_Test", 3);
                Assert.That(h1, Is.EqualTo(h2));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Fingerprint_ChangedObject_DifferentHash()
        {
            var go = new GameObject("FP_Change");
            try
            {
                var before = FingerprintHelper.Fingerprint("FP_Change", 3);
                go.name = "FP_Changed";
                var after = FingerprintHelper.Fingerprint("FP_Changed", 3);
                Assert.That(before, Is.Not.EqualTo(after));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ---- Scan Tests ----

        [Test]
        public void Scan_WithNewScene_ShowsScanHeader()
        {
            var result = ScanHelper.Scan();
            Assert.That(result, Does.StartWith("SCAN:"));
        }

        [Test]
        public void Scan_WithCollider_CountsIt()
        {
            var go = new GameObject("ScanCol");
            go.AddComponent<BoxCollider>();
            try
            {
                var result = ScanHelper.Scan();
                Assert.That(result, Does.Contain("colliders:"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void Scan_WithTrigger_CountedSeparately()
        {
            var go = new GameObject("ScanTrig");
            var col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            try
            {
                var result = ScanHelper.Scan();
                Assert.That(result, Does.Contain("colliders:"));
                Assert.That(result, Does.Contain("triggers:"));
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
