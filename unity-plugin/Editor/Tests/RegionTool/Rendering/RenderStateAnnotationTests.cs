using NUnit.Framework;
using UnityMCP.Editor.RegionTool;

namespace UnityMCP.Editor.Tests.RegionTool
{
    [TestFixture]
    internal class RenderStateAnnotationTests
    {
        [Test]
        public void DrawAnnotation_NullAnnotationType_DoesNotThrow()
        {
            var state = new RenderState { AnnotationType = null };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_PointType_DoesNotThrow()
        {
            var state = new RenderState
            {
                AnnotationType = "point",
                Vertices       = new[] { new UnityEngine.Vector2(1f, 2f) },
                Label          = "Test"
            };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_PolylineType_DoesNotThrow()
        {
            var state = new RenderState
            {
                AnnotationType = "polyline",
                Vertices       = new[]
                {
                    new UnityEngine.Vector2(0f, 0f),
                    new UnityEngine.Vector2(5f, 0f),
                    new UnityEngine.Vector2(5f, 5f),
                },
                Length = 10f,
                Label  = "Path"
            };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_MeasurementType_DoesNotThrow()
        {
            var state = new RenderState
            {
                AnnotationType = "measurement",
                Vertices       = new[]
                {
                    new UnityEngine.Vector2(0f, 0f),
                    new UnityEngine.Vector2(8f, 6f),
                },
                Length = 10f,
                Label  = "Gap"
            };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }

        [Test]
        public void DrawAnnotation_UnknownType_DoesNotThrow()
        {
            var state = new RenderState { AnnotationType = "future_type" };
            Assert.DoesNotThrow(() => RegionRenderer.DrawAnnotation(state));
        }
    }
}
