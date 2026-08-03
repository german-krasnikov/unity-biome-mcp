// TDD tests for BakeHelper — EditMode unit tests.
using System;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BakeHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void TearDown()
        {
            if (UnityEditor.Lightmapping.isRunning)
                UnityEditor.Lightmapping.Cancel();
        }

        [Test]
        public void Execute_UnknownTarget_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                BakeHelper.Execute("start", "{\"target\":\"unknown\"}"));
            StringAssert.Contains("Unknown target", ex.Message);
        }

        [Test]
        public void Execute_LightingStatus_ContainsStatusKey()
        {
            var result = BakeHelper.Execute("status", "{\"target\":\"lighting\",\"action\":\"status\"}");
            StringAssert.Contains("status:", result);
        }

        [Test]
        public void Execute_LightingStatus_AfterCancel_ContainsIdle()
        {
            // Cancel any in-progress bake, then verify idle
            BakeHelper.Execute("cancel", "{\"target\":\"lighting\",\"action\":\"cancel\"}");
            var result = BakeHelper.Execute("status", "{\"target\":\"lighting\",\"action\":\"status\"}");
            StringAssert.Contains("status:idle", result);
        }

        [Test]
        public void Execute_LightingSettings_ContainsExpectedKeys()
        {
            var result = BakeHelper.Execute("settings", "{\"target\":\"lighting\",\"action\":\"settings\"}");
            StringAssert.Contains("bakeMode:", result);
            StringAssert.Contains("maxAtlasSize:", result);
        }

        [Test]
        public void Execute_OcclusionStatus_ContainsStatusAndBaked()
        {
            var result = BakeHelper.Execute("status", "{\"target\":\"occlusion\",\"action\":\"status\"}");
            StringAssert.Contains("status:", result);
            StringAssert.Contains("baked:", result);
            StringAssert.Contains("bytes:", result);
        }

        [Test]
        public void Execute_OcclusionClear_ReturnsOkCleared()
        {
            var result = BakeHelper.Execute("clear", "{\"target\":\"occlusion\",\"action\":\"clear\"}");
            Assert.AreEqual("ok:cleared", result);
        }

        [Test]
        public void Execute_LightingCancel_ReturnsOkCancelled()
        {
            var result = BakeHelper.Execute("cancel", "{\"target\":\"lighting\",\"action\":\"cancel\"}");
            Assert.AreEqual("ok:cancelled", result);
        }

        [Test]
        public void Execute_LightingStart_ReturnsStatusStarted()
        {
            var result = BakeHelper.Execute("start", "{\"target\":\"lighting\"}");
            Assert.AreEqual("status:started", result);
        }
    }
}
