using NUnit.Framework;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationEditorWindowTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [TearDown]
        public void Cleanup()
        {
            AnnotationEditorWindow.OnAnnotationReady = null;
        }

        [Test]
        public void OnAnnotationReady_IsNullByDefault()
        {
            AnnotationEditorWindow.OnAnnotationReady = null; // ensure clean state
            Assert.IsNull(AnnotationEditorWindow.OnAnnotationReady);
        }

        [Test]
        public void OnAnnotationReady_CanBeAssigned()
        {
            bool called = false;
            AnnotationEditorWindow.OnAnnotationReady = (path, name) => called = true;
            AnnotationEditorWindow.OnAnnotationReady?.Invoke("p", "n");
            Assert.IsTrue(called);
        }
    }
}
