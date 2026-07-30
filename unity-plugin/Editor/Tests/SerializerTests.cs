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
using UnityEngine.Playables;
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

        [Test]
        public void Serialize_ClipDetail_CurveHeaderPresent()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T1", legacy = true };
            _assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T1", null);

            StringAssert.Contains("curve:", result);
        }

        [Test]
        public void Serialize_ClipDetail_KeyframesOnePerLine_NoAtSign()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T2", legacy = true };
            _assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T2", null);

            StringAssert.Contains("  0.000: 0", result);
            StringAssert.Contains("  1.000: 1", result);
            StringAssert.DoesNotContain("@", result);
        }

        [Test]
        public void Serialize_ClipDetail_ChildPathInCurveHeader()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T3", legacy = true };
            _assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Head/Jaw", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T3", null);

            StringAssert.Contains("(Head/Jaw)", result);
            foreach (var line in result.Split('\n'))
                if (line.StartsWith("  "))
                    StringAssert.DoesNotContain("Head/Jaw", line);
        }

        [Test]
        public void Serialize_ClipDetail_BindingSortDeterministic()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T4", legacy = true };
            _assets.Add(clip);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), curve);
            anim.AddClip(clip, clip.name);

            var result1 = AnimationSerializer.Serialize("/" + _go.name, "T4", null);
            var result2 = AnimationSerializer.Serialize("/" + _go.name, "T4", null);

            Assert.AreEqual(result1, result2, "Output must be identical across two calls");
            int idxX = result1.IndexOf("$pos.x", System.StringComparison.Ordinal);
            int idxZ = result1.IndexOf("$pos.z", System.StringComparison.Ordinal);
            Assert.Less(idxX, idxZ, "x curve must precede z curve in sorted output");
        }

        [Test]
        public void Serialize_ClipDetail_FiftyKeyCapWithTruncation()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T5", legacy = true };
            _assets.Add(clip);
            var keys = new Keyframe[51];
            for (int i = 0; i < 51; i++) keys[i] = new Keyframe(i * 0.02f, i);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                new AnimationCurve(keys));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T5", null);

            StringAssert.Contains("  ...+1 more", result);
            StringAssert.DoesNotContain("  ...+0", result);
        }

        [Test]
        public void Serialize_ClipDetail_EmptyClip_NoCurveBlock()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T6", legacy = true };
            _assets.Add(clip);
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T6", null);

            StringAssert.Contains("clip:", result);
            StringAssert.DoesNotContain("curve:", result);
            StringAssert.DoesNotContain("ref:", result);
        }

        [Test]
        public void Serialize_ClipDetail_PropertyAlias_EmittedAndUsed()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T7", legacy = true };
            _assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T7", null);

            StringAssert.Contains("VAL $pos m_LocalPosition", result);
            StringAssert.Contains("curve: $pos.x", result);
            StringAssert.DoesNotContain("curve: m_LocalPosition.x", result);
        }

        [Test]
        public void Serialize_ClipDetail_PathAlias_WhenUsedTwice()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T8", legacy = true };
            _assets.Add(clip);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Spine/Head", typeof(Transform), "m_LocalPosition.x"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Spine/Head", typeof(Transform), "m_LocalPosition.y"), curve);
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T8", null);

            StringAssert.Contains("VAL $head Spine/Head", result);
            StringAssert.Contains("($head)", result);
            StringAssert.DoesNotContain("(Spine/Head)", result);
        }

        [Test]
        public void Serialize_ClipDetail_PathAlias_SingleUse_NoAlias()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T9", legacy = true };
            _assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Spine/Head", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, 1f, 1f));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T9", null);

            StringAssert.DoesNotContain("VAL $head", result);
            StringAssert.Contains("(Spine/Head)", result);
        }

        [Test]
        public void Serialize_ClipDetail_PathAliasCollision_ParentSegmentPrepended()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T10", legacy = true };
            _assets.Add(clip);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Jaw/Head", typeof(Transform), "m_LocalPosition.x"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Jaw/Head", typeof(Transform), "m_LocalPosition.y"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Spine/Head", typeof(Transform), "m_LocalPosition.x"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Spine/Head", typeof(Transform), "m_LocalPosition.y"), curve);
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T10", null);

            bool hasHead     = result.Contains("VAL $head ");
            bool hasCompound = result.Contains("VAL $jaw_head ") || result.Contains("VAL $spine_head ");
            Assert.IsTrue(hasHead,     "First path must get $head alias");
            Assert.IsTrue(hasCompound, "Second path must get compound alias (parent_head)");
        }

        [Test]
        public void Serialize_ClipDetail_PPtrCurve_RefPrefixAndObjectName()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T11", legacy = true };
            _assets.Add(clip);

            var mat = new Material(Shader.Find("Hidden/InternalErrorShader")
                                   ?? Shader.Find("Standard"));
            mat.name = "IdleFrame0";
            _assets.Add(mat);

            var ptrBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, ptrBinding,
                new[] { new ObjectReferenceKeyframe { time = 0f, value = mat } });
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T11", null);

            StringAssert.Contains("ref: m_Sprite", result);
            StringAssert.Contains("IdleFrame0", result);
        }

        [Test]
        public void Serialize_ClipDetail_PPtrCurve_NullValue()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T12", legacy = true };
            _assets.Add(clip);

            var ptrBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, ptrBinding,
                new[] { new ObjectReferenceKeyframe { time = 0f, value = null } });
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T12", null);

            StringAssert.Contains("ref: m_Sprite", result);
            StringAssert.Contains("  0.000: null", result);
        }

        [Test]
        public void Serialize_ClipDetail_Header_ShowsCurvesAndRefs()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T13", legacy = true };
            _assets.Add(clip);

            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), curve);

            var ptrBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AnimationUtility.SetObjectReferenceCurve(clip, ptrBinding,
                new[] { new ObjectReferenceKeyframe { time = 0f, value = null } });

            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T13", null);

            var headerLine = result.Split('\n')[0];
            StringAssert.Contains("2 curves", headerLine);
            StringAssert.Contains("1 refs", headerLine);
        }

        [Test]
        public void Serialize_ClipDetail_MixedClip_AllFeaturesPresent()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "T14", legacy = true };
            _assets.Add(clip);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Spine/Head", typeof(Transform), "m_LocalPosition.x"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Spine/Head", typeof(Transform), "m_LocalPosition.y"), curve);

            var mat = new Material(Shader.Find("Hidden/InternalErrorShader")
                                   ?? Shader.Find("Standard")) { name = "Frame0" };
            _assets.Add(mat);
            AnimationUtility.SetObjectReferenceCurve(clip,
                EditorCurveBinding.PPtrCurve("Spine/Head", typeof(SpriteRenderer), "m_Sprite"),
                new[] { new ObjectReferenceKeyframe { time = 0f, value = mat } });

            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "T14", null);

            StringAssert.Contains("VAL $pos m_LocalPosition", result);
            StringAssert.Contains("VAL $head Spine/Head", result);
            StringAssert.Contains("curve: $pos.x ($head)", result);
            StringAssert.Contains("ref: m_Sprite ($head)", result);
            StringAssert.Contains("2 curves", result);
            StringAssert.Contains("1 refs", result);
        }

        // ── Edge-case tests ──────────────────────────────────────────────────

        [Test]
        public void Serialize_ClipDetail_100Keyframes_TruncatesAt50()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Long", legacy = true };
            _assets.Add(clip);
            var keys = new Keyframe[100];
            for (int i = 0; i < 100; i++) keys[i] = new Keyframe(i * 0.033f, i);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.x"),
                new AnimationCurve(keys));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "Long", null);

            StringAssert.Contains("+", result);
            var lines = result.Split('\n');
            int keyLines = System.Array.FindAll(lines,
                l => l.TrimStart().Length > 0 && char.IsDigit(l.TrimStart()[0]) && l.Contains(":")).Length;
            Assert.LessOrEqual(keyLines, 50, "Must not emit more than 50 keyframe lines");
        }

        [Test]
        public void Serialize_ClipDetail_SamePathMultipleCurves_OnlyOnePathVAL()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Pos", legacy = true };
            _assets.Add(clip);
            var curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Head", typeof(Transform), "m_LocalPosition.x"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Head", typeof(Transform), "m_LocalPosition.y"), curve);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("Head", typeof(Transform), "m_LocalPosition.z"), curve);
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "Pos", null);

            var valLines = System.Array.FindAll(result.Split('\n'),
                l => l.StartsWith("VAL ") && l.Contains("Head"));
            Assert.LessOrEqual(valLines.Length, 1, "Path used 3x must emit at most 1 VAL alias line");
        }

        [Test]
        public void Serialize_ClipDetail_UnknownProperty_RawNameInCurveHeader()
        {
            var anim = _go.AddComponent<Animation>();
            var clip = new AnimationClip { name = "Custom", legacy = true };
            _assets.Add(clip);
            AnimationUtility.SetEditorCurve(clip,
                EditorCurveBinding.FloatCurve("", typeof(Transform), "m_SomeCustomProp"),
                AnimationCurve.Constant(0f, 1f, 0f));
            anim.AddClip(clip, clip.name);

            var result = AnimationSerializer.Serialize("/" + _go.name, "Custom", null);

            StringAssert.Contains("m_SomeCustomProp", result);
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
    public class TimelineSerializerTests : SceneTestBase
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

        // ── Change 1: GroupTrack Indent ───────────────────────────────────────

        [Test]
        public void RootTrack_HasNoLeadingSpaces()
        {
            _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "RootAnim");
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.Contains("\n[Animation] RootAnim", result);
        }

        [Test]
        public void GroupTrackChildren_AreIndentedTwoSpaces()
        {
            var group = _timeline.CreateTrack<GroupTrack>(null, "MyGroup");
            _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(group, "ChildTrack");
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.Contains("[Group] MyGroup", result);
            StringAssert.Contains("  [Animation] ChildTrack", result);
        }

        [Test]
        public void NestedGroupTrack_ChildrenGetDeeperIndent()
        {
            var outer = _timeline.CreateTrack<GroupTrack>(null, "Outer");
            var inner = _timeline.CreateTrack<GroupTrack>(outer, "Inner");
            _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(inner, "Deep");
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.Contains("[Group] Outer", result);
            StringAssert.Contains("  [Group] Inner", result);
            StringAssert.Contains("    [Animation] Deep", result);
        }

        [Test]
        public void ClipLinesUnderNestedTrack_UseDeepIndent()
        {
            var group = _timeline.CreateTrack<GroupTrack>(null, "G");
            var child = _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(group, "T");
            child.CreateDefaultClip();
            AssetDatabase.SaveAssets();
            var result = TimelineSerializer.Serialize(AssetPath, null);
            // child is at indent=1 → clip prefix = indent*2+2 = 4 spaces
            var lines = result.Split('\n');
            bool found = false;
            foreach (var line in lines)
                if (line.StartsWith("    ") && !line.StartsWith("     ") && !line.TrimStart().StartsWith("["))
                    found = true;
            Assert.IsTrue(found, "Expected a clip line with exactly 4-space indent under child track");
        }

        // ── Change 2: Markers Inline ──────────────────────────────────────────

        [Test]
        public void SignalTrack_WithMarkers_ShowsMarkerCountNotClipCount()
        {
            var signal = _timeline.CreateTrack<UnityEngine.Timeline.SignalTrack>(null, "Events");
            signal.CreateMarker<UnityEngine.Timeline.SignalEmitter>(2.5);
            signal.CreateMarker<UnityEngine.Timeline.SignalEmitter>(5.0);
            signal.CreateMarker<UnityEngine.Timeline.SignalEmitter>(10.0);
            AssetDatabase.SaveAssets();
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.Contains("| 3 markers", result);
            StringAssert.DoesNotContain("| 0 clips", result);
        }

        [Test]
        public void SignalTrack_WithMarkers_ShowsMarkersInline()
        {
            var signal = _timeline.CreateTrack<UnityEngine.Timeline.SignalTrack>(null, "Events");
            signal.CreateMarker<UnityEngine.Timeline.SignalEmitter>(2.5);
            signal.CreateMarker<UnityEngine.Timeline.SignalEmitter>(5.0);
            AssetDatabase.SaveAssets();
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.Contains("  2.5s: ", result);
            StringAssert.Contains("  5.0s: ", result);
        }

        [Test]
        public void AnimationTrack_WithoutMarkers_ShowsClipCount()
        {
            var track = _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "Hero");
            track.CreateDefaultClip();
            track.CreateDefaultClip();
            AssetDatabase.SaveAssets();
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.Contains("| 2 clips", result);
            StringAssert.DoesNotContain("markers", result);
        }

        // ── Change 3: Compact Binding Syntax ─────────────────────────────────

        [Test]
        public void UnboundTrack_NoDirector_NoBindingText()
        {
            _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "Hero");
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.DoesNotContain("unbound", result);
            StringAssert.DoesNotContain("bound:", result);
            StringAssert.DoesNotContain(" → ", result);
        }

        [Test]
        public void BoundTrack_UsesArrowSyntax()
        {
            var track = _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(null, "Cam");
            var dirGo = new GameObject("BindingTestDirector");
            try
            {
                var director = dirGo.AddComponent<PlayableDirector>();
                director.playableAsset = _timeline;
                director.SetGenericBinding(track, dirGo);
                var result = TimelineSerializer.Serialize("/BindingTestDirector", null);
                StringAssert.Contains("→ /BindingTestDirector", result);
                StringAssert.DoesNotContain("bound:", result);
                StringAssert.DoesNotContain("unbound", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(dirGo);
            }
        }

        [Test]
        public void MutedChildTrack_ShowsIndentAndMutedFlag()
        {
            var group = _timeline.CreateTrack<GroupTrack>(null, "G");
            var child = _timeline.CreateTrack<UnityEngine.Timeline.AnimationTrack>(group, "Hero");
            child.muted = true;
            AssetDatabase.SaveAssets();
            var result = TimelineSerializer.Serialize(AssetPath, null);
            StringAssert.Contains("  [Animation] Hero", result);
            StringAssert.Contains("| muted", result);
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

    // ─────────────────────────────────────────────────────────────────────────
    // AnimatorControllerSerializer — overview output (11 tests + 2 edge-case)
    // ─────────────────────────────────────────────────────────────────────────

    [TestFixture]
    public class AnimatorControllerSerializerOverviewTests
    {
        private AnimatorController _ctrl;
        private static readonly string CtrlFolder = TestPaths.ForFixture("AnimCtrlSerializerOverviewTests");
        private static readonly string CtrlPath = CtrlFolder + "/Tests_AnimCtrlOverview.controller";

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

        // T01: mask: present when AvatarMask assigned
        [Test]
        public void LayerHeader_WithAvatarMask_ContainsMaskName()
        {
            var sm = _ctrl.layers[0].stateMachine;
            sm.AddState("Idle");
            var mask = new AvatarMask { name = "BodyMask" };
            var layers = _ctrl.layers;
            layers[0].avatarMask = mask;
            _ctrl.layers = layers;

            try
            {
                var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);
                StringAssert.Contains("mask:BodyMask", result);
            }
            finally { UnityEngine.Object.DestroyImmediate(mask); }
        }

        // T02: mask: absent when no AvatarMask
        [Test]
        public void LayerHeader_WithoutAvatarMask_NoMaskToken()
        {
            _ctrl.layers[0].stateMachine.AddState("Idle");

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.DoesNotContain("mask:", result);
        }

        // T03: !wdv when writeDefaultValues=false
        [Test]
        public void StateLine_WriteDefaultValuesFalse_ContainsWdvFlag()
        {
            var state = _ctrl.layers[0].stateMachine.AddState("Walk");
            state.writeDefaultValues = false;

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.Contains("!wdv", result);
        }

        // T04: no !wdv when writeDefaultValues=true (default)
        [Test]
        public void StateLine_WriteDefaultValuesTrue_NoWdvFlag()
        {
            _ctrl.layers[0].stateMachine.AddState("Idle");

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.DoesNotContain("!wdv", result);
        }

        // T05: SSM listed as [SSM:Name] +N states
        [Test]
        public void StateMachine_WithSSM_ShowsSSMPlaceholder()
        {
            var sm = _ctrl.layers[0].stateMachine;
            sm.AddState("Root");
            var ssm = sm.AddStateMachine("CombatSM");
            ssm.AddState("Attack");
            ssm.AddState("Defend");
            ssm.AddState("Block");

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.Contains("[SSM:CombatSM] +3 states", result);
        }

        // T06: totalStates header count includes SSM states
        [Test]
        public void Header_TotalStatesIncludesSSMStates()
        {
            var sm = _ctrl.layers[0].stateMachine;
            sm.AddState("Root");
            var ssm = sm.AddStateMachine("Sub");
            ssm.AddState("A");
            ssm.AddState("B");

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);
            var header = result.Split('\n')[0];

            StringAssert.Contains("3 states", header);
        }

        // T07: parametric speed shows speed:ParamName
        [Test]
        public void StateLine_SpeedParameterActive_ShowsParamName()
        {
            var sm = _ctrl.layers[0].stateMachine;
            var state = sm.AddState("Run");
            _ctrl.AddParameter("SpeedParam", AnimatorControllerParameterType.Float);
            state.speedParameterActive = true;
            state.speedParameter = "SpeedParam";

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.Contains("speed:SpeedParam", result);
            StringAssert.DoesNotContain("1x", result);
        }

        // T08: non-parametric speed shows Nx literal
        [Test]
        public void StateLine_SpeedParameterInactive_ShowsLiteralSpeed()
        {
            _ctrl.layers[0].stateMachine.AddState("Idle");

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.Contains("1x", result);
            StringAssert.DoesNotContain("speed:", result);
        }

        // T09: empty layer (0 states) — layer section skipped
        [Test]
        public void EmptyLayer_ZeroStates_LayerSectionSkipped()
        {
            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.DoesNotContain("states [", result);
        }

        // T10: multiple layers with different masks — both mask names present
        [Test]
        public void MultipleLayers_DifferentMasks_BothMaskNamesPresent()
        {
            _ctrl.AddLayer("UpperBody");

            var sm0 = _ctrl.layers[0].stateMachine;
            var sm1 = _ctrl.layers[1].stateMachine;
            sm0.AddState("Walk");
            sm1.AddState("Shoot");

            var maskA = new AvatarMask { name = "LowerBody" };
            var maskB = new AvatarMask { name = "UpperBodyMask" };
            var layers = _ctrl.layers;
            layers[0].avatarMask = maskA;
            layers[1].avatarMask = maskB;
            _ctrl.layers = layers;

            try
            {
                var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);
                StringAssert.Contains("mask:LowerBody", result);
                StringAssert.Contains("mask:UpperBodyMask", result);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(maskA);
                UnityEngine.Object.DestroyImmediate(maskB);
            }
        }

        // T11: state with tag + !wdv + speedParam — all three tokens on same line
        [Test]
        public void StateLine_TagAndWdvAndSpeedParam_AllThreeTokensPresent()
        {
            var sm = _ctrl.layers[0].stateMachine;
            var state = sm.AddState("Combat");
            state.tag = "Fighter";
            state.writeDefaultValues = false;
            _ctrl.AddParameter("MoveSpeed", AnimatorControllerParameterType.Float);
            state.speedParameterActive = true;
            state.speedParameter = "MoveSpeed";

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            var combatLine = System.Array.Find(result.Split('\n'), l => l.Contains("Combat"));
            Assert.IsNotNull(combatLine, "Combat state line not found");
            StringAssert.Contains("tag:Fighter", combatLine);
            StringAssert.Contains("!wdv", combatLine);
            StringAssert.Contains("speed:MoveSpeed", combatLine);
        }

        // EC-B1: layer with only SSM, no direct states → layer block skipped
        [Test]
        public void Layer_OnlySSMNoDirectStates_LayerSectionSkipped()
        {
            var sm = _ctrl.layers[0].stateMachine;
            var ssm = sm.AddStateMachine("Sub");
            ssm.AddState("A");

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);

            StringAssert.DoesNotContain("states [", result);
            StringAssert.Contains("1 states", result.Split('\n')[0]);
        }

        // EC-B2: deeply nested SSM (3 levels) → CountStates totals all recursively
        [Test]
        public void CountStates_DeeplyNestedSSM_TotalsAllLevels()
        {
            var sm = _ctrl.layers[0].stateMachine;
            sm.AddState("Root");

            var l1 = sm.AddStateMachine("L1");
            l1.AddState("L1A");

            var l2 = l1.AddStateMachine("L2");
            l2.AddState("L2A");
            l2.AddState("L2B");

            var result = AnimatorControllerSerializer.Serialize(CtrlPath, null);
            var header = result.Split('\n')[0];

            StringAssert.Contains("4 states", header);
        }
    }
}
