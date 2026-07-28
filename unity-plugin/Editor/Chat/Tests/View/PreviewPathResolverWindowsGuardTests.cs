// TDD — RC-4: guard against ArgumentException on Windows when ChipData.Path contains '|'.
// Path.GetInvalidPathChars() is OS-specific: '|' is illegal on Windows, legal on macOS.
// Tests are written so they pass on all platforms — platform-specific behavior noted inline.
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class PreviewPathResolverWindowsGuardTests
    {
        // Component chip path "Root|Transform" — has no extension → false on all platforms
        [Test]
        public void IsImageFile_ComponentPath_ReturnsFalse()
            => Assert.IsFalse(PreviewPathResolver.IsImageFile("Root|Transform"));

        [Test]
        public void IsImageFile_FieldPath_ReturnsFalse()
            => Assert.IsFalse(PreviewPathResolver.IsImageFile("Root|Transform|m_localPosition"));

        [Test]
        public void IsAudioFile_ComponentPath_ReturnsFalse()
            => Assert.IsFalse(PreviewPathResolver.IsAudioFile("Root|Transform"));

        [Test]
        public void IsModelFile_ComponentPath_ReturnsFalse()
            => Assert.IsFalse(PreviewPathResolver.IsModelFile("Root|Transform"));

        // Valid paths still work correctly
        [Test]
        public void IsImageFile_ValidPngPath_ReturnsTrue()
            => Assert.IsTrue(PreviewPathResolver.IsImageFile("Assets/Textures/foo.png"));

        [Test]
        public void IsImageFile_EmptyPath_ReturnsFalse()
            => Assert.IsFalse(PreviewPathResolver.IsImageFile(""));

        // Cross-platform: verify the call never throws (root protection for the 45s WER freeze)
        [Test]
        public void IsImageFile_PipeInPath_NeverThrows()
            => Assert.DoesNotThrow(() => PreviewPathResolver.IsImageFile("Root|Transform.png"));

        [Test]
        public void IsAudioFile_PipeInPath_NeverThrows()
            => Assert.DoesNotThrow(() => PreviewPathResolver.IsAudioFile("a|b.wav"));

        [Test]
        public void IsModelFile_PipeInPath_NeverThrows()
            => Assert.DoesNotThrow(() => PreviewPathResolver.IsModelFile("a|b.fbx"));

        [Test]
        public void Resolve_PipeInPath_NeverThrows()
            => Assert.DoesNotThrow(() => PreviewPathResolver.Resolve("Root|Transform"));

        // On Windows, '|' is an illegal path char → guard must return "" instead of throwing
        [Test]
        public void Resolve_ComponentPath_OnWindows_ReturnsEmpty()
        {
#if UNITY_EDITOR_WIN
            Assert.AreEqual("", PreviewPathResolver.Resolve("Root|Transform"));
#else
            Assert.Pass("Windows-only: on other platforms '|' is legal in paths");
#endif
        }
    }
}
