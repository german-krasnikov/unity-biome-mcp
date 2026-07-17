using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ScriptableObjectHelperTests
    {
        const string TempPath = "Assets/TestsTemp/SOHelper_OrphanTest.asset";

        [SetUp]
        public void SetUp() => TestPaths.EnsureFolder();

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(TempPath) != null)
                AssetDatabase.DeleteAsset(TempPath);
        }

        [Test]
        public void Create_WithFields_InvalidField_ReturnsError()
        {
            var args = $"{{\"type\":\"PlaytestConfig\",\"path\":\"{TempPath}\",\"fields\":\"nonExistentField999=42\"}}";

            Assert.Throws<System.ArgumentException>(() =>
                ScriptableObjectHelper.Execute("create", args));

            Assert.IsNull(AssetDatabase.LoadAssetAtPath<ScriptableObject>(TempPath),
                "Orphan asset must not remain on disk after failed create");
        }
    }
}
