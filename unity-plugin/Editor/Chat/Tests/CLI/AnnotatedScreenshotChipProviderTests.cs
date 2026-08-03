using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    internal sealed class AnnotatedScreenshotChipProviderTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        [Test] public void Key_IsAnnotatedScreenshot()
        {
            var p = new AnnotatedScreenshotChipProvider();
            Assert.AreEqual("annotated_screenshot", p.Key);
        }

        [Test] public void Priority_Is40()
            => Assert.AreEqual(40, new AnnotatedScreenshotChipProvider().Priority);

        [Test] public void CanHandle_ReturnsFalse()
            => Assert.IsFalse(new AnnotatedScreenshotChipProvider().CanHandle(null, ""));

        [Test] public void DefaultDepth_IsSummary()
            => Assert.AreEqual("summary", new AnnotatedScreenshotChipProvider().DefaultDepth);

        [Test] public void Navigate_NullReference_NoThrow()
            => Assert.DoesNotThrow(() => new AnnotatedScreenshotChipProvider().Navigate(null));

        [Test] public void Navigate_EmptyReference_NoThrow()
            => Assert.DoesNotThrow(() => new AnnotatedScreenshotChipProvider().Navigate(""));

        [Test] public void BarePathExtensions_IsEmpty()
            => Assert.AreEqual(0, new AnnotatedScreenshotChipProvider().BarePathExtensions.Length);

        [Test] public void HexColor_IsRed()
            => Assert.AreEqual("#e74c3c", new AnnotatedScreenshotChipProvider().HexColor);

        [Test] public void FormatPayload_DepthNone_ReturnsEmpty()
        {
            var p = new AnnotatedScreenshotChipProvider();
            var chip = new ChipData(ChipKindKeys.AnnotatedScreenshot, "/tmp/test.png", "test", 0);
            Assert.AreEqual("", p.FormatPayload(chip, new ChipPayloadContext("none", "")));
        }

        [Test] public void FormatPayload_DepthPath_ReturnsBracketOnly()
        {
            var p = new AnnotatedScreenshotChipProvider();
            var chip = new ChipData(ChipKindKeys.AnnotatedScreenshot, "/tmp/test.png", "my_shot", 0);
            var result = p.FormatPayload(chip, new ChipPayloadContext("path", ""));
            Assert.AreEqual("[annotated_screenshot:my_shot]", result);
        }

        [Test] public void FormatPayload_SummaryNoMeta_ReturnsBracketOnly()
        {
            var p = new AnnotatedScreenshotChipProvider();
            // Path points to non-existent file so meta sidecar won't exist
            var chip = new ChipData(ChipKindKeys.AnnotatedScreenshot, "/tmp/nonexistent_annotated.png", "shot", 0);
            var result = p.FormatPayload(chip, new ChipPayloadContext("summary", ""));
            Assert.AreEqual("[annotated_screenshot:shot]", result);
        }
    }
}
