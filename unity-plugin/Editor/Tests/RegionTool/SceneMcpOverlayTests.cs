using NUnit.Framework;
using System;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class SceneMcpOverlayTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void ResetToolSeams()
        {
            // Static seams survive domain-reload gaps in EditMode test runs.
            // Reset before each test so order of test execution doesn't matter.
            SceneRegionTool.ConfirmPointAction = null;
            SceneRegionTool.CanConfirmQuery    = null;
            SceneRegionTool.CanCommitQuery     = null;
            SceneRegionTool.CancelAction       = null;
            SceneAnnotationTool.ConfirmPointAction = null;
            SceneAnnotationTool.CanConfirmQuery    = null;
            SceneAnnotationTool.CanCommitQuery     = null;
            SceneAnnotationTool.CancelAction       = null;
        }

        [Test]
        public void SceneRegionOverlay_TypeDoesNotExist()
        {
            var t = Type.GetType(
                "UnityMCP.Editor.RegionTool.SceneRegionOverlay, UnityMCP.Editor");
            Assert.IsNull(t, "SceneRegionOverlay should have been deleted");
        }

        [Test]
        public void SceneAnnotationOverlay_TypeDoesNotExist()
        {
            var t = Type.GetType(
                "UnityMCP.Editor.RegionTool.SceneAnnotationOverlay, UnityMCP.Editor");
            Assert.IsNull(t, "SceneAnnotationOverlay should have been deleted");
        }

        [Test]
        public void SceneMcpOverlay_TypeExists()
        {
            var t = Type.GetType(
                "UnityMCP.Editor.RegionTool.SceneMcpOverlay, UnityMCP.Editor");
            Assert.IsNotNull(t);
        }

        [Test]
        public void RegionTool_NewSeams_NullWhenToolNotActive()
        {
            Assert.IsNull(SceneRegionTool.ConfirmPointAction);
            Assert.IsNull(SceneRegionTool.CanConfirmQuery);
            Assert.IsNull(SceneRegionTool.CanCommitQuery);
            Assert.IsNull(SceneRegionTool.CancelAction);
        }

        [Test]
        public void AnnotationTool_NewSeams_NullWhenToolNotActive()
        {
            Assert.IsNull(SceneAnnotationTool.ConfirmPointAction);
            Assert.IsNull(SceneAnnotationTool.CanConfirmQuery);
            Assert.IsNull(SceneAnnotationTool.CanCommitQuery);
            Assert.IsNull(SceneAnnotationTool.CancelAction);
        }
    }
}
