using NUnit.Framework;
using UnityEngine;
using UnityMCP.Editor.UI;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal sealed class IconCanvasTests
    {
        private readonly System.Collections.Generic.List<Texture2D> _textures = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var tex in _textures)
                if (tex != null) Object.DestroyImmediate(tex);
            _textures.Clear();
        }

        // Horizontal line through center: each x column in [2..15] must have exactly 2 lit pixels.
        // Uses Bake() (before Apply) so GetPixel is valid.
        [Test]
        public void HorizontalLine_StrokeBandIs2Pixels()
        {
            var pixels = IconCanvas.New()
                .Line(1, 8.5f, 16, 8.5f)
                .Bake();

            for (int x = 2; x <= 15; x++)
            {
                int lit = 0;
                for (int y = 0; y < 18; y++)
                    if (pixels[y * 18 + x].a > 0.5f) lit++;
                Assert.AreEqual(2, lit, $"Column x={x} should have exactly 2 lit pixels for 2px stroke");
            }
        }

        // Border rows/cols (y=0, y=17, x=0, x=17) must remain transparent even for edge-touching lines.
        [Test]
        public void ContentStaysWithinBox_BorderRowsAndColsEmpty()
        {
            var pixels = IconCanvas.New()
                .Line(1, 1, 16, 16)
                .Bake();

            int S = IconCanvas.CanvasSize;
            for (int i = 0; i < S; i++)
            {
                Assert.AreEqual(0f, pixels[0 * S + i].a,  $"Bottom border col {i} must be transparent");
                Assert.AreEqual(0f, pixels[17 * S + i].a, $"Top border col {i} must be transparent");
                Assert.AreEqual(0f, pixels[i * S + 0].a,  $"Left border row {i} must be transparent");
                Assert.AreEqual(0f, pixels[i * S + 17].a, $"Right border row {i} must be transparent");
            }
        }

        // Every lit pixel must match DefaultInk exactly (no AA = binary alpha).
        [Test]
        public void AllLitPixels_HaveDefaultInkColor()
        {
            var pixels = IconCanvas.New()
                .Line(3, 3, 14, 14)
                .Bake();

            var ink = IconCanvas.DefaultInk;
            foreach (var p in pixels)
            {
                if (p.a < 0.5f) continue;
                Assert.AreEqual(ink.r, p.r, 0.001f, "Lit pixel R must match DefaultInk");
                Assert.AreEqual(ink.g, p.g, 0.001f, "Lit pixel G must match DefaultInk");
                Assert.AreEqual(ink.b, p.b, 0.001f, "Lit pixel B must match DefaultInk");
            }
        }

        // Build() must make texture non-readable (Apply(false, true)).
        [Test]
        public void Build_TextureIsNonReadable()
        {
            var tex = IconCanvas.New().Line(3, 3, 14, 14).Build();
            _textures.Add(tex);
            Assert.Throws<UnityException>(() => tex.GetPixel(0, 0));
        }

        // Default size = 18.
        [Test]
        public void Build_DefaultSize_Is18x18()
        {
            var tex = IconCanvas.New().Build();
            _textures.Add(tex);
            Assert.AreEqual(18, tex.width);
            Assert.AreEqual(18, tex.height);
        }

        // Custom size = 16.
        [Test]
        public void Build_Size16_Is16x16()
        {
            var tex = IconCanvas.New(16).Build();
            _textures.Add(tex);
            Assert.AreEqual(16, tex.width);
            Assert.AreEqual(16, tex.height);
        }

        // HideAndDontSave must be set.
        [Test]
        public void Build_HideFlags_HideAndDontSave()
        {
            var tex = IconCanvas.New().Line(1, 1, 16, 16).Build();
            _textures.Add(tex);
            Assert.AreEqual(HideFlags.HideAndDontSave, tex.hideFlags);
        }

        // WithInk overrides color for subsequent drawing.
        [Test]
        public void WithInk_ChangesLitPixelColor()
        {
            var red = new Color(1f, 0f, 0f, 1f);
            var pixels = IconCanvas.New()
                .WithInk(red)
                .Line(3, 8, 14, 8)
                .Bake();

            bool foundRed = false;
            foreach (var p in pixels)
            {
                if (p.a < 0.5f) continue;
                Assert.AreEqual(1f, p.r, 0.001f, "Lit pixel must be red");
                foundRed = true;
                break;
            }
            Assert.IsTrue(foundRed, "Should have at least one lit red pixel");
        }

        // Disc: center pixel must be lit.
        [Test]
        public void Disc_CenterPixelIsLit()
        {
            var pixels = IconCanvas.New().Disc(9, 9, 3).Bake();
            Assert.Greater(pixels[9 * 18 + 9].a, 0.5f, "Center pixel of disc must be lit");
        }

        // Vertical line stroke: each y row in [2..15] must have exactly 2 lit pixels.
        [Test]
        public void VerticalLine_StrokeBandIs2Pixels()
        {
            var pixels = IconCanvas.New()
                .Line(8.5f, 1, 8.5f, 16)
                .Bake();

            for (int y = 2; y <= 15; y++)
            {
                int lit = 0;
                for (int x = 0; x < 18; x++)
                    if (pixels[y * 18 + x].a > 0.5f) lit++;
                Assert.AreEqual(2, lit, $"Row y={y} should have exactly 2 lit pixels for 2px stroke");
            }
        }
    }
}
