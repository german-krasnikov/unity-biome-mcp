using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SceneContextTests : SceneTestBase
    {
        [Test]
        public void SingleScene_IsMulti_False()
        {
            var ctx = SceneContext.Current;
            Assert.That(ctx.IsMulti, Is.False);
        }

        [Test]
        public void QualifyPath_SingleScene_SlashPrefix()
        {
            var go = new GameObject("QP_Test");
            try
            {
                var ctx = SceneContext.Current;
                var result = ctx.QualifyPath(go, go.name);
                Assert.That(result, Does.StartWith("/"));
                Assert.That(result, Does.Not.Contain(":/"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void FilterByScene_Null_ReturnsAll()
        {
            var ctx = SceneContext.Current;
            var result = ctx.FilterByScene(null);
            Assert.That(result.Count, Is.GreaterThan(0));
        }

        [Test]
        public void FilterByScene_Unknown_ReturnsEmpty()
        {
            var ctx = SceneContext.Current;
            var result = ctx.FilterByScene("NonExistentScene_XYZ_999");
            Assert.That(result.Count, Is.EqualTo(0));
        }
    }
}
