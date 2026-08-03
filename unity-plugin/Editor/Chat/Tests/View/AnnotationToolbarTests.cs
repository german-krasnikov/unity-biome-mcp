using NUnit.Framework;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationToolbarTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Constructor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new AnnotationToolbar(new AnnotationToolState(), new AnnotationHistory()));
        }

        [Test]
        public void SendClicked_DefaultFalse()
        {
            var tb = new AnnotationToolbar(new AnnotationToolState(), new AnnotationHistory());
            Assert.IsFalse(tb.SendClicked);
        }

        [Test]
        public void ClearClicked_DefaultFalse()
        {
            var tb = new AnnotationToolbar(new AnnotationToolState(), new AnnotationHistory());
            Assert.IsFalse(tb.ClearClicked);
        }
    }
}
