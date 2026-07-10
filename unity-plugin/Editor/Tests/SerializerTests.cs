// TDD — AnimationSerializer, ParticleSerializer, ShaderSerializer,
//         TimelineSerializer, AnimatorControllerHelper (pure-logic paths).
// EditMode tests — run in Unity Test Runner (Window > General > Test Runner > EditMode).
//
// SKIPPED (require PlayMode / AnimationMode / asset files):
//   AnimationSerializer.SerializeClipAtTime — requires AnimationMode.StartAnimationMode
//   AnimatorControllerSerializer.Serialize  — requires AnimatorController asset on disk
//   TimelineSerializer.Serialize            — requires PlayableDirector bound to TimelineAsset

using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

namespace UnityMCP.Editor.Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // AnimationSerializer — GetAllClips + FindClip
    // ─────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class AnimationSerializerTests : SceneTestBase
    {
        private GameObject _go;
        private readonly System.Collections.Generic.List<UnityEngine.Object> _assets = new();

        [SetUp]
        public void SetUp() => _go = new GameObject("AnimSerTest");

        [TearDown]
        public void TearDown()
        {
            foreach (var a in _assets)
                if (a != null) UnityEngine.Object.DestroyImmediate(a);
            _assets.Clear();
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void GetAllClips_NoAnimatorNoAnimation_ReturnsNull()
        {
            Assert.IsNull(AnimationSerializer.GetAllClips(_go));
        }

        [Test]
        public void GetAllClips_LegacyAnimationWithClip_ReturnsClipArray()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "TestClip", legacy = true };
            _assets.Add(clip);
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.GetAllClips(_go);

            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("TestClip", result[0].name);
        }

        [Test]
        public void FindClip_NullClips_ReturnsNull()
        {
            // GO has no Animator/Animation → GetAllClips returns null
            var result = AnimationSerializer.FindClip(_go, "Missing");
            Assert.IsNull(result);
        }

        [Test]
        public void FindClip_MatchingName_ReturnsClip()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Walk", legacy = true };
            _assets.Add(clip);
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.FindClip(_go, "Walk");

            Assert.IsNotNull(result);
            Assert.AreEqual("Walk", result.name);
        }

        [Test]
        public void FindClip_WrongName_ReturnsNull()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Walk", legacy = true };
            _assets.Add(clip);
            anim.AddClip(clip, clip.name);

            Assert.IsNull(AnimationSerializer.FindClip(_go, "Run"));
        }

        [Test]
        public void Serialize_NoAnimation_ReturnsNoClipsMessage()
        {
            // Serialize public entry: path only (no clipName) → goes to SerializeClipList
            // but that calls ComponentSerializer.FindObject which needs a scene path
            // Register the object via its hierarchy path
            var result = AnimationSerializer.Serialize("/" + _go.name, null, null);
            Assert.AreEqual("No animation clips", result);
        }

        [Test]
        public void Serialize_WithClip_ContainsClipNameAndCurves()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Jump", legacy = true };
            _assets.Add(clip);
            // Add one curve so binding count is 1
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), curve);
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, null, null);

            StringAssert.Contains("Jump", result);
            StringAssert.Contains("1 curves", result);
        }

        [Test]
        public void Serialize_ClipDetail_ContainsClipNameAndLength()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Idle", legacy = true };
            _assets.Add(clip);
            var curve = AnimationCurve.Linear(0f, 0f, 2f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), curve);
            anim.AddClip(clip, clip.name);

            // Serialize with clipName — calls SerializeClipDetail
            var result = AnimationSerializer.Serialize("/" + _go.name, "Idle", null);

            StringAssert.Contains("Idle", result);
            StringAssert.Contains("2.0s", result);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ParticleSerializer — overview + module paths via scene GO
    // ─────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class ParticleSerializerTests : SceneTestBase
    {
        private GameObject _go;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("PSTest");
            _go.AddComponent<ParticleSystem>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_go);

        private string Path => "/" + _go.name;

        [Test]
        public void Serialize_Overview_ContainsParticleSystemHeader()
        {
            var result = ParticleSerializer.Serialize(Path);
            StringAssert.Contains("ParticleSystem on", result);
            StringAssert.Contains("PSTest", result);
        }

        [Test]
        public void Serialize_Overview_ContainsMainModuleFields()
        {
            var result = ParticleSerializer.Serialize(Path);
            StringAssert.Contains("main:", result);
            StringAssert.Contains("duration=", result);
            StringAssert.Contains("maxParticles=", result);
        }

        [Test]
        public void Serialize_Overview_ListsKnownModules()
        {
            var result = ParticleSerializer.Serialize(Path);
            StringAssert.Contains("emission:", result);
            StringAssert.Contains("shape:", result);
            StringAssert.Contains("noise:", result);
        }

        [Test]
        public void Serialize_MainModule_ContainsAllKeys()
        {
            var result = ParticleSerializer.Serialize(Path, "main");
            StringAssert.Contains("main:", result);
            StringAssert.Contains("duration:", result);
            StringAssert.Contains("loop:", result);
            StringAssert.Contains("maxParticles:", result);
        }

        [Test]
        public void Serialize_EmissionModule_ContainsEnabledAndRate()
        {
            var result = ParticleSerializer.Serialize(Path, "emission");
            StringAssert.Contains("emission:", result);
            StringAssert.Contains("enabled:", result);
            StringAssert.Contains("rateOverTime:", result);
        }

        [Test]
        public void Serialize_ShapeModule_ContainsShapeType()
        {
            var result = ParticleSerializer.Serialize(Path, "shape");
            StringAssert.Contains("shape:", result);
            StringAssert.Contains("shapeType:", result);
        }

        [Test]
        public void Serialize_UnknownModule_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(
                () => ParticleSerializer.Serialize(Path, "bogusmodule"));
        }

        [Test]
        public void Serialize_RendererModule_ContainsRenderMode()
        {
            var result = ParticleSerializer.Serialize(Path, "renderer");
            StringAssert.Contains("renderer:", result);
            StringAssert.Contains("renderMode:", result);
        }

        [Test]
        public void Serialize_NoiseModule_ContainsFrequency()
        {
            var result = ParticleSerializer.Serialize(Path, "noise");
            StringAssert.Contains("noise:", result);
            StringAssert.Contains("frequency:", result);
        }

        [Test]
        public void Serialize_ColorOverLifetime_ContainsEnabled()
        {
            var result = ParticleSerializer.Serialize(Path, "colorOverLifetime");
            StringAssert.Contains("colorOverLifetime:", result);
            StringAssert.Contains("enabled:", result);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ShaderSerializer — material serialization via scene GO with Renderer
    // ─────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class ShaderSerializerTests : SceneTestBase
    {
        private GameObject _go;
        private Material _mat;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("ShaderTest");
            var renderer = _go.AddComponent<MeshRenderer>();
            // Prefer URP Lit; fall back to Standard or Unlit
            var shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Unlit/Color");
            if (shader != null)
            {
                _mat = new Material(shader);
                renderer.sharedMaterial = _mat;
            }
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
            if (_mat != null) UnityEngine.Object.DestroyImmediate(_mat);
        }

        private string Path => "/" + _go.name;

        [Test]
        public void Serialize_Material_ContainsShaderLine()
        {
            var renderer = _go.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                Assert.Inconclusive("No standard/URP shader available in this project");

            var result = ShaderSerializer.Serialize(Path, "material");

            StringAssert.Contains("shader:", result);
        }

        [Test]
        public void Serialize_Material_ContainsObjectPath()
        {
            var renderer = _go.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                Assert.Inconclusive("No standard/URP shader available in this project");

            var result = ShaderSerializer.Serialize(Path, "material");

            StringAssert.Contains("ShaderTest", result);
        }

        [Test]
        public void Serialize_Material_ContainsKeywordsLine()
        {
            var renderer = _go.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                Assert.Inconclusive("No standard/URP shader available in this project");

            var result = ShaderSerializer.Serialize(Path, "material");

            StringAssert.Contains("keywords:", result);
        }

        [Test]
        public void Serialize_NoRenderer_ThrowsInvalidOperation()
        {
            // Use a GO with no renderer
            var plain = new GameObject("PlainGO");
            try
            {
                Assert.Throws<InvalidOperationException>(
                    () => ShaderSerializer.Serialize("/PlainGO", "material"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(plain);
            }
        }

        [Test]
        public void Serialize_BuiltinShaderAssetPath_ContainsProperties()
        {
            var renderer = _go.GetComponent<Renderer>();
            if (renderer == null || renderer.sharedMaterial == null)
                Assert.Inconclusive("No shader available in this project (no Standard or URP Lit)");

            // Use scene-object path with target != "material" → LoadShader via renderer
            var result = ShaderSerializer.Serialize(Path, "shader");

            StringAssert.Contains("Shader:", result);
            StringAssert.Contains("properties:", result);
        }

        [Test]
        public void Serialize_ShaderWithIntProperty_DoesNotThrow()
        {
            // Exercises the GetPropertyDefaultIntValue branch in ShaderSerializer (line 110)
            // Creates its own minimal shader so the test is order-independent.
            var folder = TestPaths.ForFixture("ShaderSerializerTests");
            var assetPath = folder + "/IntProp.shader";
            const string shaderSrc =
                "Shader \"Test/IntProp\" {\n" +
                "    Properties { _IntProp (\"Int\", Int) = 42 }\n" +
                "    SubShader { Pass { CGPROGRAM\n" +
                "    #pragma vertex vert\n" +
                "    #pragma fragment frag\n" +
                "    float4 vert(float4 v:POSITION):SV_POSITION{return v;}\n" +
                "    float4 frag():SV_TARGET{return 0;}\n" +
                "    ENDCG } }\n" +
                "}\n";

            try
            {
                TestPaths.EnsureFolder(folder);

                System.IO.File.WriteAllText(
                    System.IO.Path.Combine(UnityEngine.Application.dataPath, "../" + assetPath),
                    shaderSrc);

                LogAssert.ignoreFailingMessages = true;
                AssetDatabase.Refresh();
                LogAssert.ignoreFailingMessages = false;

                var loaded = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(loaded, "Shader asset not created at " + assetPath);

                string result = null;
                Assert.DoesNotThrow(() => result = ShaderSerializer.Serialize(assetPath, "shader"));
                Assert.IsNotNull(result);
                StringAssert.Contains("_IntProp", result);
                StringAssert.Contains("42", result);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = true;
                if (AssetDatabase.IsValidFolder(folder))
                    AssetDatabase.DeleteAsset(folder);
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TimelineSerializer — TrackTypeName (pure string) + FindTrack (in-memory asset)
    // ─────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class TimelineSerializerTests
    {
        private TimelineAsset _timeline;
        private static readonly string AssetFolder = TestPaths.ForFixture("TimelineSerializerTests");
        private static readonly string AssetPath = AssetFolder + "/Tests_TimelineTemp.playable";

        [SetUp]
        public void SetUp()
        {
            TestPaths.EnsureFolder(AssetFolder);
            _timeline = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(_timeline, AssetPath);
            AssetDatabase.SaveAssets();
            _timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(AssetPath);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(AssetPath);
        }

        // ── TrackTypeName ─────────────────────────────────────────────────────

        [Test]
        public void TrackTypeName_AnimationTrack_ReturnsAnimation()
        {
            var track = _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "Anim");
            Assert.AreEqual("Animation", TimelineSerializer.TrackTypeName(track));
        }

        [Test]
        public void TrackTypeName_GroupTrack_ReturnsGroup()
        {
            var group = _timeline.CreateTrack<GroupTrack>(null, "MyGroup");
            Assert.AreEqual("Group", TimelineSerializer.TrackTypeName(group));
        }

        // ── FindTrack ─────────────────────────────────────────────────────────

        [Test]
        public void FindTrack_ExactName_ReturnsTrack()
        {
            _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "Hero");

            var result = TimelineSerializer.FindTrack(_timeline, "Hero");

            Assert.IsNotNull(result);
            Assert.AreEqual("Hero", result.name);
        }

        [Test]
        public void FindTrack_CaseInsensitive_ReturnsTrack()
        {
            _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "Fx");

            var result = TimelineSerializer.FindTrack(_timeline, "fx");

            Assert.IsNotNull(result);
        }

        [Test]
        public void FindTrack_Missing_ReturnsNull()
        {
            Assert.IsNull(TimelineSerializer.FindTrack(_timeline, "NonExistent"));
        }

        // ── Resolve via asset path ────────────────────────────────────────────

        [Test]
        public void Resolve_ValidAssetPath_ReturnsTimeline()
        {
            var (director, timeline) = TimelineSerializer.Resolve(AssetPath);
            Assert.IsNull(director);
            Assert.IsNotNull(timeline);
            Assert.AreEqual(_timeline.name, timeline.name);
        }

        [Test]
        public void Resolve_InvalidPath_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(
                () => TimelineSerializer.Resolve("/NonExistentGO_XYZ"));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AnimatorControllerHelper — ParseCondition (pure logic, no disk I/O)
    // ─────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class AnimatorControllerHelperParseConditionTests
    {
        // ParseCondition(string condStr, AnimatorController ctrl)
        // ctrl is only used to look up existing params for Trigger/Bool type gating.
        // For pure operator parsing, ctrl can be null since we don't hit that branch.

        private AnimatorController _ctrl;
        private static readonly string CtrlFolder = TestPaths.ForFixture("AnimatorControllerTests");
        private static readonly string CtrlPath = CtrlFolder + "/Tests_AnimCtrlTemp.controller";

        [SetUp]
        public void SetUp()
        {
            TestPaths.EnsureFolder(CtrlFolder);
            _ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(CtrlPath);
            AssetDatabase.Refresh();
        }

        [Test]
        public void ParseCondition_BangPrefix_ReturnsIfNot()
        {
            var result = AnimatorControllerHelper.ParseCondition("!IsGrounded", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.IfNot, result.mode);
            Assert.AreEqual("IsGrounded", result.parameter);
        }

        [Test]
        public void ParseCondition_GreaterOp_ReturnsGreater()
        {
            var result = AnimatorControllerHelper.ParseCondition("Speed>0.5", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.Greater, result.mode);
            Assert.AreEqual("Speed", result.parameter);
            Assert.AreEqual(0.5f, result.threshold, 0.0001f);
        }

        [Test]
        public void ParseCondition_LessOp_ReturnsLess()
        {
            var result = AnimatorControllerHelper.ParseCondition("HP<10", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.Less, result.mode);
            Assert.AreEqual("HP", result.parameter);
            Assert.AreEqual(10f, result.threshold, 0.0001f);
        }

        [Test]
        public void ParseCondition_EqualOp_ReturnsEquals()
        {
            var result = AnimatorControllerHelper.ParseCondition("State=2", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.Equals, result.mode);
            Assert.AreEqual("State", result.parameter);
            Assert.AreEqual(2f, result.threshold, 0.0001f);
        }

        [Test]
        public void ParseCondition_NotEqualOp_ReturnsNotEqual()
        {
            var result = AnimatorControllerHelper.ParseCondition("State!=0", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.NotEqual, result.mode);
            Assert.AreEqual("State", result.parameter);
        }

        [Test]
        public void ParseCondition_EqualTrueValue_ReturnsIf()
        {
            var result = AnimatorControllerHelper.ParseCondition("IsRunning==true", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.If, result.mode);
            Assert.AreEqual("IsRunning", result.parameter);
        }

        [Test]
        public void ParseCondition_EqualFalseValue_ReturnsIfNot()
        {
            var result = AnimatorControllerHelper.ParseCondition("IsRunning==false", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.IfNot, result.mode);
            Assert.AreEqual("IsRunning", result.parameter);
        }

        [Test]
        public void ParseCondition_BareName_ReturnsIf()
        {
            var result = AnimatorControllerHelper.ParseCondition("Jump", _ctrl);
            Assert.AreEqual(AnimatorConditionMode.If, result.mode);
            Assert.AreEqual("Jump", result.parameter);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AnimatorControllerHelper — FindState (in-memory SM via temp asset)
    // ─────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class AnimatorControllerHelperFindStateTests
    {
        private AnimatorController _ctrl;
        private static readonly string CtrlFolder = TestPaths.ForFixture("AnimControllerFindStateTests");
        private static readonly string CtrlPath = CtrlFolder + "/Tests_AnimCtrlFindState.controller";

        [SetUp]
        public void SetUp()
        {
            TestPaths.EnsureFolder(CtrlFolder);
            _ctrl = AnimatorController.CreateAnimatorControllerAtPath(CtrlPath);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(CtrlPath);
            AssetDatabase.Refresh();
        }

        [Test]
        public void FindState_ExistingState_ReturnsState()
        {
            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            sm.AddState("Idle");

            var result = AnimatorControllerHelper.FindState(sm, "Idle");

            Assert.IsNotNull(result);
            Assert.AreEqual("Idle", result.name);
        }

        [Test]
        public void FindState_MissingState_ReturnsNull()
        {
            var sm = AnimatorControllerHelper.GetStateMachine(_ctrl);
            Assert.IsNull(AnimatorControllerHelper.FindState(sm, "Ghost"));
        }
    }
}
