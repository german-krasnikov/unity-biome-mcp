using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityMCP.Editor.Testing;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class SceneHelperSaveCopyTests : SceneCleanTestBase
    {
        private const string TempCopyPath = "Assets/TestsTemp/SceneHelperSaveCopy.unity";

        [SetUp]
        public void SetUp()
        {
            TrackOwnedAsset(TempCopyPath);
            TestPaths.EnsureFolder("Assets/TestsTemp");
        }

        [Test]
        public void SaveCopy_Throws_WhenPathOutsideAssets()
        {
            Assert.Throws<System.ArgumentException>(
                () => SceneHelper.SaveCopy("Packages/some.unity"));
        }

        [Test]
        public void SaveCopy_Throws_WhenDestinationIsActivePath()
        {
            var activePath = SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(activePath)) Assert.Ignore("Active scene has no path");

            Assert.Throws<System.ArgumentException>(() => SceneHelper.SaveCopy(activePath));
        }

        [Test]
        [BiomeWorkerOnly("writes a temp .unity file to Assets/TestsTemp")]
        public void SaveCopy_WritesFile_ActiveScenePathUnchanged()
        {
            var before = SceneManager.GetActiveScene().path;

            var result = SceneHelper.SaveCopy(TempCopyPath);

            Assert.AreEqual(before, SceneManager.GetActiveScene().path,
                "Active scene path must not change after save_copy");
            Assert.IsTrue(result.StartsWith("ok "), $"Expected ok response, got: {result}");
            Assert.IsTrue(result.Contains($"path={TempCopyPath}"), $"Expected path in result, got: {result}");
        }
    }
}
