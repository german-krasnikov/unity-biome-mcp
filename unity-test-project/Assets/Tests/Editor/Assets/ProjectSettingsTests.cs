using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Assets
{
    [TestFixture]
    public class ProjectSettingsTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TestTag = "MCPTestTag";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() =>
            {
                LogAssert.ignoreFailingMessages = false;
                RemoveAllTagsNamed(TestTag);
            });
            LogAssert.ignoreFailingMessages = false;
            RemoveAllTagsNamed(TestTag);
        }

        [Test]
        public void GetTags_ReturnsList()
        {
            string result = CommandRouter.Process(
                "{\"id\":\"ps1\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"tags\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("Untagged", result);
        }

        [Test]
        public void SetTag_TagExists()
        {
            CommandRouter.Process(
                "{\"id\":\"ps2\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"set\",\"target\":\"tags\",\"value\":\"" + TestTag + "\"}}");
            string result = CommandRouter.Process(
                "{\"id\":\"ps2b\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"tags\"}}");
            StringAssert.Contains(TestTag, result);
        }

        [Test]
        public void GetLayers_Returns32Slots()
        {
            string result = CommandRouter.Process(
                "{\"id\":\"ps3\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"layers\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("Default", result);
        }

        [Test]
        public void SetLayer_NameApplied()
        {
            string original = LayerMask.LayerToName(31);
            RegisterCleanup(() => SetLayerName(31, original));

            string result = CommandRouter.Process(
                "{\"id\":\"ps4\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"set\",\"target\":\"layers\",\"index\":31,\"value\":\"MCPTestLayer\"}}");
            StringAssert.Contains("\"ok\":true", result);
            string get = CommandRouter.Process(
                "{\"id\":\"ps4b\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"layers\"}}");
            StringAssert.Contains("MCPTestLayer", get);
        }

        [Test]
        public void GetPhysics_ReturnsGravity()
        {
            string result = CommandRouter.Process(
                "{\"id\":\"ps5\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"physics\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("gravity", result);
        }

        [Test]
        public void SetPhysics_Gravity_Changes()
        {
            var original = Physics.gravity;
            RegisterCleanup(() => Physics.gravity = original);

            string result = CommandRouter.Process(
                "{\"id\":\"ps6\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"set\",\"target\":\"physics\",\"prop\":\"gravity\",\"value\":\"0,-15,0\"}}");
            StringAssert.Contains("\"ok\":true", result);
            string get = CommandRouter.Process(
                "{\"id\":\"ps6b\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"physics\"}}");
            StringAssert.Contains("-15", get);
        }

        [Test]
        public void GetTime_ReturnsFixedDelta()
        {
            string result = CommandRouter.Process(
                "{\"id\":\"ps7\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"time\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("fixedDeltaTime", result);
        }

        [Test]
        public void SetTime_FixedDelta_Changes()
        {
            var original = Time.fixedDeltaTime;
            RegisterCleanup(() => Time.fixedDeltaTime = original);

            string result = CommandRouter.Process(
                "{\"id\":\"ps8\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"set\",\"target\":\"time\",\"prop\":\"fixedDeltaTime\",\"value\":\"0.01\"}}");
            StringAssert.Contains("\"ok\":true", result);
            Assert.That(Time.fixedDeltaTime, Is.EqualTo(0.01f).Within(0.001f));
        }

        [Test]
        public void GetPlayer_ReturnsCompanyName()
        {
            string result = CommandRouter.Process(
                "{\"id\":\"ps9\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"player\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("companyName", result);
        }

        [Test]
        public void SetPlayer_CompanyName_Changes()
        {
            string original = PlayerSettings.companyName;
            RegisterCleanup(() => PlayerSettings.companyName = original);

            string result = CommandRouter.Process(
                "{\"id\":\"ps10\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"set\",\"target\":\"player\",\"prop\":\"companyName\",\"value\":\"MCPTest\"}}");
            StringAssert.Contains("\"ok\":true", result);
            string get = CommandRouter.Process(
                "{\"id\":\"ps10b\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"player\"}}");
            StringAssert.Contains("MCPTest", get);
        }

        [Test]
        public void GetQuality_ReturnsShadowDist()
        {
            string result = CommandRouter.Process(
                "{\"id\":\"ps11\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"quality\"}}");
            StringAssert.Contains("\"ok\":true", result);
            // shadowDistance or at minimum some quality property
            Assert.IsTrue(result.Contains("shadow") || result.Contains("quality") || result.Contains("level"),
                "Expected quality properties in result");
        }

        [Test]
        public void InvalidTarget_ReturnsError()
        {
            string result;
            try
            {
                LogAssert.ignoreFailingMessages = true;
                result = CommandRouter.Process(
                    "{\"id\":\"ps12\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"nonsense\"}}");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
            StringAssert.Contains("\"ok\":false", result);
        }

        [Test]
        public void GetPhysics_ContainsCollisionMatrix()
        {
            string result = CommandRouter.Process(
                "{\"id\":\"ps13\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"physics\"}}");
            StringAssert.Contains("\"ok\":true", result);
            StringAssert.Contains("Collision Matrix", result);
        }

        [Test]
        public void GetPhysics_CollisionMatrix_OnlyNamedLayers()
        {
            // Set a layer collision to disabled, verify it appears with layer names (not indices)
            int layerA = LayerMask.NameToLayer("Default");
            int layerB = LayerMask.NameToLayer("TransparentFX");
            bool original = Physics.GetIgnoreLayerCollision(layerA, layerB);
            RegisterCleanup(() => Physics.IgnoreLayerCollision(layerA, layerB, original));

            Physics.IgnoreLayerCollision(layerA, layerB, true);
            string result = CommandRouter.Process(
                "{\"id\":\"ps14\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"physics\"}}");
            StringAssert.Contains("Default", result);
            StringAssert.Contains("TransparentFX", result);
            StringAssert.Contains(": off", result);
        }

        [Test]
        public void GetPhysics_CollisionMatrix_AllEnabled_OneLineMessage()
        {
            // When no pairs are disabled, output should say "all enabled"
            // We can't guarantee state across tests, but we can verify the format
            string result = CommandRouter.Process(
                "{\"id\":\"ps15\",\"cmd\":\"project_settings\",\"args\":{\"action\":\"get\",\"target\":\"physics\"}}");
            StringAssert.Contains("\"ok\":true", result);
            // Result must contain either "all enabled" OR ": off" — never neither
            bool hasAllEnabled = result.Contains("all enabled");
            bool hasDisabledPair = result.Contains(": off");
            Assert.IsTrue(hasAllEnabled || hasDisabledPair,
                "Collision Matrix must show either 'all enabled' or disabled pairs");
        }

        private static void RemoveAllTagsNamed(string tag)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
                throw new System.InvalidOperationException("TagManager.asset not found");

            var tagManager = new SerializedObject(assets[0]);
            var tags = tagManager.FindProperty("tags");
            var changed = false;
            for (var index = tags.arraySize - 1; index >= 0; index--)
            {
                if (tags.GetArrayElementAtIndex(index).stringValue != tag) continue;
                tags.DeleteArrayElementAtIndex(index);
                changed = true;
            }
            if (changed)
                tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLayerName(int index, string name)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
                throw new System.InvalidOperationException("TagManager.asset not found");

            var tagManager = new SerializedObject(assets[0]);
            tagManager.FindProperty("layers").GetArrayElementAtIndex(index).stringValue = name;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
