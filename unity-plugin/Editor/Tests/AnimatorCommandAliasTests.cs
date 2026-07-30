using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class AnimatorCommandAliasTests : SceneTestBase
    {
        private GameObject _go;
        private const string Name = "AnimatorAlias_Test";
        private const string ControllerPath = "Assets/Animations/" + Name + ".controller";

        [SetUp]
        public void SetUp()
        {
            CommandRouter.RegisterAll();
            _go = new GameObject(Name);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
            AssetDatabase.DeleteAsset(ControllerPath);
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
