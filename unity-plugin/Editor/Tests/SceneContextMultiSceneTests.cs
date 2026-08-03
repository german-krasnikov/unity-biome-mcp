// NUnit tests for SceneContext.QualifyPath in multi-scene mode — CS2.test.6.
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SceneContextMultiSceneTests : MultiSceneTestBase
    {
        [Test]
        public void QualifyPath_MultiScene_HasScenePrefix()
        {
            var go = CreateIn(_additiveScene, "SC_MS_GO");
            var ctx = SceneContext.Current;
            Assert.IsTrue(ctx.IsMulti, "Must be multi-scene for this test");

            var result = ctx.QualifyPath(go, go.name);

            Assert.That(result, Does.Contain(":/"), "Multi-scene path must contain :/");
            Assert.That(result, Does.StartWith(_additiveScene.name + ":/"));
        }

        [Test]
        public void QualifyPath_MultiScene_MainSceneGO_HasMainScenePrefix()
        {
            var go = new GameObject("SC_MS_Main");
            TrackOwnedObject(go);
            var ctx = SceneContext.Current;
            Assert.IsTrue(ctx.IsMulti);

            var result = ctx.QualifyPath(go, go.name);

            Assert.That(result, Does.Contain(":/"));
            // Main scene name should prefix the path
            StringAssert.Contains(go.scene.name + ":/", result);
        }

        [Test]
        public void IsMulti_WithAdditiveScene_True()
        {
            // MultiSceneTestBase.SetUp always opens an additive scene, so IsMulti must be true.
            var ctx = SceneContext.Current;
            Assert.IsTrue(ctx.IsMulti,
                "Base fixture adds an additive scene; IsMulti must be true here");
        }

        [Test]
        public void IsMulti_SingleScene_False()
        {
            // Close the additive scene that the base fixture opened.
            // With only one scene loaded, IsMulti must return false.
            // OLD code that hard-coded IsMulti=true would fail this assertion.
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(_additiveScene, true);
            _additiveScene = default; // prevent TearDown from double-closing

            var ctx = SceneContext.Current;
            Assert.IsFalse(ctx.IsMulti,
                "After closing the additive scene only one scene remains; IsMulti must be false");
        }
    }
}
