// TDD — SceneDiffHelper.Diff: first-call snapshot, no-change, add/remove detection.
// Tasks 6 & 7: covers SNAPSHOT SAVED, NO CHANGES, "+" for added, "-" for removed objects.
using System;
using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class SceneDiffHelperDiffTests : SceneTestBase
    {
        [SetUp]
        public void SetUp() => SceneDiffHelper.Reset();

        [TearDown]
        public void TearDown() => SceneDiffHelper.Reset();

        [Test]
        public void Diff_FirstCall_ReturnsSnapshotSavedMessage()
        {
            var result = SceneDiffHelper.Diff();
            StringAssert.StartsWith("SNAPSHOT SAVED", result);
        }

        [Test]
        public void Diff_SecondCallWithNoChanges_ReturnsNoChanges()
        {
            SceneDiffHelper.Diff(); // first call — saves snapshot
            var result = SceneDiffHelper.Diff(); // no scene change
            Assert.AreEqual("NO CHANGES", result);
        }

        [Test]
        public void Diff_AfterAddingObject_ShowsPlusForAddedObject()
        {
            SceneDiffHelper.Diff(); // baseline snapshot
            var name = "DiffAdd_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var go = TrackOwnedObject(new GameObject(name));

            var result = SceneDiffHelper.Diff();

            StringAssert.StartsWith("DIFF:", result);
            StringAssert.Contains("+ ", result);
            StringAssert.Contains(name, result);
        }

        [Test]
        public void Diff_AfterRemovingObject_ShowsMinusForRemovedObject()
        {
            var name = "DiffRemove_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            var go = TrackOwnedObject(new GameObject(name));
            SceneDiffHelper.Diff();
            GameObject.DestroyImmediate(go);

            var result = SceneDiffHelper.Diff();

            StringAssert.StartsWith("DIFF:", result);
            StringAssert.Contains("- ", result);
            StringAssert.Contains(name, result);
        }
    }
}
