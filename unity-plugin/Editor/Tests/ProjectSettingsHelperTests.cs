// TDD: ProjectSettingsHelper extensions — graphics/audio/input targets + RemoveTag + SetQuality fix.
// EditMode only — no scene required. Tags are mutated; each test cleans up.
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class ProjectSettingsHelperTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        // ── Test 1: GetGraphics ─────────────────────────────────────────────────
        [Test]
        public void GetGraphics_ReturnsRenderPipelineLine()
        {
            var result = ProjectSettingsHelper.Execute("get", "{\"target\":\"graphics\"}");
            StringAssert.Contains("renderPipeline:", result);
        }

        // ── Test 2: GetAudio ────────────────────────────────────────────────────
        [Test]
        public void GetAudio_ReturnsMasterVolumeLine()
        {
            var result = ProjectSettingsHelper.Execute("get", "{\"target\":\"audio\"}");
            StringAssert.Contains("masterVolume:", result);
        }

        // ── Test 3: GetInput ────────────────────────────────────────────────────
        [Test]
        public void GetInput_ReturnsHorizontalAxis()
        {
            var result = ProjectSettingsHelper.Execute("get", "{\"target\":\"input\"}");
            StringAssert.Contains("Horizontal", result);
        }

        // ── Test 4: SetQuality currentLevel via method (not read-only reflection) ─
        [Test]
        public void SetQualityCurrentLevel_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
                ProjectSettingsHelper.Execute("set",
                    "{\"target\":\"quality\",\"prop\":\"currentLevel\",\"value\":\"0\"}"));
        }

        // ── Test 5: AddTag round-trips ──────────────────────────────────────────
        [Test]
        public void AddTag_RoundTrips()
        {
            const string tag = "TestTag_PSHTests";
            ProjectSettingsHelper.Execute("set", $"{{\"target\":\"tags\",\"value\":\"{tag}\"}}");
            var tags = ProjectSettingsHelper.Execute("get", "{\"target\":\"tags\"}");
            Assert.That(tags, Does.Contain(tag));
            // cleanup
            ProjectSettingsHelper.Execute("set", $"{{\"target\":\"tags\",\"prop\":\"remove\",\"value\":\"{tag}\"}}");
        }

        // ── Test 6: RemoveTag removes existing tag ─────────────────────────────
        [Test]
        public void RemoveTag_ExistingTag_RemovesIt()
        {
            const string tag = "ToRemove_PSHTests";
            ProjectSettingsHelper.Execute("set", $"{{\"target\":\"tags\",\"value\":\"{tag}\"}}");
            var result = ProjectSettingsHelper.Execute("set",
                $"{{\"target\":\"tags\",\"prop\":\"remove\",\"value\":\"{tag}\"}}");
            Assert.AreEqual("ok", result);
            var tags = ProjectSettingsHelper.Execute("get", "{\"target\":\"tags\"}");
            Assert.That(tags, Does.Not.Contain(tag));
        }

        // ── Test 7: RemoveTag missing tag throws ───────────────────────────────
        [Test]
        public void RemoveTag_MissingTag_Throws()
        {
            var ex = Assert.Throws<System.Exception>(() =>
                ProjectSettingsHelper.Execute("set",
                    "{\"target\":\"tags\",\"prop\":\"remove\",\"value\":\"NoSuchTag_XYZ_PSH\"}"));
            StringAssert.Contains("not found", ex.Message);
        }

        // ── Test 8: Unknown target error message lists valid targets ───────────
        [Test]
        public void UnknownTarget_ErrorMessageListsGraphics()
        {
            var ex = Assert.Throws<System.Exception>(() =>
                ProjectSettingsHelper.Execute("get", "{\"target\":\"bogus\"}"));
            StringAssert.Contains("graphics", ex.Message);
        }
    }
}
