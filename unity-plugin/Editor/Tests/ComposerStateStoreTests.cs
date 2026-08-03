// TDD: ComposerStateStore — round-trip persistence tests (EditMode, no Unity API)
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace UnityMCP.Editor.Tests
{
    [TestFixture]
    internal class ComposerStateStoreTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        string _tempFile;

        [SetUp]
        public void SetUp()
        {
            _tempFile = Path.Combine(Path.GetTempPath(), $"ComposerStateTest_{System.Guid.NewGuid()}.json");
            ComposerStateStore._testOverride = _tempFile;
        }

        [TearDown]
        public void TearDown()
        {
            ComposerStateStore._testOverride = null;
            if (File.Exists(_tempFile)) File.Delete(_tempFile);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsSteps()
        {
            var state = new ComposerState
            {
                steps = new List<VisualStep> { new VisualStep { type = StepType.Assert, query = "Health == 100" } }
            };
            ComposerStateStore.Save(state);
            var loaded = ComposerStateStore.Load();
            Assert.AreEqual(1, loaded.steps.Count);
            Assert.AreEqual(StepType.Assert, loaded.steps[0].type);
            Assert.AreEqual("Health == 100", loaded.steps[0].query);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsTimeout()
        {
            ComposerStateStore.Save(new ComposerState { globalTimeout = 42.5f });
            Assert.AreEqual(42.5f, ComposerStateStore.Load().globalTimeout, 0.001f);
        }

        [Test]
        public void Save_ThenLoad_RoundTripsLastFilePath()
        {
            ComposerStateStore.Save(new ComposerState { lastFilePath = "/some/path/test.playtest" });
            Assert.AreEqual("/some/path/test.playtest", ComposerStateStore.Load().lastFilePath);
        }

        [Test]
        public void Load_MissingFile_ReturnsDefault()
        {
            // _tempFile not created yet — missing
            var state = ComposerStateStore.Load();
            Assert.IsNotNull(state);
            Assert.AreEqual(60f, state.globalTimeout, 0.001f);
        }

        [Test]
        public void Load_CorruptJson_ReturnsDefault()
        {
            File.WriteAllText(_tempFile, "not valid json {{{{");
            var state = ComposerStateStore.Load();
            Assert.IsNotNull(state);
            Assert.IsNotNull(state.steps);
            Assert.AreEqual(60f, state.globalTimeout, 0.001f);
        }

        [Test]
        public void Load_EmptySteps_ReturnsEmptyList()
        {
            ComposerStateStore.Save(new ComposerState { steps = new List<VisualStep>() });
            var loaded = ComposerStateStore.Load();
            Assert.IsNotNull(loaded.steps);
            Assert.AreEqual(0, loaded.steps.Count);
        }

        [Test]
        public void Save_EmptySteps_Succeeds()
        {
            Assert.DoesNotThrow(() =>
                ComposerStateStore.Save(new ComposerState { steps = new List<VisualStep>() }));
            Assert.IsTrue(File.Exists(_tempFile));
        }

        [Test]
        public void Load_GlobalAbort_RoundTrips()
        {
            ComposerStateStore.Save(new ComposerState { globalAbort = true });
            Assert.IsTrue(ComposerStateStore.Load().globalAbort);
        }
    }
}
