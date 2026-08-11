// TDD T-6.2: AgentMentionSource — disk-based agent mention search.
// Pure IO: no Unity API. Uses temp dirs for isolation.
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityMCP.Editor.Chat;

namespace UnityMCP.Editor.Chat.Tests
{
    [TestFixture]
    public class AgentMentionSourceTests : UnityMCP.Editor.Testing.UnityMcpTestBase
    {
        private string _tmpRoot;
        private string _agentsDir;

        [SetUp]
        public void SetUp()
        {
            _tmpRoot   = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            _agentsDir = Path.Combine(_tmpRoot, ".claude", "agents");
            Directory.CreateDirectory(_agentsDir);
        }

        [TearDown]
        public void TearDown()
        {
            try { if (Directory.Exists(_tmpRoot)) Directory.Delete(_tmpRoot, true); }
            catch { /* best effort */ }
        }

        private AgentMentionSource MakeSource(string home = null, Action onScan = null)
            => new AgentMentionSource(_tmpRoot, home ?? Path.GetTempPath(), onScan);

        private void WriteAgent(string stem, string content = "")
            => File.WriteAllText(Path.Combine(_agentsDir, stem + ".md"), content);

        private List<MentionCandidate> Search(AgentMentionSource src, string query, int max = 10)
        {
            src.RefreshIfDirty();
            var results = new List<MentionCandidate>();
            src.Search(query, max, results);
            return results;
        }

        // 1. Empty dir → 0 results
        [Test]
        public void Search_EmptyDir_ReturnsEmpty()
        {
            var results = Search(MakeSource(), "anything");
            Assert.AreEqual(0, results.Count);
        }

        // 2. Exact name → 1 result, chip.Path == file stem
        [Test]
        public void Search_ExactName_ReturnsHighScore()
        {
            WriteAgent("senior-developer");
            var results = Search(MakeSource(), "senior-developer");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(ChipKindKeys.Agent, results[0].Chip.KindKey);
            Assert.AreEqual("senior-developer", results[0].Chip.Path);
        }

        // 3. Prefix match — "senior" finds "senior-developer"
        [Test]
        public void Search_PrefixMatch()
        {
            WriteAgent("senior-developer");
            var results = Search(MakeSource(), "senior");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("senior-developer", results[0].Chip.Path);
        }

        // 4. Case-insensitive — "SENIOR" finds "senior-developer"
        [Test]
        public void Search_CaseInsensitive()
        {
            WriteAgent("senior-developer");
            var results = Search(MakeSource(), "SENIOR");
            Assert.AreEqual(1, results.Count);
            Assert.AreEqual("senior-developer", results[0].Chip.Path);
        }

        // 5. MaxResults capped
        [Test]
        public void Search_MaxResults_Capped()
        {
            for (int i = 1; i <= 10; i++)
                WriteAgent($"agent-{i}");

            var results = Search(MakeSource(), "agent", max: 3);
            Assert.LessOrEqual(results.Count, 3);
        }

        // 6. Second RefreshIfDirty immediately after first → scan count stays 1
        [Test]
        public void RefreshIfDirty_NoChange_SkipsRebuild()
        {
            WriteAgent("my-agent");
            int scanCount = 0;
            var src = new AgentMentionSource(_tmpRoot, Path.GetTempPath(), () => scanCount++);
            src.RefreshIfDirty();   // scan #1
            src.RefreshIfDirty();   // cooldown: no re-scan
            Assert.AreEqual(1, scanCount);
        }
    }
}
