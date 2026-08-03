using NUnit.Framework;
using Unity.Profiling;
using UnityMCP.Editor.Profiling;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public sealed class ProfilerBridgeTests : UnityMcpTestBase
    {
        [Test]
        public void RenderBatchCounter_ExactRenderCategory_IsAccepted()
        {
            Assert.IsTrue(ProfilerBridge.IsRenderBatchCounter(
                "Batches Count",
                ProfilerCategory.Render.Name));
        }

        [Test]
        public void RenderBatchCounter_SameNameInUiToolkit_IsRejected()
        {
            Assert.IsFalse(ProfilerBridge.IsRenderBatchCounter(
                "Batches Count",
                "UI Toolkit"));
        }

        [Test]
        public void BatchCountSample_Unavailable_KeepsReasonAndSentinel()
        {
            var sample = ProfilerBridge.BatchCountSample.Unavailable("no-frame-sample");

            Assert.IsFalse(sample.IsAvailable);
            Assert.AreEqual(-1, sample.Value);
            Assert.AreEqual("no-frame-sample", sample.UnavailableReason);
        }

        [Test]
        public void BatchCountSample_Available_KeepsExactValue()
        {
            var sample = ProfilerBridge.BatchCountSample.Available(42);

            Assert.IsTrue(sample.IsAvailable);
            Assert.AreEqual(42, sample.Value);
            Assert.IsNull(sample.UnavailableReason);
        }
    }
}
