using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityMCP.Editor.Chat.Annotation;

namespace UnityMCP.Editor.Chat.Tests
{
    internal sealed class FakeEraseCommand : IAnnotationCommand
    {
        public AnnotationTool Tool => AnnotationTool.Erase;
        public Color32 Color => new Color32(0, 0, 0, 0);
        public float StrokeWidth => 5f;
        public AnnotationFill Fill => AnnotationFill.None;
        public IReadOnlyList<Vector2> Points { get; } = new[] { Vector2.zero };
        public string Text => null;
    }

    [TestFixture]
    internal sealed class AnnotationRaycasterTests
    {
        [SetUp]    public void SetUp()    => AnnotationRaycaster.RaycastFunc = null;
        [TearDown] public void TearDown()
        {
            AnnotationRaycaster.RaycastFunc = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Undo.ClearAll();
        }

        // ── AnnotationHit ──────────────────────────────────────────────────────

        [Test]
        public void AnnotationHit_Miss_DidHitIsFalse()
            => Assert.IsFalse(AnnotationHit.Miss.DidHit);

        [Test]
        public void AnnotationHit_Valid_DidHitIsTrue()
            => Assert.IsTrue(new AnnotationHit(Vector3.zero, "/Player", 1).DidHit);

        // ── GetKeyPoint ────────────────────────────────────────────────────────

        [Test]
        public void GetKeyPoint_Arrow_ReturnsTip()
        {
            IAnnotationCommand cmd = new ArrowCommand(Color.red, 2f,
                new Vector2(0.1f, 0.1f), new Vector2(0.9f, 0.5f));
            Assert.AreEqual(new Vector2(0.9f, 0.5f), AnnotationRaycaster.GetKeyPoint(cmd));
        }

        [Test]
        public void GetKeyPoint_Line_ReturnsTip()
        {
            IAnnotationCommand cmd = new LineCommand(Color.red, 2f,
                new Vector2(0f, 0f), new Vector2(1f, 1f));
            Assert.AreEqual(new Vector2(1f, 1f), AnnotationRaycaster.GetKeyPoint(cmd));
        }

        [Test]
        public void GetKeyPoint_Rect_ReturnsCenter()
        {
            IAnnotationCommand cmd = new RectCommand(Color.red, 2f, AnnotationFill.None,
                new Vector2(0.2f, 0.2f), new Vector2(0.8f, 0.6f));
            var expected = new Vector2(0.5f, 0.4f);
            var actual   = AnnotationRaycaster.GetKeyPoint(cmd);
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.y, actual.y, 0.001f);
        }

        [Test]
        public void GetKeyPoint_Ellipse_ReturnsCenter()
        {
            IAnnotationCommand cmd = new EllipseCommand(Color.red, 2f, AnnotationFill.None,
                new Vector2(0.5f, 0.5f), new Vector2(0.7f, 0.6f));
            var expected = new Vector2(0.6f, 0.55f);
            var actual   = AnnotationRaycaster.GetKeyPoint(cmd);
            Assert.AreEqual(expected.x, actual.x, 0.001f);
            Assert.AreEqual(expected.y, actual.y, 0.001f);
        }

        [Test]
        public void GetKeyPoint_Text_ReturnsAnchor()
        {
            IAnnotationCommand cmd = new TextCommand(Color.white, new Vector2(0.3f, 0.7f), "hello");
            Assert.AreEqual(new Vector2(0.3f, 0.7f), AnnotationRaycaster.GetKeyPoint(cmd));
        }

        [Test]
        public void GetKeyPoint_Pen_ReturnsLastPoint()
        {
            var pts = new List<Vector2> { new(0.1f, 0.1f), new(0.5f, 0.5f), new(0.9f, 0.9f) };
            IAnnotationCommand cmd = new PenCommand(Color.red, 2f, pts);
            Assert.AreEqual(new Vector2(0.9f, 0.9f), AnnotationRaycaster.GetKeyPoint(cmd));
        }

        // ── RaycastAll ─────────────────────────────────────────────────────────

        [Test]
        public void RaycastAll_InvalidSnapshot_ReturnsEmpty()
        {
            var snapshot = default(CameraSnapshot);
            var cmds = new IAnnotationCommand[]
            {
                new ArrowCommand(Color.red, 2f, Vector2.zero, Vector2.one)
            };
            Assert.AreEqual(0, AnnotationRaycaster.RaycastAll(snapshot, cmds).Count);
        }

        [Test]
        public void RaycastAll_SkipsErase()
        {
            // Build a valid-ish snapshot using a real Camera from a temporary GO
            var go  = new GameObject("TestCam");
            var cam = go.AddComponent<Camera>();

            int callCount = 0;
            AnnotationRaycaster.RaycastFunc = _ =>
            {
                callCount++;
                return AnnotationHit.Miss;
            };

            var snapshot = new CameraSnapshot(cam);
            var cmds = new IAnnotationCommand[]
            {
                new PenCommand(Color.red, 2f, new List<Vector2> { new(0.3f, 0.3f) }),   // Erase-like but Pen → counted
                new ArrowCommand(Color.red, 2f, Vector2.zero, new Vector2(0.5f, 0.5f)),
            };

            // Replace first with actual Erase — need an EraseCommand; since there is none,
            // test that Pen IS counted (non-Erase tools ARE raycasted).
            var result = AnnotationRaycaster.RaycastAll(snapshot, cmds);
            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(2, callCount);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void RaycastAll_EraseToolSkipped()
        {
            var go  = new GameObject("TestCam2");
            var cam = go.AddComponent<Camera>();

            int callCount = 0;
            AnnotationRaycaster.RaycastFunc = _ => { callCount++; return AnnotationHit.Miss; };

            var snapshot = new CameraSnapshot(cam);
            var cmds = new IAnnotationCommand[]
            {
                new FakeEraseCommand(),
                new ArrowCommand(Color.red, 2f, Vector2.zero, new Vector2(0.5f, 0.5f)),
            };

            var result = AnnotationRaycaster.RaycastAll(snapshot, cmds);
            Assert.AreEqual(1, result.Count, "Erase should be skipped, only Arrow counted");
            Assert.AreEqual(1, callCount, "Raycast called only for Arrow, not for Erase");
            Assert.AreEqual(AnnotationTool.Arrow, result[0].tool);

            Object.DestroyImmediate(go);
        }

        [Test]
        public void DoRaycast_UsesSeam_WhenSet()
        {
            var expectedHit = new AnnotationHit(new Vector3(1, 2, 3), "/Cube", 42);
            AnnotationRaycaster.RaycastFunc = _ => expectedHit;

            var go  = new GameObject("TestCam3");
            var cam = go.AddComponent<Camera>();
            var snapshot = new CameraSnapshot(cam);

            var cmds = new IAnnotationCommand[]
            {
                new ArrowCommand(Color.red, 2f, Vector2.zero, new Vector2(0.5f, 0.5f))
            };

            var result = AnnotationRaycaster.RaycastAll(snapshot, cmds);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(expectedHit.WorldPos,   result[0].hit.WorldPos);
            Assert.AreEqual(expectedHit.ObjectPath, result[0].hit.ObjectPath);
            Assert.IsTrue(result[0].hit.DidHit);

            Object.DestroyImmediate(go);
        }

        // ── FormatAnnotations ──────────────────────────────────────────────────

        [Test]
        public void FormatAnnotations_Null_ReturnsNull()
            => Assert.IsNull(AnnotationRaycaster.FormatAnnotations(null));

        [Test]
        public void FormatAnnotations_Empty_ReturnsNull()
            => Assert.IsNull(AnnotationRaycaster.FormatAnnotations(
                new List<(AnnotationTool, string, Vector2, AnnotationHit)>()));

        [Test]
        public void FormatAnnotations_Hit_ContainsPath()
        {
            var hit  = new AnnotationHit(new Vector3(1f, 2f, 3f), "/Player", 99);
            var list = new List<(AnnotationTool, string, Vector2, AnnotationHit)>
            {
                (AnnotationTool.Arrow, null, new Vector2(0.5f, 0.5f), hit)
            };
            var text = AnnotationRaycaster.FormatAnnotations(list);
            StringAssert.Contains("annotations:", text);
            StringAssert.Contains("/Player", text);
            StringAssert.Contains("#99", text);
        }

        [Test]
        public void FormatAnnotations_Miss_ContainsSky()
        {
            var list = new List<(AnnotationTool, string, Vector2, AnnotationHit)>
            {
                (AnnotationTool.Arrow, null, new Vector2(0.5f, 0.5f), AnnotationHit.Miss)
            };
            var text = AnnotationRaycaster.FormatAnnotations(list);
            StringAssert.Contains("(sky)", text);
        }

        [Test]
        public void FormatAnnotations_TextTool_ContainsQuotedText()
        {
            var hit  = new AnnotationHit(Vector3.zero, "/Obj", 1);
            var list = new List<(AnnotationTool, string, Vector2, AnnotationHit)>
            {
                (AnnotationTool.Text, "hello", new Vector2(0.1f, 0.1f), hit)
            };
            var text = AnnotationRaycaster.FormatAnnotations(list);
            StringAssert.Contains("\"hello\"", text);
        }
    }
}
