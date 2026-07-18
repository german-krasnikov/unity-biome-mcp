// TDD: CAPTURE_FRAMES / ASSERT_FRAMES_DIFFER / ASSERT_FRAMES_STATIC — EditMode safe.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    public class PlaytestFrameCaptureTests
    {
        // ── Parser: CAPTURE_FRAMES basic ────────────────────────────────────────

        [Test]
        public void Parse_CaptureFrames_Basic()
        {
            var steps = PlaytestParser.Parse("CAPTURE_FRAMES 4 INTERVAL 0.25");
            Assert.AreEqual(1, steps.Count);
            Assert.AreEqual(StepType.CaptureFrames, steps[0].Type);
            Assert.AreEqual(4f, steps[0].Timeout, 0.001f);    // n frames
            Assert.AreEqual(0.25f, steps[0].Delay, 0.001f);   // interval
            Assert.AreEqual("game", steps[0].Component);       // default camera
            Assert.AreEqual("strip", steps[0].Op);             // default mode
            Assert.IsNull(steps[0].Message);                   // no label
        }

        [Test]
        public void Parse_CaptureFrames_AllParams()
        {
            var steps = PlaytestParser.Parse("CAPTURE_FRAMES 3 INTERVAL 0.5 CAMERA Main MODE list LABEL run1");
            Assert.AreEqual(1, steps.Count);
            var s = steps[0];
            Assert.AreEqual(StepType.CaptureFrames, s.Type);
            Assert.AreEqual(3f, s.Timeout, 0.001f);
            Assert.AreEqual(0.5f, s.Delay, 0.001f);
            Assert.AreEqual("Main", s.Component);
            Assert.AreEqual("list", s.Op);
            Assert.AreEqual("run1", s.Message);
        }

        [Test]
        public void Parse_CaptureFrames_MissingInterval_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("CAPTURE_FRAMES 4"));
            StringAssert.Contains("INTERVAL", ex.Message);
        }

        [Test]
        public void Parse_CaptureFrames_CountLessThan2_Throws()
        {
            var ex = Assert.Throws<ArgumentException>(() =>
                PlaytestParser.Parse("CAPTURE_FRAMES 1 INTERVAL 0.1"));
            StringAssert.Contains("n must be", ex.Message);
        }

        [Test]
        public void Parse_AssertFramesDiffer()
        {
            var steps = PlaytestParser.Parse("CAPTURE_FRAMES 2 INTERVAL 0.1\nASSERT_FRAMES_DIFFER run1");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.AssertFramesDiffer, steps[1].Type);
            Assert.AreEqual("run1", steps[1].Message);
        }

        [Test]
        public void Parse_AssertFramesStatic()
        {
            var steps = PlaytestParser.Parse("CAPTURE_FRAMES 2 INTERVAL 0.1\nASSERT_FRAMES_STATIC run1");
            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(StepType.AssertFramesStatic, steps[1].Type);
            Assert.AreEqual("run1", steps[1].Message);
        }

        // ── PlaytestState: frame sets ────────────────────────────────────────────

        [Test]
        public void FrameSet_InitAndAdd_Works()
        {
            var state = new PlaytestState();
            state.InitFrames("run");
            state.AddFrame("run", "/tmp/a.png");
            state.AddFrame("run", "/tmp/b.png");
            Assert.AreEqual(2, state.GetFrameCount("run"));
            CollectionAssert.AreEqual(new[] { "/tmp/a.png", "/tmp/b.png" }, state.GetFrames("run"));
        }

        [Test]
        public void FrameSet_GetFramesBeforeInit_ReturnsNull()
        {
            var state = new PlaytestState();
            Assert.IsNull(state.GetFrames("nope"));
            Assert.AreEqual(0, state.GetFrameCount("nope"));
        }

        [Test]
        public void FrameSet_MultipleLabels_Independent()
        {
            var state = new PlaytestState();
            state.InitFrames("a");
            state.InitFrames("b");
            state.AddFrame("a", "a1.png");
            state.AddFrame("b", "b1.png");
            state.AddFrame("b", "b2.png");
            Assert.AreEqual(1, state.GetFrameCount("a"));
            Assert.AreEqual(2, state.GetFrameCount("b"));
        }

        // ── FrameStitcher: AreFramesDifferent ───────────────────────────────────

        [Test]
        public void AreFramesDifferent_SingleFile_ReturnsFalse()
        {
            var path = WriteSolidColorPng(Color.red, "frame_single");
            try
            {
                Assert.IsFalse(FrameStitcher.AreFramesDifferent(new List<string> { path }));
            }
            finally { File.Delete(path); }
        }

        [Test]
        public void AreFramesDifferent_IdenticalFiles_ReturnsFalse()
        {
            var p1 = WriteSolidColorPng(Color.blue, "frame_id1");
            var p2 = WriteSolidColorPng(Color.blue, "frame_id2");
            try
            {
                Assert.IsFalse(FrameStitcher.AreFramesDifferent(new List<string> { p1, p2 }));
            }
            finally { File.Delete(p1); File.Delete(p2); }
        }

        [Test]
        public void AreFramesDifferent_DifferentFiles_ReturnsTrue()
        {
            var p1 = WriteSolidColorPng(Color.red, "frame_diff1");
            var p2 = WriteSolidColorPng(Color.green, "frame_diff2");
            try
            {
                Assert.IsTrue(FrameStitcher.AreFramesDifferent(new List<string> { p1, p2 }));
            }
            finally { File.Delete(p1); File.Delete(p2); }
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        static string WriteSolidColorPng(Color color, string name)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGB24, false);
            var pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            var path = Path.Combine(Application.temporaryCachePath, $"{name}_{Guid.NewGuid():N}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return path;
        }
    }
}
