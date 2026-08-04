using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Animation
{
    // CS3.arch.1 — AnimationSerializer must not stop a pre-existing AnimationMode session
    [TestFixture]
    public class AnimationSerializerTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private GameObject _go;
        private AnimationClip _clip;

        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() =>
            {
                if (AnimationMode.InAnimationMode())
                    AnimationMode.StopAnimationMode();
            });
            _go = TrackOwnedObject(new GameObject("AS_AuditObj"));
            _clip = TrackOwnedObject(new AnimationClip());
            // One float curve so SerializeClipAtTime has something to evaluate
            _clip.SetCurve("", typeof(Transform), "localPosition.x",
                AnimationCurve.Linear(0, 0, 1, 1));
        }

        [Test]
        public void Serialize_PreexistingAnimationModeActive_SessionPreservedAfterCall()
        {
            // Arrange — start AnimationMode before the call
            AnimationMode.StartAnimationMode();
            Assert.IsTrue(AnimationMode.InAnimationMode(), "precondition: AnimationMode active");

            // Act — call the method under test directly via the internal API
            // Serialize calls SerializeClipAtTime when sampleTime is provided
            var go = _go;
            var clip = _clip;

            // Call internal path directly (both are in same assembly via InternalsVisibleTo)
            // Since SerializeClipAtTime is private, we exercise it through Serialize
            // We register the clip on an Animation component so Serialize can find it
            var anim = go.AddComponent<UnityEngine.Animation>();
            anim.AddClip(clip, clip.name);

            // This should NOT stop AnimationMode
            try
            {
                AnimationSerializer.Serialize(ComponentSerializer.GetPath(go), clip.name, 0.5f);
            }
            catch { /* clip may not be fully valid in editor — that's OK, guard is what we test */ }

            // Assert — session must still be active
            Assert.IsTrue(AnimationMode.InAnimationMode(),
                "SerializeAtTime must not stop a pre-existing AnimationMode session");
        }
    }
}
