// TDD T2.1 — ScreenshotResultParser: pure path extraction from screenshot tool results.
// Test data uses realistic paths including spaces and Cyrillic to catch encoding edge cases.
// Double-red requirement:
//   A — break any Assert → test goes RED
//   B — remove parser or make ExtractPath return null → ALL tests go RED
using NUnit.Framework;
using UnityMCP.Editor.Chat.Parsers;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ScreenshotResultParserTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Pattern 1: "Data saved to: /path" ──────────────────────────────────

        [Test]
        public void ExtractPath_DataSavedTo_ReturnsPath()
        {
            var result = ScreenshotResultParser.ExtractPath(
                "Data saved to: /Users/german/Work/unity-test-project/ScreenShots/shot.png");
            Assert.AreEqual(
                "/Users/german/Work/unity-test-project/ScreenShots/shot.png", result);
        }

        [Test]
        public void ExtractPath_DataSavedTo_PathWithSpaces_ReturnsPath()
        {
            var result = ScreenshotResultParser.ExtractPath(
                "Data saved to: /Users/german/Скриншоты/игра снимок.png");
            Assert.AreEqual("/Users/german/Скриншоты/игра снимок.png", result);
        }

        [Test]
        public void ExtractPath_ManifestThenDataSavedTo_ReturnsPath()
        {
            var result = ScreenshotResultParser.ExtractPath(
                "multi_view: front=ok, side=ok\nData saved to: /abs/composite.png");
            Assert.AreEqual("/abs/composite.png", result);
        }

        // ── Pattern 2: "Baseline saved: /path" ─────────────────────────────────

        [Test]
        public void ExtractPath_BaselineSaved_ReturnsPath()
        {
            var result = ScreenshotResultParser.ExtractPath(
                "Baseline saved: /Users/german/Baselines/ref скриншот.png");
            Assert.AreEqual("/Users/german/Baselines/ref скриншот.png", result);
        }

        // ── Pattern 3: "[img:/path]" ────────────────────────────────────────────

        [Test]
        public void ExtractPath_ImgMarker_ReturnsPath()
        {
            var result = ScreenshotResultParser.ExtractPath(
                "A vivid scene with a red cube.\n[img:/var/folders/tmp/screenshot 2026.png]");
            Assert.AreEqual("/var/folders/tmp/screenshot 2026.png", result);
        }

        // ── Pattern priority: first match wins ─────────────────────────────────

        /// <summary>
        /// When both primary patterns appear, "Data saved to:" must win (first-match semantics).
        /// RED B: swap "Baseline saved:" before "Data saved to:" in ExtractPath → this fails.
        /// </summary>
        [Test]
        public void ExtractPath_DataSavedAndBaselineBothPresent_DataSavedWins()
        {
            const string text =
                "Data saved to: /Users/german/ScreenShots/primary.png\n" +
                "Baseline saved: /Users/german/Baselines/secondary.png";
            var result = ScreenshotResultParser.ExtractPath(text);
            Assert.AreEqual("/Users/german/ScreenShots/primary.png", result,
                "'Data saved to:' is the primary pattern and must win over 'Baseline saved:' " +
                "when both appear in the same result text.");
        }

        // ── No match → null ─────────────────────────────────────────────────────

        [Test]
        public void ExtractPath_PlainText_ReturnsNull()
        {
            var result = ScreenshotResultParser.ExtractPath(
                "Screenshot comparison: PASS — 98.3% similarity, delta within threshold.");
            Assert.IsNull(result);
        }

        [Test]
        public void ExtractPath_Null_ReturnsNull()
            => Assert.IsNull(ScreenshotResultParser.ExtractPath(null));

        [Test]
        public void ExtractPath_Empty_ReturnsNull()
            => Assert.IsNull(ScreenshotResultParser.ExtractPath(""));
    }
}
