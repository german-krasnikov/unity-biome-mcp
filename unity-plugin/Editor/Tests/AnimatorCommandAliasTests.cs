using NUnit.Framework;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AnimatorCommandAliasTests : SceneTestBase
    {
        private GameObject _go;
        private const string Name = "AnimatorAlias_Test";
        private const string AssetFolder =
            "Assets/TestsTemp/AnimatorCommandAliasTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(AnimationHelper.ResetAssetDirectoryForTests);
            TrackOwnedAsset(AssetFolder);
            TestPaths.EnsureFolder(AssetFolder);
            AnimationHelper.SetAssetDirectoryForTests(AssetFolder);
            CommandRouter.RegisterAll();
            _go = TrackOwnedObject(new GameObject(Name));
        }

        [Test]
        public void AddParam_TypeNameAlias_AddsParameter()
        {
            var result = CommandRouter.ExecuteCommand("animator",
                "{\"action\":\"add_param\",\"path\":\"/" + Name + "\",\"type\":\"float\",\"name\":\"Speed\",\"value\":\"0\"}");

            StringAssert.Contains("Speed(float)", result);
            var readback = CommandRouter.ExecuteCommand("animator",
                "{\"action\":\"get\",\"path\":\"/" + Name + "\"}");
            StringAssert.Contains("Speed : float = 0", readback);
        }

        [Test]
        public void AddState_StateAlias_AddsState()
        {
            var result = CommandRouter.ExecuteCommand("animator",
                "{\"action\":\"add_state\",\"path\":\"/" + Name + "\",\"state\":\"Idle\"}");

            StringAssert.Contains("Idle", result);
            var readback = CommandRouter.ExecuteCommand("animator",
                "{\"action\":\"get\",\"path\":\"/" + Name + "\"}");
            StringAssert.Contains("Idle", readback);
        }
    }
}
