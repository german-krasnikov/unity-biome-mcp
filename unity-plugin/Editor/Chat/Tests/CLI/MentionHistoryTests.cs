// TDD — RED first. Tests for MentionHistory persistence layer.
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class MentionHistoryTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tempPath;

        [SetUp]
        public void SetUp()
        {
            _tempPath = Path.Combine(
                Path.GetTempPath(),
                $"MentionHistoryTest_{System.Guid.NewGuid():N}.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_tempPath)) File.Delete(_tempPath);
        }

        [Test]
        public void RecordCommit_StoresNonZeroTimestamp()
        {
            var history = new MentionHistory(_tempPath);
            history.RecordCommit("/Player");
            Assert.Greater(history.GetTimestamp("/Player"), 0L);
        }

        [Test]
        public void GetTimestamp_Unknown_ReturnsZero()
        {
            var history = new MentionHistory(_tempPath);
            Assert.AreEqual(0L, history.GetTimestamp("/NonExistent"));
        }

        [Test]
        public void RecordCommit_CapAt100_EvictsOldest()
        {
            var history = new MentionHistory(_tempPath);
            // Add 101 entries; the oldest should be evicted
            for (int i = 0; i < 101; i++)
                history.RecordCommit($"/obj{i}");

            // Latest entry must still be present
            Assert.Greater(history.GetTimestamp("/obj100"), 0L);

            // Count of non-zero timestamps must be <= 100
            int present = 0;
            for (int i = 0; i < 101; i++)
                if (history.GetTimestamp($"/obj{i}") > 0) present++;

            Assert.LessOrEqual(present, 100);
        }

        [Test]
        public void SaveLoad_RoundTrips_WithTempFile()
        {
            var history1 = new MentionHistory(_tempPath);
            history1.RecordCommit("/Alpha");
            history1.RecordCommit("/Beta");

            // New instance reads from the same temp file
            var history2 = new MentionHistory(_tempPath);
            Assert.Greater(history2.GetTimestamp("/Alpha"), 0L);
            Assert.Greater(history2.GetTimestamp("/Beta"), 0L);
            Assert.AreEqual(0L, history2.GetTimestamp("/Gamma"));
        }

        [Test]
        public void Load_CorruptFile_ReturnsEmptyHistory()
        {
            File.WriteAllText(_tempPath, "not valid json {{{");
            var history = new MentionHistory(_tempPath);
            // Should not throw; timestamp for any path is 0
            Assert.AreEqual(0L, history.GetTimestamp("/any"));
        }

        [Test]
        public void RecordCommit_UpdatesTimestamp_OnSecondRecord()
        {
            var history = new MentionHistory(_tempPath);
            history.RecordCommit("/Player");
            long first = history.GetTimestamp("/Player");

            // Record again — timestamp should be >= first (same or newer tick)
            history.RecordCommit("/Player");
            long second = history.GetTimestamp("/Player");

            Assert.GreaterOrEqual(second, first);
        }
    }
}
