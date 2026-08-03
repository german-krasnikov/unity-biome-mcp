// TDD tests for BuildHelper — EditMode unit tests.
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class BuildHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void ParseTarget_StandaloneOSX_ReturnsCorrectEnum()
        {
            var result = BuildHelper.ParseTarget("StandaloneOSX");
            Assert.AreEqual(BuildTarget.StandaloneOSX, result);
        }

        [Test]
        public void ParseTarget_WebGL_ReturnsCorrectEnum()
        {
            var result = BuildHelper.ParseTarget("WebGL");
            Assert.AreEqual(BuildTarget.WebGL, result);
        }

        [Test]
        public void ParseTarget_CaseInsensitive_Succeeds()
        {
            var result = BuildHelper.ParseTarget("webgl");
            Assert.AreEqual(BuildTarget.WebGL, result);
        }

        [Test]
        public void ParseTarget_UnknownTarget_Throws()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                BuildHelper.ParseTarget("PS6"));
            StringAssert.Contains("Unknown build target", ex.Message);
            StringAssert.Contains("PS6", ex.Message);
        }

        [Test]
        public void ParseScenes_CommaSeparated_ReturnsArray()
        {
            var result = BuildHelper.ParseScenes("A.unity,B.unity");
            Assert.AreEqual(2, result.Length);
            Assert.AreEqual("A.unity", result[0]);
            Assert.AreEqual("B.unity", result[1]);
        }

        [Test]
        public void ParseScenes_NullInput_ReturnsEmptyArray()
        {
            var result = BuildHelper.ParseScenes(null);
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public void ParseScenes_EmptyString_ReturnsEmptyArray()
        {
            var result = BuildHelper.ParseScenes("");
            Assert.AreEqual(0, result.Length);
        }

        [Test]
        public async Task Execute_UnknownAction_SetsErrResult()
        {
            var inner = new System.Threading.Tasks.TaskCompletionSource<string>();
            BuildHelper.Execute("deploy", null, null, null, false, inner);
            var result = await inner.Task;
            StringAssert.StartsWith("err:", result);
            StringAssert.Contains("deploy", result);
        }
    }
}
