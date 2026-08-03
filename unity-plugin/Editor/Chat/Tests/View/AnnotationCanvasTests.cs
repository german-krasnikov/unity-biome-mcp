using NUnit.Framework;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotationCanvasTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test]
        public void Constructor_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => new AnnotationCanvas(new AnnotationToolState(), new AnnotationHistory()));
        }

        [Test]
        public void Dispose_NullBackground_NoThrow()
        {
            var canvas = new AnnotationCanvas(new AnnotationToolState(), new AnnotationHistory());
            Assert.DoesNotThrow(() => canvas.Dispose());
        }

        [Test]
        public void Dispose_CanBeCalledTwice_NoThrow()
        {
            var canvas = new AnnotationCanvas(new AnnotationToolState(), new AnnotationHistory());
            canvas.Dispose();
            Assert.DoesNotThrow(() => canvas.Dispose());
        }
    }
}
