// TDD T2.1 — ScreenshotCard: tool card renderer for screenshot + screenshot_baseline.
//
// Double-red requirement:
//   A — break any Assert → test goes RED
//   B — remove registration (ToolCardRendererRegistry.Unregister) → registration test RED
//       OR make ExtractPath return null → image tests RED
//       OR move "screenshot-card-rendered" marker above content → retry test RED
//
// T2.5: Inherits ToolCardTestBase for shared registration / OnStart / grouper helpers.
//
// Test data: real paths including spaces and Cyrillic; missing file paths;
// multi-turn idempotency. Texture loading requires a real file on disk.
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class ScreenshotCardTests : ToolCardTestBase
    {
        // ── Registration ─────────────────────────────────────────────────────────

        [Test]
        public void ScreenshotCard_IsRegisteredForScreenshot() =>
            AssertRegistered("screenshot", typeof(ScreenshotCard));

        [Test]
        public void ScreenshotCard_IsRegisteredForScreenshotBaseline() =>
            AssertRegistered("screenshot_baseline", typeof(ScreenshotCard));

        // ── OnStart ──────────────────────────────────────────────────────────────

        [Test]
        public void OnStart_DoesNotModifyChip() =>
            AssertOnStartIsNoop(new ScreenshotCard(), "screenshot");

        // ── OnUpdate: no result yet ──────────────────────────────────────────────

        [Test]
        public void OnUpdate_NoResult_NoImageElement()
        {
            var card = new ScreenshotCard();
            var chip = new VisualElement();
            // ArgsJson set but ResultText null → HasResult == false
            var rec = new ToolCallRecord("screenshot", "id-2",
                argsJson: "{\"path\":\"Game\"}",
                resultText: null);
            card.OnUpdate(chip, rec);
            Assert.AreEqual(0, chip.childCount,
                "OnUpdate must add nothing when result has not arrived yet");
        }

        // ── OnUpdate: missing file → fallback label ──────────────────────────────

        [Test]
        public void OnUpdate_MissingFile_ContainsFallbackLabel()
        {
            var card = new ScreenshotCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("screenshot", "id-3",
                argsJson: "{}",
                resultText: "Data saved to: /nonexistent/Снимок экрана.png");
            card.OnUpdate(chip, rec);
            Assert.AreEqual(1, chip.childCount,
                "Fallback label must be added for a missing file");
            Assert.IsInstanceOf<Label>(chip[0],
                "Fallback for a missing file must be a Label, not an Image");
            card.OnUpdate(chip, rec); // second call must be no-op (marker already set by first call)
            Assert.AreEqual(1, chip.childCount,
                "Second OnUpdate must be idempotent after fallback rendered");
        }

        // ── OnUpdate: valid file → image element ─────────────────────────────────

        [Test]
        public void OnUpdate_ValidFile_ContainsImageContainer()
        {
            // Create a minimal 1x1 PNG on disk so File.Exists passes and texture loads.
            var tmpPath = Path.Combine(
                Path.GetTempPath(), "screenshot_card_test_игра снимок.png");
            var setup = new Texture2D(1, 1);
            setup.SetPixel(0, 0, Color.cyan);
            setup.Apply();
            File.WriteAllBytes(tmpPath, setup.EncodeToPNG());
            Object.DestroyImmediate(setup);

            try
            {
                var card = new ScreenshotCard();
                var chip = new VisualElement();
                var rec  = new ToolCallRecord("screenshot", "id-4",
                    argsJson: "{}",
                    resultText: $"Data saved to: {tmpPath}");

                // Suppress GUI errors in headless mode.
                LogAssert.ignoreFailingMessages = true;
                card.OnUpdate(chip, rec);
                LogAssert.ignoreFailingMessages = false;

                Assert.AreEqual(1, chip.childCount,
                    "Image container must be added for a valid screenshot path");
                // BuildImageElement wraps in a container with class "md-image-container"
                Assert.IsTrue(chip[0].ClassListContains("md-image-container"),
                    "Child element must be the md-image-container from BuildImageElement");
            }
            finally
            {
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
                ImageBlockRenderer.ClearCache();
            }
        }

        // ── Idempotency: two OnUpdate calls → exactly one child ─────────────────

        [Test]
        public void OnUpdate_CalledTwice_ExactlyOneImageElement()
        {
            var card = new ScreenshotCard();
            var chip = new VisualElement();
            var rec  = new ToolCallRecord("screenshot", "id-5",
                argsJson: "{}",
                resultText: "Data saved to: /definitely/missing/path.png");
            card.OnUpdate(chip, rec);
            card.OnUpdate(chip, rec); // must be idempotent
            Assert.AreEqual(1, chip.childCount,
                "Second OnUpdate call must not add a duplicate child (idempotency)");
        }

        // ── Retry after failed build ─────────────────────────────────────────────

        /// <summary>
        /// Verifies that if content building throws (file disappears after File.Exists check),
        /// the rendered marker is NOT set — so the next OnUpdate can retry successfully.
        ///
        /// RED B: move "screenshot-card-rendered" AddToClassList above BuildImageElement →
        ///        first call sets the marker before throwing; second call exits early; test fails.
        /// </summary>
        [Test]
        public void OnUpdate_ContentBuildThrows_CardRetriableOnNextCall()
        {
            var tmpPath = Path.Combine(
                Path.GetTempPath(), "screenshot_retry_test_карточка.png");

            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.magenta);
            tex.Apply();
            File.WriteAllBytes(tmpPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            try
            {
                // Make file unreadable: File.Exists=true, ReadAllBytes throws UnauthorizedAccessException.
                // This reproduces the TOCTOU scenario (file existed at check time, gone at read time).
                Chmod(tmpPath, "000");

                var card = new ScreenshotCard();
                var chip = new VisualElement();
                var rec  = new ToolCallRecord("screenshot", "id-retry", "{}",
                    $"Data saved to: {tmpPath}");

                LogAssert.ignoreFailingMessages = true;
                card.OnUpdate(chip, rec);   // must NOT propagate exception; marker must NOT be set
                LogAssert.ignoreFailingMessages = false;

                Assert.AreEqual(0, chip.childCount,
                    "Unreadable file must produce no content on first call");
                Assert.IsFalse(chip.ClassListContains("screenshot-card-rendered"),
                    "Marker must NOT be set after a failed build. " +
                    "Bug: marker was placed before content, blocking all future retries.");

                // Restore readability → retry must now render
                Chmod(tmpPath, "644");

                LogAssert.ignoreFailingMessages = true;
                card.OnUpdate(chip, rec);
                LogAssert.ignoreFailingMessages = false;

                Assert.AreEqual(1, chip.childCount,
                    "Card must render the image on retry after file becomes readable");
                Assert.IsTrue(chip.ClassListContains("screenshot-card-rendered"),
                    "Marker must be set after successful render");
            }
            finally
            {
                Chmod(tmpPath, "644");
                if (File.Exists(tmpPath)) File.Delete(tmpPath);
                ImageBlockRenderer.ClearCache();
            }
        }

        private static void Chmod(string path, string mode)
        {
            using var p = System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("/bin/chmod", $"{mode} \"{path}\"")
                { UseShellExecute = false });
            p?.WaitForExit();
        }

        // ── Grouper bypass: real ScreenshotCard must trigger card-chip class ─────
        //
        // Uses two different tool names (screenshot + screenshot_baseline), so kept
        // inline rather than delegating to AssertGrouperBypass.
        // RED B: if ScreenshotCard is unregistered, card-chip count drops to 0.

        [Test]
        public void TwoScreenshotChips_BothVisibleInFeed_NotAbsorbedByGrouper()
        {
            var container = new VisualElement();
            var registry  = ChatBlockRendererFactory.CreateDefault(null, null);
            var transcript = new ChatTranscript(container, registry);

            transcript.AppendToolChip("screenshot",          ok: true, toolId: "ss-1");
            transcript.AppendToolChip("screenshot_baseline", ok: true, toolId: "ss-2");
            transcript.FinalizeAssistant();

            var cardChips = container.Query(className: "card-chip").ToList();
            Assert.AreEqual(2, cardChips.Count,
                "Both screenshot chips must bypass the grouper and appear as card-chip elements");

            var foldout = container.Q<Foldout>(className: "tool-group");
            if (foldout != null)
                Assert.IsNull(foldout.Q(className: "card-chip"),
                    "No card-chip may reside inside a collapsed tool-group foldout");
        }
    }
}
