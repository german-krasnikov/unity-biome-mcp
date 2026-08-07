using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;
using UnityMCP.Editor;
using UnityMCP.Editor.Tests;

namespace UnityMCP.TestProject.Animation
{
    [TestFixture]
    public class AnimationComponentTypeTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private const string TempFolder = "Assets/TestsTemp/AnimationComponentTypeTests";

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(AnimationHelper.ResetAssetDirectoryForTests);
            TrackOwnedAsset(TempFolder);
            TestPaths.EnsureFolder(TempFolder);
            AnimationHelper.SetAssetDirectoryForTests(TempFolder);
        }

        [Test]
        public void CreateClip_DefaultComponentType_UsesTransform()
        {
            var go = new GameObject("CT_Default");
            try
            {
                var result = CommandRouter.Process(
                    "{\"id\":\"ct1\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/CT_Default\",\"clip_name\":\"CT_DefaultClip\",\"property\":\"localPosition\",\"keys\":\"t:0 v:(0,0,0); t:1 v:(1,0,0)\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("created", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateClip_LightIntensity_UsesCurveOnLightType()
        {
            var go = new GameObject("CT_Light");
            go.AddComponent<Light>();
            try
            {
                // Light.m_Intensity is the serialized field name for intensity
                var result = CommandRouter.Process(
                    "{\"id\":\"ct2\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/CT_Light\",\"clip_name\":\"CT_LightClip\",\"property\":\"m_Intensity\",\"keys\":\"t:0 v:0; t:1 v:5\",\"component_type\":\"Light\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("created", result);

                // Verify binding is on Light, not Transform
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TempFolder + "/CT_LightClip.anim");
                Assert.IsNotNull(clip);
                var bindings = AnimationUtility.GetCurveBindings(clip);
                Assert.AreEqual(1, bindings.Length, "Should have one curve for m_Intensity");
                Assert.AreEqual(typeof(Light), bindings[0].type, "Binding type should be Light");
                Assert.AreEqual("m_Intensity", bindings[0].propertyName);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void CreateClip_UnknownComponentType_ThrowsError()
        {
            var go = new GameObject("CT_Unknown");
            try
            {
                LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Component type not found: NonExistentComponent99"));
                var result = CommandRouter.Process(
                    "{\"id\":\"ct3\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/CT_Unknown\",\"clip_name\":\"CT_UnknownClip\",\"property\":\"someField\",\"keys\":\"t:0 v:0; t:1 v:1\",\"component_type\":\"NonExistentComponent99\"}}");
                StringAssert.Contains("\"ok\":false", result);
                StringAssert.Contains("NonExistentComponent99", result);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void EditClip_AddKey_WithComponentType_UsesCorrectBinding()
        {
            var go = new GameObject("CT_EditLight");
            go.AddComponent<Light>();
            try
            {
                // Create clip with Light component type
                CommandRouter.Process(
                    "{\"id\":\"ct4a\",\"cmd\":\"animation\",\"args\":{\"action\":\"create\",\"path\":\"/CT_EditLight\",\"clip_name\":\"CT_EditLightClip\",\"property\":\"m_Intensity\",\"keys\":\"t:0 v:0; t:1 v:5\",\"component_type\":\"Light\"}}");

                // Add a key using same component_type
                var result = CommandRouter.Process(
                    "{\"id\":\"ct4b\",\"cmd\":\"animation\",\"args\":{\"action\":\"add_key\",\"path\":\"/CT_EditLight\",\"clip\":\"CT_EditLightClip\",\"property\":\"m_Intensity\",\"keys\":\"t:0.5 v:10\",\"component_type\":\"Light\"}}");
                StringAssert.Contains("\"ok\":true", result);
                StringAssert.Contains("edited", result);

                // Verify 3 keyframes exist
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(TempFolder + "/CT_EditLightClip.anim");
                var binding = EditorCurveBinding.FloatCurve("", typeof(Light), "m_Intensity");
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                Assert.IsNotNull(curve, "Curve on Light should exist");
                Assert.AreEqual(3, curve.keys.Length, "Should have 3 keys: t=0, t=0.5, t=1");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
