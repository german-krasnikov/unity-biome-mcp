using System;
using NUnit.Framework;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Command
{
    /// <summary>
    /// Verifies asset staging in UndoGroupStack: StageAsset accumulates paths,
    /// Push captures and clears them, RevertLast warns about staged assets.
    /// </summary>
    [TestFixture]
    public class UndoGroupStackAssetStagingTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            UndoGroupStack.Clear();
            UndoGroupStack.ClearStaged();
            // Inject a no-op revert so we don't touch Unity Undo API
            RegisterCleanup(() =>
            {
                UndoGroupStack.RevertAction = UndoGroupHelper.RevertToBeforeGroup;
                UndoGroupStack.Clear();
                UndoGroupStack.ClearStaged();
            });
            UndoGroupStack.RevertAction = _ => { };
        }

        [Test]
        public void StageAsset_AddsToStagedList()
        {
            UndoGroupStack.StageAsset("Assets/Foo.mat");
            UndoGroupStack.StageAsset("Assets/Bar.prefab");
            Assert.That(UndoGroupStack.StagedCount, Is.EqualTo(2));
        }

        [Test]
        public void Push_CapturesStagedAssets_AndClearsStaged()
        {
            UndoGroupStack.StageAsset("Assets/Test.mat");
            UndoGroupStack.Push(0, 1);

            // Staged list should be cleared after Push
            Assert.That(UndoGroupStack.StagedCount, Is.EqualTo(0));
            // Group was pushed
            Assert.That(UndoGroupStack.Count, Is.EqualTo(1));
        }

        [Test]
        public void RevertLast_WithStagedAssets_ReturnsWarnString()
        {
            UndoGroupStack.StageAsset("Assets/Mat1.mat");
            UndoGroupStack.StageAsset("Assets/Mat2.mat");
            UndoGroupStack.Push(0, 1);

            var result = UndoGroupStack.RevertLast();

            StringAssert.Contains("warn:", result);
            StringAssert.Contains("asset file(s) not reverted", result);
            StringAssert.Contains("Assets/Mat1.mat", result);
            StringAssert.Contains("Assets/Mat2.mat", result);
        }

        [Test]
        public void RevertLast_WithNoStagedAssets_ReturnsNormalString()
        {
            UndoGroupStack.Push(0, 1);
            var result = UndoGroupStack.RevertLast();

            StringAssert.Contains("reverted", result);
            StringAssert.DoesNotContain("warn:", result);
        }

        [Test]
        public void ClearStaged_EmptiesStagedList()
        {
            UndoGroupStack.StageAsset("Assets/Foo.mat");
            UndoGroupStack.ClearStaged();
            Assert.That(UndoGroupStack.StagedCount, Is.EqualTo(0));
        }
    }
}
