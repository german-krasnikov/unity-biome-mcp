using System;
using NUnit.Framework;
using UnityEditor;
using UnityMCP.Editor;

namespace UnityMCP.TestProject.Server
{
    [TestFixture]
    public class EditorStateHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [SetUp]
        public void SetUp()
        {
            RegisterCleanup(() =>
            {
                EditorStateHelper.GetIsPlaying = () => EditorApplication.isPlaying;
                EditorStateHelper.SetIsPlaying = v => { EditorApplication.isPlaying = v; };
            });
        }

        [Test]
        public void EditorControl_Play_WhenNotPlaying_ReturnsRequested()
        {
            EditorStateHelper.GetIsPlaying = () => false;
            EditorStateHelper.SetIsPlaying = _ => { };  // no-op — don't actually enter Play Mode

            var result = EditorStateHelper.Control("play", null);

            Assert.AreEqual("requested", result);
        }

        [Test]
        public void EditorControl_Play_WhenAlreadyPlaying_ReturnsAlreadyPlaying()
        {
            EditorStateHelper.GetIsPlaying = () => true;

            var result = EditorStateHelper.Control("play", null);

            Assert.AreEqual("already_playing", result);
        }

        [Test]
        public void EditorControl_Play_CallsSetIsPlayingTrue()
        {
            bool wasSet = false;
            bool setValue = false;
            EditorStateHelper.GetIsPlaying = () => false;
            EditorStateHelper.SetIsPlaying = v => { wasSet = true; setValue = v; };

            EditorStateHelper.Control("play", null);

            Assert.IsTrue(wasSet, "SetIsPlaying must be called");
            Assert.IsTrue(setValue, "SetIsPlaying must receive true");
        }
    }
}
