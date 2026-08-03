using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class ScriptableObjectHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        const string AssetFolder = "Assets/TestsTemp/ScriptableObjectHelperTests";
        const string TempPath = AssetFolder + "/SOHelper_OrphanTest.asset";
        const string EchoPath = AssetFolder + "/SOHelper_EchoTest.asset";

        [SetUp]
        public void SetUp()
        {
            TrackOwnedAsset(AssetFolder);
            TestPaths.EnsureFolder(AssetFolder);
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

        [Test]
        public void Set_WithMultipleFields_EchoesOldToNewValues()
        {
            var asset = ScriptableObject.CreateInstance<SOEchoTestAsset>();
            asset.speed = 3;
            asset.label = "old";
            AssetDatabase.CreateAsset(asset, EchoPath);

            var result = ScriptableObjectHelper.Execute("set",
                $"{{\"path\":\"{EchoPath}\",\"fields\":\"speed=9\\nlabel=new\"}}");

            StringAssert.Contains("speed = 3", result);
            StringAssert.Contains("→ 9", result);
            StringAssert.Contains("label = old", result);
            StringAssert.Contains("→ new", result);
        }
    }

    public class SOEchoTestAsset : ScriptableObject
    {
        public int speed;
        public string label;
    }
}
