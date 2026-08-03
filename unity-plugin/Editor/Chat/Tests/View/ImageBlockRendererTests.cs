// CH5.arch.2 / CH5.test.2: Unit tests for ImageBlockRenderer pure-logic methods.
// Tests: IsImageFile extension filter, AltLabel fallback, ResolvePath relative-vs-absolute.
// No Texture2D/File.ReadAllBytes — pure headless.
using NUnit.Framework;
using System.IO;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ImageBlockRendererTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── IsImageFile ───────────────────────────────────────────────────────

        [Test]
        public void IsImageFile_Png_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("/some/path/img.png"));

        [Test]
        public void IsImageFile_Jpg_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("photo.jpg"));

        [Test]
        public void IsImageFile_Jpeg_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("image.jpeg"));

        [Test]
        public void IsImageFile_Gif_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("anim.gif"));

        [Test]
        public void IsImageFile_Bmp_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("icon.bmp"));

        [Test]
        public void IsImageFile_UpperCaseExt_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("img.PNG"));

        [Test]
        public void IsImageFile_Txt_ReturnsFalse()
            => Assert.IsFalse(ImageBlockRenderer.IsImageFile("log.txt"));

        [Test]
        public void IsImageFile_Pdf_ReturnsFalse()
            => Assert.IsFalse(ImageBlockRenderer.IsImageFile("doc.pdf"));

        [Test]
        public void IsImageFile_NoExtension_ReturnsFalse()
            => Assert.IsFalse(ImageBlockRenderer.IsImageFile("nodot"));

        [Test]
        public void IsImageFile_NullPath_ReturnsFalse()
            => Assert.IsFalse(ImageBlockRenderer.IsImageFile(null));

        // BUG 2: missing extensions — these FAIL until IsImageFile is extended
        [Test]
        public void IsImageFile_Webp_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("img.webp"));

        [Test]
        public void IsImageFile_Tiff_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("img.tiff"));

        [Test]
        public void IsImageFile_Tif_ReturnsTrue()
            => Assert.IsTrue(ImageBlockRenderer.IsImageFile("img.tif"));

        // ── AltLabel fallback ─────────────────────────────────────────────────

        [Test]
        public void AltLabel_NonEmpty_UsesAltText()
        {
            var lbl = ImageBlockRenderer.AltLabel("my caption") as Label;
            Assert.IsNotNull(lbl);
            Assert.AreEqual("my caption", lbl.text);
        }

        [Test]
        public void AltLabel_Empty_FallsBackToPlaceholder()
        {
            var lbl = ImageBlockRenderer.AltLabel("") as Label;
            Assert.IsNotNull(lbl);
            Assert.AreEqual("[image]", lbl.text);
        }

        [Test]
        public void AltLabel_Null_FallsBackToPlaceholder()
        {
            var lbl = ImageBlockRenderer.AltLabel(null) as Label;
            Assert.IsNotNull(lbl);
            Assert.AreEqual("[image]", lbl.text);
        }

        [Test]
        public void AltLabel_HasCssClass()
        {
            var lbl = ImageBlockRenderer.AltLabel("x") as Label;
            Assert.IsNotNull(lbl);
            Assert.IsTrue(lbl.ClassListContains("md-image-alt"));
        }

        // ── ResolvePath ───────────────────────────────────────────────────────

        [Test]
        public void ResolvePath_AbsolutePath_ReturnedUnchanged()
        {
            var abs = "/absolute/path/img.png";
            Assert.AreEqual(abs, ImageBlockRenderer.ResolvePath(abs));
        }

        [Test]
        public void ResolvePath_RelativePath_PrependsCwd()
        {
            const string rel = "Screenshots/img.png";
            var expected = Path.Combine(Directory.GetCurrentDirectory(), rel);
            Assert.AreEqual(expected, ImageBlockRenderer.ResolvePath(rel));
        }

        [Test]
        public void ResolvePath_DotRelative_PrependsCwd()
        {
            const string rel = "./foo/bar.png";
            var result = ImageBlockRenderer.ResolvePath(rel);
            Assert.IsFalse(Path.IsPathRooted(rel)); // precondition
            Assert.IsTrue(Path.IsPathRooted(result), "ResolvePath must return rooted path");
        }

        // ── Render fallback on missing file ───────────────────────────────────

        [Test]
        public void Render_MissingFile_ReturnsAltLabel()
        {
            var renderer = new ImageBlockRenderer();
            var block    = MdBlock.Image("/nonexistent/path/missing.png", "alt text");
            var result   = renderer.Render(in block);

            Assert.IsNotNull(result, "Render must not return null");
            // Missing file → AltLabel fallback with the alt text
            Assert.IsInstanceOf<Label>(result);
            Assert.AreEqual("alt text", ((Label)result).text);
        }

        [Test]
        public void Render_NonImageExtension_ReturnsAltLabel()
        {
            var renderer = new ImageBlockRenderer();
            var block    = MdBlock.Image("/some/file.pdf", "pdf alt");
            var result   = renderer.Render(in block);

            Assert.IsNotNull(result);
            Assert.IsInstanceOf<Label>(result);
        }

        [Test]
        public void Render_EmptySrc_ReturnsAltLabel()
        {
            var renderer = new ImageBlockRenderer();
            var block    = MdBlock.Image("", "empty src");
            var result   = renderer.Render(in block);
            Assert.IsNotNull(result);
        }
    }
}
