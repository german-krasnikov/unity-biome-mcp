using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Animation
{
    [TestFixture]
    public class AnimationCompactTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;
        private UnityEngine.Animation _anim;

        [SetUp]
        public void SetUp()
        {
            _go = TrackOwnedObject(new GameObject("CompactTestObj"));
            _anim = _go.AddComponent<UnityEngine.Animation>();
        }

        // legacy=true required: Animation component only iterates legacy clips.
        // clip.SetCurve stores bindings that GetCurveBindings returns correctly.
        private AnimationClip MakeClip(string name, (string prop, System.Type type, float v0, float v1)[] curves)
        {
            var clip = TrackOwnedObject(new AnimationClip { name = name });
            clip.legacy = true;
            foreach (var (prop, type, v0, v1) in curves)
                clip.SetCurve("", type, prop, new AnimationCurve(new Keyframe(0, v0), new Keyframe(1, v1)));
            _anim.AddClip(clip, name);
            return clip;
        }

        [Test]
        public void CompactAnimator_XYZGrouped_IntoVector()
        {
            MakeClip("XYZClip", new[]
            {
                ("m_LocalPosition.x", typeof(Transform), 0f, 1f),
                ("m_LocalPosition.y", typeof(Transform), 0f, 0f),
                ("m_LocalPosition.z", typeof(Transform), 0f, 0f),
            });

            var result = AnimationSerializer.Serialize("/CompactTestObj", "XYZClip", null, compact: true);

            StringAssert.Contains("(0,0,0)→(1,0,0)", result);
            Assert.IsFalse(result.Contains("$pos.x"), "Should not show individual xyz curves");
        }

        [Test]
        public void CompactAnimator_RGBAGrouped_IntoColor()
        {
            MakeClip("RGBAClip", new[]
            {
                ("m_Color.r", typeof(Light), 1f, 0.5f),
                ("m_Color.g", typeof(Light), 1f, 0.5f),
                ("m_Color.b", typeof(Light), 1f, 0.5f),
                ("m_Color.a", typeof(Light), 1f, 1f),
            });

            var result = AnimationSerializer.Serialize("/CompactTestObj", "RGBAClip", null, compact: true);

            StringAssert.Contains("(1,1,1,1)→(0.5,0.5,0.5,1)", result);
            Assert.IsFalse(result.Contains("$color.r"), "Should not show individual rgba curves");
        }

        [Test]
        public void CompactAnimator_NoChange_PropertyOmitted()
        {
            MakeClip("NoChangeClip", new[]
            {
                ("m_LocalPosition.x", typeof(Transform), 0f, 0f),
                ("m_LocalPosition.y", typeof(Transform), 0f, 0f),
                ("m_LocalPosition.z", typeof(Transform), 0f, 0f),
            });

            var result = AnimationSerializer.Serialize("/CompactTestObj", "NoChangeClip", null, compact: true);

            // All components unchanged — vector summary must be omitted
            Assert.IsFalse(result.Contains("(0,0,0)"), "Unchanged vector should be omitted in compact mode");
        }

        [Test]
        public void CompactAnimator_MixedProperties_CorrectOutput()
        {
            MakeClip("MixedClip", new[]
            {
                ("m_LocalPosition.x", typeof(Transform), 0f, 1f),
                ("m_LocalPosition.y", typeof(Transform), 0f, 2f),
                ("m_LocalPosition.z", typeof(Transform), 0f, 3f),
                ("m_IsActive", typeof(GameObject), 0f, 1f),
            });

            var result = AnimationSerializer.Serialize("/CompactTestObj", "MixedClip", null, compact: true);

            StringAssert.Contains("(0,0,0)→(1,2,3)", result);
            StringAssert.Contains("$active 0→1", result);
            Assert.IsFalse(result.Contains("$pos.x"), "Should not show individual xyz curves");
        }
    }
}
